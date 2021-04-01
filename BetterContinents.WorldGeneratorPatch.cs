using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace BetterContinents
{
    public partial class BetterContinents
    {
        // Changes to height, biome, forests, rivers etc. (this is the functional part of the mod)
        [HarmonyPatch(typeof(WorldGenerator))]
        public class WorldGeneratorPatch
        {
            // The base map x, y coordinates in 0..1 range
            private static Dictionary<Vector2, float> cachedHeights;
            private static bool cacheEnabled = true;

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

            public static void DisableCache() => cacheEnabled = false;
            public static void EnableCache()
            {
                cacheEnabled = true;
                cachedHeights.Clear();
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
                
                if (cacheEnabled && cachedHeights.TryGetValue(new Vector2(wx, wy), out __result))
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

                if (cacheEnabled)
                {
                    if (cachedHeights.Count >= 100000)
                    {
                        // Can't easily do LRU, so we will just clear it entirely
                        cachedHeights.Clear();
                    }

                    // Do this AFTER clearing so we have it available below
                    cachedHeights[new Vector2(wx, wy)] = __result;
                }
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
}