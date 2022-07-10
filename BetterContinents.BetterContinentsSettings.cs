using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BetterContinents
{
    public partial class BetterContinents
    {
        // These are what are baked into the world when it is created
        public struct BetterContinentsSettings
        {
            // Add new properties at the end, and comment where new versions start
            public const int LatestVersion = 7;
            
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
            
            // Version 6
            public bool HeightmapOverrideAll;
            public float HeightmapMask;
            
            // Version 7
            public NoiseStackSettings BaseHeightNoise;

            // Non-serialized
            private ImageMapFloat Heightmap;
            private ImageMapBiome Biomemap;
            private ImageMapSpawn Spawnmap;
            private ImageMapFloat Roughmap;
            private ImageMapFloat Flatmap;
            private ImageMapFloat Forestmap;

            public bool HasHeightmap => Heightmap != null;
            public bool HasBiomemap => Biomemap != null;
            public bool HasSpawnmap => Spawnmap != null;
            public bool HasRoughmap => Roughmap != null;
            public bool HasFlatmap => Flatmap != null;
            public bool HasForestmap => Forestmap != null;

            public bool AnyImageMap => HasHeightmap
                                       || HasRoughmap
                                       || HasFlatmap
                                       || HasBiomemap
                                       || HasSpawnmap
                                       || HasForestmap;
            public bool ShouldHeightmapOverrideAll => HasHeightmap && HeightmapOverrideAll;
            
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

            private static string GetPath(string projectDir, string projectDirFileName, string defaultFileName)
            {
                if (!string.IsNullOrEmpty(projectDir))
                {
                    if (File.Exists(Path.Combine(projectDir, CleanPath(projectDirFileName))))
                    {
                        return Path.Combine(projectDir, CleanPath(projectDirFileName));
                    }
                    else
                    {
                        return string.Empty;
                    }
                }

                return CleanPath(defaultFileName);
            }

            private static readonly string HeightmapFilename = "Heightmap.png";
            private static readonly string BiomemapFilename = "Biomemap.png";
            private static readonly string SpawnmapFilename = "Spawnmap.png";
            private static readonly string RoughmapFilename = "Roughmap.png";
            private static readonly string ForestmapFilename = "Forestmap.png";

            private static string HeightmapPath(string defaultFilename, string projectDir) => GetPath(projectDir, HeightmapFilename, defaultFilename);
            private static string BiomemapPath(string defaultFilename, string projectDir) => GetPath(projectDir, BiomemapFilename, defaultFilename);
            private static string SpawnmapPath(string defaultFilename, string projectDir) => GetPath(projectDir, SpawnmapFilename, defaultFilename);
            private static string RoughmapPath(string defaultFilename, string projectDir) => GetPath(projectDir, RoughmapFilename, defaultFilename);
            private static string ForestmapPath(string defaultFilename, string projectDir) => GetPath(projectDir, ForestmapFilename, defaultFilename);
            
            private void InitSettings(long worldUId, bool enabled)
            {
                Log($"Init settings for new world");

                Version = LatestVersion;

                WorldUId = worldUId;

                EnabledForThisWorld = enabled;

                if (EnabledForThisWorld)
                {
                    ContinentSize = ConfigContinentSize.Value;
                    SeaLevel = ConfigSeaLevelAdjustment.Value;

                    string heightmapPath = HeightmapPath(ConfigHeightmapFile.Value, ConfigMapSourceDir.Value);
                    if (!string.IsNullOrEmpty(heightmapPath))
                    {
                        HeightmapAmount = ConfigHeightmapAmount.Value;
                        HeightmapBlend = ConfigHeightmapBlend.Value;
                        HeightmapAdd = ConfigHeightmapAdd.Value;
                        HeightmapMask = ConfigHeightmapMask.Value;
                        HeightmapOverrideAll = ConfigHeightmapOverrideAll.Value;

                        Heightmap = new ImageMapFloat(heightmapPath);
                        if (!Heightmap.LoadSourceImage() || !Heightmap.CreateMap())
                        {
                            Heightmap = null;
                        }
                    }

                    BaseHeightNoise = NoiseStackSettings.Default();
                    
                    string biomemapPath = BiomemapPath(ConfigBiomemapFile.Value, ConfigMapSourceDir.Value);
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

                    ForestScaleFactor = ConfigForestScale.Value;
                    ForestAmount = ConfigForestAmount.Value;
                    ForestFactorOverrideAllTrees = ConfigForestFactorOverrideAllTrees.Value;

                    OverrideStartPosition = ConfigOverrideStartPosition.Value;
                    StartPositionX = ConfigStartPositionX.Value;
                    StartPositionY = ConfigStartPositionY.Value;
                    
                    string spawnmapPath = SpawnmapPath(ConfigSpawnmapFile.Value, ConfigMapSourceDir.Value);
                    if (!string.IsNullOrEmpty(spawnmapPath))
                    {
                        Spawnmap = new ImageMapSpawn(spawnmapPath);
                        if (!Spawnmap.LoadSourceImage() || !Spawnmap.CreateMap())
                        {
                            Spawnmap = null;
                        }
                    }

                    string roughmapPath = RoughmapPath(ConfigRoughmapFile.Value, ConfigMapSourceDir.Value);
                    if (!string.IsNullOrEmpty(roughmapPath))
                    {
                        RoughmapBlend = ConfigRoughmapBlend.Value;
                        
                        Roughmap = new ImageMapFloat(roughmapPath);
                        if (!Roughmap.LoadSourceImage() || !Roughmap.CreateMap())
                        {
                            Roughmap = null;
                        }
                    }
                    
                    string forestmapPath = ForestmapPath(ConfigForestmapFile.Value, ConfigMapSourceDir.Value);
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

                    MapEdgeDropoff = ConfigMapEdgeDropoff.Value;
                    MountainsAllowedAtCenter = ConfigMountainsAllowedAtCenter.Value;
                }
            }

            #region Setters
            public float ContinentSize
            {
                set => GlobalScale = FeatureScaleCurve(value);
                get => InvFeatureScaleCurve(GlobalScale);
            }

            public float SeaLevel
            {
                set => SeaLevelAdjustment = Mathf.Lerp(1f, -1f, value);
                get => Mathf.InverseLerp(1f, -1f, SeaLevelAdjustment);
            }

            public bool MapEdgeDropoff
            {
                set => DisableMapEdgeDropoff = !value;
                get => !DisableMapEdgeDropoff;
            }

            public float ForestScaleFactor
            {
                set => ForestScale = FeatureScaleCurve(value);
                get => InvFeatureScaleCurve(ForestScale);
            }

            public float ForestAmount
            {
                set => ForestAmountOffset = Mathf.Lerp(1, -1, value);
                get => Mathf.InverseLerp(1, -1, ForestAmountOffset);
            }

            public void SetHeightmapPath(string path, string projectDir = null)
            {
                string finalPath = HeightmapPath(path, projectDir);
                if (!string.IsNullOrEmpty(finalPath))
                {
                    Heightmap = new ImageMapFloat(finalPath);
                    if (!Heightmap.LoadSourceImage() || !Heightmap.CreateMap())
                    {
                        Heightmap = null;
                    }
                }
                else
                {
                    Heightmap = null;
                }
            }

            public string GetHeightmapPath() => Heightmap?.FilePath ?? string.Empty;
            
            public void DisableHeightmap() => Heightmap = null;

            public void SetBiomemapPath(string path, string projectDir = null)
            {
                string finalPath = BiomemapPath(path, projectDir);
                if (!string.IsNullOrEmpty(finalPath))
                {
                    Biomemap = new ImageMapBiome(finalPath);
                    if (!Biomemap.LoadSourceImage() || !Biomemap.CreateMap())
                    {
                        Biomemap = null;
                    }
                }
                else
                {
                    Biomemap = null;
                }
            }
            public string GetBiomemapPath() => Biomemap?.FilePath ?? string.Empty;
            public void DisableBiomemap() => Biomemap = null;
            
            public void SetSpawnmapPath(string path, string projectDir = null)
            {
                string finalPath = SpawnmapPath(path, projectDir);
                if (!string.IsNullOrEmpty(finalPath))
                {
                    Spawnmap = new ImageMapSpawn(finalPath);
                    if (!Spawnmap.LoadSourceImage() || !Spawnmap.CreateMap())
                    {
                        Spawnmap = null;
                    }
                }
                else
                {
                    Spawnmap = null;
                }
            }
            public string GetSpawnmapPath() => Spawnmap?.FilePath ?? string.Empty;
            public void DisableSpawnmap() => Spawnmap = null;
            
            public void SetRoughmapPath(string path, string projectDir = null)
            {
                string finalPath = RoughmapPath(path, projectDir);
                if (!string.IsNullOrEmpty(finalPath))
                {
                    Roughmap = new ImageMapFloat(finalPath);
                    if (!Roughmap.LoadSourceImage() || !Roughmap.CreateMap())
                    {
                        Roughmap = null;
                    }
                }
                else
                {
                    Roughmap = null;
                }
            }
            public string GetRoughmapPath() => Roughmap?.FilePath ?? string.Empty;
            public void DisableRoughmap() => Roughmap = null;

            public void SetForestmapPath(string path, string projectDir = null)
            {
                string finalPath = ForestmapPath(path, projectDir);
                if (!string.IsNullOrEmpty(finalPath))
                {
                    Forestmap = new ImageMapFloat(finalPath);
                    if (!Forestmap.LoadSourceImage() || !Forestmap.CreateMap())
                    {
                        Forestmap = null;
                    }
                }
                else
                {
                    Forestmap = null;
                }
            }
            public string GetForestmapPath() => Forestmap?.FilePath ?? string.Empty;
            public void DisableForestmap() => Forestmap = null;
            #endregion
            
            private static float FeatureScaleCurve(float x) => ScaleRange(Gamma(x, 0.726965071031f), 0.2f, 3f);
            private static float InvFeatureScaleCurve(float y) => InvGamma(InvScaleRange(y, 0.2f, 3f), 0.726965071031f);

            private static float Gamma(float x, float h) => Mathf.Pow(x, Mathf.Pow(1 - h * 0.5f + 0.25f, 6f));
            private static float InvGamma(float g, float h) => Mathf.Pow(g, 1 / Mathf.Pow(1 - h * 0.5f + 0.25f, 6f));
            
            private static float ScaleRange(float x, float a, float b) => a + (b - a) * (1 - x); 
            private static float InvScaleRange(float y, float a, float b) => 1f - (y - a) / (b - a);
            

            public void Dump(Action<string> output = null)
            {
                output = output ?? Log;
                output($"Version {Version}");
                output($"WorldUId {WorldUId}");
                
                if (EnabledForThisWorld)
                {
                    output($"Continent size {ContinentSize}");
                    output($"Mountains amount {MountainsAmount}");
                    output($"Sea level adjustment {SeaLevel}");
                    output($"Ocean channels enabled {OceanChannelsEnabled}");
                    output($"Rivers enabled {RiversEnabled}");
                    
                    output($"Map edge dropoff {MapEdgeDropoff}");
                    output($"Mountains allowed at center {MountainsAllowedAtCenter}");
                    
                    if (Heightmap != null)
                    {
                        output($"Heightmap file {Heightmap.FilePath}");
                        output($"Heightmap size {Heightmap.Size}x{Heightmap.Size}, amount {HeightmapAmount}, blend {HeightmapBlend}, add {HeightmapAdd}, mask {HeightmapMask}");
                        if (HeightmapOverrideAll)
                        {
                            output($"Heightmap overrides ALL");
                        }
                    }
                    else
                    {
                        output($"Heightmap disabled");
                    }
                    
                    if (Version < 7)
                    {
                        if (UseRoughInvertedAsFlat)
                        {
                            output($"Using inverted Roughmap as Flatmap");
                        }
                        else
                        {
                            if (Flatmap != null)
                            {
                                output($"Flatmap file {Flatmap.FilePath}");
                                output($"Flatmap size {Flatmap.Size}x{Flatmap.Size}, blend {FlatmapBlend}");
                            }
                            else
                            {
                                output($"Flatmap disabled");
                            }
                        }
                    }
                    else
                    {
                        output($"Base height noise stack:");
                        BaseHeightNoise.Dump(str => output($"    {str}"));
                    }

                    if (Roughmap != null)
                    {
                        output($"Roughmap file {Roughmap.FilePath}");
                        output($"Roughmap size {Roughmap.Size}x{Roughmap.Size}, blend {RoughmapBlend}");
                    }
                    else
                    {
                        output($"Roughmap disabled");
                    }
                    
                    if (Biomemap != null)
                    {
                        output($"Biomemap file {Biomemap.FilePath}");
                        output($"Biomemap size {Biomemap.Size}x{Biomemap.Size}");
                    }
                    else
                    {
                        output($"Biomemap disabled");
                    }
                    
                    output($"Forest scale {ForestScaleFactor}");
                    output($"Forest amount {ForestAmount}");
                    if (Forestmap != null)
                    {
                        output($"Forestmap file {Forestmap.FilePath}");
                        output($"Forestmap size {Forestmap.Size}x{Forestmap.Size}, multiply {ForestmapMultiply}, add {ForestmapAdd}");
                        if (ForestFactorOverrideAllTrees)
                        {
                            output($"Forest Factor overrides all trees");
                        }
                        else
                        {
                            output($"Forest Factor applies only to the same trees as vanilla");
                        }
                    }
                    else
                    {
                        output($"Forestmap disabled");
                    }
                    
                    if (Spawnmap != null)
                    {
                        output($"Spawnmap file {Spawnmap.FilePath}");
                        output($"Spawnmap includes spawns for {Spawnmap.RemainingSpawnAreas.Count} types");
                    }
                    else
                    {
                        output($"Spawnmap disabled");
                    }

                    if (OverrideStartPosition)
                    {
                        output($"StartPosition {StartPositionX}, {StartPositionY}");
                    }
                }
                else
                {
                    output($"DISABLED");
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
                    // (nothing)

                    // Version 5
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
                    
                    // Version 6
                    if (Version >= 6)
                    {
                        pkg.Write(HeightmapOverrideAll);
                        pkg.Write(HeightmapMask);
                    }
                    
                    // Version 7
                    if (Version >= 7)
                    {
                        BaseHeightNoise.Serialize(pkg);
                    }
                }
            }

            public static BetterContinentsSettings Load(ZPackage pkg)
            {
                var settings = new BetterContinentsSettings();
                settings.Deserialize(pkg);
                return settings;
            }

            public static BetterContinentsSettings Load(string path)
            {
                using (var binaryReader = new BinaryReader(File.OpenRead(path)))
                {
                    int count = binaryReader.ReadInt32();
                    return Load(new ZPackage(binaryReader.ReadBytes(count)));
                }
            }

            public void Save(string path)
            {
                var zpackage = new ZPackage();
                Serialize(zpackage);

                byte[] binaryData = zpackage.GetArray();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (BinaryWriter binaryWriter = new BinaryWriter(File.Create(path)))
                {
                    binaryWriter.Write(binaryData.Length);
                    binaryWriter.Write(binaryData);
                }
            }
            
            public static BetterContinentsSettings LoadFromSource(string path, FileHelpers.FileSource fileSource)
            {
                var fileReader = new FileReader(path, fileSource);
                try
                {
                    var binaryReader = (BinaryReader)fileReader;
                    int count = binaryReader.ReadInt32();
                    return Load(new ZPackage(binaryReader.ReadBytes(count)));
                }
                finally
                {
                    fileReader.Dispose();
                }
            }

            public void SaveToSource(string path, FileHelpers.FileSource fileSource)
            {
                var zpackage = new ZPackage();
                Serialize(zpackage);

                byte[] binaryData = zpackage.GetArray();
                var fileWriter = new FileWriter(path, FileHelpers.FileHelperType.Binary, fileSource);
                fileWriter.m_binary.Write(binaryData.Length);
                fileWriter.m_binary.Write(binaryData);
                fileWriter.Finish();
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

                    string heightmapFilePath = pkg.ReadString();
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
                        
                        string biomemapFilePath = pkg.ReadString();
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
                        string spawnmapFilePath = pkg.ReadString();
                        if (!string.IsNullOrEmpty(spawnmapFilePath))
                        {
                            Spawnmap = new ImageMapSpawn(spawnmapFilePath, pkg);
                        }
                    }
                    
                    // Version 4
                    // (nothing)
                    
                    // Version 5
                    if (Version >= 5)
                    {
                        string roughmapFilePath = pkg.ReadString();
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
                            string flatmapFilePath = pkg.ReadString();
                            if (!string.IsNullOrEmpty(flatmapFilePath))
                            {
                                Flatmap = new ImageMapFloat(flatmapFilePath, pkg.ReadByteArray());
                                if (!Flatmap.CreateMap())
                                {
                                    Flatmap = null;
                                }
                            }
                        }

                        string forestmapFilePath = pkg.ReadString();
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

                    // Version 6
                    if (Version >= 6)
                    {
                        HeightmapOverrideAll = pkg.ReadBool();
                        HeightmapMask = pkg.ReadSingle();
                    }
                    else
                    {
                        HeightmapOverrideAll = false;
                        HeightmapMask = 0;
                    }
                    
                    // Version 7
                    if (Version >= 7)
                    {
                        BaseHeightNoise = NoiseStackSettings.Deserialize(pkg);
                    }
                    else
                    {
                        BaseHeightNoise = null;
                    }
                }
            }

            public float ApplyHeightmap(float x, float y, float height)
            {
                if (Heightmap == null || (HeightmapBlend == 0 && HeightmapAdd == 0 && HeightmapMask == 0))
                {
                    return height;
                }

                float h = Heightmap.GetValue(x, y);
                float blendedHeight = Mathf.Lerp(height, h * HeightmapAmount, HeightmapBlend);
                return Mathf.Lerp(blendedHeight, blendedHeight * h, HeightmapMask) + h * HeightmapAdd;
            }
            
            public float ApplyRoughmap(float x, float y, float smoothHeight, float roughHeight)
            {
                if (Roughmap == null)
                {
                    return roughHeight;
                }

                float r = Roughmap.GetValue(x, y);
                return Mathf.Lerp(smoothHeight, roughHeight, r * RoughmapBlend);
            }
            
            public float ApplyFlatmap(float x, float y, float flatHeight, float height)
            {
                if (Settings.ShouldHeightmapOverrideAll)
                {
                    return flatHeight;
                }
                
                if (UseRoughInvertedAsFlat && Roughmap == null 
                    || !UseRoughInvertedAsFlat && Flatmap == null 
                    || RoughmapBlend == 0)
                {
                    return height;
                }

                float f = UseRoughInvertedAsFlat ? 1 - Roughmap.GetValue(x, y) : Flatmap.GetValue(x, y);
                return Mathf.Lerp(height, flatHeight, f * FlatmapBlend);
            }
            
            public float ApplyForest(float x, float y, float forest)
            {
                float finalValue = forest;
                if (Forestmap != null)
                {
                    // Map forest from weird vanilla range to 0 - 1
                    float normalizedForestValue = Mathf.InverseLerp(1.850145f, 0.145071f, forest);
                    float fmap = Forestmap.GetValue(x, y);
                    float calculatedValue = Mathf.Lerp(normalizedForestValue, normalizedForestValue * fmap, ForestmapMultiply) + fmap * ForestmapAdd;
                    // Map back to weird values
                    finalValue = Mathf.Lerp(1.850145f, 0.145071f, calculatedValue);
                }

                // Clamp between the known good values (that vanilla generates)
                finalValue = Mathf.Clamp(finalValue + ForestAmountOffset, 0.145071f, 1.850145f);
                return finalValue;
            }
            
            public Heightmap.Biome GetBiomeOverride(float mapX, float mapY) => Biomemap.GetValue(mapX, mapY);

            public Vector2? FindSpawn(string spawn) => Spawnmap.FindSpawn(spawn);
            public IEnumerable<Vector2> GetAllSpawns(string spawn) => Spawnmap.GetAllSpawns(spawn);
            
            public void ReloadHeightmap()
            {
                if (Heightmap != null && Heightmap.LoadSourceImage())
                {
                    Heightmap.CreateMap();
                }
            }
            
            public void ReloadBiomemap()
            {
                if (Biomemap != null && Biomemap.LoadSourceImage())
                {
                    Biomemap.CreateMap();
                }
            }
            
            public void ReloadSpawnmap()
            {
                if (Spawnmap != null && Spawnmap.LoadSourceImage())
                {
                    Spawnmap.CreateMap();
                }
            }       
            
            public void ReloadRoughmap()
            {
                if (Roughmap != null && Roughmap.LoadSourceImage())
                {
                    Roughmap.CreateMap();
                }
            }
                        
            public void ReloadFlatmap()
            {
                if (UseRoughInvertedAsFlat)
                {
                    ReloadRoughmap();
                }
                else if (Flatmap != null && Flatmap.LoadSourceImage())
                {
                    Flatmap.CreateMap();
                }
            }
                        
            public void ReloadForestmap()
            {
                if (Forestmap != null && Forestmap.LoadSourceImage())
                {
                    Forestmap.CreateMap();
                }
            }
        }
    }
}