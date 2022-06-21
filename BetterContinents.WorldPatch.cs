using System.IO;
using HarmonyLib;

namespace BetterContinents
{
    public partial class BetterContinents
    {
        // Saving and removing of worlds
        [HarmonyPatch(typeof(World))]
        private class WorldPatch
        {
            [HarmonyPrefix, HarmonyPatch(nameof(World.SaveWorldMetaData))]
            private static void SaveWorldMetaDataPrefix(World __instance, out bool __state)
            {
                // We need to record whether this is a first time save (during world creation),
                // so we know if we need to apply a template config or not.
                // However we can't write the config here, because if this is an *upgrade* save where
                // the path is changing (e.g. legacy to local or cloud), then GetMetaPath is going
                // to point to the old location currently, and we need to save to the new location.
                __state = File.Exists(__instance.GetMetaPath());
            }
            
            // When the world metadata is saved we write an extra file next to it for our own config
            // Do this as a Postfix because the targeted directory (and thus the GetMetaPath() result) might change
            // if the world is being "upgraded" to the new save location.
            [HarmonyPostfix, HarmonyPatch(nameof(World.SaveWorldMetaData))]
            private static void SaveWorldMetaDataPostfix(World __instance, bool __state)
            {
                Log($"Saving settings for {__instance.m_name}");

                BetterContinentsSettings settingsToSave = default;
                
                // Vanilla metadata is always saved when a world is created for the first time, before it is actually loaded or generated.
                // So if that metadata doesn't exist it means the world is being created now.
                if (!__state)
                {
                    // World is being created, so bake our settings from the preset
                    Log($"First time save of {__instance.m_name}, applying selected preset {ConfigSelectedPreset.Value}");
                    settingsToSave = Presets.LoadActivePreset(__instance.m_uid);
                }
                else
                {
                    settingsToSave = Settings;
                }
                settingsToSave.Dump();

                // Duplicating the careful behaviour of the metadata save function
                string ourMetaPath = __instance.GetMetaPath() + ".BetterContinents";
                string newName = ourMetaPath + ".new";
                string oldName = ourMetaPath + ".old";
                settingsToSave.Save(newName);
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
    }
}