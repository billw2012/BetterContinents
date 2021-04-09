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
        }
    }
}