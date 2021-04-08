using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace BetterContinents
{
    [BepInPlugin("BetterContinents", ModInfo.Name, ModInfo.Version)]
    public partial class BetterContinents : BaseUnityPlugin
    {
        // See the Awake function for the config descriptions
        public static ConfigEntry<int> NexusID;
        public static ConfigEntry<bool> ConfigEnabled;
        
        public static ConfigEntry<float> ConfigContinentSize;
        public static ConfigEntry<float> ConfigMountainsAmount;
        public static ConfigEntry<float> ConfigSeaLevelAdjustment;
        public static ConfigEntry<bool> ConfigOceanChannelsEnabled;
        public static ConfigEntry<bool> ConfigRiversEnabled;
        public static ConfigEntry<bool> ConfigMapEdgeDropoff;
        public static ConfigEntry<bool> ConfigMountainsAllowedAtCenter;

        public static ConfigEntry<string> ConfigMapSourceDir;
        
        public static ConfigEntry<string> ConfigHeightmapFile;
        public static ConfigEntry<float> ConfigHeightmapAmount;
        public static ConfigEntry<float> ConfigHeightmapBlend;
        public static ConfigEntry<float> ConfigHeightmapAdd;
        public static ConfigEntry<float> ConfigHeightmapMask;
        public static ConfigEntry<bool> ConfigHeightmapOverrideAll;
        
        public static ConfigEntry<string> ConfigBiomemapFile;

        public static ConfigEntry<string> ConfigSpawnmapFile;
        
        public static ConfigEntry<string> ConfigRoughmapFile;
        public static ConfigEntry<float> ConfigRoughmapBlend;
        
        public static ConfigEntry<bool> ConfigUseRoughInvertedForFlat;
        public static ConfigEntry<string> ConfigFlatmapFile;
        public static ConfigEntry<float> ConfigFlatmapBlend;
        
        public static ConfigEntry<float> ConfigForestScale;
        public static ConfigEntry<float> ConfigForestAmount;
        public static ConfigEntry<bool> ConfigForestFactorOverrideAllTrees;
        public static ConfigEntry<string> ConfigForestmapFile;
        public static ConfigEntry<float> ConfigForestmapMultiply;
        public static ConfigEntry<float> ConfigForestmapAdd;
        
        public static ConfigEntry<float> ConfigMaxRidgeHeight;
        public static ConfigEntry<float> ConfigRidgeSize;
        public static ConfigEntry<float> ConfigRidgeBlend;
        public static ConfigEntry<float> ConfigRidgeAmount;
        
        public static ConfigEntry<bool> ConfigOverrideStartPosition;
        public static ConfigEntry<float> ConfigStartPositionX;
        public static ConfigEntry<float> ConfigStartPositionY;
        
        public static ConfigEntry<bool> ConfigDebugModeEnabled;
        public static ConfigEntry<bool> ConfigDebugSkipDefaultLocationPlacement;

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

        public static bool AllowDebugActions => ZNet.instance && ZNet.instance.IsServer() &&
                                                Settings.EnabledForThisWorld && ConfigDebugModeEnabled.Value;

        public static BetterContinentsSettings Settings;

        public static BetterContinents instance;
        
        private void Awake()
        {
            instance = this;
            
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
                    .Value("Heightmap Mask").Description("How strongly to apply the heightmap as a mask on normal height generation (i.e. it limits maximum height to the height of the mask)").Default(0f).Range(0f, 1f).Bind(out ConfigHeightmapMask)
                    .Value("Heightmap Override All").Description("All other aspects of the height calculation will be disabled, so the world will perfectly conform to your heightmap").Default(true).Bind(out ConfigHeightmapOverrideAll)
                .Group("BetterContinents.Roughmap")
                    .Value("Roughmap File").Description("Path to a roughmap file to use. See the description on Nexusmods.com for the specifications (it will fail if they are not met)").Bind(out ConfigRoughmapFile)
                    .Value("Roughmap Blend").Description("How strongly to apply the roughmap file").Default(1f).Range(0f, 1f).Bind(out ConfigRoughmapBlend)
                .Group("BetterContinents.Flatmap")
                    .Value("Use Roughmap For Flatmap").Description("Use the flatmap as the rough map, but inverted (black rough map results in totally flat terrain)").Default(true).Bind(out ConfigUseRoughInvertedForFlat)
                    .Value("Flatmap File").Description("Path to a flatmap file to use. See the description on Nexusmods.com for the specifications (it will fail if they are not met)").Bind(out ConfigFlatmapFile)
                    .Value("Flatmap Blend").Description("How strongly to apply the flatmap file (also applies when using Use Roughmap For Flatmap)").Default(1f).Range(0f, 1f).Bind(out ConfigFlatmapBlend)
                .Group("BetterContinents.Biomemap")
                    .Value("Biomemap File").Description("Path to a biomemap file to use. See the description on Nexusmods.com for the specifications (it will fail if they are not met)").Bind(out ConfigBiomemapFile)
                .Group("BetterContinents.Forest")
                    .Value("Forest Scale").Description("Scales forested/cleared area size").Default(0.5f).Range(0f, 1f).Bind(out ConfigForestScale)
                    .Value("Forest Amount").Description("Adjusts how much forest there is, relative to clearings").Default(0.5f).Range(0f, 1f).Bind(out ConfigForestAmount)
                    .Value("Forest Factor Overrides All Trees").Description("Trees in all biomes will be affected by forest factor (both procedural and from forestmap)").Default(false).Bind(out ConfigForestFactorOverrideAllTrees)
                    .Value("Forestmap File").Description("Path to a forestmap file to use. See the description on Nexusmods.com for the specifications (it will fail if they are not met)").Bind(out ConfigForestmapFile)
                    .Value("Forestmap Multiply").Description("How strongly to scale the vanilla forest factor by the forestmap").Default(1f).Range(0f, 1f).Bind(out ConfigForestmapMultiply)
                    .Value("Forestmap Add").Description("How strongly to add the forestmap directly to the vanilla forest factor").Default(1f).Range(0f, 1f).Bind(out ConfigForestmapAdd)
                .Group("BetterContinents.Spawnmap")
                    .Value("Spawnmap File").Description("Path to a spawnmap file to use. See the description on Nexusmods.com for the specifications (it will fail if they are not met)").Bind(out ConfigSpawnmapFile)
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
                .Group("BetterContinents.Misc")
                    .Value("NexusID").Default(446).Description("For Nexus Update Check compatibility").Bind(out NexusID);
                ;

            new Harmony("BetterContinents.Harmony").PatchAll();
            Log("Awake");

            UI.Init();
        }

        private void OnGUI()
        {
            UI.OnGUI();
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
                    EnvMan.instance.m_debugEnv = "clear";
                    EnvMan.instance.m_debugTimeOfDay = true;
                    EnvMan.instance.m_debugTime = 0.5f;
                    Player.m_localPlayer.SetGodMode(true);
                    GameCamera.instance.m_minWaterDistance = -1000f;
                }
            }
        }
        
        // Debug mode helpers
        [HarmonyPatch(typeof(Character))]
        private class CharacterPatch
        {
            private delegate bool TakeInputDelegate(Character instance);
            private static readonly TakeInputDelegate TakeInput = DebugUtils.GetDelegate<TakeInputDelegate>(typeof(Character), "TakeInput");
                
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
            private static readonly ScreenToWorldPointDelegate ScreenToWorldPoint = DebugUtils.GetDelegate<ScreenToWorldPointDelegate>(typeof(Minimap), "ScreenToWorldPoint");
            
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

            [HarmonyPrefix, HarmonyPatch(nameof(Minimap.GetMaskColor))]
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

            [HarmonyPrefix, HarmonyPatch(nameof(Minimap.GenerateWorldMap))]
            private static bool GenerateWorldMapPrefix(Minimap __instance)
            {
                GenerateWorldMapMT(__instance);
                return false;
            }

            private static void GenerateWorldMapMT(Minimap __instance)
            {
                int halfSize = __instance.m_textureSize / 2;
                float halfSizeF = __instance.m_pixelSize / 2f;
                var mapPixels = new Color32[__instance.m_textureSize * __instance.m_textureSize];
                var forestPixels = new Color32[__instance.m_textureSize * __instance.m_textureSize];
                var heightPixels = new Color[__instance.m_textureSize * __instance.m_textureSize];
                GameUtils.SimpleParallelFor(4, 0, __instance.m_textureSize, i =>
                {
                    for (int j = 0; j < __instance.m_textureSize; j++)
                    {
                        float wx = (float) (j - halfSize) * __instance.m_pixelSize + halfSizeF;
                        float wy = (float) (i - halfSize) * __instance.m_pixelSize + halfSizeF;
                        var biome = WorldGenerator.instance.GetBiome(wx, wy);
                        float biomeHeight = WorldGenerator.instance.GetBiomeHeight(biome, wx, wy);
                        mapPixels[i * __instance.m_textureSize + j] = __instance.GetPixelColor(biome);
                        forestPixels[i * __instance.m_textureSize + j] = __instance.GetMaskColor(wx, wy, biomeHeight, biome);
                        heightPixels[i * __instance.m_textureSize + j] = new Color(biomeHeight, 0f, 0f);
                    }
                });
                
                __instance.m_forestMaskTexture.SetPixels32(forestPixels);
                __instance.m_forestMaskTexture.Apply();
                __instance.m_mapTexture.SetPixels32(mapPixels);
                __instance.m_mapTexture.Apply();
                __instance.m_heightTexture.SetPixels(heightPixels);
                __instance.m_heightTexture.Apply();
            }
        }
        
        // Show the connection error message
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
