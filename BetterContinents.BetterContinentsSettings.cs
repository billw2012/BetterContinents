using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BetterContinents
{
    public partial class BetterContinents
    {
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
            
            public BetterContinentsSettings Clean()
            {
                var copy = this;
                if(copy.Heightmap != null) copy.Heightmap.FilePath = "(heightmap)";
                if(copy.Biomemap != null) copy.Biomemap.FilePath = "(biomemap)";
                if(copy.Spawnmap != null) copy.Spawnmap.FilePath = "(spawnmap)";
                if(copy.Roughmap != null) copy.Roughmap.FilePath = "(roughmap)";
                if(copy.Flatmap != null) copy.Flatmap.FilePath = "(flatmap)";
                if(copy.Forestmap != null) copy.Forestmap.FilePath = "(forestmap)";
                return copy;
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

            public ZPackage Serialize()
            {
                var pkg = new ZPackage();
                Serialize(pkg);
                return pkg;
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
    }
}