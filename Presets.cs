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
        private static readonly string PresetsDir = Path.Combine(Utils.GetSaveDataPath(), "BetterContinents", "presets");
        private static AssetBundle assetBundle;

        private List<string> presets;
        private const string Disabled = "Disabled";
        private const string FromConfig = "From Config";

        private Dropdown dropdown;
                
        public Presets() { Refresh(); }

        public void InitUI(FejdStartup __instance)
        {
            var panel = (RectTransform)__instance.m_newWorldSeed.transform.parent;

            if (assetBundle == null)
            {
                assetBundle = GameUtils.GetAssetBundleFromResources("bcassets");
            }

            var prefab = assetBundle.LoadAsset<GameObject>("Assets/BCPresetPrefab.prefab");

            var item = Object.Instantiate(prefab, panel);
            item.FixReferences(typeof(Image));

            dropdown = item.GetComponentInChildren<Dropdown>();
            dropdown.onValueChanged.AddListener(idx =>
            {
                if (idx >= 0 && idx < presets.Count)
                {
                    BetterContinents.ConfigSelectedPreset.Value = presets[idx];
                }
            });
            Refresh();
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
        }

        public static BetterContinents.BetterContinentsSettings LoadActivePreset(long worldId)
        {
            if (BetterContinents.ConfigSelectedPreset.Value == Disabled)
            {
                return BetterContinents.BetterContinentsSettings.Disabled(worldId);
            }

            if (BetterContinents.ConfigSelectedPreset.Value == FromConfig)
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
            settings.Save(Path.Combine(PresetsDir, name + ".BetterContinents"));
        }
    }
}