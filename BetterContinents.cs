using System;
using System.Collections.Generic;
using System.IO;
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
        private static ConfigEntry<float> ConfigContinentSize;
        private static ConfigEntry<float> ConfigMountainsAmount;
        private static ConfigEntry<float> ConfigSeaLevelAdjustment;
        private static ConfigEntry<float> ConfigMaxRidgeHeight;
        private static ConfigEntry<float> ConfigRidgeSize;
        private static ConfigEntry<float> ConfigRidgeBlend;
        private static ConfigEntry<float> ConfigRidgeAmount;

        private struct BetterContinentsSettings
        {
            public const int LatestVersion = 1;
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

            public static BetterContinentsSettings Create(long worldUId)
            {
                var settings = new BetterContinentsSettings();
                settings.InitSettings(worldUId);
                return settings;
            }
            
            public static BetterContinentsSettings Disabled(long worldUId)
            {
                var settings = Create(worldUId);
                settings.EnabledForThisWorld = false;
                return settings;
            }
            
            private void InitSettings(long worldUId)
            {
                Debug.Log("[BetterContinents] Init settings for new world");

                Version = LatestVersion;

                WorldUId = worldUId;

                EnabledForThisWorld = ConfigEnabled.Value;
                
                GlobalScale = FeatureScaleCurve(ConfigContinentSize.Value);
                MountainsAmount = ConfigMountainsAmount.Value;
                SeaLevelAdjustment = Mathf.Lerp(-0.05f, 0.05f, ConfigSeaLevelAdjustment.Value);

                MaxRidgeHeight = ConfigMaxRidgeHeight.Value;
                RidgeScale = FeatureScaleCurve(ConfigRidgeSize.Value);
                RidgeBlendSigmoidB = Mathf.Lerp(-30f, -10f, ConfigRidgeBlend.Value);
                RidgeBlendSigmoidXOffset = Mathf.Lerp(1f, 0.35f, ConfigRidgeAmount.Value);
            }
            
            public void Dump()
            {
                Debug.Log($"[BetterContinents] Version {Version}");
                Debug.Log($"[BetterContinents] WorldUId {WorldUId}");
                Debug.Log($"[BetterContinents] EnabledForThisWorld {EnabledForThisWorld}");
                Debug.Log($"[BetterContinents] GlobalScale {GlobalScale}");
                Debug.Log($"[BetterContinents] MountainsAmount {MountainsAmount}");
                Debug.Log($"[BetterContinents] MaxRidgeHeight {MaxRidgeHeight}");
                Debug.Log($"[BetterContinents] SeaLevelAdjustment {SeaLevelAdjustment}");
                Debug.Log($"[BetterContinents] RidgeScale {RidgeScale}");
                Debug.Log($"[BetterContinents] RidgeBlendSigmoidB {RidgeBlendSigmoidB}");
                Debug.Log($"[BetterContinents] RidgeBlendSigmoidXOffset {RidgeBlendSigmoidXOffset}");
            }

            public void Serialize(ZPackage pkg)
            {
                pkg.Write(Version);

                pkg.Write(WorldUId);

                pkg.Write(EnabledForThisWorld);

                pkg.Write(GlobalScale);
                pkg.Write(MountainsAmount);
                pkg.Write(SeaLevelAdjustment);

                pkg.Write(MaxRidgeHeight);
                pkg.Write(RidgeScale);
                pkg.Write(RidgeBlendSigmoidB);
                pkg.Write(RidgeBlendSigmoidXOffset);
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
                    throw new Exception($"BetterContinents mod is out of date: world expects config version {Version}, mod config version is {LatestVersion}");
                }

                WorldUId = pkg.ReadLong();
                
                EnabledForThisWorld = pkg.ReadBool();

                GlobalScale = pkg.ReadSingle();
                MountainsAmount = pkg.ReadSingle();
                SeaLevelAdjustment = pkg.ReadSingle();

                MaxRidgeHeight = pkg.ReadSingle();
                RidgeScale = pkg.ReadSingle();
                RidgeBlendSigmoidB = pkg.ReadSingle();
                RidgeBlendSigmoidXOffset = pkg.ReadSingle();
            }
        }
        
        private static Dictionary<long, BetterContinentsSettings> AllSettings = new Dictionary<long, BetterContinentsSettings>();
        private static BetterContinentsSettings Settings;
        
        private void Awake()
        {
            ConfigEnabled = Config.Bind("BetterContinents.Global", "Enabled", true, "Whether this mod is enabled");

            ConfigContinentSize = Config.Bind("BetterContinents.Global", "ContinentSize", 0.5f,
                new ConfigDescription("Continent Size", new AcceptableValueRange<float>(0, 1)));
            ConfigMountainsAmount = Config.Bind("BetterContinents.Global", "MountainsAmount", 0.5f,
                new ConfigDescription("Mountains amount", new AcceptableValueRange<float>(0, 1)));
            ConfigSeaLevelAdjustment = Config.Bind("BetterContinents.Global", "SeaLevelAdjustment", 0.5f,
                new ConfigDescription("Modify sea level, which changes the land:sea ratio", new AcceptableValueRange<float>(0, 1)));

            ConfigMaxRidgeHeight = Config.Bind("BetterContinents.Ridges", "MaxRidgeHeight", 0.5f,
                new ConfigDescription("Max height of ridge features", new AcceptableValueRange<float>(0, 1)));
            ConfigRidgeSize = Config.Bind("BetterContinents.Ridges", "RidgeSize", 0.5f,
                new ConfigDescription("Size of ridge features", new AcceptableValueRange<float>(0, 1)));
            ConfigRidgeBlend = Config.Bind("BetterContinents.Ridges", "RidgeBlend", 0.5f,
                new ConfigDescription("Smoothness of ridges blending into base terrain", new AcceptableValueRange<float>(0, 1)));
            ConfigRidgeAmount = Config.Bind("BetterContinents.Ridges", "RidgeAmount", 0.5f,
                new ConfigDescription("How much ridges", new AcceptableValueRange<float>(0, 1)));

            new Harmony("BetterContinents.Harmony").PatchAll();
            Debug.Log("[BetterContinents] Awake");
        }

        // When the world metadata is saved we write an extra file next to it for our own config
        [HarmonyPatch(typeof(World), "SaveWorldMetaData")]
        private class WorldSaveWorldMetaDataPatch
        {
            private static void Postfix(World __instance)
            {
                Debug.Log($"[BetterContinents] Saving BetterContinents settings for {__instance.m_name}");

                // Check if this world has been saved before (the main world data file should exist)
                if (!File.Exists(__instance.GetDBPath()))
                {
                    // First time save, so bake our settings as they currently are
                    Debug.Log($"[BetterContinents] First time save of {__instance.m_name}, baking BetterContinents settings");
                    Settings = BetterContinentsSettings.Create(__instance.m_uid);
                }

                var zpackage = new ZPackage();
                Settings.Serialize(zpackage);
                
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
        }
        
        // When the world is loaded we read an extra file next to it for our own config
        [HarmonyPatch(typeof(World), "LoadWorld")]
        private class WorldLoadWorldPatch
        {
            private static void Prefix(string name)
            {
                Debug.Log($"[BetterContinents] Loading BetterContinents settings for {name}");

                try
                {
                    using (var binaryReader = new BinaryReader(File.OpenRead(World.GetMetaPath(name) + ".BetterContinents")))
                    {
                        int count = binaryReader.ReadInt32();
                        var newSettings = BetterContinentsSettings.Load(new ZPackage(binaryReader.ReadBytes(count))); 
                        AllSettings[newSettings.WorldUId] = newSettings;
                    }
                }
                catch
                {
                    Debug.LogError($"[BetterContinents] Loading BetterContinents settings for {name} failed, mod is disabled for this World");
                    return;
                }
            }
        }
        
        // When the world is set on the server (applies to single player as well), we should select the correct loaded settings
        [HarmonyPatch(typeof(ZNet), "SetServer")]
        private class ZNetSetServerPatch
        {
            private static void Prefix(World world)
            {
                Debug.Log($"[BetterContinents] Selected world {world.m_name}, applying settings");
                if (!AllSettings.TryGetValue(world.m_uid, out Settings))
                {
                    Debug.Log($"[BetterContinents] Couldn't find loaded settings for world {world.m_name}, mod is disabled for this World");
                    Settings = BetterContinentsSettings.Disabled(world.m_uid);
                    AllSettings.Add(world.m_uid, Settings);
                }
                Settings.Dump();
            }
        }
        
        // Register our RPC for receiving settings on clients
        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        private class ZNetOnNewConnectionPatch
        {
            private static void Prefix(ZNetPeer peer)
            {
                Debug.Log($"[BetterContinents] Registering BetterContinentsConfig RPC");
                peer.m_rpc.Register<ZPackage>("BetterContinentsConfig", (ZRpc rpc, ZPackage pkg) =>
                {
                    Debug.Log($"[BetterContinents] Received BetterContinents Config from server");
                    var newSettings = BetterContinentsSettings.Load(pkg); 
                    AllSettings[newSettings.WorldUId] = newSettings;
                    Settings = newSettings;
                    Settings.Dump();
                });
            }
        }
        
        // Send our clients the settings for the currently loaded world. We do this before
        // the body of the SendPeerInfo function, so as to ensure the data arrives before we might need it.
        [HarmonyPatch(typeof(ZNet), "SendPeerInfo")]
        private class ZNetSendPeerInfoPatch
        {
            private static void Prefix(ZNet __instance, ZRpc rpc)
            {
                if (__instance.IsServer())
                {
                    Debug.Log($"[BetterContinents] Sending BetterContinents Config to clients");
                    var zpackage = new ZPackage();
                    Settings.Serialize(zpackage);
                    rpc.Invoke("BetterContinentsConfig", zpackage);
                }
            }
        }
        
        private static float FeatureScaleCurve(float x) => ScaleRange(Gamma(x, 0.726965071031f), 0.2f, 3f);
        private static float Gamma(float x, float h) => Mathf.Pow(x, Mathf.Pow(1 - h * 0.5f + 0.25f, 6f));
        private static float ScaleRange(float g, float n, float m) => n + (m - n) * (1 - g); 

        // [HarmonyPatch(typeof(ZDOMan), "PrepareSave")]
        // private class ZDOManPrepareSavePatch
        // {
        //     [HarmonyPrefix]
        //     private static void Prefix(ZDOMan __instance, int ___m_nextUid, Dictionary<ZDOID, ZDO> ___m_objectsByID)
        //     {
        //         // The only way to get here without our ZDO existing is if this was a new world created with this mod loaded (if the mod wasn't loaded then this code wouldn't be called!)
        //         var ourZDO = __instance.GetZDO(id);
        //         if (ourZDO == null)
        //         {
        //             Debug.Log($"[BetterContinents] Recording BetterContinents ");
        //             ourZDO = __instance.CreateNewZDO(id, Vector3.zero);
        //             ourZDO.Set("BetterContinentsEnabled", Enabled);
        //             EnabledForThisWorld = Enabled;
        //             Debug.Log($"[BetterContinents] New World Loaded, EnabledForThisWorld {EnabledForThisWorld}");
        //         }
        //     }
        // }

        // [HarmonyPatch(typeof(ZDOMan), "Load")]
        // private class ZDOManLoadPatch
        // {
        //     [HarmonyPostfix]
        //     private static void Postfix(ZDOMan __instance, int ___m_nextUid, Dictionary<ZDOID, ZDO> ___m_objectsByID)
        //     {
        //         Debug.Log($"[BetterContinents] World Loaded {___m_nextUid} / {___m_objectsByID.Count}");
        //         var ourZDO = __instance.GetZDO(id);
        //         if (ourZDO != null)
        //         {
        //             EnabledForThisWorld = ourZDO.GetBool("BetterContinentsEnabled");
        //             Debug.Log($"[BetterContinents] World Loaded, EnabledForThisWorld {EnabledForThisWorld}");
        //         }
        //         else
        //         {
        //             Debug.Log($"[BetterContinents] Legacy World Loaded, disabling BetterContinents");
        //             EnabledForThisWorld = false;
        //         }
        //     }
        // }

        [HarmonyPatch(typeof(WorldGenerator), "GetBaseHeight")]
        private class WorldGeneratorGetBaseHeightPatch
        {
            // XY ranges -10180.57, -10187.99 ... 10136.63, 10162.5
            [HarmonyPrefix]
            private static bool Prefix(ref float wx, ref float wy, bool menuTerrain, ref float __result, float ___m_offset0, float ___m_offset1, float ___m_minMountainDistance)
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

                wx *= Settings.GlobalScale;
                wy *= Settings.GlobalScale;

                const float Size = 10500f;
                float WarpScale = 0.001f * Settings.RidgeScale;

                float warpX = (Mathf.PerlinNoise(wx * WarpScale, wy * WarpScale) - 0.5f) * Size;
                float warpY = (Mathf.PerlinNoise(wx * WarpScale + 2f, wy * WarpScale + 3f) - 0.5f) * Size;

                wx += 100000f + ___m_offset0;
                wy += 100000f + ___m_offset1;

                float bigFeatureHeight = Mathf.PerlinNoise(wx * 0.002f * 0.5f, wy * 0.002f * 0.5f) * Mathf.PerlinNoise(wx * 0.003f * 0.5f, wy * 0.003f * 0.5f) * 1f;
                float ridgeHeight = (Mathf.PerlinNoise(warpX * 0.002f * 0.5f, warpY * 0.002f * 0.5f) * Mathf.PerlinNoise(warpX * 0.003f * 0.5f, warpY * 0.003f * 0.5f)) * Settings.MaxRidgeHeight;
                //float scaling = Mathf.Lerp(0.5f, 2f, Mathf.PerlinNoise(wx * 0.002f * 0.5f, wy * 0.002f * 0.5f));

                // https://www.desmos.com/calculator/uq8wmu6dy7
                float SigmoidActivation(float x, float a, float b) => 1 / (1 + Mathf.Exp(a + b * x));
                float lerp = Mathf.Clamp(SigmoidActivation(Mathf.PerlinNoise(wx * 0.005f - 10000, wy * 0.005f - 5000) - Settings.RidgeBlendSigmoidXOffset, 0, Settings.RidgeBlendSigmoidB), 0, 1);
                
                //Mathf.Pow(Mathf.PerlinNoise(wx * 0.005f - 10000, wy * 0.005f - 5000), 2f);

                float finalHeight = 0f;

                float bigFeature = //bigFeatureHeight + ridgeHeight * lerp;
                    Mathf.Clamp(Mathf.Lerp(bigFeatureHeight, ridgeHeight, lerp), 0, 1);

                // const float MountainsAmount = 0.5f; // 0 - 1
                const float SeaLevel = 0.05f;
                float ApplyMountains(float x, float n) => x * (1 - Mathf.Pow(1 - x, 1.2f + n * 0.8f)) + x * (1 - x);

                finalHeight += ApplyMountains(bigFeature - SeaLevel, Settings.MountainsAmount) + SeaLevel;

                finalHeight += Mathf.PerlinNoise(wx * 0.002f * 1f, wy * 0.002f * 1f) * Mathf.PerlinNoise(wx * 0.003f * 1f, wy * 0.003f * 1f) * finalHeight * 0.9f;

                finalHeight += Mathf.PerlinNoise(wx * 0.005f * 1f, wy * 0.005f * 1f) * Mathf.PerlinNoise(wx * 0.01f * 1f, wy * 0.01f * 1f) * 0.5f * finalHeight;

                finalHeight -= 0.07f;

                finalHeight += Settings.SeaLevelAdjustment;

                float v = Mathf.Abs(Mathf.PerlinNoise(wx * 0.002f * 0.25f + 0.123f, wy * 0.002f * 0.25f + 0.15123f) - Mathf.PerlinNoise(wx * 0.002f * 0.25f + 0.321f, wy * 0.002f * 0.25f + 0.231f));
                finalHeight *= 1f - (1f - Utils.LerpStep(0.02f, 0.12f, v)) * Utils.SmoothStep(744f, 1000f, distance);
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
            
                    
            //[HarmonyPatch(typeof(WorldGenerator), "GetBiomeHeight")]
            //private class GetBiomeHeightPatch
            //{
            //    [HarmonyPrefix]
            //    private static void Prefix(ref float wx, ref float wy)
            //    {
            //        wx *= 0.25f;
            //        wy *= 0.25f;
            //    }
            //}

            //[HarmonyPatch(typeof(WorldGenerator), "GetBaseHeight")]
            //private class GetBaseHeightPatch
            //{
            //    // XY ranges -10180.57, -10187.99 ... 10136.63, 10162.5
            //    [HarmonyPrefix]
            //    private static void Prefix(ref float wx, ref float wy)
            //    {
            //        //wx *= 0.5f;
            //        //wy *= 0.5f;
            //    }

            //    //private static float Gamma(float x, float h) => Mathf.Pow(x, Mathf.Pow(1 - h * 0.5f + 0.25f, 6f));
            //    //private static float Slope(float x, float h) => Mathf.Pow(x, Mathf.Pow(1 - h * 0.5f + 0.25f, 6f));

            //    //private const float WaterThreshold = 0.05f;
            //    //private const float MinHeight = -0.1333731f;
            //    //private const float MaxHeight = 0.8320156f;
            //    //// Height ranges -0.1333731 ... 0.8320156
            //    //[HarmonyPostfix]
            //    //private static void Postfix(ref float __result)
            //    //{
            //    //    if (__result > WaterThreshold)
            //    //    {
            //    //        __result = Mathf.Pow((__result - WaterThreshold) / MaxHeight, 2) * MaxHeight + WaterThreshold;
            //    //    }
            //    //}
            //}
        }
    }
}
