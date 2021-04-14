using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace BetterContinents
{
    public partial class BetterContinents
    {
        // Changes to height, biome, forests, rivers etc. (this is the functional part of the mod)
        [HarmonyPatch(typeof(WorldGenerator))]
        public class WorldGeneratorPatch
        {
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

            private static int currentSeed;

            //private static Noise 
            [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.Initialize))]
            private static void InitializePrefix(World world)
            {
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

                if (Settings.EnabledForThisWorld)
                {
                    //baseNoiseLayer = new FastNoiseLite(world.m_seed);
                    //ridgeLayer = new FastNoiseLite(world.m_seed);
                    currentSeed = world.m_seed;
                    ApplyNoiseSettings();
                }
            }

            public static NoiseStack BaseHeightNoise;

            public static void ApplyNoiseSettings()
            {
                BaseHeightNoise = new NoiseStack(currentSeed, Settings.BaseHeightNoise);
            }

            // wx, wy are [-10500, 10500]
            // __result should be [0, 1]
            [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.GetBaseHeight))]
            private static bool GetBaseHeightPrefix(ref float wx, ref float wy, bool menuTerrain, ref float __result, float ___m_offset0, float ___m_offset1, float ___m_minMountainDistance)
            {
                if (!Settings.EnabledForThisWorld || menuTerrain)
                {
                    return true;
                }
                
                switch (Settings.Version)
                {
                    case 1:
                    case 2:
                        __result = GetBaseHeightV1(wx, wy, ___m_offset0, ___m_offset1, ___m_minMountainDistance);
                        break;
                    case 3:
                    case 4:
                    case 5:
                    case 6:
                        __result = GetBaseHeightV2(wx, wy, ___m_offset0, ___m_offset1, ___m_minMountainDistance);
                        break;
                    case 7:
                    default:
                        __result = GetBaseHeightV3(wx, wy, ___m_minMountainDistance);
                        break;
                }

                return false;
            }

            private delegate float GetBaseHeightDelegate(WorldGenerator instance, float wx, float wy, bool menuTerrain);
            private static readonly GetBaseHeightDelegate GetBaseHeightMethod 
                = DebugUtils.GetDelegate<GetBaseHeightDelegate>(typeof(WorldGenerator), nameof(WorldGenerator.GetBaseHeight));

            [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.GetBiomeHeight))]
            private static bool GetBiomeHeightPrefix(WorldGenerator __instance, Heightmap.Biome biome, float wx, float wy, ref float __result, World ___m_world)
            {
                if (!Settings.EnabledForThisWorld || ___m_world.m_menu || Settings.Version <= 6)
                {
                    return true;
                }

                switch (biome)
                {
                    case Heightmap.Biome.Meadows: 
                        __result = GetMeadowsHeight(__instance, wx, wy);
                        break;
                    case Heightmap.Biome.Mistlands: 
                        __result = GetMistlandsHeight(__instance, wx, wy);
                        break;
                    case Heightmap.Biome.Mountain: 
                        __result = GetMountainHeight(__instance, wx, wy);
                        break;
                    case Heightmap.Biome.Ocean: 
                        __result = GetOceanHeight(__instance, wx, wy);
                        break;
                    case Heightmap.Biome.Plains: 
                        __result = GetPlainsHeight(__instance, wx, wy);
                        break;
                    case Heightmap.Biome.Swamp: 
                        __result = GetSwampHeight(__instance, wx, wy);
                        break;
                    case Heightmap.Biome.AshLands: 
                        __result = GetAshLandsHeight(__instance, wx, wy);
                        break;
                    case Heightmap.Biome.BlackForest: 
                        __result = GetBlackForestHeight(__instance, wx, wy);
                        break;
                    case Heightmap.Biome.DeepNorth: 
                        __result = GetDeepNorthHeight(__instance, wx, wy);
                        break;
                }

                __result *= 200f;

                return false;
            }

            [HarmonyPostfix, HarmonyPatch(nameof(WorldGenerator.GetBiomeHeight))]
            private static void GetBiomeHeightPostfix(WorldGenerator __instance, float wx, float wy, ref float __result, World ___m_world)
            {
                if (!Settings.EnabledForThisWorld || ___m_world.m_menu)
                {
                    return;
                }

                if (!Settings.ShouldHeightmapOverrideAll && !Settings.HasRoughmap)
                {
                    return;
                }
                
                float smoothHeight = GetBaseHeightMethod(__instance, wx, wy, false) * 200f;
                __result = Settings.ShouldHeightmapOverrideAll 
                    ? smoothHeight 
                    : Settings.ApplyRoughmap(NormalizedX(wx), NormalizedY(wy), smoothHeight, __result);
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
                float lerp = Settings.ShouldHeightmapOverrideAll 
                    ? 0 
                    : Mathf.Clamp01(SigmoidActivation(Mathf.PerlinNoise(wx * 0.005f - 10000, wy * 0.005f - 5000) - Settings.RidgeBlendSigmoidXOffset, 0, Settings.RidgeBlendSigmoidB));
                
                float finalHeight = 0f;

                float bigFeature = Mathf.Clamp01(Mathf.Lerp(bigFeatureHeight, ridgeHeight, lerp));
                const float SeaLevel = 0.05f;
                float ApplyMountains(float x, float n) => x * (1 - Mathf.Pow(1 - x, 1.2f + n * 0.8f)) + x * (1 - x);

                finalHeight += ApplyMountains(bigFeature - SeaLevel, Settings.MountainsAmount) + SeaLevel;

                finalHeight += Mathf.PerlinNoise(wx * 0.002f * 1f, wy * 0.002f * 1f) * Mathf.PerlinNoise(wx * 0.003f * 1f, wy * 0.003f * 1f) * finalHeight * 0.9f;

                finalHeight += Mathf.PerlinNoise(wx * 0.005f * 1f, wy * 0.005f * 1f) * Mathf.PerlinNoise(wx * 0.01f * 1f, wy * 0.01f * 1f) * 0.5f * finalHeight;

                finalHeight -= 0.07f;

                finalHeight += Settings.SeaLevelAdjustment;

                if (Settings.OceanChannelsEnabled && !Settings.ShouldHeightmapOverrideAll)
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
                if (distance < ___m_minMountainDistance && finalHeight > 0.28f && !Settings.ShouldHeightmapOverrideAll)
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
            
            private static float GetBaseHeightV3(float wx, float wy, float ___m_minMountainDistance)
            {
                float distance = Utils.Length(wx, wy);
                
                // The base map x, y coordinates in 0..1 range
                float mapX = NormalizedX(wx);
                float mapY = NormalizedY(wy);

                float baseHeight = BaseHeightNoise.Apply(wx, wy, 0f);
                
                float finalHeight = Settings.ApplyHeightmap(mapX, mapY, baseHeight);
                finalHeight -= 0.15f; // Resulting in about 30% water coverage by default
                finalHeight += Settings.SeaLevelAdjustment;

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

            private static float GetMeadowsHeight(WorldGenerator __instance, float fx, float fy)
            {
                float baseHeight = GetBaseHeightMethod(__instance, fx, fy, false);
                return baseHeight;
            }
            
            private static float GetMistlandsHeight(WorldGenerator __instance, float fx, float fy)
            {
                float baseHeight = GetBaseHeightMethod(__instance, fx, fy, false);
                return baseHeight;
            }
            
            private static float GetMountainHeight(WorldGenerator __instance, float fx, float fy)
            {
                float baseHeight = GetBaseHeightMethod(__instance, fx, fy, false);
                return baseHeight;
            } 
            
            private static float GetOceanHeight(WorldGenerator __instance, float fx, float fy)
            {
                float baseHeight = GetBaseHeightMethod(__instance, fx, fy, false);
                return baseHeight;
            }

            private static float GetPlainsHeight(WorldGenerator __instance, float fx, float fy)
            {
                float baseHeight = GetBaseHeightMethod(__instance, fx, fy, false);
                return baseHeight;
            }

            private static float GetSwampHeight(WorldGenerator __instance, float fx, float fy)
            {
                float baseHeight = GetBaseHeightMethod(__instance, fx, fy, false);
                return baseHeight;
            }
            
            private static float GetAshLandsHeight(WorldGenerator __instance, float fx, float fy)
            {
                float baseHeight = GetBaseHeightMethod(__instance, fx, fy, false);
                return baseHeight;
            }

            private static float GetBlackForestHeight(WorldGenerator __instance, float fx, float fy)
            {
                float baseHeight = GetBaseHeightMethod(__instance, fx, fy, false);
                return baseHeight;
            }
            
            private static float GetDeepNorthHeight(WorldGenerator __instance, float fx, float fy)
            {
                float baseHeight = GetBaseHeightMethod(__instance, fx, fy, false);
                return baseHeight;
            }

            // We must come after WorldGenOptions, as that mod always replaces the GetBiome function.
            // Note in HarmonyX (which BepInEx uses), all prefixes always run, unless they opt out themselves.
            // So coming BEFORE a prefex and returning false doesn't stop that prefix from running.
            [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.GetBiome), typeof(float), typeof(float)), HarmonyAfter("org.github.spacedrive.worldgen")]
            private static bool GetBiomePrefix(float wx, float wy, ref Heightmap.Biome __result, World ___m_world)
            {
                if (!Settings.EnabledForThisWorld || ___m_world.m_menu || !Settings.HasBiomemap)
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
            
            [HarmonyPrefix, HarmonyPatch(nameof(WorldGenerator.AddRivers))]
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
            private static void GetForestFactorPrefix(ref Vector3 pos, ref object __state)
            {
                // Save the pos before modifying it, for use in the postfix below (to save possible float accuracy
                // weirdness if we were to attempt to reverse the scaling instead)
                __state = pos;
                if (Settings.EnabledForThisWorld && Settings.ForestScale != 1)
                {
                    pos *= Settings.ForestScale;
                }
            }

            // Range: 0.145071 1.850145
            [HarmonyPostfix, HarmonyPatch(nameof(WorldGenerator.GetForestFactor))]
            private static void GetForestFactorPostfix(object __state, ref float __result)
            {
                if (Settings.EnabledForThisWorld)
                {
                    // We stored the original unmodified pos in the prefix above
                    var pos = (Vector3) __state;
                    __result = Settings.ApplyForest(NormalizedX(pos.x), NormalizedY(pos.z), __result);
                }
            }
        }
    }

    // None of this is faster in testing
    [HarmonyPatch(typeof(HeightmapBuilder))]
    public class HeightmapBuilderPatch
    {
        [HarmonyPrefix, HarmonyPatch(nameof(HeightmapBuilder.Build))]
        private static bool BuildPrefix(HeightmapBuilder.HMBuildData data, out Stopwatch __state)
        {
            __state = new Stopwatch();
            __state.Start();
            if (BetterContinents.ConfigExperimentalMultithreadedHeightmapBuild.Value)
            {
                BuildMT(data);
                return false;
            }
            else
            {
                return true;
            }
        }
        
        [HarmonyPostfix, HarmonyPatch(nameof(HeightmapBuilder.Build))]
        private static void BuildPostfix(Stopwatch __state)
        {
            BetterContinents.Log(BetterContinents.ConfigExperimentalMultithreadedHeightmapBuild.Value
                ? $"Heightmap Build MT {__state.ElapsedMilliseconds} ms"
                : $"Heightmap Build Vanilla {__state.ElapsedMilliseconds} ms"
            );
        }
        
        private delegate void BuildDelegate(HeightmapBuilder instance, HeightmapBuilder.HMBuildData data);
        private static readonly BuildDelegate BuildFn = (BuildDelegate)typeof(HeightmapBuilder)
                .GetMethod(nameof(HeightmapBuilder.Build), BindingFlags.NonPublic | BindingFlags.Instance)
                .CreateDelegate(typeof(BuildDelegate)); 

        private static List<float> buildTimes = new List<float>();
        [HarmonyPrefix, HarmonyPatch(nameof(HeightmapBuilder.BuildThread))]
        private static bool BuildThreadPrefix(HeightmapBuilder __instance)
        {
            ZLog.Log("Builder started");
            while (!__instance.m_stop)
            {
                if (!BetterContinents.ConfigExperimentalParallelChunksBuild.Value)
                {
                    __instance.m_lock.WaitOne();
                    bool flag = __instance.m_toBuild.Count > 0;
                    __instance.m_lock.ReleaseMutex();
                    if (flag)
                    {
                        __instance.m_lock.WaitOne();
                        HeightmapBuilder.HMBuildData hmbuildData = __instance.m_toBuild[0];
                        __instance.m_lock.ReleaseMutex();

                        var st = new Stopwatch();
                        st.Start();
                        __instance.Build(hmbuildData);
                        buildTimes.Add(st.ElapsedMilliseconds);
                        if (buildTimes.Count > 100)
                        {
                            buildTimes.RemoveAt(0);
                        }
                        BetterContinents.Log($"Average chunk build time with vanilla {buildTimes.Average()} ms");
                        
                        __instance.m_lock.WaitOne();
                        __instance.m_toBuild.Remove(hmbuildData);
                        __instance.m_ready.Add(hmbuildData);
                        while (__instance.m_ready.Count > 16)
                        {
                            __instance.m_ready.RemoveAt(0);
                        }
                        __instance.m_lock.ReleaseMutex();
                    }
                    Thread.Sleep(10);
                }
                else
                {
                    __instance.m_lock.WaitOne();
                    var buildTasks = __instance.m_toBuild
                        .Select(b => new
                        {
                            task = Task.Run(() => BuildFn(__instance, b)),
                            buildData = b
                        })
                        .ToList(); // Important to use ToList for force immediate enumeration inside the lock!
                    __instance.m_toBuild.Clear();
                    __instance.m_lock.ReleaseMutex();

                    if (buildTasks.Any())
                    {
                        var st = new Stopwatch();
                        st.Start();
                        Task.WaitAll(buildTasks.Select(bt => bt.task).ToArray());
                        buildTimes.Add(st.ElapsedMilliseconds / (float) buildTasks.Count);
                        if (buildTimes.Count > 100)
                        {
                            buildTimes.RemoveAt(0);
                        }
                        BetterContinents.Log($"Average chunk build time with MT {buildTimes.Average()} ms");

                        __instance.m_lock.WaitOne();
                        foreach (var bt in buildTasks)
                        {
                            __instance.m_ready.Add(bt.buildData);
                        }

                        while (__instance.m_ready.Count > 16)
                        {
                            __instance.m_ready.RemoveAt(0);
                        }

                        __instance.m_lock.ReleaseMutex();
                    }
                }

                Thread.Sleep(10);
            }

            return false;
        }

        // This is slower, at least for the 64x64 tiles
        private static void BuildMT(HeightmapBuilder.HMBuildData data)
	    {
		    int num = data.m_width + 1;
		    int num2 = num * num;
		    Vector3 vector = data.m_center + new Vector3((float)data.m_width * data.m_scale * -0.5f, 0f, (float)data.m_width * data.m_scale * -0.5f);
		    WorldGenerator worldGen = data.m_worldGen;
		    data.m_cornerBiomes = new Heightmap.Biome[4];
		    data.m_cornerBiomes[0] = worldGen.GetBiome(vector.x, vector.z);
		    data.m_cornerBiomes[1] = worldGen.GetBiome(vector.x + (float)data.m_width * data.m_scale, vector.z);
		    data.m_cornerBiomes[2] = worldGen.GetBiome(vector.x, vector.z + (float)data.m_width * data.m_scale);
		    data.m_cornerBiomes[3] = worldGen.GetBiome(vector.x + (float)data.m_width * data.m_scale, vector.z + (float)data.m_width * data.m_scale);
		    Heightmap.Biome biome = data.m_cornerBiomes[0];
		    Heightmap.Biome biome2 = data.m_cornerBiomes[1];
		    Heightmap.Biome biome3 = data.m_cornerBiomes[2];
		    Heightmap.Biome biome4 = data.m_cornerBiomes[3];
		    data.m_baseHeights = new List<float>(num * num);
		    for (int i = 0; i < num2; i++)
		    {
			    data.m_baseHeights.Add(0f);
		    }

            GameUtils.SimpleParallelFor(4, 0, num, j => 
                //for (int j = 0; j < num; j++)
            {
                float wy = vector.z + (float) j * data.m_scale;
                float t = Mathf.SmoothStep(0f, 1f, (float) j / (float) data.m_width);
                for (int k = 0; k < num; k++)
                {
                    float wx = vector.x + (float) k * data.m_scale;
                    float t2 = Mathf.SmoothStep(0f, 1f, (float) k / (float) data.m_width);
                    float value;
                    if (data.m_distantLod)
                    {
                        Heightmap.Biome biome5 = worldGen.GetBiome(wx, wy);
                        value = worldGen.GetBiomeHeight(biome5, wx, wy);
                    }
                    else if (biome3 == biome && biome2 == biome && biome4 == biome)
                    {
                        value = worldGen.GetBiomeHeight(biome, wx, wy);
                    }
                    else
                    {
                        float biomeHeight = worldGen.GetBiomeHeight(biome, wx, wy);
                        float biomeHeight2 = worldGen.GetBiomeHeight(biome2, wx, wy);
                        float biomeHeight3 = worldGen.GetBiomeHeight(biome3, wx, wy);
                        float biomeHeight4 = worldGen.GetBiomeHeight(biome4, wx, wy);
                        float a = Mathf.Lerp(biomeHeight, biomeHeight2, t2);
                        float b = Mathf.Lerp(biomeHeight3, biomeHeight4, t2);
                        value = Mathf.Lerp(a, b, t);
                    }

                    data.m_baseHeights[j * num + k] = value;
                }
            });

		    if (data.m_distantLod)
		    {
			    for (int l = 0; l < 4; l++)
			    {
				    List<float> list = new List<float>(data.m_baseHeights);
				    for (int m = 1; m < num - 1; m++)
				    {
					    for (int n = 1; n < num - 1; n++)
					    {
						    float num3 = list[m * num + n];
						    float num4 = list[(m - 1) * num + n];
						    float num5 = list[(m + 1) * num + n];
						    float num6 = list[m * num + n - 1];
						    float num7 = list[m * num + n + 1];
						    if (Mathf.Abs(num3 - num4) > 10f)
						    {
							    num3 = (num3 + num4) * 0.5f;
						    }
						    if (Mathf.Abs(num3 - num5) > 10f)
						    {
							    num3 = (num3 + num5) * 0.5f;
						    }
						    if (Mathf.Abs(num3 - num6) > 10f)
						    {
							    num3 = (num3 + num6) * 0.5f;
						    }
						    if (Mathf.Abs(num3 - num7) > 10f)
						    {
							    num3 = (num3 + num7) * 0.5f;
						    }
						    data.m_baseHeights[m * num + n] = num3;
					    }
				    }
			    }
		    }
	    }
    }
}