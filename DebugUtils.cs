using System;
using System.Linq;
using UnityEngine;

namespace BetterContinents
{
    public class DebugUtils
    {
        private static Texture CloudTexture;
        private static Texture TransparentTexture;

        public static void RunConsoleCommand(string text)
        {
            if (Subcommand(ref text, "reload"))
            {
                string[] maps = text.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                bool all = maps.Contains("all");
                var HeightAffectingMaps = new[] { "hm", "rm", "fm" };

                bool affectingHeight = maps.Intersect(HeightAffectingMaps).Any(); 
                if (affectingHeight)
                {
                    GameUtils.BeginHeightChanges();
                }

                if (maps.Contains("hm") || all)
                {
                    BetterContinents.Settings.ReloadHeightmap();
                }
                if (maps.Contains("bm") || all)
                {
                    BetterContinents.Settings.ReloadBiomemap();
                }
                if (maps.Contains("sm") || all)
                {
                    BetterContinents.Settings.ReloadSpawnmap();
                }
                if (maps.Contains("rm") || all)
                {
                    BetterContinents.Settings.ReloadRoughmap();
                }
                if (maps.Contains("fm") || all)
                {
                    BetterContinents.Settings.ReloadFlatmap();
                }
                if (maps.Contains("fom") || all)
                {
                    BetterContinents.Settings.ReloadForestmap();
                }
                
                if (affectingHeight)
                {
                    GameUtils.EndHeightChanges();
                }
            }
            else if (text == "locs")
            {
                var locationInstances = GameUtils.GetLocationInstances();

                foreach (var lg in locationInstances.Values.GroupBy(l => l.m_location.m_prefabName))
                {
                    BetterContinents.Log($"Placed {lg.Count()} {lg.Key} locations");
                }
            }
            else if (text == "bosses")
            {
                GameUtils.ShowOnMap("StartTemple", "Eikthymir", "GDKing", "GoblinKing", "Bonemass", "Dragonqueen", "Vendor");
            }
            else if (text == "show")
            {
                GameUtils.ShowOnMap();
            }
            else if (Subcommand(ref text, "show"))
            {
                GameUtils.ShowOnMap(text
                    .Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim())
                    .ToArray());
            }
            else if (text == "hide")
            {
                GameUtils.HideOnMap();
            }
            else if (Subcommand(ref text, "hide"))
            {
                GameUtils.HideOnMap(text
                    .Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim())
                    .ToArray());
            }
            else if (Subcommand(ref text, "clouds"))
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
                        TransparentTexture = UI.CreateFillTexture(new Color32(0, 0, 0, 0));
                    }

                    mat.SetTexture("_CloudTex", TransparentTexture);
                }
            }
            else if (text == "fall")
            {
                GameUtils.FallAllObjects();
            }
        }

        public static bool Subcommand(ref string str, string cmd)
        {
            if (!str.StartsWith($"{cmd} "))
            {
                return false;
            }
            str = str.Substring($"{cmd} ".Length).Trim();
            return true;
        }

        public static void ShowHelp()
        {
            Console.instance.Print("bc locs - dump all location instance counts to the log/console");
            Console.instance.Print("bc show (filter) - pin locations matching optional filter oo the map");
            Console.instance.Print("bc hide (filter) - odd locations matching optional filter oo the map");
            Console.instance.Print("bc bosses - toggle pins for bosses");
            Console.instance.Print("bc clouds - toggle the map clouds");
            Console.instance.Print("bc reload hm/bm/sm/rm/fm/fom/all - reload one or more of the maps");
            Console.instance.Print("bc fall - cause all floating objects to fall to the ground");
            // __instance.Print("Better Continents: bc reset hm/bm/sm/rm/fm/fom/all - reload one or more of the maps");
        }
    }
}