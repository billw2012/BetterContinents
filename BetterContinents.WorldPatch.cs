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
            // New saving logic:
            //  FileHelpers.CheckMove() -- checks for source=legacy, changes source directly to cloud or local, moves file passed in to backup
            //  ZNet.SaveWorldThread() -- saves async during gameplay
            //      Called in:
            //          Auto-save
            //          Explicit save
            //          Exit save
            //          ... world must already be created at these points, i.e. NOT called during world creation.
            //      Calls:
            //          FileHelpers.CheckMove()
            //          World.SaveWorldMetaData()
            //      PREFIX to backup the bc file 
            // World.SaveWorldMetaData() saves the world metadata specifically (as opposed to the db)
            //      Called in:
            //          FejdStartup.OnNewWorldDone() -- world creation on client
            //          World.GetCreateWorld() -- world creation on server (only in the world create branch)
            //          World.GetDevWorld() -- same
            //          ZNet.SaveWorldThread() -- during live game
            //      Calls:
            //          FileHelpers.CheckMove()
            // FejdStartup.OnNewWorldDone()
            //      PREFIX to set bWorldBeingCreated flag
            //      POSTFIX to clear bWorldBeingCreated flag

            // An explicit flag that indicates that the next SaveWorldMetaData call relates to a newly created world, 
            // and that we should generate / save a new BC config from the appropriate source (preset / config etc.).
            public static bool bWorldBeingCreated = false;

            // This isn't needed, it is replaced by bWorldBeingCreated
            // [HarmonyPrefix, HarmonyPatch(nameof(World.SaveWorldMetaData))]
            // private static void SaveWorldMetaDataPrefix(World __instance, out bool __state)
            // {
            //     // We need to record whether this is a first time save (during world creation),
            //     // so we know if we need to apply a template config or not.
            //     // However we can't write the config here, because if this is an *upgrade* save where
            //     // the path is changing (e.g. legacy to local or cloud), then GetMetaPath is going
            //     // to point to the old location currently, and we need to save to the new location.
            //     if (__instance.m_fileSource == FileHelpers.FileSource.Legacy)
            //     {
            //         Log($"[Saving][{__instance.m_name}] Updating from legacy save");
            //         string bcConfigFile = __instance.GetMetaPath() + BetterContinents.ConfigFileExtension;
            //         Log($"[Saving][{__instance.m_name}] Backing up {bcConfigFile}");
            //         FileHelpers.MoveToBackup(__instance.GetMetaPath() + BetterContinents.ConfigFileExtension);
            //         // Legacy save by definition already has the metadata file, as we can't create a new legacy save
            //         __state = true;
            //     }
            //     else
            //     {
            //         // Non-legacy save, we check for existence of the bc metadata file in the original save location. 
            //         // Except it won't exist if the save occurred during the gameplay, as SaveWorldThread will have already moved it...
            //         __state = File.Exists(__instance.GetMetaPath());
            //     }
            //     Log($"[Saving][{__instance.m_name}] Meta path = {__instance.GetMetaPath()}, exists = {__state}");
            // }
            
            // When the world metadata is saved we write an extra file next to it for our own config
            // Do this as a Postfix because the targeted directory (and thus the GetMetaPath() result) might change
            // if the world is being "upgraded" to the new save location.
            [HarmonyPostfix, HarmonyPatch(nameof(World.SaveWorldMetaData))]
            private static void SaveWorldMetaDataPostfix(World __instance)
            {
                Log($"[Saving][{__instance.m_name}] Saving settings for {__instance.m_name}");

                BetterContinentsSettings settingsToSave = default;
                
                // This flag is set explicitly in the OnNewWorldDonePrefix function only
                if (bWorldBeingCreated)
                {
                    // World is being created, so bake our settings from the preset
                    Log($"[Saving][{__instance.m_name}] bWorldBeingCreated flag set, first time save of {__instance.m_name}, applying selected preset '{ConfigSelectedPreset.Value}'");
                    settingsToSave = Presets.LoadActivePreset(__instance.m_uid);
                    bWorldBeingCreated = false;
                }
                else
                {
                    Log($"[Saving][{__instance.m_name}] bWorldBeingCreated flag NOT set, saving active world settings");
                    settingsToSave = Settings;
                }
                settingsToSave.Dump();

                // Duplicating the careful behaviour of the metadata save function
                string bcConfigFile = __instance.GetMetaPath() + BetterContinents.ConfigFileExtension;
                string newName = bcConfigFile + ".new";
                string oldName = bcConfigFile + ".old";
                settingsToSave.SaveToSource(newName, __instance.m_fileSource);
                FileHelpers.ReplaceOldFile(bcConfigFile, newName, oldName, __instance.m_fileSource);

                // if (File.Exists(bcConfigFile))
                // {
                //     if (File.Exists(oldName))
                //     {
                //         File.Delete(oldName);
                //     }
                //     File.Move(bcConfigFile, oldName);
                // }
                // File.Move(newName, bcConfigFile);
            }

            [HarmonyPostfix, HarmonyPatch(nameof(World.RemoveWorld))]
            private static void RemoveWorldPostfix(string name)
            {
                try
                {
                    File.Delete(World.GetMetaPath(name) + BetterContinents.ConfigFileExtension);
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