using System;
using System.ComponentModel;
using System.IO;
using HarmonyLib;
using UnityEngine.UI;

namespace BetterContinents
{
    public partial class BetterContinents
    {
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

            private static Presets presets = new Presets();
            
            [HarmonyPostfix, HarmonyPatch("Start")]
            private static void StartPostfix(FejdStartup __instance)
            {
                //if (ZNet.instance.IsDedicated())
                //    return;

                Log("Start postfix");
                presets.InitUI(__instance);
                
                // Code from before I used an assetbundle instead...
                // var newLabel = Instantiate(panel.Find("seed").gameObject, panel);
                // ((RectTransform) newLabel.transform).anchoredPosition = new Vector2(-194.6217f, -160.95f);
                // newLabel.GetComponent<Text>().text = "Preset";
                //
                // var templateInputField = panel.GetComponentInChildren<InputField>();
                // var dropdownObject = DefaultControls.CreateDropdown(new DefaultControls.Resources
                // {
                //     inputField = templateInputField.image.sprite,
                //     background = templateInputField.image.sprite,
                //     dropdown = templateInputField.image.sprite,
                //     standard = templateInputField.image.sprite,
                // });
                //
                // var dropdown = dropdownObject.GetComponent<Dropdown>();
                // dropdown.captionText.font = templateInputField.textComponent.font;
                // dropdown.captionText.fontSize = templateInputField.textComponent.fontSize;
                // dropdown.itemText.font = templateInputField.textComponent.font;
                // dropdown.itemText.fontSize = templateInputField.textComponent.fontSize;
                // dropdown.
                //
                // var dropdownTransform = (RectTransform) dropdownObject.transform;
                // dropdownTransform.SetParent(panel);
                // dropdownTransform.anchoredPosition = new Vector2(51.447f, -33f);
                // dropdownTransform.sizeDelta = new Vector2(364.075f, 30f);
                //
            }

            // [HarmonyPrefix, HarmonyPatch("MoveWorld")]
            // private static void MoveWorldPrefix(FejdStartup __instance, World ___m_world, BackgroundWorker ___m_moveFileWorker)
            // {
            //     // Same early out conditions as MoveWorld
            //     if (___m_world == null || ___m_moveFileWorker != null)
            //     {
            //         return;
            //     }
            //     
            //     // Move the .BetterContinents file if it exists. Not doing this async, unlike the rest of the copy, and it
            //     // might be by far the slowest bit.
            //     var moveTarget = __instance.GetMoveTarget(___m_world.m_fileSource);
            //     string sourcePath = ___m_world.GetMetaPath() + BetterContinents.ConfigFileExtension;
            //     string targetPath = ___m_world.GetMetaPath(moveTarget) + BetterContinents.ConfigFileExtension;
            //     if (moveTarget == FileHelpers.FileSource.Cloud)
            //     {
            //         if (File.Exists(sourcePath))
            //         {
            //             Log( $"Copying BetterContinents config file from {sourcePath} to {targetPath}");
            //             FileHelpers.FileCopyIntoCloud(sourcePath, targetPath);
            //         }
            //     }
            //     else if (___m_world.m_fileSource == FileHelpers.FileSource.Cloud)
            //     {
            //         if (FileHelpers.FileExistsCloud(sourcePath))
            //         {
            //             Log( $"Copying BetterContinents config file from {sourcePath} to {targetPath}");
            //             FileHelpers.FileCopyOutFromCloud(sourcePath, targetPath, true);
            //         }
            //     }
            //     else
            //     {
            //         string directoryName = Path.GetDirectoryName(targetPath);
            //         if (!Directory.Exists(directoryName))
            //         {
            //             Directory.CreateDirectory(directoryName);
            //         }
            //         if (File.Exists(sourcePath))
            //         {
            //             Log( $"Copying BetterContinents config file from {sourcePath} to {targetPath}");
            //             File.Copy(sourcePath, targetPath);
            //         }
            //     }
            //     if (___m_world.m_fileSource != FileHelpers.FileSource.Cloud)
            //     {
            //         if (File.Exists(sourcePath))
            //         {
            //             Log( $"Moving old BetterContinents config file from {sourcePath} to backup");
            //             FileHelpers.MoveToBackup(sourcePath, DateTime.Now);
            //         }
            //     }
            // }

            [HarmonyPrefix, HarmonyPatch("OnNewWorldDone")]
            private static void OnNewWorldDonePrefix()
            {
                // Indicator to SaveWorldMetaDataPostfix that it should save a new BC config file using the 
                // selected preset, rather than saving the active worlds settings.
                WorldPatch.bWorldBeingCreated = true;
                Log($"[Saving] Setting the bWorldBeingCreated flag");
            }
            
            [HarmonyPostfix, HarmonyPatch("OnNewWorldDone")]
            private static void OnNewWorldDonePostfix()
            {
                // Clear the flag again, ready for normal save operations 
                WorldPatch.bWorldBeingCreated = false;
                Log($"[Saving] Clearing the bWorldBeingCreated flag again");
            }
        }
    }
}