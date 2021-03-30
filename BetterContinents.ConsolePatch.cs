using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace BetterContinents
{
    public partial class BetterContinents
    {
        // Debug mode helpers
        [HarmonyPatch(typeof(Console))]
        private class ConsolePatch
        {
            private static Texture CloudTexture;
            private static Texture TransparentTexture;

            [HarmonyPrefix, HarmonyPatch("InputText")]
            private static void InputTextPrefix(Console __instance)
            {
                if (AllowDebugActions)
                {
                    string text = __instance.m_input.text.Trim();
                    if (text.Trim() == "bc" || text.Trim() == "bc help")
                    {
                        // __instance.Print("Better Continents: bc reload (hm/bm/sm) - reload a specific image from the source file, or all of them");
                        __instance.Print("Better Continents: bc dump - dump all location instance counts to the log/console");
                        __instance.Print("Better Continents: bc show (filter) - pin locations matching optional filter oo the map");
                        __instance.Print("Better Continents: bc hide (filter) - odd locations matching optional filter oo the map");
                        __instance.Print("Better Continents: bc bosses - toggle pins for bosses");
                        __instance.Print("Better Continents: bc clouds - toggle the map clouds");
                    }
                    if (text == "bc reload hm" || text == "bc reload all")
                    {
                        Settings.ReloadHeightmap();
                    }

                    Dictionary<Vector2i, ZoneSystem.LocationInstance> GetLocationInstances() =>
                        (Dictionary<Vector2i, ZoneSystem.LocationInstance>)
                        AccessTools.Field(typeof(ZoneSystem), "m_locationInstances").GetValue(ZoneSystem.instance);

                    if (text.StartsWith("bc dump"))
                    {
                        var locationInstances = GetLocationInstances();
                        
                        foreach (var lg in locationInstances.Values.GroupBy(l => l.m_location.m_prefabName))
                        {
                            Log($"Placed {lg.Count()} {lg.Key} locations");
                        }
                    }
                    
                    if (text == "bc show" || text.StartsWith("bc show ") || text == "bc bosses")
                    {
                        var typeFilters = text == "bc show" 
                            ? null
                            : text == "bc bosses"
                                ? new List<string>{"StartTemple", "Eikthymir", "GDKing", "GoblinKing", "Bonemass", "Dragonqueen", "Vendor"}
                                : text
                                    .Replace("bc show ", "")
                                    .Split(new []{' '}, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(f => f.Trim())
                                    .ToList();
                        
                        var locationInstances = GetLocationInstances(); 
                        foreach (var lg in locationInstances.Values.GroupBy(l => l.m_location.m_prefabName))
                        {
                            if(typeFilters == null || typeFilters.Any(f => lg.Key.ToLower().StartsWith(f)))
                            {
                                Log($"Marking {lg.Count()} {lg.Key} locations on map");
                                int idx = 0;
                                foreach (var li in lg)
                                {
                                    Minimap.instance.AddPin(li.m_position, Minimap.PinType.Icon3,
                                        $"{li.m_location.m_prefabName} {idx++}", false, false);
                                }
                            }
                        }
                    }
                    
                    if (text ==  "bc hide" || text.StartsWith("bc hide "))
                    {
                        var typeFilters = text == "bc hide" 
                            ? null
                            : text
                                .Replace("bc hide ", "")
                                .Split(new []{' '}, StringSplitOptions.RemoveEmptyEntries)
                                .Select(f => f.Trim())
                                .ToList();

                        var locationInstances = GetLocationInstances();

                        var pins = (List<Minimap.PinData>)AccessTools.Field(typeof(Minimap), "m_pins").GetValue(Minimap.instance);
                        foreach (var lg in locationInstances.Values.GroupBy(l => l.m_location.m_prefabName))
                        {
                            if(typeFilters == null || typeFilters.Any(f => lg.Key.ToLower().StartsWith(f)))
                            {
                                Log($"Hiding {lg.Count()} {lg.Key} locations from the map");
                                int idx = 0;
                                foreach (var li in lg)
                                {
                                    var name = $"{li.m_location.m_prefabName} {idx++}";
                                    var pin = pins.FirstOrDefault(p => p.m_name == name && p.m_pos == li.m_position);
                                    if (pin != null)
                                    {
                                        Minimap.instance.RemovePin(pins.FirstOrDefault());
                                    }
                                }
                            }
                        }
                    }

                    if (text == "bc clouds")
                    {
                        var mat = Minimap.instance.m_mapImageLarge.material;
                        if (mat.GetTexture("_CloudTex") == TransparentTexture)
                        {
                            mat.SetTexture("_CloudTex", CloudTexture);
                        }
                        else
                        {
                            CloudTexture = mat.GetTexture("_CloudTex");
                            if (TransparentTexture == null)
                            {
                                TransparentTexture = CreateFillTexture(new Color32(0, 0, 0, 0)); 
                            }
                            mat.SetTexture("_CloudTex", TransparentTexture);
                        }
                    }
                }
            }
        }
    }
}