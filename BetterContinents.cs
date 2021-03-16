using System;
using System.Collections.Generic;
using System.IO;
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
        
        private static ConfigEntry<float> ConfigContinentSize;
        private static ConfigEntry<float> ConfigMountainsAmount;
        private static ConfigEntry<float> ConfigSeaLevelAdjustment;
        private static ConfigEntry<bool> ConfigOceanChannelsEnabled;
        
        private static ConfigEntry<float> ConfigMaxRidgeHeight;
        private static ConfigEntry<float> ConfigRidgeSize;
        private static ConfigEntry<float> ConfigRidgeBlend;
        private static ConfigEntry<float> ConfigRidgeAmount;

        private static void Log(string msg) => Debug.Log($"[BetterContinents] {msg}");
        private static void LogError(string msg) => Debug.LogError($"[BetterContinents] {msg}");
        
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

            public string HeightmapSourceFilename;
            public byte[] HeightmapSource;
            
            public float HeightmapAmount;
            public float HeightmapBlend;
            public float HeightmapAdd;
            
            public bool OceanChannelsEnabled;

            private float[] Heightmap;
            private int HeightmapSize;
            
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
                    SeaLevelAdjustment = Mathf.Lerp(-0.05f, 0.05f, ConfigSeaLevelAdjustment.Value);

                    MaxRidgeHeight = ConfigMaxRidgeHeight.Value;
                    RidgeScale = FeatureScaleCurve(ConfigRidgeSize.Value);
                    RidgeBlendSigmoidB = Mathf.Lerp(-30f, -10f, ConfigRidgeBlend.Value);
                    RidgeBlendSigmoidXOffset = Mathf.Lerp(1f, 0.35f, ConfigRidgeAmount.Value);

                    if (!string.IsNullOrEmpty(ConfigHeightmapFile.Value))
                    {
                        HeightmapSourceFilename = ConfigHeightmapFile.Value;
                        HeightmapAmount = ConfigHeightmapAmount.Value;
                        HeightmapBlend = ConfigHeightmapBlend.Value;
                        HeightmapAdd = ConfigHeightmapAdd.Value;

                        if (!LoadHeightmapSourceImage() || !CreateHeightmap())
                        {
                            // Clear the source file name again as it won't work
                            HeightmapSourceFilename = null;
                        }
                    }
                    else
                    {
                        HeightmapSourceFilename = string.Empty;
                        Heightmap = null;
                    }

                    OceanChannelsEnabled = ConfigOceanChannelsEnabled.Value;
                }
            }

            private bool LoadHeightmapSourceImage()
            {
                // Already loaded?
                if (HeightmapSource != null)
                    return true;

                try
                {
                    HeightmapSource = File.ReadAllBytes(HeightmapSourceFilename);
                    return true;
                }
                catch (Exception ex)
                {
                    LogError($"Cannot load texture {HeightmapSourceFilename}: {ex.Message}");
                    return false;
                }
            }
            
            private bool CreateHeightmap()
            {
                // Already loaded?
                if (Heightmap != null)
                    return false;

                var tex = new Texture2D(2, 2);
                try
                {
                    try
                    {
                        tex.LoadImage(HeightmapSource);
                    }
                    catch (Exception ex)
                    {
                        LogError($"Cannot load texture {HeightmapSourceFilename}: {ex.Message}");
                        return false;
                    }

                    if (tex.width != tex.height)
                    {
                        LogError($"Cannot use texture {HeightmapSourceFilename}: its width ({tex.width}) does not match its height ({tex.height})");
                        return false;
                    }

                    bool IsPowerOfTwo(int x) => (x & (x - 1)) == 0;
                    if (!IsPowerOfTwo(tex.width))
                    {
                        LogError($"Cannot use texture {HeightmapSourceFilename}: it is not a power of two size (e.g. 256, 512, 1024, 2048)");
                        return false;
                    }

                    if (tex.width > 4096)
                    {
                        LogError($"Cannot use texture {HeightmapSourceFilename}: it is too big ({tex.width}x{tex.height}), keep the size to less or equal to 4096x4096");
                        return false;
                    }

                    var pixels = tex.GetPixels();
                    Heightmap = new float[pixels.Length];
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        Heightmap[i] = pixels[i].r;
                    }

                    HeightmapSize = (int) Mathf.Sqrt(Heightmap.Length);
                    
                    Log($"Loaded texture {HeightmapSourceFilename} as heightmap, {HeightmapSize}x{HeightmapSize}");
                    
                    return true;
                }
                finally
                {
                    Destroy(tex);
                }
            }
            
            private static float FeatureScaleCurve(float x) => ScaleRange(Gamma(x, 0.726965071031f), 0.2f, 3f);
            private static float Gamma(float x, float h) => Mathf.Pow(x, Mathf.Pow(1 - h * 0.5f + 0.25f, 6f));
            private static float ScaleRange(float g, float n, float m) => n + (m - n) * (1 - g); 

            public void Dump()
            {
                Log($"Version {Version}");
                Log($"WorldUId {WorldUId}");
                
                Log($"EnabledForThisWorld {EnabledForThisWorld}");

                if (EnabledForThisWorld)
                {
                    if (!string.IsNullOrEmpty(HeightmapSourceFilename))
                    {
                        Log($"Heightmap {HeightmapSourceFilename}");
                        Log(
                            $"Heightmap size {HeightmapSize}x{HeightmapSize}, amount {HeightmapAmount}, blend {HeightmapBlend}, add {HeightmapAdd}");
                    }
                    else
                    {
                        Log($"Heightmap disabled");
                    }

                    Log($"GlobalScale {GlobalScale}");
                    Log($"MountainsAmount {MountainsAmount}");
                    Log($"SeaLevelAdjustment {SeaLevelAdjustment}");
                    Log($"OceanChannelsEnabled {OceanChannelsEnabled}");

                    Log($"MaxRidgeHeight {MaxRidgeHeight}");
                    Log($"RidgeScale {RidgeScale}");
                    Log($"RidgeBlendSigmoidB {RidgeBlendSigmoidB}");
                    Log($"RidgeBlendSigmoidXOffset {RidgeBlendSigmoidXOffset}");
                }
            }

            public void Serialize(ZPackage pkg)
            {
                pkg.Write(Version);

                pkg.Write(WorldUId);

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

                    pkg.Write(HeightmapSourceFilename ?? string.Empty);
                    if (!string.IsNullOrEmpty(HeightmapSourceFilename))
                    {
                        pkg.Write(HeightmapSource);
                        pkg.Write(HeightmapAmount);
                        pkg.Write(HeightmapBlend);
                        pkg.Write(HeightmapAdd);
                    }

                    pkg.Write(OceanChannelsEnabled);
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

                    HeightmapSourceFilename = pkg.ReadString();
                    if (!string.IsNullOrEmpty(HeightmapSourceFilename))
                    {
                        HeightmapSource = pkg.ReadByteArray();
                        if (!CreateHeightmap())
                        {
                            HeightmapSourceFilename = null;
                        }
                        HeightmapAmount = pkg.ReadSingle();
                        HeightmapBlend = pkg.ReadSingle();
                        HeightmapAdd = pkg.ReadSingle();
                    }

                    OceanChannelsEnabled = pkg.ReadBool();
                }
            }

            public float ApplyHeightmap(float x, float y, float height)
            {
                if (this.Heightmap == null || (this.HeightmapBlend == 0 && this.HeightmapAdd == 0))
                {
                    return height;
                }

                float xa = x * (this.HeightmapSize - 1);
                float ya = y * (this.HeightmapSize - 1);

                int xi = Mathf.FloorToInt(xa);
                int yi = Mathf.FloorToInt(ya);

                float xd = xa - xi;
                float yd = ya - yi;

                int x0 = Mathf.Clamp(xi, 0, this.HeightmapSize - 1);
                int x1 = Mathf.Clamp(xi + 1, 0, this.HeightmapSize - 1);
                int y0 = Mathf.Clamp(yi, 0, this.HeightmapSize - 1);
                int y1 = Mathf.Clamp(yi + 1, 0, this.HeightmapSize - 1);
                float p00 = this.Heightmap[y0 * this.HeightmapSize + x0];
                float p10 = this.Heightmap[y0 * this.HeightmapSize + x1];
                float p01 = this.Heightmap[y1 * this.HeightmapSize + x0];
                float p11 = this.Heightmap[y1 * this.HeightmapSize + x1];

                float h = Mathf.Lerp(
                    Mathf.Lerp(p00, p10, xd),
                    Mathf.Lerp(p01, p11, xd),
                    yd
                );

                return Mathf.Lerp(height, h * HeightmapAmount, this.HeightmapBlend) + h * this.HeightmapAdd;
            }

            // public void LoadHeightmapSlice(ZPackage pkg)
            // {
            //     int offset = pkg.ReadInt();
            //     byte[] data = pkg.ReadByteArray();
            //     Log($"Loading heightmap slice offset {offset}, size {data.Length}");
            //     Buffer.BlockCopy(data, 0, Heightmap, offset, data.Length);
            // }
        }
        
        private static BetterContinentsSettings Settings;
        
        private void Awake()
        {
            ConfigEnabled = Config.Bind("BetterContinents.Global", "Enabled", true, "Whether this mod is enabled");

            ConfigHeightmapFile = Config.Bind("BetterContinents.Heightmap", "HeightmapFile", "", "Path to a heightmap file to use. It should be a square, sized 128, 256, 512, 1024, 2048 or 4096 pixels. Supported formats: BMP, EXR, GIF, HDR, IFF, JPG, PICT, PNG, PSD, TGA, TIFF.");
            ConfigHeightmapAmount = Config.Bind("BetterContinents.Heightmap", "HeightmapAmount", 1f,
                new ConfigDescription("Multiplier of the height value from the heightmap file", new AcceptableValueRange<float>(0, 1)));
            ConfigHeightmapBlend = Config.Bind("BetterContinents.Heightmap", "HeightmapBlend", 1f,
                new ConfigDescription("How strongly to blend the heightmap file into the final result", new AcceptableValueRange<float>(0, 1)));
            ConfigHeightmapAdd = Config.Bind("BetterContinents.Heightmap", "HeightmapAdd", 0f,
                new ConfigDescription("How strongly to add the heightmap file to the final result (usually you want to blend it instead)", new AcceptableValueRange<float>(-1, 1)));

            ConfigContinentSize = Config.Bind("BetterContinents.Global", "ContinentSize", 0.5f,
                new ConfigDescription("Continent Size", new AcceptableValueRange<float>(0, 1)));
            ConfigMountainsAmount = Config.Bind("BetterContinents.Global", "MountainsAmount", 0.5f,
                new ConfigDescription("Mountains amount", new AcceptableValueRange<float>(0, 1)));
            ConfigSeaLevelAdjustment = Config.Bind("BetterContinents.Global", "SeaLevelAdjustment", 0.5f,
                new ConfigDescription("Modify sea level, which changes the land:sea ratio", new AcceptableValueRange<float>(0, 1)));
            ConfigOceanChannelsEnabled = Config.Bind("BetterContinents.Global", "OceanChannelsEnabled", true, "Whether ocean channels should be enabled or not (useful to disable when using height map for instance)");

            ConfigMaxRidgeHeight = Config.Bind("BetterContinents.Ridges", "MaxRidgeHeight", 0.5f,
                new ConfigDescription("Max height of ridge features", new AcceptableValueRange<float>(0, 1)));
            ConfigRidgeSize = Config.Bind("BetterContinents.Ridges", "RidgeSize", 0.5f,
                new ConfigDescription("Size of ridge features", new AcceptableValueRange<float>(0, 1)));
            ConfigRidgeBlend = Config.Bind("BetterContinents.Ridges", "RidgeBlend", 0.5f,
                new ConfigDescription("Smoothness of ridges blending into base terrain", new AcceptableValueRange<float>(0, 1)));
            ConfigRidgeAmount = Config.Bind("BetterContinents.Ridges", "RidgeAmount", 0.5f,
                new ConfigDescription("How much ridges", new AcceptableValueRange<float>(0, 1)));

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
            // // DOING:
            // // - patch WorldGenerator.Initialize to load the settings or request them from server via RPC, combining it all into one place
            // // - cache world settings locally by id, verify with hash
            // [HarmonyPrefix, HarmonyPatch("Initialize")]
            // private static void InitializePrefix(World world)
            // {
            //     Log($"Loading settings for {world.m_name}");
            //
            //     try
            //     {
            //         using (var binaryReader = new BinaryReader(File.OpenRead(world.GetMetaPath() + ".BetterContinents")))
            //         {
            //             int count = binaryReader.ReadInt32();
            //             var newSettings = BetterContinentsSettings.Load(new ZPackage(binaryReader.ReadBytes(count)));
            //             if (newSettings.WorldUId != world.m_uid)
            //             {
            //                 LogError($"ID in saved settings for {world.m_name} didn't match, mod is disabled for this World");
            //             }
            //             else
            //             {
            //                 AllSettings[newSettings.WorldUId] = Settings = newSettings;
            //             }
            //         }
            //     }
            //     catch
            //     {
            //         LogError($"Loading settings for {world.m_name} failed, mod is disabled for this World");
            //         // We don't need to do anything, we disabled the mod already in SetServer
            //         return;
            //     }
            // }
            
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
                
                const float Size = 10500f;
                // The base map x, y coordinates in 0..1 range
                float mapX = Mathf.Clamp01(wx / (2 * Size) + 0.5f);
                float mapY = Mathf.Clamp01(wy / (2 * Size) + 0.5f);
                
                wx *= Settings.GlobalScale;
                wy *= Settings.GlobalScale;

                float WarpScale = 0.001f * Settings.RidgeScale;

                float warpX = (Mathf.PerlinNoise(wx * WarpScale, wy * WarpScale) - 0.5f) * Size;
                float warpY = (Mathf.PerlinNoise(wx * WarpScale + 2f, wy * WarpScale + 3f) - 0.5f) * Size;

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

            // [HarmonyPrefix, HarmonyPatch("AddRivers")]
            // private static bool AddRiversPrefix(ref float __result, float h)
            // {
            //     if (Settings.OceanChannelsEnabled)
            //     {
            //         // Fall through to normal function
            //         return true;
            //     }
            //     else
            //     {
            //         __result = h;
            //         return false;
            //     }
            // }

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
