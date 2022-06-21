using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BetterContinents
{
    public class Presets
    {
        private static readonly string PresetsDir = Path.Combine(Utils.GetSaveDataPath(FileHelpers.FileSource.Local), "BetterContinents", "presets");
        private static AssetBundle assetBundle;

        private static bool DisabledPreset => BetterContinents.ConfigSelectedPreset.Value == Disabled;
        private static bool ConfigPreset => BetterContinents.ConfigSelectedPreset.Value == FromConfig;

        private List<string> presets;
        private const string Disabled = "Disabled";
        private const string FromConfig = "From Config";

        private Dropdown dropdown;
        private GameObject previewPanel;
        private RawImage previewImage;

        private Texture2D logoIcon;
        private Texture2D settingsIcon;
                
        public Presets() { Refresh(); }

        public void InitUI(FejdStartup __instance)
        {
            var panel = (RectTransform)__instance.m_newWorldSeed.transform.parent;

            if (assetBundle == null)
            {
                assetBundle = GameUtils.GetAssetBundleFromResources("bcassets");
                BetterContinents.Log("Loaded asset bundle");
            }
            
            logoIcon = assetBundle.LoadAsset<Texture2D>("Assets/logo256.png");
            settingsIcon = assetBundle.LoadAsset<Texture2D>("Assets/settings256.png");

            var prefab = assetBundle.LoadAsset<GameObject>("Assets/BCPresetPrefab.prefab");

            var item = Object.Instantiate(prefab, panel);
            item.FixReferences(typeof(Image));

            previewPanel = item.transform.Find("MapPreview").gameObject;
            previewImage = previewPanel.GetComponentInChildren<RawImage>();
            previewPanel.SetActive(false);
            
            dropdown = item.GetComponentInChildren<Dropdown>();
            dropdown.onValueChanged.AddListener(idx =>
            {
                if (idx >= 0 && idx < presets.Count)
                {
                    BetterContinents.ConfigSelectedPreset.Value = presets[idx];
                    UpdatePreview();
                }
            });
            Refresh();
            UpdatePreview();
            
            BetterContinents.Log("Setup UI");
        }

        private void UpdatePreview()
        {
            if (previewPanel != null)
            {
                if (DisabledPreset)
                {
                    previewPanel.SetActive(false);
                }
                else if(ConfigPreset)
                {
                    previewPanel.SetActive(true);
                    previewImage.texture = settingsIcon;
                }
                else
                {
                    previewPanel.SetActive(true);
                    string configIconPath =
                        Path.Combine(Path.GetDirectoryName(BetterContinents.ConfigSelectedPreset.Value),
                            BetterContinents.ConfigSelectedPreset.Value.UpTo(".") + ".png");
                    if (File.Exists(configIconPath))
                    {
                        var icon = new Texture2D(2, 2);
                        icon.LoadImage(File.ReadAllBytes(configIconPath));
                        previewImage.texture = icon;
                    }
                    else
                    {
                        previewImage.texture = logoIcon;
                    }
                }
            }
        }
        
        private void Refresh()
        {
            string NameFromPath(string path) => Path.GetFileName(path).UpTo(".").AddSpacesToWords();
            
            presets = Directory
                    .GetFiles(PresetsDir, "*.BetterContinents")
                    .ToList()
                ;
            presets.Insert(0, Disabled);
            presets.Add(FromConfig);
                    
            if (dropdown != null)
            {
                dropdown.ClearOptions();
                dropdown.AddOptions(presets.Select(NameFromPath).ToList());
                int idx = presets.FindIndex(p => string.Equals(p, BetterContinents.ConfigSelectedPreset.Value, StringComparison.CurrentCultureIgnoreCase));
                if (idx != -1)
                {
                    dropdown.SetValueWithoutNotify(idx);
                }
            }

            UpdatePreview();
        }
        
        public static BetterContinents.BetterContinentsSettings LoadActivePreset(long worldId)
        {
            if (DisabledPreset)
            {
                return BetterContinents.BetterContinentsSettings.Disabled(worldId);
            }

            if (ConfigPreset)
            {
                return BetterContinents.BetterContinentsSettings.Create(worldId);
            }

            if (!File.Exists(BetterContinents.ConfigSelectedPreset.Value))
            {
                BetterContinents.LogError($"Selected preset path {BetterContinents.ConfigSelectedPreset.Value} doesn't exist, BC is disabled for this world!");
                return BetterContinents.BetterContinentsSettings.Disabled(worldId);
            }
                    
            try
            { 
                var settings = BetterContinents.BetterContinentsSettings.Load(BetterContinents.ConfigSelectedPreset.Value);
                settings.WorldUId = worldId;
                return settings;
            }
            catch(Exception ex)
            {
                BetterContinents.Log((string) $"Couldn't load preset {BetterContinents.ConfigSelectedPreset.Value} ({ex.Message}), BC is disabled for this world!");
                return BetterContinents.BetterContinentsSettings.Disabled(worldId);
            }
        }

        public static void Save(BetterContinents.BetterContinentsSettings settings, string name)
        {
            var path = Path.Combine(PresetsDir, name + ".BetterContinents");
            if (File.Exists(path))
                File.Move(path, path + ".old-" + DateTime.Now.ToString("yyyy-dd-M-HH-mm-ss"));
            settings.Save(path);
            var pngPath = Path.Combine(PresetsDir, name + ".png");
            if (File.Exists(pngPath))
                File.Move(pngPath, pngPath + ".old-" + DateTime.Now.ToString("yyyy-dd-M-HH-mm-ss"));
            GameUtils.SaveMinimap(pngPath, 256);
        }
    }
}