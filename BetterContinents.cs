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
        // See the Awake function for the config descriptions
        private static ConfigEntry<bool> ConfigEnabled;
        
        private static ConfigEntry<string> ConfigHeightmapFile;
        private static ConfigEntry<float> ConfigHeightmapAmount;
        private static ConfigEntry<float> ConfigHeightmapBlend;
        private static ConfigEntry<float> ConfigHeightmapAdd;
        
        private static ConfigEntry<string> ConfigBiomemapFile;

        private static ConfigEntry<string> ConfigSpawnmapFile;

        private static ConfigEntry<float> ConfigContinentSize;
        private static ConfigEntry<float> ConfigMountainsAmount;
        private static ConfigEntry<float> ConfigSeaLevelAdjustment;
        private static ConfigEntry<bool> ConfigOceanChannelsEnabled;
        private static ConfigEntry<bool> ConfigRiversEnabled;
        // Perhaps lakes aren't a thing? Hard to tell... Maybe they are biome specific
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
        
        private static ConfigEntry<bool> ConfigDebugSkipDefaultLocationPlacement;
        private static ConfigEntry<bool> ConfigDebugModeEnabled;
        
        private const float WorldSize = 10500f;
        private static readonly Vector2 Half = Vector2.one * 0.5f;
        private static Vector2 NormalizedToWorld(Vector2 p) => (p - Half) * WorldSize * 2f;
        private static Vector2 WorldToNormalized(Vector2 p) => p / (WorldSize * 2f) + Half;

        private static void Log(string msg) => Debug.Log($"[BetterContinents] {msg}");
        private static void LogError(string msg) => Debug.LogError($"[BetterContinents] {msg}");

        // Loads and stores source image file in original format.
        // Derived types will define the final type of the image pixels (the "map"), and
        // how to access them with bi-linear interpolation.
        private abstract class ImageMapBase
        {
            public string FilePath;

            public byte[] SourceData;

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

                // "Interpolate" the 4 corners (sum the weights of the biomes at the four corners)
                Heightmap.Biome GetBiome(int _x, int _y) => this.Map[Mathf.Clamp(_y, 0, this.Size - 1) * this.Size + Mathf.Clamp(_x, 0, this.Size - 1)];

                var biomes = new Heightmap.Biome[4];
                var biomeWeights = new float[4];
                int numBiomes = 0;
                int topBiomeIdx = 0;
                void SampleBiomeWeighted(int xs, int ys, float weight)
                {
                    var biome = GetBiome(xs, ys);
                    int i = 0;
                    for (; i < numBiomes; ++i)
                    {
                        if (biomes[i] == biome)
                        {
                            if (biomeWeights[i] + weight > biomeWeights[topBiomeIdx])
                                topBiomeIdx = i;
                            biomeWeights[i] += weight;
                            return;
                        }
                    }

                    if (i == numBiomes)
                    {
                        if (biomeWeights[numBiomes] + weight > biomeWeights[topBiomeIdx])
                            topBiomeIdx = numBiomes;
                        biomes[numBiomes] = biome;
                        biomeWeights[numBiomes++] = weight;
                    }
                }
                SampleBiomeWeighted(xi + 0, yi + 0, (1 - xd) * (1 - yd));
                SampleBiomeWeighted(xi + 1, yi + 0, xd * (1 - yd));
                SampleBiomeWeighted(xi + 0, yi + 1, (1 - xd) * yd);
                SampleBiomeWeighted(xi + 1, yi + 1, xd * yd);

                return biomes[topBiomeIdx];
                    
                // // Get the rounded value for the diff, so we can choose which of the 4 corner value to return
                // int xo = Mathf.RoundToInt(xd); 
                // int yo = Mathf.RoundToInt(yd);
                //
                // int xf = Mathf.Clamp(xi + xo, 0, this.Size - 1);
                // int yf = Mathf.Clamp(yi + yo, 0, this.Size - 1);
                //
                // return this.Map[yf * this.Size + xf];
            }
        }
        
        private class ImageMapSpawn : ImageMapBase
        {
            public Dictionary<string, List<Vector2>> RemainingSpawnAreas;

            public ImageMapSpawn(string filePath) : base(filePath) { }

            public ImageMapSpawn(string filePath, ZPackage from) : base(filePath)
            {
                Deserialize(from);
            }

            public void Serialize(ZPackage to)
            {
                to.Write(RemainingSpawnAreas.Count);
                foreach (var kv in RemainingSpawnAreas)
                {
                    to.Write(kv.Key);
                    to.Write(kv.Value.Count);
                    foreach (var v in kv.Value)
                    {
                        to.Write(v.x);
                        to.Write(v.y);
                    }
                }
            }

            private void Deserialize(ZPackage from)
            {
                int count = from.ReadInt();
                RemainingSpawnAreas = new Dictionary<string, List<Vector2>>();
                for (int i = 0; i < count; i++)
                {
                    var spawn = from.ReadString();
                    var positionsCount = from.ReadInt();
                    var positions = new List<Vector2>();
                    for (int k = 0; k < positionsCount; k++)
                    {
                        float x = from.ReadSingle();
                        float y = from.ReadSingle();
                        positions.Add(new Vector2(x, y));
                    }
                    RemainingSpawnAreas.Add(spawn, positions);
                }
            }
            
            private struct ColorSpawn
            {
                public Color32 color;
                public string spawn;
                
                public ColorSpawn(Color32 color, string spawn)
                {
                    this.color = color;
                    this.spawn = spawn;
                }
            }
            
            private static readonly ColorSpawn[] SpawnColorMapping = new ColorSpawn[]
            {
                new ColorSpawn(new Color32(0xFF, 0x00, 0x00, 0xFF), "StartTemple"),
                new ColorSpawn(new Color32(0xFF, 0x99, 0x00, 0xFF), "Eikthyrnir"),
                new ColorSpawn(new Color32(0x00, 0xFF, 0x00, 0xFF), "GDKing"),
                new ColorSpawn(new Color32(0xFF, 0xFF, 0x00, 0xFF), "GoblinKing"),
                new ColorSpawn(new Color32(0x00, 0xFF, 0xFF, 0xFF), "Bonemass"),
                new ColorSpawn(new Color32(0x4A, 0x86, 0xE8, 0xFF), "Dragonqueen"),
                new ColorSpawn(new Color32(0x00, 0x00, 0xFF, 0xFF), "Vendor_BlackForest"),
                new ColorSpawn(new Color32(0xE6, 0xB8, 0xAF, 0xFF), "AbandonedLogCabin02"),
                new ColorSpawn(new Color32(0xE6, 0xB8, 0xAF, 0xFF), "AbandonedLogCabin03"),
                new ColorSpawn(new Color32(0xE6, 0xB8, 0xAF, 0xFF), "AbandonedLogCabin04"),
                new ColorSpawn(new Color32(0xC9, 0xDA, 0xF8, 0xFF), "TrollCave02"),
                new ColorSpawn(new Color32(0xFF, 0xF2, 0xCC, 0xFF), "Crypt2"),
                new ColorSpawn(new Color32(0xFF, 0xF2, 0xCC, 0xFF), "Crypt3"),
                new ColorSpawn(new Color32(0xFF, 0xF2, 0xCC, 0xFF), "Crypt4"),
                new ColorSpawn(new Color32(0x45, 0x81, 0x8E, 0xFF), "SunkenCrypt4"),
                new ColorSpawn(new Color32(0xFF, 0xE5, 0x99, 0xFF), "Dolmen03"),
                new ColorSpawn(new Color32(0xFF, 0xE5, 0x99, 0xFF), "Dolmen01"),
                new ColorSpawn(new Color32(0xFF, 0xE5, 0x99, 0xFF), "Dolmen02"),
                new ColorSpawn(new Color32(0xDD, 0x7E, 0x6B, 0xFF), "Ruin3"),
                new ColorSpawn(new Color32(0xCC, 0x41, 0x25, 0xFF), "StoneTower1"),
                new ColorSpawn(new Color32(0xCC, 0x41, 0x25, 0xFF), "StoneTower3"),
                new ColorSpawn(new Color32(0x6A, 0xA8, 0x4F, 0xFF), "MountainGrave01"),
                new ColorSpawn(new Color32(0x7F, 0x60, 0x60, 0xFF), "Grave1"),
                new ColorSpawn(new Color32(0xB6, 0xD7, 0xA8, 0xFF), "InfestedTree01"),
                new ColorSpawn(new Color32(0x6D, 0x9E, 0xEB, 0xFF), "WoodHouse1"),
                new ColorSpawn(new Color32(0x6D, 0x9E, 0xEB, 0xFF), "WoodHouse10"),
                new ColorSpawn(new Color32(0x6D, 0x9E, 0xEB, 0xFF), "WoodHouse11"),
                new ColorSpawn(new Color32(0x6D, 0x9E, 0xEB, 0xFF), "WoodHouse12"),
                new ColorSpawn(new Color32(0x6D, 0x9E, 0xEB, 0xFF), "WoodHouse13"),
                new ColorSpawn(new Color32(0x6D, 0x9E, 0xEB, 0xFF), "WoodHouse2"),
                new ColorSpawn(new Color32(0x6D, 0x9E, 0xEB, 0xFF), "WoodHouse3"),
                new ColorSpawn(new Color32(0x6D, 0x9E, 0xEB, 0xFF), "WoodHouse4"),
                new ColorSpawn(new Color32(0x6D, 0x9E, 0xEB, 0xFF), "WoodHouse5"),
                new ColorSpawn(new Color32(0x6D, 0x9E, 0xEB, 0xFF), "WoodHouse6"),
                new ColorSpawn(new Color32(0x6D, 0x9E, 0xEB, 0xFF), "WoodHouse7"),
                new ColorSpawn(new Color32(0x6D, 0x9E, 0xEB, 0xFF), "WoodHouse8"),
                new ColorSpawn(new Color32(0x6D, 0x9E, 0xEB, 0xFF), "WoodHouse9"),
                new ColorSpawn(new Color32(0x76, 0xA5, 0xAF, 0xFF), "StoneHouse3"),
                new ColorSpawn(new Color32(0x76, 0xA5, 0xAF, 0xFF), "StoneHouse4"),
                new ColorSpawn(new Color32(0x93, 0xC4, 0x7D, 0xFF), "Meteorite"),
                new ColorSpawn(new Color32(0xA6, 0x1C, 0x00, 0xFF), "StoneTowerRuins04"),
                new ColorSpawn(new Color32(0xA6, 0x1C, 0x00, 0xFF), "StoneTowerRuins05"),
                new ColorSpawn(new Color32(0x13, 0x4F, 0x5C, 0xFF), "SwampRuin1"),
                new ColorSpawn(new Color32(0x13, 0x4F, 0x5C, 0xFF), "SwampRuin2"),
                new ColorSpawn(new Color32(0x27, 0x4E, 0x13, 0xFF), "Ruin1"),
                new ColorSpawn(new Color32(0x27, 0x4E, 0x13, 0xFF), "Ruin2"),
                new ColorSpawn(new Color32(0x85, 0x20, 0x0C, 0xFF), "DrakeLorestone"),
                new ColorSpawn(new Color32(0x5B, 0x0F, 0x00, 0xFF), "Runestone_Boars"),
                new ColorSpawn(new Color32(0x4F, 0xCC, 0xCC, 0xFF), "Runestone_Draugr"),
                new ColorSpawn(new Color32(0xEA, 0x99, 0x99, 0xFF), "Runestone_Greydwarfs"),
                new ColorSpawn(new Color32(0xE0, 0x66, 0x66, 0xFF), "Runestone_Meadows"),
                new ColorSpawn(new Color32(0xCC, 0x00, 0x00, 0xFF), "Runestone_Mountains"),
                new ColorSpawn(new Color32(0x99, 0x00, 0x00, 0xFF), "Runestone_Plains"),
                new ColorSpawn(new Color32(0x66, 0x00, 0x00, 0xFF), "Runestone_Swamps"),
                new ColorSpawn(new Color32(0xD0, 0xE0, 0xE3, 0xFF), "ShipSetting01"),
                new ColorSpawn(new Color32(0xFC, 0xE5, 0xCD, 0xFF), "ShipWreck01"),
                new ColorSpawn(new Color32(0xFC, 0xE5, 0xCD, 0xFF), "ShipWreck02"),
                new ColorSpawn(new Color32(0xFC, 0xE5, 0xCD, 0xFF), "ShipWreck03"),
                new ColorSpawn(new Color32(0xFC, 0xE5, 0xCD, 0xFF), "ShipWreck04"),
                new ColorSpawn(new Color32(0xBF, 0x90, 0x00, 0xFF), "GoblinCamp2"),
                new ColorSpawn(new Color32(0xFF, 0xD9, 0x66, 0xFF), "DrakeNest01"),
                new ColorSpawn(new Color32(0xF1, 0xC2, 0x32, 0xFF), "FireHole"),
                new ColorSpawn(new Color32(0xD9, 0xEA, 0xD3, 0xFF), "Greydwarf_camp1"),
                new ColorSpawn(new Color32(0xA2, 0xC4, 0xC9, 0xFF), "StoneCircle"),
                new ColorSpawn(new Color32(0xF9, 0xCB, 0x9C, 0xFF), "StoneHenge1"),
                new ColorSpawn(new Color32(0xF9, 0xCB, 0x9C, 0xFF), "StoneHenge2"),
                new ColorSpawn(new Color32(0xF9, 0xCB, 0x9C, 0xFF), "StoneHenge3"),
                new ColorSpawn(new Color32(0xF9, 0xCB, 0x9C, 0xFF), "StoneHenge4"),
                new ColorSpawn(new Color32(0xF9, 0xCB, 0x9C, 0xFF), "StoneHenge5"),
                new ColorSpawn(new Color32(0xF9, 0xCB, 0x9C, 0xFF), "StoneHenge6"),
                new ColorSpawn(new Color32(0xF6, 0xB2, 0x6B, 0xFF), "StoneTowerRuins03"),
                new ColorSpawn(new Color32(0xF6, 0xB2, 0x6B, 0xFF), "StoneTowerRuins07"),
                new ColorSpawn(new Color32(0xF6, 0xB2, 0x6B, 0xFF), "StoneTowerRuins08"),
                new ColorSpawn(new Color32(0xF6, 0xB2, 0x6B, 0xFF), "StoneTowerRuins09"),
                new ColorSpawn(new Color32(0xF6, 0xB2, 0x6B, 0xFF), "StoneTowerRuins10"),
                new ColorSpawn(new Color32(0xE6, 0x91, 0x38, 0xFF), "SwampHut5"),
                new ColorSpawn(new Color32(0xE6, 0x91, 0x38, 0xFF), "SwampHut1"),
                new ColorSpawn(new Color32(0xE6, 0x91, 0x38, 0xFF), "SwampHut2"),
                new ColorSpawn(new Color32(0xE6, 0x91, 0x38, 0xFF), "SwampHut3"),
                new ColorSpawn(new Color32(0xE6, 0x91, 0x38, 0xFF), "SwampHut4"),
                new ColorSpawn(new Color32(0xA4, 0xC2, 0xF4, 0xFF), "Waymarker01"),
                new ColorSpawn(new Color32(0xA4, 0xC2, 0xF4, 0xFF), "Waymarker02"),
                new ColorSpawn(new Color32(0x38, 0x76, 0x1D, 0xFF), "MountainWell1"),
                new ColorSpawn(new Color32(0x0C, 0x34, 0x3D, 0xFF), "SwampWell1"),
                new ColorSpawn(new Color32(0xB4, 0x5F, 0x06, 0xFF), "WoodFarm1"),
                new ColorSpawn(new Color32(0x78, 0x3F, 0x04, 0xFF), "WoodVillage1"),
            };

            protected override bool LoadTextureToMap(Texture2D tex)
            {
                int Index(int x, int y) => y * Size + x;
                
                var pixels = tex.GetPixels();

                void FloodFill(int x, int y, Action<int, int> fillfn)
                {
                    var sourceColor = pixels[Index(x, y)];
                    bool CheckValidity(int xc, int yc) => xc >= 0 && xc < Size && yc >= 0 && yc < Size && pixels[Index(xc, yc)] == sourceColor;

                    var q = new Queue<Vector2i> (Size * Size);
                    q.Enqueue (new Vector2i (x, y));

                    while (q.Count > 0) {
                        var point = q.Dequeue ();
                        var x1 = point.x;
                        var y1 = point.y;
                        if (q.Count > Size * Size) {
                            throw new Exception ($"Flood fill on spawn location failed. Queue size: {q.Count}");
                        }

                        fillfn(x1, y1);
                        
                        pixels[Index(x1, y1)] = Color.black;

                        if (CheckValidity (x1 + 1, y1))
                            q.Enqueue (new Vector2i (x1 + 1, y1));

                        if (CheckValidity (x1 - 1, y1))
                            q.Enqueue (new Vector2i(x1 - 1, y1));

                        if (CheckValidity (x1, y1 + 1))
                            q.Enqueue (new Vector2i (x1, y1 + 1));

                        if (CheckValidity (x1, y1 - 1))
                            q.Enqueue (new Vector2i (x1, y1 - 1));
                    }
                }
                
                // Determine the spawn positions by color first
                var colorSpawns = new Dictionary<Color, List<Vector2>>();
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; ++x)
                    {
                        int i = y * Size + x;
                        var color = pixels[i];
                        if (color != Color.black)
                        {
                            var area = new List<Vector2>();

                            // Do this AFTER determining the SpawnColorMapping, as it changes the color in pixels to black
                            FloodFill(x, y, (fx, fy) => area.Add(new Vector2(fx / (float) Size, fy / (float) Size)));

                            if (!colorSpawns.TryGetValue(color, out var areas))
                            {
                                areas = new List<Vector2>();
                                colorSpawns.Add(color, areas);
                            }

                            // Just select the actual position from the area now, there is no point delaying this until later
                            var position = area[UnityEngine.Random.Range(0, area.Count)];
                            areas.Add(position);
                            Log($"Found #{ColorUtility.ToHtmlStringRGB(color)} area of {area.Count} size at {x}, {Size - y}, selected position {position.x}, {position.y}");
                        }
                    }
                }
                
                // Now we need to divvy up the color spawn areas between the associated spawn types 
                RemainingSpawnAreas = new Dictionary<string, List<Vector2>>();
                foreach (var colorPositions in colorSpawns)
                {
                    var spawns = SpawnColorMapping.Where(d => d.color == colorPositions.Key).ToList();
                    if (spawns.Count > 0)
                    {
                        foreach (var position in colorPositions.Value)
                        {
                            var spawn = spawns[UnityEngine.Random.Range(0, spawns.Count)].spawn;
                            if (!RemainingSpawnAreas.TryGetValue(spawn, out var positions))
                            {
                                positions = new List<Vector2>();
                                RemainingSpawnAreas.Add(spawn, positions);
                            }

                            positions.Add(position);
                            Log($"Selected {spawn} for spawn position {position.x}, {position.y}");
                        }
                    }
                    else
                    {
                        Log($"No spawns are mapped to color #{ColorUtility.ToHtmlStringRGB(colorPositions.Key)} (which has {colorPositions.Value.Count} spawn positions defined)");
                    }
                }

                return true;
            }

            public Vector2? FindSpawn(string spawn)
            {
                if (RemainingSpawnAreas.TryGetValue(spawn, out var positions))
                {
                    int idx = UnityEngine.Random.Range(0, positions.Count);
                    var position = positions[idx];
                    positions.RemoveAt(idx);
                    if (positions.Count == 0)
                    {
                        RemainingSpawnAreas.Remove(spawn);
                    }
                    return position;
                }
                return null;
            }

            public IEnumerable<Vector2> GetAllSpawns(string spawn) => RemainingSpawnAreas.TryGetValue(spawn, out var positions) ? positions : Enumerable.Empty<Vector2>();
        }
        
        private struct BetterContinentsSettings
        {
            // Add new properties at the end, and comment where new versions start
            public const int LatestVersion = 3;
            
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

            // Non-serialized
            private ImageMapFloat Heightmap;
            private ImageMapBiome Biomemap;
            private ImageMapSpawn Spawnmap;

            public bool OverrideBiomes => this.Biomemap != null;
            public bool UseSpawnmap => this.Spawnmap != null;
            
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
                    ForestAmountOffset = Mathf.Lerp(1, -1, ConfigForestAmount.Value);

                    OverrideStartPosition = ConfigOverrideStartPosition.Value;
                    StartPositionX = ConfigStartPositionX.Value;
                    StartPositionY = ConfigStartPositionY.Value;
                    //LakesEnabled = ConfigLakesEnabled.Value;
                    
                    if (!string.IsNullOrEmpty(ConfigSpawnmapFile.Value))
                    {
                        Spawnmap = new ImageMapSpawn(ConfigSpawnmapFile.Value);
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
                    
                    if (Spawnmap != null)
                    {
                        Log($"Spawnmap file {Spawnmap.FilePath}");
                        Log($"Spawnmap includes spawns for {Spawnmap.RemainingSpawnAreas.Count} types");
                    }
                    else
                    {
                        Log($"Spawnmap disabled");
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
                    
                    // Version 3
                    if (Version >= 3)
                    {
                        var spawnmapFilePath = pkg.ReadString();
                        if (!string.IsNullOrEmpty(spawnmapFilePath))
                        {
                            Spawnmap = new ImageMapSpawn(spawnmapFilePath, pkg);
                        }
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

            public Vector2? FindSpawn(string spawn) => this.Spawnmap.FindSpawn(spawn);
            public IEnumerable<Vector2> GetAllSpawns(string spawn) => this.Spawnmap.GetAllSpawns(spawn);
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

            ConfigSpawnmapFile = Config.Bind("BetterContinents.Spawnmap", "Spawnmap", "", "Path to a spawn map file to use. See the description on Nexusmods.com for the specifications (it will fail if they are not met).");
            
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
            
            ConfigDebugSkipDefaultLocationPlacement = Config.Bind("BetterContinents.Debug", "SkipDefaultLocationPlacement", false, "Skips default location placement during world gen (spawn temple and spawnmap are still placed), for quickly testing the heightmap itself.");
            
            ConfigDebugModeEnabled = Config.Bind("BetterContinents.Debug", "DebugMode", false, "Automatically reveals the full map on respawn, enables cheat mode, and debug mode, for debugging purposes.");

            new Harmony("BetterContinents.Harmony").PatchAll();
            Log("Awake");
        }

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
                    return true;
                }

                switch (Settings.Version)
                {
                    case 1 :
                    case 2 : return GetBaseHeightV1(ref wx, ref wy, ref __result, ___m_offset0, ___m_offset1, ___m_minMountainDistance);
                    case 3 :
                    default: return GetBaseHeightV2(ref wx, ref wy, ref __result, ___m_offset0, ___m_offset1, ___m_minMountainDistance);
                }
            }
            
            private static bool GetBaseHeightV1(ref float wx, ref float wy, ref float __result, float ___m_offset0, float ___m_offset1, float ___m_minMountainDistance)
            {
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
            
            private static bool GetBaseHeightV2(ref float wx, ref float wy, ref float __result, float ___m_offset0, float ___m_offset1, float ___m_minMountainDistance)
            {
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

                float bigFeature = Mathf.Clamp(bigFeatureHeight + ridgeHeight * lerp, 0, 1);

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

            // We must come before WorldGenOptions, as that mod always replaces the GetBiome function.
            [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.GetBiome), typeof(float), typeof(float)), HarmonyBefore("org.github.spacedrive.worldgen")]
            private static bool GetBiomePrefix(WorldGenerator __instance, float wx, float wy, ref Heightmap.Biome __result, World ___m_world)
            {
                if (!Settings.EnabledForThisWorld || ___m_world.m_menu || !Settings.OverrideBiomes)
                {
                    return true;
                }
                else
                {

                    float mapX = GetMapCoord(wx);
                    float mapY = GetMapCoord(wy);
                    __result = Settings.GetBiomeOverride(__instance, mapX, mapY);

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

            [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.GetForestFactor))]
            private static void GetForestFactorPrefix(ref Vector3 pos)
            {
                if (Settings.EnabledForThisWorld && Settings.ForestScale != 1)
                {
                    pos *= Settings.ForestScale;
                }
            }
            
            [HarmonyPostfix, HarmonyPatch(nameof(WorldGenerator.GetForestFactor))]
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
            [HarmonyPrefix, HarmonyPatch(nameof(ZoneSystem.GenerateLocations), typeof(ZoneSystem.ZoneLocation))]
            private static bool GenerateLocationsPrefix(ZoneSystem __instance, ZoneSystem.ZoneLocation location)
            {
                Log($"Generating location of group {location.m_group}, required {location.m_quantity}, unique {location.m_unique}, name {location.m_prefabName}");
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
                            AccessTools.Method(typeof(ZoneSystem), "RegisterLocation")
                                .Invoke(__instance, new object[] {location, position, false});
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
                        AccessTools.Method(typeof(ZoneSystem), "RegisterLocation")
                            .Invoke(__instance, new object[] {location, position, false});
                        Log($"Start position overriden: set to {position}");
                        return false;
                    }
                    
                    if (ConfigDebugSkipDefaultLocationPlacement.Value)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Player))]
        private class PlayerPatch
        {
            [HarmonyPostfix, HarmonyPatch(nameof(Player.OnSpawned))]
            private static void OnSpawnedPostfix()
            {
                if (ZNet.instance && ZNet.instance.IsServer() && Settings.EnabledForThisWorld && ConfigDebugModeEnabled.Value)
                {
                    AccessTools.Field(typeof(Console), "m_cheat").SetValue(Console.instance, true);
                    Minimap.instance.ExploreAll();
                    Player.m_debugMode = true;
                }
            }
        }
    }
}
