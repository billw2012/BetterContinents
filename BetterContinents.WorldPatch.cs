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
                Directory.CreateDirectory(Path.GetDirectoryName(ourMetaPath));
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
    }
}