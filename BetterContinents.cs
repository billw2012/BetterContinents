using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace BetterContinents
{
    [BepInPlugin("BetterContinents", ModInfo.Name, ModInfo.Version)]
    public class BetterContinents : BaseUnityPlugin
    {
        // See the Awake function for the config descriptions
        private static ConfigEntry<bool> ConfigEnabled;
        
        private static ConfigEntry<float> ConfigContinentSize;
        private static ConfigEntry<float> ConfigMountainsAmount;
        private static ConfigEntry<float> ConfigSeaLevelAdjustment;
        private static ConfigEntry<bool> ConfigOceanChannelsEnabled;
        private static ConfigEntry<bool> ConfigRiversEnabled;
        private static ConfigEntry<bool> ConfigMapEdgeDropoff;
        private static ConfigEntry<bool> ConfigMountainsAllowedAtCenter;

        private static ConfigEntry<string> ConfigMapSourceDir;
        
        private static ConfigEntry<string> ConfigHeightmapFile;
        private static ConfigEntry<float> ConfigHeightmapAmount;
        private static ConfigEntry<float> ConfigHeightmapBlend;
        private static ConfigEntry<float> ConfigHeightmapAdd;
        
        private static ConfigEntry<string> ConfigBiomemapFile;

        private static ConfigEntry<string> ConfigSpawnmapFile;
        
        private static ConfigEntry<string> ConfigRoughmapFile;
        private static ConfigEntry<float> ConfigRoughmapBlend;
        
        private static ConfigEntry<bool> ConfigUseRoughInvertedForFlat;
        private static ConfigEntry<string> ConfigFlatmapFile;
        private static ConfigEntry<float> ConfigFlatmapBlend;
        
        private static ConfigEntry<float> ConfigForestScale;
        private static ConfigEntry<float> ConfigForestAmount;
        private static ConfigEntry<bool> ConfigForestFactorOverrideAllTrees;
        private static ConfigEntry<string> ConfigForestmapFile;
        private static ConfigEntry<float> ConfigForestmapMultiply;
        private static ConfigEntry<float> ConfigForestmapAdd;
        
        private static ConfigEntry<float> ConfigMaxRidgeHeight;
        private static ConfigEntry<float> ConfigRidgeSize;
        private static ConfigEntry<float> ConfigRidgeBlend;
        private static ConfigEntry<float> ConfigRidgeAmount;
        
        private static ConfigEntry<bool> ConfigOverrideStartPosition;
        private static ConfigEntry<float> ConfigStartPositionX;
        private static ConfigEntry<float> ConfigStartPositionY;
        
        private static ConfigEntry<bool> ConfigDebugModeEnabled;
        private static ConfigEntry<bool> ConfigDebugSkipDefaultLocationPlacement;
        
        private const float WorldSize = 10500f;
        private static readonly Vector2 Half = Vector2.one * 0.5f;
        private static float NormalizedX(float x) => x / (WorldSize * 2f) + 0.5f;
        private static float NormalizedY(float y) => y / (WorldSize * 2f) + 0.5f;
        private static float WorldX(float x) => (x - 0.5f) * WorldSize * 2f;
        private static float WorldY(float y) => (y - 0.5f) * WorldSize * 2f;
        private static Vector2 NormalizedToWorld(Vector2 p) => (p - Half) * WorldSize * 2f;
        private static Vector2 NormalizedToWorld(float x, float y) => new Vector2(WorldX(x), WorldY(y));
        private static Vector2 WorldToNormalized(Vector2 p) => p / (WorldSize * 2f) + Half;
        private static Vector2 WorldToNormalized(float x, float y) => new Vector2(NormalizedX(x), NormalizedY(y));

        public static void Log(string msg) => Debug.Log($"[BetterContinents] {msg}");
        public static void LogError(string msg) => Debug.LogError($"[BetterContinents] {msg}");

        private static bool AllowDebugActions => ZNet.instance && ZNet.instance.IsServer() &&
                                                 Settings.EnabledForThisWorld && ConfigDebugModeEnabled.Value;
        
        // These are what are baked into the world when it is created
        private struct BetterContinentsSettings
        {
            // Add new properties at the end, and comment where new versions start
            public const int LatestVersion = 5;
            
            // Version 1
            public int Version;

            public long WorldUId;

            public bool EnabledForThisWorld;

            public float GlobalScale;
            public float MountainsAmount;
            public float SeaLevelAdjustment;

            public float MaxRidgeHeight;
            public float RidgeScale;
            public float RidgeBlendSigmoidB;
            public float RidgeBlendSigmoidXOffset;

            public float HeightmapAmount;
            public float HeightmapBlend;
            public float HeightmapAdd;
            
            // Version 2
            public bool OceanChannelsEnabled;
            public bool RiversEnabled;

            public float ForestScale;
            public float ForestAmountOffset;

            public bool OverrideStartPosition;
            public float StartPositionX;
            public float StartPositionY;

            // Version 3
            // <none>

            // Version 4
            // <none>
            
            // Version 5
            public float RoughmapBlend;
            
            public bool UseRoughInvertedAsFlat;
            public float FlatmapBlend;
            
            public float ForestmapMultiply;
            public float ForestmapAdd;

            public bool DisableMapEdgeDropoff;
            public bool MountainsAllowedAtCenter;

            public bool ForestFactorOverrideAllTrees;

            // Non-serialized
            private ImageMapFloat Heightmap;
            private ImageMapBiome Biomemap;
            private ImageMapSpawn Spawnmap;
            private ImageMapFloat Roughmap;
            private ImageMapFloat Flatmap;
            private ImageMapFloat Forestmap;

            public bool OverrideBiomes => this.Biomemap != null;
            public bool UseSpawnmap => this.Spawnmap != null;
            public bool UseRoughmap => this.Roughmap != null && this.RoughmapBlend > 0;
            
            public static BetterContinentsSettings Create(long worldUId)
            {
                var settings = new BetterContinentsSettings();
                settings.InitSettings(worldUId, ConfigEnabled.Value);
                return settings;
            }
            
            public static BetterContinentsSettings Disabled(long worldUId = -1)
            {
                var settings = new BetterContinentsSettings();
                settings.InitSettings(worldUId, false);
                return settings;
            }

            private static string CleanPath(string path) => path?.Replace("\\\"", "").Replace("\"", "").Trim();

            private static string GetPath(string folderFileName, string defaultFileName)
            {
                if (!string.IsNullOrEmpty(ConfigMapSourceDir.Value))
                {
                    if (File.Exists(Path.Combine(ConfigMapSourceDir.Value, CleanPath(folderFileName))))
                    {
                        return Path.Combine(ConfigMapSourceDir.Value, CleanPath(folderFileName));
                    }
                    else
                    {
                        return string.Empty;
                    }
                }

                return CleanPath(defaultFileName);
            }

            private static string HeightmapPath() => GetPath("Heightmap.png", ConfigHeightmapFile.Value);
            private static string BiomemapPath() => GetPath("Biomemap.png", ConfigBiomemapFile.Value);
            private static string SpawnmapPath() => GetPath("Spawnmap.png", ConfigSpawnmapFile.Value);
            private static string RoughmapPath() => GetPath("Roughmap.png", ConfigRoughmapFile.Value);
            private static string FlatmapPath() => ConfigUseRoughInvertedForFlat.Value
                ? null
                : GetPath("Flatmap.png", ConfigFlatmapFile.Value);
            private static string ForestmapPath() => GetPath("Forestmap.png", ConfigForestmapFile.Value);
            
            private void InitSettings(long worldUId, bool enabled)
            {
                Log($"Init settings for new world");

                Version = LatestVersion;

                WorldUId = worldUId;

                EnabledForThisWorld = enabled;

                if (EnabledForThisWorld)
                {
                    GlobalScale = FeatureScaleCurve(ConfigContinentSize.Value);
                    MountainsAmount = ConfigMountainsAmount.Value;
                    SeaLevelAdjustment = Mathf.Lerp(0.25f, -0.25f, ConfigSeaLevelAdjustment.Value);

                    MaxRidgeHeight = ConfigMaxRidgeHeight.Value;
                    RidgeScale = FeatureScaleCurve(ConfigRidgeSize.Value);
                    RidgeBlendSigmoidB = Mathf.Lerp(-30f, -10f, ConfigRidgeBlend.Value);
                    RidgeBlendSigmoidXOffset = Mathf.Lerp(1f, 0.35f, ConfigRidgeAmount.Value);

                    var heightmapPath = HeightmapPath();
                    if (!string.IsNullOrEmpty(heightmapPath))
                    {
                        HeightmapAmount = ConfigHeightmapAmount.Value;
                        HeightmapBlend = ConfigHeightmapBlend.Value;
                        HeightmapAdd = ConfigHeightmapAdd.Value;

                        Heightmap = new ImageMapFloat(heightmapPath);
                        if (!Heightmap.LoadSourceImage() || !Heightmap.CreateMap())
                        {
                            Heightmap = null;
                        }
                    }

                    var biomemapPath = BiomemapPath();
                    if (!string.IsNullOrEmpty(biomemapPath))
                    {
                        Biomemap = new ImageMapBiome(biomemapPath);
                        if (!Biomemap.LoadSourceImage() || !Biomemap.CreateMap())
                        {
                            Biomemap = null;
                        }
                    }

                    OceanChannelsEnabled = ConfigOceanChannelsEnabled.Value;
                    RiversEnabled = ConfigRiversEnabled.Value;

                    ForestScale = FeatureScaleCurve(ConfigForestScale.Value);
                    ForestAmountOffset = Mathf.Lerp(1, -1, ConfigForestAmount.Value);
                    ForestFactorOverrideAllTrees = ConfigForestFactorOverrideAllTrees.Value;

                    OverrideStartPosition = ConfigOverrideStartPosition.Value;
                    StartPositionX = ConfigStartPositionX.Value;
                    StartPositionY = ConfigStartPositionY.Value;
                    //LakesEnabled = ConfigLakesEnabled.Value;
                    
                    var spawnmapPath = SpawnmapPath();
                    if (!string.IsNullOrEmpty(spawnmapPath))
                    {
                        Spawnmap = new ImageMapSpawn(spawnmapPath);
                        if (!Spawnmap.LoadSourceImage() || !Spawnmap.CreateMap())
                        {
                            Spawnmap = null;
                        }
                    }

                    var roughmapPath = RoughmapPath();
                    if (!string.IsNullOrEmpty(roughmapPath))
                    {
                        RoughmapBlend = ConfigRoughmapBlend.Value;
                        
                        Roughmap = new ImageMapFloat(roughmapPath);
                        if (!Roughmap.LoadSourceImage() || !Roughmap.CreateMap())
                        {
                            Roughmap = null;
                        }
                    }

                    var flatmapPath = FlatmapPath();
                    if (ConfigUseRoughInvertedForFlat.Value && Roughmap != null ||
                        !ConfigUseRoughInvertedForFlat.Value && string.IsNullOrEmpty(flatmapPath))
                    {
                        UseRoughInvertedAsFlat = ConfigUseRoughInvertedForFlat.Value;
                        FlatmapBlend = ConfigFlatmapBlend.Value;
                        if (!string.IsNullOrEmpty(flatmapPath) && !UseRoughInvertedAsFlat)
                        {
                            Flatmap = new ImageMapFloat(flatmapPath);
                            if (!Flatmap.LoadSourceImage() || !Flatmap.CreateMap())
                            {
                                Flatmap = null;
                            }
                        }
                    }
                    
                    var forestmapPath = ForestmapPath();
                    if (!string.IsNullOrEmpty(forestmapPath))
                    {
                        ForestmapAdd = ConfigForestmapAdd.Value;
                        ForestmapMultiply = ConfigForestmapMultiply.Value;
                        
                        Forestmap = new ImageMapFloat(forestmapPath);
                        if (!Forestmap.LoadSourceImage() || !Forestmap.CreateMap())
                        {
                            Forestmap = null;
                        }
                    }

                    DisableMapEdgeDropoff = !ConfigMapEdgeDropoff.Value;
                    MountainsAllowedAtCenter = ConfigMountainsAllowedAtCenter.Value;
                }
            }

            private static float FeatureScaleCurve(float x) => ScaleRange(Gamma(x, 0.726965071031f), 0.2f, 3f);
            private static float Gamma(float x, float h) => Mathf.Pow(x, Mathf.Pow(1 - h * 0.5f + 0.25f, 6f));
            private static float ScaleRange(float g, float n, float m) => n + (m - n) * (1 - g); 

            public void Dump()
            {
                Log($"Version {Version}");
                Log($"WorldUId {WorldUId}");
                
                if (EnabledForThisWorld)
                {
                    Log($"GlobalScale {GlobalScale}");
                    Log($"MountainsAmount {MountainsAmount}");
                    Log($"SeaLevelAdjustment {SeaLevelAdjustment}");
                    Log($"OceanChannelsEnabled {OceanChannelsEnabled}");
                    Log($"RiversEnabled {RiversEnabled}");
                    
                    Log($"DisableMapEdgeDropoff {DisableMapEdgeDropoff}");
                    Log($"MountainsAllowedAtCenter {MountainsAllowedAtCenter}");
                    
                    if (Heightmap != null)
                    {
                        Log($"Heightmap file {Heightmap.FilePath}");
                        Log($"Heightmap size {Heightmap.Size}x{Heightmap.Size}, amount {HeightmapAmount}, blend {HeightmapBlend}, add {HeightmapAdd}");
                    }
                    else
                    {
                        Log($"Heightmap disabled");
                    }

                    if (Biomemap != null)
                    {
                        Log($"Biomemap file {Biomemap.FilePath}");
                        Log($"Biomemap size {Biomemap.Size}x{Biomemap.Size}");
                    }
                    else
                    {
                        Log($"Biomemap disabled");
                    }
                    
                    if (Spawnmap != null)
                    {
                        Log($"Spawnmap file {Spawnmap.FilePath}");
                        Log($"Spawnmap includes spawns for {Spawnmap.RemainingSpawnAreas.Count} types");
                    }
                    else
                    {
                        Log($"Spawnmap disabled");
                    }
                    
                    if (Roughmap != null)
                    {
                        Log($"Roughmap file {Roughmap.FilePath}");
                        Log($"Roughmap size {Roughmap.Size}x{Roughmap.Size}, blend {RoughmapBlend}");
                    }
                    else
                    {
                        Log($"Roughmap disabled");
                    }

                    if (UseRoughInvertedAsFlat)
                    {
                        Log($"Using inverted Roughmap as Flatmap");
                    }
                    else
                    {
                        if (Flatmap != null)
                        {
                            Log($"Flatmap file {Flatmap.FilePath}");
                            Log($"Flatmap size {Flatmap.Size}x{Flatmap.Size}, blend {FlatmapBlend}");
                        }
                        else
                        {
                            Log($"Flatmap disabled");
                        }
                    }
                    
                    Log($"ForestScale {ForestScale}");
                    Log($"ForestAmountOffset {ForestAmountOffset}");
                    if (Forestmap != null)
                    {
                        Log($"Forestmap file {Forestmap.FilePath}");
                        Log($"Forestmap size {Forestmap.Size}x{Forestmap.Size}, multiply {ForestmapMultiply}, add {ForestmapAdd}");
                        if (ForestFactorOverrideAllTrees)
                        {
                            Log($"Forest Factor overrides all trees");
                        }
                        else
                        {
                            Log($"Forest Factor applies only to the same trees as vanilla");
                        }
                    }
                    else
                    {
                        Log($"Forestmap disabled");
                    }
                    
                    Log($"MaxRidgeHeight {MaxRidgeHeight}");
                    Log($"RidgeScale {RidgeScale}");
                    Log($"RidgeBlendSigmoidB {RidgeBlendSigmoidB}");
                    Log($"RidgeBlendSigmoidXOffset {RidgeBlendSigmoidXOffset}");

                    if (OverrideStartPosition)
                    {
                        Log($"StartPosition {StartPositionX}, {StartPositionY}");
                    }
                }
                else
                {
                    Log($"DISABLED");
                }
            }

            public void Serialize(ZPackage pkg)
            {
                pkg.Write(Version);

                pkg.Write(WorldUId);

                // Version 1
                pkg.Write(EnabledForThisWorld);

                if (EnabledForThisWorld)
                {
                    pkg.Write(GlobalScale);
                    pkg.Write(MountainsAmount);
                    pkg.Write(SeaLevelAdjustment);

                    pkg.Write(MaxRidgeHeight);
                    pkg.Write(RidgeScale);
                    pkg.Write(RidgeBlendSigmoidB);
                    pkg.Write(RidgeBlendSigmoidXOffset);

                    pkg.Write(Heightmap?.FilePath ?? string.Empty);
                    if (Heightmap != null)
                    {
                        pkg.Write(Heightmap.SourceData);
                        pkg.Write(HeightmapAmount);
                        pkg.Write(HeightmapBlend);
                        pkg.Write(HeightmapAdd);
                    }

                    pkg.Write(OceanChannelsEnabled);

                    // Version 2
                    if (Version >= 2)
                    {
                        pkg.Write(RiversEnabled);
                        //pkg.Write(LakesEnabled);

                        pkg.Write(Biomemap?.FilePath ?? string.Empty);
                        if (Biomemap != null)
                        {
                            pkg.Write(Biomemap.SourceData);
                        }

                        pkg.Write(ForestScale);
                        pkg.Write(ForestAmountOffset);

                        pkg.Write(OverrideStartPosition);
                        pkg.Write(StartPositionX);
                        pkg.Write(StartPositionY);
                    }
                    
                    // Version 3
                    if (Version >= 3)
                    {
                        pkg.Write(Spawnmap?.FilePath ?? string.Empty);
                        if (Spawnmap != null)
                        {
                            Spawnmap.Serialize(pkg);
                        }
                    }
                    
                    // Version 4
                    if (Version >= 5)
                    {
                        pkg.Write(Roughmap?.FilePath ?? string.Empty);
                        if (Roughmap != null)
                        {
                            pkg.Write(Roughmap.SourceData);
                            pkg.Write(RoughmapBlend);
                        }

                        pkg.Write(UseRoughInvertedAsFlat);
                        pkg.Write(FlatmapBlend);
                        if (!UseRoughInvertedAsFlat)
                        {
                            pkg.Write(Flatmap?.FilePath ?? string.Empty);
                            if (Flatmap != null)
                            {
                                pkg.Write(Flatmap.SourceData);
                            }
                        }

                        pkg.Write(Forestmap?.FilePath ?? string.Empty);
                        if (Forestmap != null)
                        {
                            pkg.Write(Forestmap.SourceData);
                            pkg.Write(ForestmapMultiply);
                            pkg.Write(ForestmapAdd);
                        }
                        
                        pkg.Write(DisableMapEdgeDropoff);
                        pkg.Write(MountainsAllowedAtCenter);
                        pkg.Write(ForestFactorOverrideAllTrees);
                    }
                }
            }

            public static BetterContinentsSettings Load(ZPackage pkg)
            {
                var settings = new BetterContinentsSettings();
                settings.Deserialize(pkg);
                return settings;
            }
            
            private void Deserialize(ZPackage pkg)
            {
                Version = pkg.ReadInt();
                if (Version > LatestVersion)
                {
                    LogError($"BetterContinents mod is out of date: world expects config version {Version}, mod config version is {LatestVersion}");
                    throw new Exception($"BetterContinents mod is out of date: world expects config version {Version}, mod config version is {LatestVersion}");
                }

                WorldUId = pkg.ReadLong();
                
                EnabledForThisWorld = pkg.ReadBool();

                if (EnabledForThisWorld)
                {
                    GlobalScale = pkg.ReadSingle();
                    MountainsAmount = pkg.ReadSingle();
                    SeaLevelAdjustment = pkg.ReadSingle();

                    MaxRidgeHeight = pkg.ReadSingle();
                    RidgeScale = pkg.ReadSingle();
                    RidgeBlendSigmoidB = pkg.ReadSingle();
                    RidgeBlendSigmoidXOffset = pkg.ReadSingle();

                    var heightmapFilePath = pkg.ReadString();
                    if (!string.IsNullOrEmpty(heightmapFilePath))
                    {
                        Heightmap = new ImageMapFloat(heightmapFilePath, pkg.ReadByteArray());
                        if (Version <= 4 && !Heightmap.CreateMapLegacy() 
                            || Version > 4 && !Heightmap.CreateMap())
                        {
                            Heightmap = null;
                        }
                        HeightmapAmount = pkg.ReadSingle();
                        HeightmapBlend = pkg.ReadSingle();
                        HeightmapAdd = pkg.ReadSingle();
                    }

                    OceanChannelsEnabled = pkg.ReadBool();

                    if (Version >= 2)
                    {
                        RiversEnabled = pkg.ReadBool();
                        //LakesEnabled = pkg.ReadBool();
                        
                        var biomemapFilePath = pkg.ReadString();
                        if (!string.IsNullOrEmpty(biomemapFilePath))
                        {
                            Biomemap = new ImageMapBiome(biomemapFilePath, pkg.ReadByteArray());
                            if (!Biomemap.CreateMap())
                            {
                                Biomemap = null;
                            }
                        }

                        ForestScale = pkg.ReadSingle();
                        ForestAmountOffset = pkg.ReadSingle();
                        
                        OverrideStartPosition = pkg.ReadBool();
                        StartPositionX = pkg.ReadSingle();
                        StartPositionY = pkg.ReadSingle();
                    }
                    else
                    {
                        RiversEnabled = true;
                        ForestScale = 1;
                        ForestAmountOffset = 0;
                        OverrideStartPosition = false;
                        StartPositionX = 0;
                        StartPositionY = 0;
                        //LakesEnabled = true;
                    }
                    
                    // Version 3
                    if (Version >= 3)
                    {
                        var spawnmapFilePath = pkg.ReadString();
                        if (!string.IsNullOrEmpty(spawnmapFilePath))
                        {
                            Spawnmap = new ImageMapSpawn(spawnmapFilePath, pkg);
                        }
                    }
                    
                    // Version 4
                    // Nothing...
                    
                    // Version 5
                    if (Version >= 5)
                    {
                        var roughmapFilePath = pkg.ReadString();
                        if (!string.IsNullOrEmpty(roughmapFilePath))
                        {
                            Roughmap = new ImageMapFloat(roughmapFilePath, pkg.ReadByteArray());
                            if (!Roughmap.CreateMap())
                            {
                                Roughmap = null;
                            }
                            RoughmapBlend = pkg.ReadSingle();
                        }

                        UseRoughInvertedAsFlat = pkg.ReadBool();
                        FlatmapBlend = pkg.ReadSingle();
                        if (!UseRoughInvertedAsFlat)
                        {
                            var flatmapFilePath = pkg.ReadString();
                            if (!string.IsNullOrEmpty(flatmapFilePath))
                            {
                                Flatmap = new ImageMapFloat(flatmapFilePath, pkg.ReadByteArray());
                                if (!Flatmap.CreateMap())
                                {
                                    Flatmap = null;
                                }
                            }
                        }

                        var forestmapFilePath = pkg.ReadString();
                        if (!string.IsNullOrEmpty(forestmapFilePath))
                        {
                            Forestmap = new ImageMapFloat(forestmapFilePath, pkg.ReadByteArray());
                            if (!Forestmap.CreateMap())
                            {
                                Forestmap = null;
                            }
                            ForestmapMultiply = pkg.ReadSingle();
                            ForestmapAdd = pkg.ReadSingle();
                        }
                        
                        DisableMapEdgeDropoff = pkg.ReadBool();
                        MountainsAllowedAtCenter = pkg.ReadBool();
                        ForestFactorOverrideAllTrees = pkg.ReadBool();
                    }
                }
            }

            public float ApplyHeightmap(float x, float y, float height)
            {
                if (this.Heightmap == null || (this.HeightmapBlend == 0 && this.HeightmapAdd == 0))
                {
                    return height;
                }

                float h = this.Heightmap.GetValue(x, y);

                return Mathf.Lerp(height, h * HeightmapAmount, this.HeightmapBlend) + h * this.HeightmapAdd;
            }
            
            public float ApplyRoughmap(float x, float y, float smoothHeight, float roughHeight)
            {
                if (this.Roughmap == null || this.RoughmapBlend == 0)
                {
                    return roughHeight;
                }

                float r = this.Roughmap.GetValue(x, y);

                return Mathf.Lerp(smoothHeight, roughHeight, r * this.RoughmapBlend);
            }
            
            public float ApplyFlatmap(float x, float y, float flatHeight, float height)
            {
                if ((this.UseRoughInvertedAsFlat && this.Roughmap == null) || (!this.UseRoughInvertedAsFlat && this.Flatmap == null) || this.RoughmapBlend == 0)
                {
                    return height;
                }

                float f = this.UseRoughInvertedAsFlat ? 1 - this.Roughmap.GetValue(x, y) : this.Flatmap.GetValue(x, y); 

                return Mathf.Lerp(height, flatHeight, f * this.FlatmapBlend);
            }
            
            public float ApplyForest(float x, float y, float forest)
            {
                float finalValue = forest;
                if (this.Forestmap != null)
                {
                    // Map forest from weird vanilla range to 0 - 1
                    float normalizedForestValue = Mathf.InverseLerp(1.850145f, 0.145071f, forest);
                    float fmap = this.Forestmap.GetValue(x, y);
                    float calculatedValue = Mathf.Lerp(normalizedForestValue, normalizedForestValue * fmap, ForestmapMultiply) + fmap * ForestmapAdd;
                    // Map back to weird values
                    finalValue = Mathf.Lerp(1.850145f, 0.145071f, calculatedValue);
                    // Log($"Forest: {forest}, {normalizedForestValue}, {fmap} => {finalValue}");
                }

                // Clamp between the known good values (that vanilla generates)
                finalValue = Mathf.Clamp(finalValue + ForestAmountOffset, 0.145071f, 1.850145f);
                return finalValue;
            }
            
            public Heightmap.Biome GetBiomeOverride(float mapX, float mapY) => this.Biomemap.GetValue(mapX, mapY);

            public Vector2? FindSpawn(string spawn) => this.Spawnmap.FindSpawn(spawn);
            public IEnumerable<Vector2> GetAllSpawns(string spawn) => this.Spawnmap.GetAllSpawns(spawn);

            public void ReloadHeightmap()
            {
                if (Heightmap != null)
                {
                    if (Heightmap.LoadSourceImage())
                    {
                        Heightmap.CreateMap();
                    }
                }
            }
            
            public void ReloadBiomemap()
            {
                if (Biomemap != null)
                {
                    if (Biomemap.LoadSourceImage())
                    {
                        Biomemap.CreateMap();
                    }
                }
            }
            
            public void ReloadSpawnmap()
            {
                if (Spawnmap != null)
                {
                    if (Spawnmap.LoadSourceImage())
                    {
                        Spawnmap.CreateMap();
                    }
                }
            }       
            
            public void ReloadRoughmap()
            {
                if (Roughmap != null)
                {
                    if (Roughmap.LoadSourceImage())
                    {
                        Roughmap.CreateMap();
                    }
                }
            }
                        
            public void ReloadFlatmap()
            {
                if (UseRoughInvertedAsFlat)
                {
                    ReloadRoughmap();
                }
                else if (Flatmap != null)
                {
                    if (Flatmap.LoadSourceImage())
                    {
                        Flatmap.CreateMap();
                    }
                }
            }
        }
        
        private static BetterContinentsSettings Settings;
        
        private void Awake()
        {
            // Cos why...
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            // Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);

            Console.SetConsoleEnabled(true);

            Config.Declare()
                .Group("BetterContinents.Global")
                    .Value("Enabled").Description("Whether this mod is enabled").Default(true).Bind(out ConfigEnabled)
                    .Value("Continent Size").Description("Continent size").Default(0.5f).Range(0f, 1f).Bind(out ConfigContinentSize)
                    .Value("Mountains Amount").Description("Mountains amount").Default(0.5f).Range(0f, 1f).Bind(out ConfigMountainsAmount)
                    .Value("Sea Level Adjustment").Description("Modify sea level, which changes the land:sea ratio").Default(0.5f).Range(0f, 1f).Bind(out ConfigSeaLevelAdjustment)
                    .Value("Ocean Channels").Description("Whether ocean channels should be enabled or not (useful to disable when using height map for instance)").Default(true).Bind(out ConfigOceanChannelsEnabled)
                    .Value("Rivers").Description("Whether rivers should be enabled or not").Default(true).Bind(out ConfigRiversEnabled)
                    .Value("Map Edge Drop-off").Description("Whether the map should drop off at the edges or not (consequences unknown!)").Default(true).Bind(out ConfigMapEdgeDropoff)
                    .Value("Mountains Allowed At Center").Description("Whether the map should allow mountains to occur at the map center (if you have default spawn then you should keep this unchecked)").Default(false).Bind(out ConfigMountainsAllowedAtCenter)
                .Group("BetterContinents.Project")
                    .Value("Directory").Description("This directory will load automatically any existing map files matching the correct names, overriding specific files specified below. Filenames must match: heightmap.png, biomemap.png, spawnmap.png, roughmap.png, flatmap.png, forestmap.png.").Bind(out ConfigMapSourceDir)
                .Group("BetterContinents.Heightmap")
                    .Value("Heightmap File").Description("Path to a heightmap file to use. See the description on Nexusmods.com for the specifications (it will fail if they are not met)").Bind(out ConfigHeightmapFile)
                    .Value("Heightmap Amount").Description("Multiplier of the height value from the heightmap file (more than 1 leads to higher max height than vanilla, good results are not guaranteed)").Default(1f).Range(0f, 5f).Bind(out ConfigHeightmapAmount)
                    .Value("Heightmap Blend").Description("How strongly to blend the heightmap file into the final result").Default(1f).Range(0f, 1f).Bind(out ConfigHeightmapBlend)
                    .Value("Heightmap Add").Description("How strongly to add the heightmap file to the final result (usually you want to blend it instead)").Default(0f).Range(-1f, 1f).Bind(out ConfigHeightmapAdd)
                .Group("BetterContinents.Biomemap")
                    .Value("Biomemap").Description("Path to a biomemap file to use. See the description on Nexusmods.com for the specifications (it will fail if they are not met)").Bind(out ConfigBiomemapFile)
                .Group("BetterContinents.Forest")
                    .Value("Forest Scale").Description("Scales forested/cleared area size").Default(0.5f).Range(0f, 1f).Bind(out ConfigForestScale)
                    .Value("Forest Amount").Description("Adjusts how much forest there is, relative to clearings").Default(0.5f).Range(0f, 1f).Bind(out ConfigForestAmount)
                    .Value("Forest Factor Overrides All Trees").Description("Trees in all biomes will be affected by forest factor (both procedural and from forestmap)").Default(false).Bind(out ConfigForestFactorOverrideAllTrees)
                    .Value("Forestmap").Description("Path to a forestmap file to use. See the description on Nexusmods.com for the specifications (it will fail if they are not met)").Bind(out ConfigForestmapFile)
                    .Value("Forestmap Multiply").Description("How strongly to scale the vanilla forest factor by the forestmap").Default(1f).Range(0f, 1f).Bind(out ConfigForestmapMultiply)
                    .Value("Forestmap Add").Description("How strongly to add the forestmap directly to the vanilla forest factor").Default(1f).Range(0f, 1f).Bind(out ConfigForestmapAdd)
                .Group("BetterContinents.Spawnmap")
                    .Value("Spawnmap").Description("Path to a spawnmap file to use. See the description on Nexusmods.com for the specifications (it will fail if they are not met)").Bind(out ConfigSpawnmapFile)
                .Group("BetterContinents.Roughmap")
                    .Value("Roughmap").Description("Path to a roughmap file to use. See the description on Nexusmods.com for the specifications (it will fail if they are not met)").Bind(out ConfigRoughmapFile)
                    .Value("Roughmap Blend").Description("How strongly to apply the roughmap file").Default(1f).Range(0f, 1f).Bind(out ConfigRoughmapBlend)
                .Group("BetterContinents.Flatmap")
                    .Value("Use Roughmap For Flatmap").Description("Use the flatmap as the rough map, but inverted (black rough map results in totally flat terrain)").Default(true).Bind(out ConfigUseRoughInvertedForFlat)
                    .Value("Flatmap").Description("Path to a flatmap file to use. See the description on Nexusmods.com for the specifications (it will fail if they are not met)").Bind(out ConfigFlatmapFile)
                    .Value("Flatmap Blend").Description("How strongly to apply the flatmap file (also applies when using Use Roughmap For Flatmap)").Default(1f).Range(0f, 1f).Bind(out ConfigFlatmapBlend)
                .Group("BetterContinents.Ridges")
                    .Value("Max Ridge Height").Description("Max height of ridge features (set this to 0 to turn OFF ridges entirely)").Default(0.5f).Range(0f, 1f).Bind(out ConfigMaxRidgeHeight)
                    .Value("Ridge Size").Description("Size of ridge features").Default(0.5f).Range(0f, 1f).Bind(out ConfigRidgeSize)
                    .Value("Ridge Blend").Description("Smoothness of ridges blending into base terrain").Default(0.5f).Range(0f, 1f).Bind(out ConfigRidgeBlend)
                    .Value("Ridge Amount").Description("How much ridges").Default(0.5f).Range(0f, 1f).Bind(out ConfigRidgeAmount)
                .Group("BetterContinents.StartPosition")
                    .Value("Override Start Position").Description("Whether to override the start position using the values provided (warning: will disable all validation of the position)").Default(false).Bind(out ConfigOverrideStartPosition)
                    .Value("Start Position X").Description("Start position override X value, in ranges -10500 to 10500").Default(0f).Range(-10500f, 10500f).Bind(out ConfigStartPositionX)
                    .Value("Start Position Y").Description("Start position override Y value, in ranges -10500 to 10500").Default(0f).Range(-10500f, 10500f).Bind(out ConfigStartPositionY)
                .Group("BetterContinents.Debug")
                    .Value("Debug Mode").Description("Automatically reveals the full map on respawn, enables cheat mode, and debug mode, for debugging purposes").Bind(out ConfigDebugModeEnabled)
                    .Value("Skip Default Location Placement").Description("Skips default location placement during world gen (spawn temple and spawnmap are still placed), for quickly testing the heightmap itself").Bind(out ConfigDebugSkipDefaultLocationPlacement)
                ;

            new Harmony("BetterContinents.Harmony").PatchAll();
            Log("Awake");
        }
        
        private static T GetDelegate<T>(Type type, string method) where T : Delegate 
            => AccessTools.MethodDelegate<T>(
                AccessTools.Method(type, method));
        
        // Saving and removing of worlds
        [HarmonyPatch(typeof(World))]
        private class WorldPatch
        {
            // When the world metadata is saved we write an extra file next to it for our own config
            [HarmonyPrefix, HarmonyPatch(nameof(World.SaveWorldMetaData))]
            private static void SaveWorldMetaDataPrefix(World __instance)
            {
                Log($"Saving settings for {__instance.m_name}");

                BetterContinentsSettings settingsToSave = default;
                
                // Vanilla metadata is always saved when a world is created for the first time, before it is actually loaded or generated.
                // So if that metadata doesn't exist it means the world is being created now.
                if (!File.Exists(__instance.GetMetaPath()))
                {
                    // World is being created, so bake our settings as they currently are.
                    Log($"First time save of {__instance.m_name}, baking settings");
                    settingsToSave = BetterContinentsSettings.Create(__instance.m_uid);
                }
                else
                {
                    settingsToSave = Settings;
                }
                settingsToSave.Dump();

                var zpackage = new ZPackage();
                settingsToSave.Serialize(zpackage);
                
                // Duplicating the careful behaviour of the metadata save function
                string ourMetaPath = __instance.GetMetaPath() + ".BetterContinents";
                string newName = ourMetaPath + ".new";
                string oldName = ourMetaPath + ".old";
                byte[] binaryData = zpackage.GetArray();
                using (BinaryWriter binaryWriter = new BinaryWriter(File.Create(newName)))
                {
                    binaryWriter.Write(binaryData.Length);
                    binaryWriter.Write(binaryData);
                }
                if (File.Exists(ourMetaPath))
                {
                    if (File.Exists(oldName))
                    {
                        File.Delete(oldName);
                    }
                    File.Move(ourMetaPath, oldName);
                }
                File.Move(newName, ourMetaPath);
            }

            [HarmonyPostfix, HarmonyPatch(nameof(World.RemoveWorld))]
            private static void RemoveWorldPostfix(string name)
            {
                try
                {
                    File.Delete(World.GetMetaPath(name) + ".BetterContinents");
                    Log($"Deleted saved settings for {name}");
                }
                catch
                {
                    // ignored
                }
            }
        }

        private static string LastConnectionError = null;
        
        // Dealing with settings, synchronization of them in multiplayer
        [HarmonyPatch(typeof(ZNet))]
        private class ZNetPatch
        {
            // When the world is set on the server (applies to single player as well), we should select the correct loaded settings
            [HarmonyPrefix, HarmonyPatch(nameof(ZNet.SetServer))]
            private static void SetServerPrefix(bool server, World world)
            {
                if (server)
                {
                    Log($"Selected world {world.m_name}, applying settings");

                    // Load in our settings for this world
                    try
                    {
                        using (var binaryReader = new BinaryReader(File.OpenRead(world.GetMetaPath() + ".BetterContinents")))
                        {
                            int count = binaryReader.ReadInt32();
                            var newSettings = BetterContinentsSettings.Load(new ZPackage(binaryReader.ReadBytes(count)));
                            if (newSettings.WorldUId != world.m_uid)
                            {
                                LogError($"ID in saved settings for {world.m_name} didn't match, mod is disabled for this World");
                            }
                            else
                            {
                                Settings = newSettings;
                            }
                        }
                    }
                    catch
                    {
                        Log($"Couldn't find loaded settings for world {world.m_name}, mod is disabled for this World");
                        Settings = BetterContinentsSettings.Disabled(world.m_uid);
                    }
            
                    Settings.Dump();
                }
                else
                {
                    // Disable the mod so we don't end up breaking if the server doesn't use it
                    Log($"Joining a server, so disabling local settings");
                    Settings = BetterContinentsSettings.Disabled();
                }
            }

            private static byte[] SettingsReceiveBuffer;
            private static int SettingsReceiveBufferBytesReceived;
            private static int SettingsReceiveHash;
            
            private static int GetHashCode<T>(T[] array)
            {
                unchecked
                {
                    if (array == null)
                    {
                        return 0;
                    }
                    int hash = 17;
                    foreach (T element in array)
                    {
                        hash = hash * 31 + element.GetHashCode();
                    }
                    return hash;
                }
            }

            private static string ServerVersion;

            // Register our RPC for receiving settings on clients
            [HarmonyPrefix, HarmonyPatch("OnNewConnection")]
            private static void OnNewConnectionPrefix(ZNetPeer peer)
            {
                Log($"Registering settings RPC");

                ServerVersion = "(old)";

                peer.m_rpc.Register("BetterContinentsVersion", (ZRpc rpc, string serverVersion) =>
                {
                    ServerVersion = serverVersion;
                    Log($"Receiving server version {serverVersion}");
                });

                peer.m_rpc.Register("BetterContinentsConfigStart", (ZRpc rpc, int totalBytes, int hash) =>
                {
                    SettingsReceiveBuffer = new byte[totalBytes];
                    SettingsReceiveHash = hash;
                    SettingsReceiveBufferBytesReceived = 0;
                    Log($"Receiving settings from server ({SettingsReceiveBuffer.Length} bytes)");
                });

                peer.m_rpc.Register("BetterContinentsConfigPacket", (ZRpc rpc, int offset, int packetHash, ZPackage packet) =>
                {
                    var m_connectionStatus = AccessTools.Field(typeof(ZNet), "m_connectionStatus");

                    var packetData = packet.GetArray();
                    int hash = GetHashCode(packetData);
                    if (hash != packetHash)
                    {
                        LastConnectionError = $"Better Continents settings from server were corrupted";
                        LogError($"{LastConnectionError}: packet hash mismatch, got {hash}, expected {packetHash}");
                        
                        m_connectionStatus.SetValue(null, ZNet.ConnectionStatus.ErrorConnectFailed);
                        // ZNet.m_connectionStatus = ZNet.ConnectionStatus.ErrorConnectFailed;
                        ZNet.instance.Disconnect(peer);
                        return;
                    }

                    Buffer.BlockCopy(packetData, 0, SettingsReceiveBuffer, offset, packetData.Length);
                    
                    SettingsReceiveBufferBytesReceived += packetData.Length;

                    Log($"Received settings packet {packetData.Length} bytes at {offset}, {SettingsReceiveBufferBytesReceived} received so far");
                    if (SettingsReceiveBufferBytesReceived == SettingsReceiveBuffer.Length)
                    {
                        int finalHash = GetHashCode(SettingsReceiveBuffer);
                        if (finalHash == SettingsReceiveHash)
                        {
                            Log($"Settings transfer complete");

                            Settings = BetterContinentsSettings.Load(new ZPackage(SettingsReceiveBuffer));
                            Settings.Dump();

                            // We only care about server/client version match when the server sends a world that actually uses the mod
                            if (Settings.EnabledForThisWorld && ServerVersion != ModInfo.Version)
                            {
                                LastConnectionError = $"Server world has Better Continents enabled, but server mod {ServerVersion} and client mod {ModInfo.Version} don't match";
                                LogError(LastConnectionError);
                                m_connectionStatus.SetValue(null, ZNet.ConnectionStatus.ErrorConnectFailed);
                                //ZNet.m_connectionStatus = ZNet.ConnectionStatus.ErrorConnectFailed;
                                ZNet.instance.Disconnect(peer);
                            }
                            else if (!Settings.EnabledForThisWorld)
                            {
                                Log($"Server world does not have Better Continents enabled, skipping version check");
                            }
                        }
                        else
                        {
                            LogError($"{LastConnectionError}: hash mismatch, got {finalHash}, expected {SettingsReceiveHash}");
                            m_connectionStatus.SetValue(null, ZNet.ConnectionStatus.ErrorConnectFailed);
                            //ZNet.m_connectionStatus = ZNet.ConnectionStatus.ErrorConnectFailed;
                            ZNet.instance.Disconnect(peer);
                        }
                    }
                });
            }
            
            private static byte[] ArraySlice(byte[] source, int offset, int length)
            {
                var target = new byte[length];
                Buffer.BlockCopy(source, offset, target, 0, length);
                return target;
            }
            
            // Send our clients the settings for the currently loaded world. We do this before
            // the body of the SendPeerInfo function, so as to ensure the data arrives before we might need it.
            [HarmonyPrefix, HarmonyPatch("RPC_PeerInfo")]
            private static bool RPC_PeerInfoPrefix(ZNet __instance, ZRpc rpc, ZPackage pkg)
            {
                if (__instance.IsServer())
                {
                    __instance.StartCoroutine(SendSettings(__instance, rpc, pkg));
                    return false;
                }
                else
                {
                    return true;
                }
            }

            [HarmonyReversePatch]
            [HarmonyPatch(typeof(ZNet), "RPC_PeerInfo")]
            public static void RPC_PeerInfo(object instance, ZRpc rpc, ZPackage pkg)
            {
                // its a stub so it has no initial content
                throw new NotImplementedException("It's a stub");
            }
            
            private static IEnumerator SendSettings(ZNet instance, ZRpc rpc, ZPackage pkg)
            {
                Log($"Sending version {ModInfo.Version} to new client");
                rpc.Invoke("BetterContinentsVersion", ModInfo.Version);
                
                Log($"Sending settings to new client");
                Settings.Dump();
                
                var settingsPackage = new ZPackage();
                Settings.Serialize(settingsPackage);

                var settingsData = settingsPackage.GetArray();
                Log($"Sending settings package header for {settingsData.Length} byte stream");
                rpc.Invoke("BetterContinentsConfigStart", settingsData.Length, GetHashCode(settingsData));

                const int SendChunkSize = 256 * 1024;

                for (int sentBytes = 0; sentBytes < settingsData.Length; )
                {
                    int packetSize = Mathf.Min(settingsData.Length - sentBytes, SendChunkSize);
                    var packet = ArraySlice(settingsData, sentBytes, packetSize);
                    rpc.Invoke("BetterContinentsConfigPacket", sentBytes, GetHashCode(packet), new ZPackage(packet));
                    // Make sure to flush or we will saturate the queue...
                    rpc.GetSocket().Flush();
                    sentBytes += packetSize;
                    Log($"Sent {sentBytes} of {settingsData.Length} bytes");
                    // Thread.Sleep(2000);
                    yield return new WaitUntil(() => rpc.GetSocket().GetSendQueueSize() < SendChunkSize);
                }

                RPC_PeerInfo(instance, rpc, pkg);
            }
        }

        // Changes to height, biome, forests, rivers etc. (this is the functional part of the mod)
        [HarmonyPatch(typeof(WorldGenerator))]
        private class WorldGeneratorPatch
        {
            // The base map x, y coordinates in 0..1 range
            private static Dictionary<Vector2, float> cachedHeights; 

            private static readonly string[] TreePrefixes =
            {
                "FirTree",
                "Pinetree_01",
                "SwampTree2_darkland",
                "SwampTree1",
                "SwampTree2",
                "FirTree_small",
                "FirTree_small_dead",
                "HugeRoot1",
                "SwampTree2_log",
                "FirTree_oldLog",
                "vertical_web",
                "horizontal_web",
                "tunnel_web",
            };
            
            [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.Initialize))]
            private static void InitializePrefix(World world)
            {
                cachedHeights = new Dictionary<Vector2, float>(100000);
                
                if(Settings.EnabledForThisWorld && !world.m_menu && Settings.ForestFactorOverrideAllTrees && ZoneSystem.instance != null)
                {
                    foreach (var v in ZoneSystem.instance.m_vegetation)
                    {
                        if (TreePrefixes.Contains(v.m_prefab.name))
                        {
                            v.m_inForest = true;
                            v.m_forestTresholdMin = 0f;
                            v.m_forestTresholdMax = 1.15f;
                        }
                        // Log($"{v.m_name}, {v.m_prefab}, {v.m_biomeArea}, {v.m_biome}, {v.m_inForest}, {v.m_forestTresholdMin}, {v.m_forestTresholdMax}, {v.m_forcePlacement}");
                    }
                }
            }
            
            // wx, wy are [-10500, 10500]
            // __result should be [0, 1]
            [HarmonyPrefix, HarmonyPatch("GetBaseHeight")]
            private static bool GetBaseHeightPrefix(ref float wx, ref float wy, bool menuTerrain, ref float __result, float ___m_offset0, float ___m_offset1, float ___m_minMountainDistance)
            {
                if (!Settings.EnabledForThisWorld || menuTerrain)
                {
                    return true;
                }
                
                if (cachedHeights.TryGetValue(new Vector2(wx, wy), out __result))
                {
                    return false;
                }
                
                switch (Settings.Version)
                {
                    case 1:
                    case 2:
                        __result = GetBaseHeightV1(wx, wy, ___m_offset0, ___m_offset1, ___m_minMountainDistance);
                        break;
                    case 3:
                    default:
                        __result = GetBaseHeightV2(wx, wy, ___m_offset0, ___m_offset1, ___m_minMountainDistance);
                        break;
                }

                return false;
            }

                
            [HarmonyPostfix, HarmonyPatch("GetBaseHeight")]
            private static void GetBaseHeightPostfix(float wx, float wy, float ___m_offset0, float ___m_offset1, bool menuTerrain, float __result, World ___m_world)
            {
                if (!Settings.EnabledForThisWorld || ___m_world.m_menu)
                {
                    return;
                }
                
                if (cachedHeights.Count >= 100000)
                {
                    // Can't easily do LRU, so we will just clear it entirely
                    cachedHeights.Clear();
                }
                // Do this AFTER clearing so we have it available below
                cachedHeights[new Vector2(wx, wy)] = __result;
            }
            
            private delegate float GetBaseHeightDelegate(WorldGenerator instance, float wx, float wy, bool menuTerrain);
            private static readonly GetBaseHeightDelegate GetBaseHeightMethod 
                = GetDelegate<GetBaseHeightDelegate>(typeof(WorldGenerator), "GetBaseHeight");
            
            [HarmonyPostfix, HarmonyPatch("GetBiomeHeight")]
            private static void GetBiomeHeightPostfix(WorldGenerator __instance, float wx, float wy, ref float __result, World ___m_world)
            {
                if (!Settings.EnabledForThisWorld || ___m_world.m_menu || !Settings.UseRoughmap)
                {
                    return;
                }

                float smoothHeight = GetBaseHeightMethod(__instance, wx, wy, false) * 200f;
                __result = Settings.ApplyRoughmap(WorldX(wx), WorldY(wy), smoothHeight, __result);
            }

            private static float GetBaseHeightV1(float wx, float wy, float ___m_offset0, float ___m_offset1, float ___m_minMountainDistance)
            {
                float distance = Utils.Length(wx, wy);
                
                // The base map x, y coordinates in 0..1 range
                float mapX = NormalizedX(wx);
                float mapY = NormalizedY(wy);
                
                wx *= Settings.GlobalScale;
                wy *= Settings.GlobalScale;

                float WarpScale = 0.001f * Settings.RidgeScale;

                float warpX = (Mathf.PerlinNoise(wx * WarpScale, wy * WarpScale) - 0.5f) * WorldSize;
                float warpY = (Mathf.PerlinNoise(wx * WarpScale + 2f, wy * WarpScale + 3f) - 0.5f) * WorldSize;

                wx += 100000f + ___m_offset0;
                wy += 100000f + ___m_offset1;

                float bigFeatureNoiseHeight = Mathf.PerlinNoise(wx * 0.002f * 0.5f, wy * 0.002f * 0.5f) * Mathf.PerlinNoise(wx * 0.003f * 0.5f, wy * 0.003f * 0.5f) * 1f;
                float bigFeatureHeight = Settings.ApplyHeightmap(mapX, mapY, bigFeatureNoiseHeight);
                float ridgeHeight = (Mathf.PerlinNoise(warpX * 0.002f * 0.5f, warpY * 0.002f * 0.5f) * Mathf.PerlinNoise(warpX * 0.003f * 0.5f, warpY * 0.003f * 0.5f)) * Settings.MaxRidgeHeight;

                // https://www.desmos.com/calculator/uq8wmu6dy7
                float SigmoidActivation(float x, float a, float b) => 1 / (1 + Mathf.Exp(a + b * x));
                float lerp = Mathf.Clamp01(SigmoidActivation(Mathf.PerlinNoise(wx * 0.005f - 10000, wy * 0.005f - 5000) - Settings.RidgeBlendSigmoidXOffset, 0, Settings.RidgeBlendSigmoidB));
                
                float finalHeight = 0f;

                float bigFeature = Mathf.Clamp01(Mathf.Lerp(bigFeatureHeight, ridgeHeight, lerp));
                const float SeaLevel = 0.05f;
                float ApplyMountains(float x, float n) => x * (1 - Mathf.Pow(1 - x, 1.2f + n * 0.8f)) + x * (1 - x);

                finalHeight += ApplyMountains(bigFeature - SeaLevel, Settings.MountainsAmount) + SeaLevel;

                finalHeight += Mathf.PerlinNoise(wx * 0.002f * 1f, wy * 0.002f * 1f) * Mathf.PerlinNoise(wx * 0.003f * 1f, wy * 0.003f * 1f) * finalHeight * 0.9f;

                finalHeight += Mathf.PerlinNoise(wx * 0.005f * 1f, wy * 0.005f * 1f) * Mathf.PerlinNoise(wx * 0.01f * 1f, wy * 0.01f * 1f) * 0.5f * finalHeight;

                finalHeight -= 0.07f;

                finalHeight += Settings.SeaLevelAdjustment;

                if (Settings.OceanChannelsEnabled)
                {
                    float v = Mathf.Abs(
                        Mathf.PerlinNoise(wx * 0.002f * 0.25f + 0.123f, wy * 0.002f * 0.25f + 0.15123f) -
                        Mathf.PerlinNoise(wx * 0.002f * 0.25f + 0.321f, wy * 0.002f * 0.25f + 0.231f));
                    finalHeight *= 1f - (1f - Utils.LerpStep(0.02f, 0.12f, v)) *
                        Utils.SmoothStep(744f, 1000f, distance);
                }

                // Edge of the world
                if (distance > 10000f)
                {
                    float t = Utils.LerpStep(10000f, 10500f, distance);
                    finalHeight = Mathf.Lerp(finalHeight, -0.2f, t);
                    if (distance > 10490f)
                    {
                        float t2 = Utils.LerpStep(10490f, 10500f, distance);
                        finalHeight = Mathf.Lerp(finalHeight, -2f, t2);
                    }
                }
                if (distance < ___m_minMountainDistance && finalHeight > 0.28f)
                {
                    float t3 = Mathf.Clamp01((finalHeight - 0.28f) / 0.099999994f);
                    finalHeight = Mathf.Lerp(Mathf.Lerp(0.28f, 0.38f, t3), finalHeight, Utils.LerpStep(___m_minMountainDistance - 400f, ___m_minMountainDistance, distance));
                }
                return finalHeight;
            }
            
            private static float GetBaseHeightV2(float wx, float wy, float ___m_offset0, float ___m_offset1, float ___m_minMountainDistance)
            {
                float distance = Utils.Length(wx, wy);
                
                // The base map x, y coordinates in 0..1 range
                float mapX = NormalizedX(wx);
                float mapY = NormalizedY(wy);
                
                wx *= Settings.GlobalScale;
                wy *= Settings.GlobalScale;

                float WarpScale = 0.001f * Settings.RidgeScale;

                float warpX = (Mathf.PerlinNoise(wx * WarpScale, wy * WarpScale) - 0.5f) * WorldSize;
                float warpY = (Mathf.PerlinNoise(wx * WarpScale + 2f, wy * WarpScale + 3f) - 0.5f) * WorldSize;

                wx += 100000f + ___m_offset0;
                wy += 100000f + ___m_offset1;

                float bigFeatureNoiseHeight = Mathf.PerlinNoise(wx * 0.002f * 0.5f, wy * 0.002f * 0.5f) * Mathf.PerlinNoise(wx * 0.003f * 0.5f, wy * 0.003f * 0.5f) * 1f;
                float bigFeatureHeight = Settings.ApplyHeightmap(mapX, mapY, bigFeatureNoiseHeight);
                float ridgeHeight = (Mathf.PerlinNoise(warpX * 0.002f * 0.5f, warpY * 0.002f * 0.5f) * Mathf.PerlinNoise(warpX * 0.003f * 0.5f, warpY * 0.003f * 0.5f)) * Settings.MaxRidgeHeight;
                
                // https://www.desmos.com/calculator/uq8wmu6dy7
                float SigmoidActivation(float x, float a, float b) => 1 / (1 + Mathf.Exp(a + b * x));
                float lerp = Mathf.Clamp01(SigmoidActivation(Mathf.PerlinNoise(wx * 0.005f - 10000, wy * 0.005f - 5000) - Settings.RidgeBlendSigmoidXOffset, 0, Settings.RidgeBlendSigmoidB));

                float bigFeature = Mathf.Clamp01(bigFeatureHeight + ridgeHeight * lerp);
                
                const float SeaLevel = 0.05f;
                float ApplyMountains(float x, float n) => x * (1 - Mathf.Pow(1 - x, 1.2f + n * 0.8f)) + x * (1 - x);
                
                float detailedFinalHeight = ApplyMountains(bigFeature - SeaLevel, Settings.MountainsAmount) + SeaLevel;

                // Finer height variation
                detailedFinalHeight += Mathf.PerlinNoise(wx * 0.002f * 1f, wy * 0.002f * 1f) * Mathf.PerlinNoise(wx * 0.003f * 1f, wy * 0.003f * 1f) * detailedFinalHeight * 0.9f;
                detailedFinalHeight += Mathf.PerlinNoise(wx * 0.005f * 1f, wy * 0.005f * 1f) * Mathf.PerlinNoise(wx * 0.01f * 1f, wy * 0.01f * 1f) * 0.5f * detailedFinalHeight;

                float finalHeight = Settings.ApplyFlatmap(mapX, mapY, bigFeatureHeight, detailedFinalHeight);

                finalHeight -= 0.07f;

                finalHeight += Settings.SeaLevelAdjustment;

                if (Settings.OceanChannelsEnabled)
                {
                    float v = Mathf.Abs(
                        Mathf.PerlinNoise(wx * 0.002f * 0.25f + 0.123f, wy * 0.002f * 0.25f + 0.15123f) -
                        Mathf.PerlinNoise(wx * 0.002f * 0.25f + 0.321f, wy * 0.002f * 0.25f + 0.231f));
                    finalHeight *= 1f - (1f - Utils.LerpStep(0.02f, 0.12f, v)) *
                        Utils.SmoothStep(744f, 1000f, distance);
                }

                // Edge of the world
                if (!Settings.DisableMapEdgeDropoff && distance > 10000f)
                {
                    float t = Utils.LerpStep(10000f, 10500f, distance);
                    finalHeight = Mathf.Lerp(finalHeight, -0.2f, t);
                    if (distance > 10490f)
                    {
                        float t2 = Utils.LerpStep(10490f, 10500f, distance);
                        finalHeight = Mathf.Lerp(finalHeight, -2f, t2);
                    }
                }
                
                // Avoid mountains in the center
                if (!Settings.MountainsAllowedAtCenter && distance < ___m_minMountainDistance && finalHeight > 0.28f)
                {
                    float t3 = Mathf.Clamp01((finalHeight - 0.28f) / 0.099999994f);
                    finalHeight = Mathf.Lerp(Mathf.Lerp(0.28f, 0.38f, t3), finalHeight, Utils.LerpStep(___m_minMountainDistance - 400f, ___m_minMountainDistance, distance));
                }
                return finalHeight;
            }

            // We must come after WorldGenOptions, as that mod always replaces the GetBiome function.
            // Note in HarmonyX (which BepInEx uses), all prefixes always run, unless they opt out themselves.
            // So coming BEFORE a prefex and returning false doesn't stop that prefix from running.
            [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.GetBiome), typeof(float), typeof(float)), HarmonyAfter("org.github.spacedrive.worldgen")]
            private static bool GetBiomePrefix(float wx, float wy, ref Heightmap.Biome __result, World ___m_world)
            {
                if (!Settings.EnabledForThisWorld || ___m_world.m_menu || !Settings.OverrideBiomes)
                {
                    return true;
                }
                else
                {
                    var normalized = WorldToNormalized(wx, wy);
                    __result = Settings.GetBiomeOverride(normalized.x, normalized.y);

                    return false;
                }
            }
            
            [HarmonyPrefix, HarmonyPatch("AddRivers")]
            private static bool AddRiversPrefix(ref float __result, float h, World ___m_world)
            {
                if (!Settings.EnabledForThisWorld || ___m_world.m_menu || Settings.RiversEnabled)
                {
                    // Fall through to normal function
                    return true;
                }
                else
                {
                    __result = h;
                    return false;
                }
            }

            [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.GetForestFactor))]
            private static void GetForestFactorPrefix(ref Vector3 pos)
            {
                if (Settings.EnabledForThisWorld && Settings.ForestScale != 1)
                {
                    pos *= Settings.ForestScale;
                }
            }

            // Range: 0.145071 1.850145
            [HarmonyPostfix, HarmonyPatch(nameof(WorldGenerator.GetForestFactor))]
            private static void GetForestFactorPostfix(Vector3 pos, ref float __result)
            {
                if (Settings.EnabledForThisWorld)
                {
                    __result = Settings.ApplyForest(NormalizedX(pos.x), NormalizedY(pos.z), __result);
                }
            }
        }

        // Changes to location type spawn placement (this is the functional part of the mod)
        [HarmonyPatch(typeof(ZoneSystem))]
        private class ZoneSystemPatch
        {
            private delegate void RegisterLocationDelegate(ZoneSystem instance, ZoneSystem.ZoneLocation location, Vector3 pos, bool generated);
            private static readonly RegisterLocationDelegate RegisterLocation = GetDelegate<RegisterLocationDelegate>(typeof(ZoneSystem), "RegisterLocation");

            // [HarmonyPostfix, HarmonyPatch(nameof(ZoneSystem.ValidateVegetation))]
            // private static void ValidateVegetationPostfix(ZoneSystem __instance)
            // {
            // }
            [HarmonyPrefix, HarmonyPatch(nameof(ZoneSystem.GenerateLocations), typeof(ZoneSystem.ZoneLocation))]
            private static bool GenerateLocationsPrefix(ZoneSystem __instance, ZoneSystem.ZoneLocation location)
            {
                var groupName = string.IsNullOrEmpty(location.m_group) ? "<unnamed>" : location.m_group;
                Log($"Generating location of group {groupName}, required {location.m_quantity}, unique {location.m_unique}, name {location.m_prefabName}");
                if (Settings.EnabledForThisWorld)
                {
                    if (Settings.UseSpawnmap)
                    {
                        // Place all locations specified by the spawn map, ignoring counts specified in the prefab
                        int placed = 0;
                        foreach (var normalizedPosition in Settings.GetAllSpawns(location.m_prefabName))
                        {
                            var worldPos = NormalizedToWorld(normalizedPosition);
                            var position = new Vector3(
                                worldPos.x,
                                WorldGenerator.instance.GetHeight(worldPos.x, worldPos.y),
                                worldPos.y
                            );
                            RegisterLocation(__instance, location, position, false);
                            Log($"Position of {location.m_prefabName} ({++placed}/{location.m_quantity}) overriden: set to {position}");
                        }

                        // The vanilla placement algorithm considers already placed zones, but we can early out here anyway if we place them all
                        // (this is required in the case of the StartTemple as we don't want to place it twice if OverrideStartPosition is specified) 
                        if (placed >= location.m_quantity)
                        {
                            return false;
                        }
                    }
                    
                    if (Settings.OverrideStartPosition && location.m_prefabName == "StartTemple")
                    {
                        var position = new Vector3(
                            Settings.StartPositionX,
                            WorldGenerator.instance.GetHeight(Settings.StartPositionX, Settings.StartPositionY),
                            Settings.StartPositionY
                        );
                        RegisterLocation(__instance, location, position, false);
                        Log($"Start position overriden: set to {position}");
                        return false;
                    }
                    
                    if (location.m_prefabName != "StartTemple" && ConfigDebugSkipDefaultLocationPlacement.Value)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        // Debug mode helpers
        [HarmonyPatch(typeof(Player))]
        private class PlayerPatch
        {
            [HarmonyPrefix, HarmonyPatch(nameof(Player.OnSpawned))]
            private static void OnSpawnedPrefix(Player __instance)
            {
                if (AllowDebugActions)
                {
                    AccessTools.Field(typeof(Player), "m_firstSpawn").SetValue(__instance, false);
                }
            }
            
            [HarmonyPostfix, HarmonyPatch(nameof(Player.OnSpawned))]
            private static void OnSpawnedPostfix()
            {
                if (AllowDebugActions)
                {
                    AccessTools.Field(typeof(Console), "m_cheat").SetValue(Console.instance, true);
                    Minimap.instance.ExploreAll();
                    Player.m_debugMode = true;
                    Player.m_localPlayer.SetGodMode(true);
                }
            }
        }
        
        // Debug mode helpers
        [HarmonyPatch(typeof(Character))]
        private class CharacterPatch
        {
            private delegate bool TakeInputDelegate(Character instance);
            private static readonly TakeInputDelegate TakeInput = GetDelegate<TakeInputDelegate>(typeof(Character), "TakeInput");
                
            [HarmonyPrefix, HarmonyPatch("UpdateDebugFly")]
            private static void UpdateDebugFlyPrefix(Character __instance, Vector3 ___m_moveDir, ref Vector3 ___m_currentVel)
            {
                if (AllowDebugActions)
                {
                    // Add some extra velocity
                    Vector3 newVel = ___m_moveDir * 200f;
                    
                    if (TakeInput(__instance))
                    {
                        if (ZInput.GetButton("Jump"))
                        {
                            newVel.y = 200;
                        }
                        else if (Input.GetKey(KeyCode.LeftControl))
                        {
                            newVel.y = -200;
                        }
                    }
                    ___m_currentVel = Vector3.Lerp(___m_currentVel, newVel, 0.5f);
                }
            }
        }
        
        // Debug mode helpers
        [HarmonyPatch(typeof(Minimap))]
        private class MinimapPatch
        {
            private delegate Vector3 ScreenToWorldPointDelegate(Minimap instance, Vector3 mousePos);
            private static readonly ScreenToWorldPointDelegate ScreenToWorldPoint = GetDelegate<ScreenToWorldPointDelegate>(typeof(Minimap), "ScreenToWorldPoint");
            
            [HarmonyPostfix, HarmonyPatch(nameof(Minimap.OnMapMiddleClick))]
            private static void OnMapMiddleClickPostfix(Minimap __instance)
            {
                if (AllowDebugActions && Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.LeftControl))
                {
                    var player = Player.m_localPlayer;
                    if (player)
                    {
                        var position = ScreenToWorldPoint(__instance, Input.mousePosition);
                        var pos = new Vector3(position.x, player.transform.position.y, position.z);
                        player.TeleportTo(pos, player.transform.rotation, true);
                    }
                }
            }

            private static Heightmap.Biome ForestableBiomes =
                Heightmap.Biome.Meadows |
                Heightmap.Biome.Mistlands |
                Heightmap.Biome.Mountain |
                Heightmap.Biome.Plains |
                Heightmap.Biome.Swamp |
                Heightmap.Biome.BlackForest
            ;
            [HarmonyPrefix, HarmonyPatch("GetMaskColor")]
            private static bool GetMaskColorPrefix(Minimap __instance, float wx, float wy, float height, Heightmap.Biome biome, ref Color __result, Color ___noForest, Color ___forest)
            {
                if (Settings.EnabledForThisWorld && Settings.ForestFactorOverrideAllTrees && (biome & ForestableBiomes) != 0)
                {
                    float forestFactor = WorldGenerator.GetForestFactor(new Vector3(wx, 0f, wy));
                    float limit = biome == Heightmap.Biome.Plains ? 0.8f : 1.15f;
                    __result = forestFactor < limit ? ___forest : ___noForest;
                    return false;
                }

                return true;
            }
            
            // private Color GetMaskColor(float wx, float wy, float height, Heightmap.Biome biome)
            // {
            //     if (height < ZoneSystem.instance.m_waterLevel)
            //     {
            //         return this.noForest;
            //     }
            //     if (biome == Heightmap.Biome.Meadows)
            //     {
            //         if (!WorldGenerator.InForest(new Vector3(wx, 0f, wy)))
            //         {
            //             return this.noForest;
            //         }
            //         return this.forest;
            //     }
            //     else if (biome == Heightmap.Biome.Plains)
            //     {
            //         if (WorldGenerator.GetForestFactor(new Vector3(wx, 0f, wy)) >= 0.8f)
            //         {
            //             return this.noForest;
            //         }
            //         return this.forest;
            //     }
            //     else
            //     {
            //         if (biome == Heightmap.Biome.BlackForest || biome == Heightmap.Biome.Mistlands)
            //         {
            //             return this.forest;
            //         }
            //         return this.noForest;
            //     }
            // }
        }
        
        // Debug mode helpers
        [HarmonyPatch(typeof(Console))]
        private class ConsolePatch
        {
            private static Texture CloudTexture;
            private static Texture2D TransparentTexture;

            [HarmonyPrefix, HarmonyPatch("InputText")]
            private static void InputTextPrefix(Console __instance)
            {
                if (AllowDebugActions)
                {
                    string text = __instance.m_input.text.Trim();
                    if (text.Trim() == "bc" || text.Trim() == "bc help")
                    {
                        // __instance.Print("Better Continents: bc reload (hm/bm/sm) - reload a specific image from the source file, or all of them");
                        __instance.Print("Better Continents: bc dump - dump all location instance counts to the log/console");
                        __instance.Print("Better Continents: bc show (filter) - add locations matching optional filter to the map");
                        __instance.Print("Better Continents: bc bosses");
                        __instance.Print("Better Continents: bc hide (filter) - add locations matching optional filter to the map");
                        __instance.Print("Better Continents: bc clouds - toggle the map clouds");
                    }
                    if (text == "bc reload hm" || text == "bc reload all")
                    {
                        Settings.ReloadHeightmap();
                    }

                    Dictionary<Vector2i, ZoneSystem.LocationInstance> GetLocationInstances() =>
                        (Dictionary<Vector2i, ZoneSystem.LocationInstance>)
                        AccessTools.Field(typeof(ZoneSystem), "m_locationInstances").GetValue(ZoneSystem.instance);

                    if (text.StartsWith("bc dump"))
                    {
                        var locationInstances = GetLocationInstances();
                        
                        foreach (var lg in locationInstances.Values.GroupBy(l => l.m_location.m_prefabName))
                        {
                            Log($"Placed {lg.Count()} {lg.Key} locations");
                        }
                    }
                    
                    if (text == "bc show" || text.StartsWith("bc show ") || text == "bc bosses")
                    {
                        var typeFilters = text == "bc show" 
                            ? null
                            : text == "bc bosses"
                            ? new List<string>{"StartTemple", "Eikthymir", "GDKing", "GoblinKing", "Bonemass", "Dragonqueen", "Vendor"}
                            : text
                                .Replace("bc show ", "")
                                .Split(new []{' '}, StringSplitOptions.RemoveEmptyEntries)
                                .Select(f => f.Trim())
                                .ToList();
                        
                        var locationInstances = GetLocationInstances(); 
                        foreach (var lg in locationInstances.Values.GroupBy(l => l.m_location.m_prefabName))
                        {
                            if(typeFilters == null || typeFilters.Any(f => lg.Key.ToLower().StartsWith(f)))
                            {
                                Log($"Marking {lg.Count()} {lg.Key} locations on map");
                                int idx = 0;
                                foreach (var li in lg)
                                {
                                    Minimap.instance.AddPin(li.m_position, Minimap.PinType.Icon3,
                                        $"{li.m_location.m_prefabName} {idx++}", false, false);
                                }
                            }
                        }
                    }
                    
                    if (text ==  "bc hide" || text.StartsWith("bc hide "))
                    {
                        var typeFilters = text == "bc hide" 
                            ? null
                            : text
                                .Replace("bc hide ", "")
                                .Split(new []{' '}, StringSplitOptions.RemoveEmptyEntries)
                                .Select(f => f.Trim())
                                .ToList();

                        var locationInstances = GetLocationInstances();

                        var pins = (List<Minimap.PinData>)AccessTools.Field(typeof(Minimap), "m_pins").GetValue(Minimap.instance);
                        foreach (var lg in locationInstances.Values.GroupBy(l => l.m_location.m_prefabName))
                        {
                            if(typeFilters == null || typeFilters.Any(f => lg.Key.ToLower().StartsWith(f)))
                            {
                                Log($"Hiding {lg.Count()} {lg.Key} locations from the map");
                                int idx = 0;
                                foreach (var li in lg)
                                {
                                    var name = $"{li.m_location.m_prefabName} {idx++}";
                                    var pin = pins.FirstOrDefault(p => p.m_name == name && p.m_pos == li.m_position);
                                    if (pin != null)
                                    {
                                        Minimap.instance.RemovePin(pins.FirstOrDefault());
                                    }
                                }
                            }
                        }
                    }

                    if (text == "bc clouds")
                    {
                        var mat = Minimap.instance.m_mapImageLarge.material;
                        if (mat.GetTexture("_CloudTex") == TransparentTexture)
                        {
                            mat.SetTexture("_CloudTex", CloudTexture);
                        }
                        else
                        {
                            CloudTexture = mat.GetTexture("_CloudTex");
                            if (TransparentTexture == null)
                            {
                                TransparentTexture = new Texture2D(1, 1);
                                TransparentTexture.SetPixels32(new []{ new Color32(0, 0, 0, 0) });
                                TransparentTexture.Apply(false);
                            }
                            mat.SetTexture("_CloudTex", TransparentTexture);
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(FejdStartup))]
        private class FejdStartupPatch
        {
            [HarmonyPostfix, HarmonyPatch("ShowConnectError")]
            private static void ShowConnectErrorPrefix(Text ___m_connectionFailedError)
            {
                if (LastConnectionError != null)
                {
                    ___m_connectionFailedError.text = LastConnectionError;
                    LastConnectionError = null;
                }
            }
        }
    }
}
