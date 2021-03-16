using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace BetterContinents
{
    [BepInPlugin("BetterContinents", ModInfo.Name, ModInfo.Version)]
    public class BetterContinents : BaseUnityPlugin
    {
        private static ConfigEntry<bool> ConfigEnabled;
        
        private static ConfigEntry<string> ConfigHeightmapFile;
        private static ConfigEntry<float> ConfigHeightmapAmount;
        private static ConfigEntry<float> ConfigHeightmapBlend;
        private static ConfigEntry<float> ConfigHeightmapAdd;
        
        private static ConfigEntry<string> ConfigBiomemapFile;
        
        private static ConfigEntry<float> ConfigContinentSize;
        private static ConfigEntry<float> ConfigMountainsAmount;
        private static ConfigEntry<float> ConfigSeaLevelAdjustment;
        private static ConfigEntry<bool> ConfigOceanChannelsEnabled;
        private static ConfigEntry<bool> ConfigRiversEnabled;
        // Perhaps lakes aren't a think? Hard to tell... Maybe they are biome specific
        //private static ConfigEntry<bool> ConfigLakesEnabled;
        
        private static ConfigEntry<float> ConfigMaxRidgeHeight;
        private static ConfigEntry<float> ConfigRidgeSize;
        private static ConfigEntry<float> ConfigRidgeBlend;
        private static ConfigEntry<float> ConfigRidgeAmount;
        
        private static ConfigEntry<float> ConfigForestScale;
        private static ConfigEntry<float> ConfigForestAmount;

        private static ConfigEntry<bool> ConfigOverrideStartPosition;
        private static ConfigEntry<float> ConfigStartPositionX;
        private static ConfigEntry<float> ConfigStartPositionY;
        
        private static void Log(string msg) => Debug.Log($"[BetterContinents] {msg}");
        private static void LogError(string msg) => Debug.LogError($"[BetterContinents] {msg}");

        private abstract class ImageMapBase
        {
            public string FilePath;

            public byte[] SourceData;

            //public T[] Map;
            public int Size;

            public ImageMapBase(string filePath)
            {
                this.FilePath = filePath;
            }

            public ImageMapBase(string filePath, byte[] sourceData) : this(filePath)
            {
                this.SourceData = sourceData;
            }

            public bool LoadSourceImage()
            {
                // Already loaded?
                if (SourceData != null)
                    return true;

                try
                {
                    SourceData = File.ReadAllBytes(FilePath);
                    return true;
                }
                catch (Exception ex)
                {
                    LogError($"Cannot load image {FilePath}: {ex.Message}");
                    return false;
                }
            }

            public bool CreateMap()
            {
                var tex = new Texture2D(2, 2);
                try
                {
                    try
                    {
                        tex.LoadImage(SourceData);
                    }
                    catch (Exception ex)
                    {
                        LogError($"Cannot load texture {FilePath}: {ex.Message}");
                        return false;
                    }

                    if (tex.width != tex.height)
                    {
                        LogError(
                            $"Cannot use texture {FilePath}: its width ({tex.width}) does not match its height ({tex.height})");
                        return false;
                    }

                    bool IsPowerOfTwo(int x) => (x & (x - 1)) == 0;
                    if (!IsPowerOfTwo(tex.width))
                    {
                        LogError(
                            $"Cannot use texture {FilePath}: it is not a power of two size (e.g. 256, 512, 1024, 2048)");
                        return false;
                    }

                    if (tex.width > 4096)
                    {
                        LogError(
                            $"Cannot use texture {FilePath}: it is too big ({tex.width}x{tex.height}), keep the size to less or equal to 4096x4096");
                        return false;
                    }
                    
                    Size = tex.width;

                    return this.LoadTextureToMap(tex);
                }
                finally
                {
                    Destroy(tex);
                }
            }

            protected abstract bool LoadTextureToMap(Texture2D tex);
        }

        private class ImageMapFloat : ImageMapBase
        {
            private float[] Map;

            public ImageMapFloat(string filePath) : base(filePath) { }

            public ImageMapFloat(string filePath, byte[] sourceData) : base(filePath, sourceData) { }

            protected override bool LoadTextureToMap(Texture2D tex)
            {
                var pixels = tex.GetPixels();
                Map = new float[pixels.Length];
                for (int i = 0; i < pixels.Length; i++)
                {
                    Map[i] = pixels[i].r;
                }
                return true;
            }

            public float GetValue(float x, float y)
            {
                float xa = x * (this.Size - 1);
                float ya = y * (this.Size - 1);

                int xi = Mathf.FloorToInt(xa);
                int yi = Mathf.FloorToInt(ya);

                float xd = xa - xi;
                float yd = ya - yi;

                int x0 = Mathf.Clamp(xi, 0, this.Size - 1);
                int x1 = Mathf.Clamp(xi + 1, 0, this.Size - 1);
                int y0 = Mathf.Clamp(yi, 0, this.Size - 1);
                int y1 = Mathf.Clamp(yi + 1, 0, this.Size - 1);

                float p00 = this.Map[y0 * this.Size + x0];
                float p10 = this.Map[y0 * this.Size + x1];
                float p01 = this.Map[y1 * this.Size + x0];
                float p11 = this.Map[y1 * this.Size + x1];

                return Mathf.Lerp(
                    Mathf.Lerp(p00, p10, xd),
                    Mathf.Lerp(p01, p11, xd),
                    yd
                );
            }
        }
        
        private class ImageMapBiome : ImageMapBase
        {
            private Heightmap.Biome[] Map;

            public ImageMapBiome(string filePath) : base(filePath) { }

            public ImageMapBiome(string filePath, byte[] sourceData) : base(filePath, sourceData) { }

            private struct ColorBiome
            {
                public Color32 color;
                public Heightmap.Biome biome;


                public ColorBiome(Color32 color, Heightmap.Biome biome)
                {
                    this.color = color;
                    this.biome = biome;
                }
            }
            
            private static readonly ColorBiome[] BiomeColorMapping = new ColorBiome[]
            {
                /*
                    Ocean #0000FF 
                    Meadows #00FF00
                    Black Forest #007F00
                    Swamp #7F7F00
                    Mountains #FFFFFF
                    Plains #FFFF00
                    Mistlands #7F7F7F
                    Deep North #00FFFF
                    Ash Lands #FF0000
                */
                new ColorBiome(new Color32(0, 0, 255, 255), Heightmap.Biome.Ocean),
                new ColorBiome(new Color32(0, 255, 0, 255), Heightmap.Biome.Meadows),
                new ColorBiome(new Color32(0, 127, 0, 255), Heightmap.Biome.BlackForest),
                new ColorBiome(new Color32(127, 127, 0, 255), Heightmap.Biome.Swamp),
                new ColorBiome(new Color32(255, 255, 255, 255), Heightmap.Biome.Mountain),
                new ColorBiome(new Color32(255, 255, 0, 255), Heightmap.Biome.Plains),
                new ColorBiome(new Color32(127, 127, 127, 255), Heightmap.Biome.Mistlands),
                new ColorBiome(new Color32(0, 255, 255, 255), Heightmap.Biome.DeepNorth),
                new ColorBiome(new Color32(255, 0, 0, 255), Heightmap.Biome.AshLands),
            };

            protected override bool LoadTextureToMap(Texture2D tex)
            {
                float ColorDistance(Color a, Color b) =>
                    Vector3.Distance(new Vector3(a.r, a.g, a.b), new Vector3(b.r, b.g, b.b));

                var pixels = tex.GetPixels();
                Map = new Heightmap.Biome[pixels.Length]; 
                for (int i = 0; i < pixels.Length; i++)
                {
                    Map[i] = BiomeColorMapping.OrderBy(d => ColorDistance(pixels[i], d.color)).First().biome;
                }
                return true;
            }

            public Heightmap.Biome GetValue(float x, float y)
            {
                float xa = x * (this.Size - 1);
                float ya = y * (this.Size - 1);

                int xi = Mathf.FloorToInt(xa);
                int yi = Mathf.FloorToInt(ya);

                float xd = xa - xi;
                float yd = ya - yi;

                // Get the rounded value for the diff, so we can choose which of the 4 corner value to return
                int xo = Mathf.RoundToInt(xd); 
                int yo = Mathf.RoundToInt(yd);

                int xf = Mathf.Clamp(xi + xo, 0, this.Size - 1);
                int yf = Mathf.Clamp(yi + yo, 0, this.Size - 1);
                
                return this.Map[yf * this.Size + xf];
            }
        }
        
        private struct BetterContinentsSettings
        {
            // Add new properties at the end for versioning serialization
            public const int LatestVersion = 2;
            
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

            // public string HeightmapSourceFilename; Replaced by Heightmap.FilePath
            // public byte[] HeightmapSource; Replaced by Heightmap.SourceData
            
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
            
            //public bool LakesEnabled;
            // public string BiomemapSourceFilename; Replaced by Biomemap.FilePath
            // public byte[] BiomemapSource; Replaced by Biomemap.SourceData

            // Non-serialized
            private ImageMapFloat Heightmap;
            private ImageMapBiome Biomemap;
            
            //private float[] Heightmap;
            //private int HeightmapSize;

            //private float[] Biomemap;
            //private int BiomemapSize;

            public bool OverrideBiomes => this.Biomemap != null;
            
            public static BetterContinentsSettings Create(long worldUId)
            {
                var settings = new BetterContinentsSettings();
                settings.InitSettings(worldUId);
                return settings;
            }
            
            public static BetterContinentsSettings Disabled(long worldUId = -1)
            {
                var settings = Create(worldUId);
                settings.EnabledForThisWorld = false;
                return settings;
            }

            private void InitSettings(long worldUId)
            {
                Log($"Init settings for new world");

                Version = LatestVersion;

                WorldUId = worldUId;

                EnabledForThisWorld = ConfigEnabled.Value;

                if (EnabledForThisWorld)
                {
                    GlobalScale = FeatureScaleCurve(ConfigContinentSize.Value);
                    MountainsAmount = ConfigMountainsAmount.Value;
                    SeaLevelAdjustment = Mathf.Lerp(0.25f, -0.25f, ConfigSeaLevelAdjustment.Value);

                    MaxRidgeHeight = ConfigMaxRidgeHeight.Value;
                    RidgeScale = FeatureScaleCurve(ConfigRidgeSize.Value);
                    RidgeBlendSigmoidB = Mathf.Lerp(-30f, -10f, ConfigRidgeBlend.Value);
                    RidgeBlendSigmoidXOffset = Mathf.Lerp(1f, 0.35f, ConfigRidgeAmount.Value);

                    if (!string.IsNullOrEmpty(ConfigHeightmapFile.Value))
                    {
                        HeightmapAmount = ConfigHeightmapAmount.Value;
                        HeightmapBlend = ConfigHeightmapBlend.Value;
                        HeightmapAdd = ConfigHeightmapAdd.Value;

                        Heightmap = new ImageMapFloat(ConfigHeightmapFile.Value);
                        if (!Heightmap.LoadSourceImage() || !Heightmap.CreateMap())
                        {
                            Heightmap = null;
                        }
                    }
                    else
                    {
                        Heightmap = null;
                    }
                    
                    
                    if (!string.IsNullOrEmpty(ConfigBiomemapFile.Value))
                    {
                        Biomemap = new ImageMapBiome(ConfigBiomemapFile.Value);
                        if (!Biomemap.LoadSourceImage() || !Biomemap.CreateMap())
                        {
                            Biomemap = null;
                        }
                    }
                    else
                    {
                        Biomemap = null;
                    }

                    OceanChannelsEnabled = ConfigOceanChannelsEnabled.Value;
                    RiversEnabled = ConfigRiversEnabled.Value;

                    ForestScale = FeatureScaleCurve(ConfigForestScale.Value);
                    ForestAmountOffset = Mathf.Lerp(-1, 1, ConfigForestAmount.Value);

                    OverrideStartPosition = ConfigOverrideStartPosition.Value;
                    StartPositionX = ConfigStartPositionX.Value;
                    StartPositionY = ConfigStartPositionY.Value;
                    //LakesEnabled = ConfigLakesEnabled.Value;
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

                    Log($"GlobalScale {GlobalScale}");
                    Log($"MountainsAmount {MountainsAmount}");
                    Log($"SeaLevelAdjustment {SeaLevelAdjustment}");
                    Log($"OceanChannelsEnabled {OceanChannelsEnabled}");
                    Log($"RiversEnabled {RiversEnabled}");
                    
                    Log($"ForestScale {ForestScale}");
                    Log($"ForestAmount {ForestAmountOffset}");
                    //Log($"LakesEnabled {LakesEnabled}");

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
                        if (!Heightmap.CreateMap())
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

            public Heightmap.Biome GetBiomeOverride(WorldGenerator __instance, float mapX, float mapY) => this.Biomemap.GetValue(mapX, mapY); 
        }
        
        private static BetterContinentsSettings Settings;
        
        private void Awake()
        {
            ConfigEnabled = Config.Bind("BetterContinents.Global", "Enabled", true, "Whether this mod is enabled");

            ConfigHeightmapFile = Config.Bind("BetterContinents.Heightmap", "HeightmapFile", "", "Path to a heightmap file to use. See the description on Nexusmods.com for the specifications (it will fail if they are not met).");
            ConfigHeightmapAmount = Config.Bind("BetterContinents.Heightmap", "HeightmapAmount", 1f,
                new ConfigDescription("Multiplier of the height value from the heightmap file", new AcceptableValueRange<float>(0, 1)));
            ConfigHeightmapBlend = Config.Bind("BetterContinents.Heightmap", "HeightmapBlend", 1f,
                new ConfigDescription("How strongly to blend the heightmap file into the final result", new AcceptableValueRange<float>(0, 1)));
            ConfigHeightmapAdd = Config.Bind("BetterContinents.Heightmap", "HeightmapAdd", 0f,
                new ConfigDescription("How strongly to add the heightmap file to the final result (usually you want to blend it instead)", new AcceptableValueRange<float>(-1, 1)));

            ConfigBiomemapFile = Config.Bind("BetterContinents.Biomemap", "Biomemap", "", "Path to a biome map file to use. See the description on Nexusmods.com for the specifications (it will fail if they are not met).");

            ConfigContinentSize = Config.Bind("BetterContinents.Global", "ContinentSize", 0.5f,
                new ConfigDescription("Continent Size", new AcceptableValueRange<float>(0, 1)));
            ConfigMountainsAmount = Config.Bind("BetterContinents.Global", "MountainsAmount", 0.5f,
                new ConfigDescription("Mountains amount", new AcceptableValueRange<float>(0, 1)));
            ConfigSeaLevelAdjustment = Config.Bind("BetterContinents.Global", "SeaLevelAdjustment", 0.5f,
                new ConfigDescription("Modify sea level, which changes the land:sea ratio", new AcceptableValueRange<float>(0, 1)));
            ConfigOceanChannelsEnabled = Config.Bind("BetterContinents.Global", "OceanChannelsEnabled", true, "Whether ocean channels should be enabled or not (useful to disable when using height map for instance)");
            ConfigRiversEnabled = Config.Bind("BetterContinents.Global", "RiversEnabled", true, "Whether rivers should be enabled or not");
            
            ConfigForestScale = Config.Bind("BetterContinents.Global", "ForestScale", 0.5f,
                new ConfigDescription("Scales forested/cleared area size", new AcceptableValueRange<float>(0, 1)));
            ConfigForestAmount = Config.Bind("BetterContinents.Global", "ForestAmount", 0.5f,
                new ConfigDescription("Adjusts how much forest there is, relative to clearings", new AcceptableValueRange<float>(0, 1)));
            //ConfigLakesEnabled = Config.Bind("BetterContinents.Global", "LakesEnabled", true, "Whether 'lakes' should be enabled or not");

            ConfigMaxRidgeHeight = Config.Bind("BetterContinents.Ridges", "MaxRidgeHeight", 0.5f,
                new ConfigDescription("Max height of ridge features", new AcceptableValueRange<float>(0, 1)));
            ConfigRidgeSize = Config.Bind("BetterContinents.Ridges", "RidgeSize", 0.5f,
                new ConfigDescription("Size of ridge features", new AcceptableValueRange<float>(0, 1)));
            ConfigRidgeBlend = Config.Bind("BetterContinents.Ridges", "RidgeBlend", 0.5f,
                new ConfigDescription("Smoothness of ridges blending into base terrain", new AcceptableValueRange<float>(0, 1)));
            ConfigRidgeAmount = Config.Bind("BetterContinents.Ridges", "RidgeAmount", 0.5f,
                new ConfigDescription("How much ridges", new AcceptableValueRange<float>(0, 1)));

            ConfigOverrideStartPosition = Config.Bind("BetterContinents.StartPosition", "OverrideStartPosition", false, "Whether to override the start position using the values provided (warning: will disable all validation of the position)");
            ConfigStartPositionX = Config.Bind("BetterContinents.StartPosition", "StartPositionX", 0f,
                new ConfigDescription("Start position override X value, in ranges -10500 to 10500", new AcceptableValueRange<float>(-10500, 10500)));
            ConfigStartPositionY = Config.Bind("BetterContinents.StartPosition", "StartPositionY", 0f,
                new ConfigDescription("Start position override Y value, in ranges -10500 to 10500", new AcceptableValueRange<float>(-10500, 10500)));
            
            new Harmony("BetterContinents.Harmony").PatchAll();
            Log("Awake");
        }

        [HarmonyPatch(typeof(World))]
        private class WorldPatch
        {
            // When the world metadata is saved we write an extra file next to it for our own config
            [HarmonyPrefix, HarmonyPatch("SaveWorldMetaData")]
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

            [HarmonyPostfix, HarmonyPatch("RemoveWorld")]
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

        [HarmonyPatch(typeof(ZNet))]
        private class ZNetPatch
        {
            // When the world is set on the server (applies to single player as well), we should select the correct loaded settings
            [HarmonyPrefix, HarmonyPatch("SetServer")]
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

            // Register our RPC for receiving settings on clients
            [HarmonyPrefix, HarmonyPatch("OnNewConnection")]
            private static void OnNewConnectionPrefix(ZNetPeer peer)
            {
                Log($"Registering settings RPC");

                peer.m_rpc.Register("BetterContinentsConfigStart", (ZRpc rpc, int totalBytes, int hash) =>
                {
                    SettingsReceiveBuffer = new byte[totalBytes];
                    SettingsReceiveHash = hash;
                    SettingsReceiveBufferBytesReceived = 0;
                    Log($"Receiving settings from server ({SettingsReceiveBuffer.Length} bytes)");
                });

                peer.m_rpc.Register("BetterContinentsConfigPacket", (ZRpc rpc, int offset, int packetHash, ZPackage packet) =>
                {
                    var packetData = packet.GetArray();
                    int hash = GetHashCode(packetData);
                    if (hash != packetHash)
                    {
                        LogError($"Settings transfer failed: packet hash mismatch got {hash} expected {packetHash}");
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
                        }
                        else
                        {
                            LogError($"Settings transfer failed: hash mismatch got {finalHash} expected {SettingsReceiveHash}");
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
            [HarmonyPrefix, HarmonyPatch("SendPeerInfo")]
            private static void SendPeerInfoPrefix(ZNet __instance, ZRpc rpc)
            {
                if (__instance.IsServer())
                {
                    Log($"Sending settings to clients");
                    Settings.Dump();
                    
                    var settingsPackage = new ZPackage();
                    Settings.Serialize(settingsPackage);

                    var settingsData = settingsPackage.GetArray();
                    Log($"Sending settings package header for {settingsData.Length} byte stream");
                    rpc.Invoke("BetterContinentsConfigStart", settingsData.Length, GetHashCode(settingsData));

                    for (int sentBytes = 0; sentBytes < settingsData.Length; )
                    {
                        int packetSize = Mathf.Min(settingsData.Length - sentBytes, 256 * 1024);
                        var packet = ArraySlice(settingsData, sentBytes, packetSize);
                        rpc.Invoke("BetterContinentsConfigPacket", sentBytes, GetHashCode(packet), new ZPackage(packet));
                        // Make sure to flush or we will saturate the queue...
                        rpc.GetSocket().Flush();
                        sentBytes += packetSize;
                        Log($"Sent {sentBytes} of {settingsData.Length} bytes");
                        Thread.Sleep(2000);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(WorldGenerator))]
        private class WorldGeneratorPatch
        {
            const float WorldSize = 10500f;
            
            // The base map x, y coordinates in 0..1 range
            private static float GetMapCoord(float coord) => Mathf.Clamp01(coord / (2 * WorldSize) + 0.5f);
            
            // wx, wy are [-10500, 10500]
            // __result should be [0, 1]
            [HarmonyPrefix, HarmonyPatch("GetBaseHeight")]
            private static bool GetBaseHeightPrefix(ref float wx, ref float wy, bool menuTerrain, ref float __result, float ___m_offset0, float ___m_offset1, float ___m_minMountainDistance)
            {
                if (!Settings.EnabledForThisWorld)
                {
                    return true;
                }

                if (menuTerrain)
                {
                    wx += 100000f + ___m_offset0;
                    wy += 100000f + ___m_offset1;
                    float num = 0f;
                    num += Mathf.PerlinNoise(wx * 0.002f * 0.5f, wy * 0.002f * 0.5f) * Mathf.PerlinNoise(wx * 0.003f * 0.5f, wy * 0.003f * 0.5f) * 1f;
                    num += Mathf.PerlinNoise(wx * 0.002f * 1f, wy * 0.002f * 1f) * Mathf.PerlinNoise(wx * 0.003f * 1f, wy * 0.003f * 1f) * num * 0.9f;
                    num += Mathf.PerlinNoise(wx * 0.005f * 1f, wy * 0.005f * 1f) * Mathf.PerlinNoise(wx * 0.01f * 1f, wy * 0.01f * 1f) * 0.5f * num;
                    __result = num - 0.07f;
                    return false;
                }
                float distance = Utils.Length(wx, wy);
                
                // The base map x, y coordinates in 0..1 range
                float mapX = GetMapCoord(wx);
                float mapY = GetMapCoord(wy);
                
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
                float lerp = Mathf.Clamp(SigmoidActivation(Mathf.PerlinNoise(wx * 0.005f - 10000, wy * 0.005f - 5000) - Settings.RidgeBlendSigmoidXOffset, 0, Settings.RidgeBlendSigmoidB), 0, 1);
                
                float finalHeight = 0f;

                float bigFeature = Mathf.Clamp(Mathf.Lerp(bigFeatureHeight, ridgeHeight, lerp), 0, 1);

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
                __result = finalHeight;
                return false;
            }

            [HarmonyPrefix, HarmonyPatch("GetBiome", typeof(float), typeof(float))]
            private static bool GetBiomePrefix(WorldGenerator __instance, float wx, float wy, ref Heightmap.Biome __result, World ___m_world)
            {
                if (!Settings.EnabledForThisWorld || ___m_world.m_menu || !Settings.OverrideBiomes)
                {
                    return true;
                }
                else
                {
                    // float baseHeight = (float)AccessTools.Method(typeof(WorldGenerator), "GetBaseHeight").Invoke(__instance, new object[] { wx, wy });
                    // if (baseHeight <= 0.02)
                    // {
                    //     // We always return ocean based on height
                    //     __result = global::Heightmap.Biome.Ocean;
                    // }
                    // else
                    // {
                        // The base map x, y coordinates in 0..1 range
                        float mapX = GetMapCoord(wx);
                        float mapY = GetMapCoord(wy);
                        __result = Settings.GetBiomeOverride(__instance, mapX, mapY);
                    //}
                    return false;
                }
            }
            
            [HarmonyPrefix, HarmonyPatch("AddRivers")]
            private static bool AddRiversPrefix(ref float __result, float h)
            {
                if (!Settings.EnabledForThisWorld || Settings.RiversEnabled)
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

            [HarmonyPrefix, HarmonyPatch("GetForestFactor")]
            private static void GetForestFactorPrefix(ref Vector3 pos)
            {
                if (Settings.EnabledForThisWorld && Settings.ForestScale != 1)
                {
                    pos *= Settings.ForestScale;
                }
            }
            
            [HarmonyPostfix, HarmonyPatch("GetForestFactor")]
            private static void GetForestFactorPostfix(ref float __result)
            {
                if (Settings.EnabledForThisWorld && Settings.ForestAmountOffset != 0)
                {
                    __result += Settings.ForestAmountOffset;
                }
            }
        }

        [HarmonyPatch(typeof(ZoneSystem))]
        private class ZoneSystemPatch
        {
            [HarmonyPrefix, HarmonyPatch("GenerateLocations", typeof(ZoneSystem.ZoneLocation))]
            private static bool GenerateLocationsPrefix(ZoneSystem __instance, ZoneSystem.ZoneLocation location)
            {
                if (Settings.EnabledForThisWorld && Settings.OverrideStartPosition && location.m_prefabName == "StartTemple")
                {
                    var position = new Vector3(
                        Settings.StartPositionX, 
                        WorldGenerator.instance.GetHeight(Settings.StartPositionX, Settings.StartPositionY),
                        Settings.StartPositionY
                    );
                    AccessTools.Method(typeof(ZoneSystem), "RegisterLocation")
                        .Invoke(__instance, new object[] { location, position, false });
                    Log($"Start position overriden: set to {position}");
                    return false;
                }
                else
                {
                    return true;
                }
                // Log($"Loc {location.m_group}, {location.m_quantity}, {location.m_unique}, {location.m_prefabName}");
            }
        }
    }
}
