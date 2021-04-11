using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace BetterContinents
{
    public class DebugUtils
    {
        private static Texture CloudTexture;
        private static Texture TransparentTexture;

        private static readonly string[] Bosses =
        {
            "StartTemple", "Eikthyrnir", "GDKing", "GoblinKing", "Bonemass", "Dragonqueen",
            "Vendor_BlackForest"
        };
        
        private delegate void AddCmdActionDelegate(string cmd, string desc, string args, Action<string> action, Func<object> getCurrentValue);
        private delegate NoiseStackSettings.NoiseSettings GetSettingsFromArgsDelegate();

        static DebugUtils()
        {
            AddCommand("info", "print current settings to console", _ =>
            {
                BetterContinents.Settings.Dump(str => Console.instance.Print($"<size=15><color=silver>{str}</color></size>"));
                Console.instance.Print($"<color=orange>NOTE: these settings don't map exactly to console param function or the config file, as some of them are derived.</color>");
            });

            AddCommand("reload", "reload and reapply one or more of the image maps (e.g. 'bc reload hm rm' to reload height map and roughmap)", "hm/rm/fm/bm/sm/fom/all", args =>
            {
                string[] maps = args.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                bool all = maps.Contains("all");

                GameUtils.BeginTerrainChanges();

                if (maps.Contains("hm") || all)     BetterContinents.Settings.ReloadHeightmap();
                if (maps.Contains("rm") || all)     BetterContinents.Settings.ReloadRoughmap();
                if (maps.Contains("fm") || all)     BetterContinents.Settings.ReloadFlatmap();
                if (maps.Contains("bm") || all)     BetterContinents.Settings.ReloadBiomemap();
                if (maps.Contains("sm") || all)     BetterContinents.Settings.ReloadSpawnmap();
                if (maps.Contains("fom") || all)    BetterContinents.Settings.ReloadForestmap();
            
                GameUtils.EndTerrainChanges();
            });
            AddCommand("locs", "print all location spawn instance counts to the console", _ =>
            {
                var locationInstances = GameUtils.GetLocationInstances();

                var locationTypes = locationInstances.Values
                    .GroupBy(l => l.m_location.m_prefabName)
                    .ToDictionary(g => g.Key, g => g.ToList());
                foreach (var lg in locationTypes)
                {
                    Console.instance.Print($"Placed {lg.Value.Count} {lg.Key} locations");
                }

                foreach (var boss in Bosses)
                {
                    if (!locationTypes.ContainsKey(boss))
                    {
                        Console.instance.Print($"<color=orange>WARNING: No {boss} generated</color>");
                    }
                }
            });
            AddCommand("bosses", "show pins for bosses, start temple and trader", _ =>
            {
                GameUtils.ShowOnMap(Bosses);
            });
            AddCommand("show", "pin locations matching optional filter on the map", "(optional filter)", args =>
            {
                GameUtils.ShowOnMap(args
                    .Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim())
                    .ToArray());
            });
            AddCommand("hide", "remove pins matching optional filter from the map", "(optional filter)", args =>
            {
                GameUtils.HideOnMap(args
                    .Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim())
                    .ToArray());
            });
            AddCommand("clouds", "toggle the map clouds", _ =>
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
            });
            AddCommand("mapds", "set minimap downscaling factor (for faster updates)", "(0 = vanilla quality, 1 = 1/2 res, 2 = 1/4 res, 3 = 1/8 res, 2 is default)", args =>
            {
                GameUtils.SetMinimapDownscalingPower(int.Parse(args));
                GameUtils.FastMinimapRegen();
            });

            AddCommand("refresh", "resets all vegetation and locations (done automatically on every change)", _ => {
                GameUtils.Refresh();
            });
            AddCommand("despawnall", "despawn everything", _ => {
                GameUtils.DespawnAll();
            });
            AddCommand("resetall", "reset everything (WARNING: this deletes everything that has been build in the map!)", _ => {
                GameUtils.ResetAll();
                Console.instance.Print($"<color=orange>All constructions removed!</color>");
            });
            AddCommand("regenloc", "regenerate all locations", _ =>
            {
                bool prevLocSetting = BetterContinents.ConfigDebugSkipDefaultLocationPlacement.Value;
                BetterContinents.ConfigDebugSkipDefaultLocationPlacement.Value = false;
                GameUtils.RegenerateLocations();
                BetterContinents.ConfigDebugSkipDefaultLocationPlacement.Value = prevLocSetting;
                Console.instance.Print($"<color=orange>All locations regenerated!</color>");
            });
            AddCommand("scr", "save the minimap to a png", "(optional resolution, default is 2048)", arg => {
                var filename = DateTime.Now.ToString("yyyy-dd-M-HH-mm-ss") + ".png";
                var screenshotDir = Path.Combine(Utils.GetSaveDataPath(), "BetterContinents", WorldGenerator.instance.m_world.m_name);
                var path = Path.Combine(screenshotDir, filename);
                int size = string.IsNullOrEmpty(arg) ? 2048 : int.Parse(arg);
                GameUtils.SaveMinimap(path, size);
                Console.instance.Print($"Map screenshot saved to {path}, size {size} x {size}");
            });
            AddCommand("savepreset", "save current world settings as a preset, including a thumbnail", "(name)", arg =>
            {
                Presets.Save(BetterContinents.Settings, arg);
            });

            void AddHeightmapSubcommand(Command command, string cmd, string desc, string args, Action<string> action, Func<object> getCurrentValue = null)
            {
                command.AddSubcommand(cmd, desc, args, args2 =>
                {
                    GameUtils.BeginTerrainChanges();
                    action(args2);
                    BetterContinents.WorldGeneratorPatch.ApplyNoiseSettings();
                    GameUtils.EndTerrainChanges();
                }, getCurrentValue: getCurrentValue);            
            }

            AddCommand("param", "set parameters directly", configCmd: command =>
            {
                command.AddSubcommand("g", "global settings, get more info with 'bc param g help'", 
                    subcmdConfig: subcmd => { 
                        AddHeightmapSubcommand(subcmd, "cs", "continent size", "(between 0 and 1)", 
                            args => BetterContinents.Settings.ContinentSize = float.Parse(args),
                            () => BetterContinents.Settings.ContinentSize);
                        AddHeightmapSubcommand(subcmd, "ma", "mountains amount", "(between 0 and 1)", 
                            args => BetterContinents.Settings.MountainsAmount = float.Parse(args),
                            () => BetterContinents.Settings.MountainsAmount);
                        AddHeightmapSubcommand(subcmd, "sl", "sea level adjustment", "(between 0 and 1)", 
                            args => BetterContinents.Settings.SeaLevel = float.Parse(args),
                            () => BetterContinents.Settings.SeaLevel);
                        AddHeightmapSubcommand(subcmd, "oc", "ocean channels", "(0 to disable, 1 to enable)", 
                            args => BetterContinents.Settings.OceanChannelsEnabled = int.Parse(args) != 0,
                            () => BetterContinents.Settings.OceanChannelsEnabled);
                        AddHeightmapSubcommand(subcmd, "r", "rivers", "(0 to disable, 1 to enable)", 
                            args => BetterContinents.Settings.RiversEnabled = int.Parse(args) != 0,
                            () => BetterContinents.Settings.RiversEnabled);
                        AddHeightmapSubcommand(subcmd, "me", "map edge drop off", "(0 to disable, 1 to enable)", 
                            args => BetterContinents.Settings.MapEdgeDropoff = int.Parse(args) != 0,
                            () => BetterContinents.Settings.MapEdgeDropoff);
                        AddHeightmapSubcommand(subcmd, "mc", "mountains allowed at center", "(0 to disable, 1 to enable)", 
                            args => BetterContinents.Settings.MountainsAllowedAtCenter = int.Parse(args) != 0,
                            () => BetterContinents.Settings.MountainsAllowedAtCenter);
                    });
                command.AddSubcommand("h", "heightmap settings, get more info with 'bc param h help'", 
                    subcmdConfig: subcmd =>
                    {
                        AddHeightmapSubcommand(subcmd, "fn", "set heightmap filename", 
                            "(full path including filename, or nothing to disable)", 
                            args =>
                            {
                                if (string.IsNullOrEmpty(args))
                                {
                                    BetterContinents.Settings.DisableHeightmap();
                                    Console.instance.Print($"<color=orange>Heightmap disabled!</color>");
                                }
                                else if (!File.Exists(BetterContinents.CleanPath(args)))
                                    Console.instance.Print($"<color=red>ERROR: {args} doesn't exist</color>");
                                else
                                    BetterContinents.Settings.SetHeightmapPath(args);
                            });
                        AddHeightmapSubcommand(subcmd, "ov", "heightmap override all", "(0 to disable, 1 to enable)",
                            args => BetterContinents.Settings.HeightmapOverrideAll = int.Parse(args) != 0,
                            () => BetterContinents.Settings.HeightmapOverrideAll);
                        AddHeightmapSubcommand(subcmd, "am", "heightmap amount", "(between 0 and 5)", 
                            args => BetterContinents.Settings.HeightmapAmount = float.Parse(args),
                            () => BetterContinents.Settings.HeightmapAmount);
                        AddHeightmapSubcommand(subcmd, "bl", "heightmap blend", "(between 0 and 1)", 
                            args => BetterContinents.Settings.HeightmapBlend = float.Parse(args),
                            () => BetterContinents.Settings.HeightmapBlend);
                        AddHeightmapSubcommand(subcmd, "ad", "heightmap add", "(between -1 and 1)", 
                            args => BetterContinents.Settings.HeightmapAdd = float.Parse(args),
                            () => BetterContinents.Settings.HeightmapAdd);
                        AddHeightmapSubcommand(subcmd, "ma", "heightmap mask", "(between 0 and 1)", 
                            args => BetterContinents.Settings.HeightmapMask = float.Parse(args),
                            () => BetterContinents.Settings.HeightmapMask);
                    });

                string EnumHelp<T>() => "(" + string.Join(", ", Enum.GetNames(typeof(T))) + ")";
                T EnumParse<T>(string str) => (T)Enum.Parse(typeof(T), str, ignoreCase: true);
                void AddNoiseCommands(AddCmdActionDelegate addCmdAction, GetSettingsFromArgsDelegate getSettings)
                {
                    // Basic
                    addCmdAction("nt", "noise type", EnumHelp<FastNoiseLite.NoiseType>(), 
                        args => getSettings().NoiseType = EnumParse<FastNoiseLite.NoiseType>(args),
                        () => getSettings().NoiseType);
                    addCmdAction("fq", "frequency", "(0.0001 to 0.001)", 
                        args => getSettings().Frequency = float.Parse(args),
                        () => getSettings().Frequency);
                    // Fractal
                    addCmdAction("ft", "fractal type", EnumHelp<FastNoiseLite.FractalType>(), 
                        args => getSettings().FractalType = EnumParse<FastNoiseLite.FractalType>(args),
                        () => getSettings().FractalType);
                    addCmdAction("fo", "fractal octaves", "(1 to 10)", 
                        args => getSettings().FractalOctaves = int.Parse(args),
                        () => getSettings().FractalOctaves);
                    addCmdAction("fl", "fractal lacunarity", "(0 to 10)", 
                        args => getSettings().FractalLacunarity = float.Parse(args),
                        () => getSettings().FractalLacunarity);
                    addCmdAction("fg", "fractal gain", "(0 to 1)", 
                        args => getSettings().FractalGain = float.Parse(args),
                        () => getSettings().FractalGain);
                    addCmdAction("ws", "weighted strength", "(-1 to 1)", 
                        args => getSettings().FractalWeightedStrength = float.Parse(args),
                        () => getSettings().FractalWeightedStrength);
                    addCmdAction("ps", "ping-pong strength", "(0 to 10)", 
                        args => getSettings().FractalPingPongStrength = float.Parse(args),
                        () => getSettings().FractalPingPongStrength);
                    // Cellular
                    addCmdAction("cf", "cellular distance function", EnumHelp<FastNoiseLite.CellularDistanceFunction>(), 
                        args => getSettings().CellularDistanceFunction = EnumParse<FastNoiseLite.CellularDistanceFunction>(args),
                        () => getSettings().CellularDistanceFunction);
                    addCmdAction("ct", "cellular return type", EnumHelp<FastNoiseLite.CellularReturnType>(), 
                        args => getSettings().CellularReturnType = EnumParse<FastNoiseLite.CellularReturnType>(args),
                        () => getSettings().CellularReturnType);
                    addCmdAction("cj", "cellular jitter", "(0 to 2)", 
                        args => getSettings().CellularJitter = float.Parse(args),
                        () => getSettings().CellularJitter);
                    // Warp
                    addCmdAction("dt", "domain warp type", EnumHelp<FastNoiseLite.DomainWarpType>(), 
                        args => getSettings().DomainWarpType = EnumParse<FastNoiseLite.DomainWarpType>(args),
                        () => getSettings().DomainWarpType);
                    addCmdAction("da", "domain warp amp", "(0 to 200)", 
                        args => getSettings().DomainWarpAmp = float.Parse(args),
                        () => getSettings().DomainWarpAmp);
                    // Filters
                    addCmdAction("in", "invert", "(0 or 1)", 
                        args => getSettings().Invert = int.Parse(args) != 0,
                        () => getSettings().Invert);
                    addCmdAction("ss", "smooth step", "(0 - 1) (0 - 1) or off to disable", 
                        args =>
                        {
                            var parts = args.Split(' ');
                            var settings = getSettings();
                            if (parts.Length == 1 && parts[0] == "off")
                            {
                                settings.SmoothStepStart = settings.SmoothStepEnd = null;
                            }
                            else if(parts.Length == 2)
                            {
                                settings.SmoothStepStart = int.Parse(parts[0]);
                                settings.SmoothStepEnd = int.Parse(parts[1]);
                            }
                            else
                            {
                                throw new ArgumentException("smooth step expects either two float arguments, or 0 to disable");
                            }
                        },
                        () =>
                        {
                            var settings = getSettings();
                            return settings.SmoothStepStart == null || settings.SmoothStepEnd == null
                                ? "(disabled)"
                                : $"[{settings.SmoothStepStart}, {settings.SmoothStepEnd}]";
                        });
                    addCmdAction("th", "threshold", "(0 to 1) or off to disable", 
                        args => getSettings().Threshold = args == "off" ? (float?) null : float.Parse(args),
                        () => (object)getSettings().Threshold ?? "(disabled)");
                    addCmdAction("ra", "range", "(0 - 1) (0 - 1) or off to disable", 
                        args =>
                        {
                            var parts = args.Split(' ');
                            var settings = getSettings();
                            if (parts.Length == 1 && parts[0] == "off")
                            {
                                settings.RangeStart = settings.RangeEnd = null;
                            }
                            else if(parts.Length == 2)
                            {
                                settings.RangeStart = int.Parse(parts[0]);
                                settings.RangeEnd = int.Parse(parts[1]);
                            }
                            else
                            {
                                throw new ArgumentException("range expects either two float arguments, or 0 to disable");
                            }
                        },
                        () =>
                        {
                            var settings = getSettings();
                            return settings.RangeStart == null || settings.RangeEnd == null
                                ? "(disabled)"
                                : $"[{settings.RangeStart}, {settings.RangeEnd}]";
                        });
                    addCmdAction("th", "opacity", "(0 to 1) or off to disable", 
                        args => getSettings().Opacity = args == "off" ? (float?) null : float.Parse(args),
                        () => (object)getSettings().Opacity ?? "(disabled)");
                }
                
                command.AddSubcommand("hb", "base height noise settings, get more info with 'bc param hb help'", subcmdConfig: subcmd =>
                {
                    AddNoiseCommands((cmd, desc, args, action, getCurrentValue) => AddHeightmapSubcommand(subcmd, cmd, desc, args, action, getCurrentValue),
                        () => BetterContinents.Settings.BaseHeightNoise.BaseLayer);
                });
                
                command.AddSubcommand("hl", "height layer noise settings", subcmdConfig: subcmd =>
                {
                    AddHeightmapSubcommand(subcmd, "num", "layer count", "(count from 0 to 4)", args => BetterContinents.Settings.BaseHeightNoise.SetNoiseLayerCount(int.Parse(args)));
                    for (int i = 0; i < 4; i++)
                    {
                        int index = i;
                        subcmd.AddSubcommand(index.ToString(), $"height layer {index}", subcmdConfig: subcmdLayer =>
                        {
                            subcmdLayer.AddSubcommand("n", $"height layer {index} noise", 
                                subcmdConfig: subcmdLayerPart => AddNoiseCommands(
                                    (cmd, desc, args, action, getCurrentValue) => AddHeightmapSubcommand(subcmdLayerPart, cmd, desc, args, action, getCurrentValue),
                                    () => BetterContinents.Settings.BaseHeightNoise.Layers[index].noiseSettings));
                            subcmdLayer.AddSubcommand("nw", $"height layer {index} noise domain wrap", 
                                subcmdConfig: subcmdLayerPart => AddNoiseCommands(
                                    (cmd, desc, args, action, getCurrentValue) => AddHeightmapSubcommand(subcmdLayerPart, cmd, desc, args, action, getCurrentValue),
                                    () => BetterContinents.Settings.BaseHeightNoise.Layers[index].noiseWarpSettings));
                            subcmdLayer.AddSubcommand("m", $"height layer {index} mask", 
                                subcmdConfig: subcmdLayerPart => AddNoiseCommands(
                                    (cmd, desc, args, action, getCurrentValue) => AddHeightmapSubcommand(subcmdLayerPart, cmd, desc, args, action, getCurrentValue),
                                    () => BetterContinents.Settings.BaseHeightNoise.Layers[index].maskSettings));
                            subcmdLayer.AddSubcommand("mw", $"height layer {index} mask domain warp", 
                                subcmdConfig: subcmdLayerPart => AddNoiseCommands(
                                    (cmd, desc, args, action, getCurrentValue) => AddHeightmapSubcommand(subcmdLayerPart, cmd, desc, args, action, getCurrentValue),
                                    () => BetterContinents.Settings.BaseHeightNoise.Layers[index].maskWarpSettings));
                        });
                    }
                    // AddNoiseCommands((cmd, desc, args, action) => AddHeightmapSubcommand(subcmd, cmd, desc, "(n, nw, m, or mw) (layer index 0 - 4) " + args, action),
                    //     (ref string args) =>
                    //     {
                    //         var parts = args.Split(' ');
                    //         int index = int.Parse(parts[0]);
                    //         string part = parts[1];
                    //         args = parts.Length >= 3 ? parts[2] : string.Empty;
                    //         
                    //         if (index >= BetterContinents.Settings.BaseHeightNoise.Layers.Count)
                    //         {
                    //             throw new IndexOutOfRangeException("Layer index out of range, set layer count first");
                    //         }
                    //
                    //         NoiseStackSettings.NoiseSettings settings = null;
                    //         switch (part)
                    //         {
                    //             case "n": settings = BetterContinents.Settings.BaseHeightNoise.Layers[index].noiseSettings; break; 
                    //             case "nw": settings = BetterContinents.Settings.BaseHeightNoise.Layers[index].noiseWarpSettings; break;
                    //             case "m": settings = BetterContinents.Settings.BaseHeightNoise.Layers[index].maskSettings; break;
                    //             case "mw": settings = BetterContinents.Settings.BaseHeightNoise.Layers[index].maskWarpSettings; break;
                    //         }
                    //         
                    //         if (settings == null)
                    //         {
                    //             throw new ArgumentException($"height layer part id unknown: {part}");
                    //         }
                    //
                    //         if(string.IsNullOrEmpty(args.Trim()))
                    //         {
                    //             settings.Dump(Console.instance.Print);
                    //         }
                    //
                    //
                    //         return settings;
                    //     });
                });
                
                command.AddSubcommand("r", "roughmap settings, get more info with 'bc param r help'", subcmdConfig: subcmd =>
                {
                    AddHeightmapSubcommand(subcmd, "fn", "set roughmap filename", "(full path including filename, or nothing to disable)", args =>
                    {
                        if (string.IsNullOrEmpty(args))
                        {
                            BetterContinents.Settings.DisableRoughmap();
                            Console.instance.Print($"<color=orange>Roughmap disabled!</color>");
                        }
                        else if (!File.Exists(BetterContinents.CleanPath(args)))
                            Console.instance.Print($"<color=red>ERROR: {args} doesn't exist</color>");
                        else
                            BetterContinents.Settings.SetRoughmapPath(args);
                    });
                    AddHeightmapSubcommand(subcmd, "bl", "roughmap blend", "(between 0 and 1)", args =>
                    {
                        BetterContinents.Settings.RoughmapBlend = float.Parse(args);
                    });
                });
                command.AddSubcommand("f", "flatmap settings, get more info with 'bc param f help'", subcmdConfig: subcmd =>
                {
                    AddHeightmapSubcommand(subcmd, "fn", "set flatmap filename", "(full path including filename, or nothing to disable)", args =>
                    {
                        if(string.IsNullOrEmpty(args))
                        {
                            BetterContinents.Settings.DisableFlatmap();
                            Console.instance.Print($"<color=orange>Flatmap disabled!</color>");
                        }
                        else if (!File.Exists(BetterContinents.CleanPath(args)))
                            Console.instance.Print($"<color=red>ERROR: {args} doesn't exist</color>");
                        else
                        {
                            BetterContinents.Settings.SetFlatmapPath(args);
                            if (BetterContinents.Settings.UseRoughInvertedAsFlat)
                            {
                                Console.instance.Print(
                                    $"<color=orange>WARNING: 'Use Rough Inverted as Flat' is enabled so flatmap has no effect. Use 'bc urm 0' to disable it.</color>");
                            }
                        }
                    });

                    AddHeightmapSubcommand(subcmd, "u", "use roughmap inverted for flat", "(0 to disable, 1 to enable)", args =>
                    {
                        BetterContinents.Settings.UseRoughInvertedAsFlat = int.Parse(args) != 0;
                    });
                    AddHeightmapSubcommand(subcmd, "bl", "flatmap blend", "(between 0 and 1)", args =>
                    {
                        BetterContinents.Settings.FlatmapBlend = float.Parse(args);
                    });
                });
                command.AddSubcommand("b", "biomemap settings, get more info with 'bc param b help'", subcmdConfig: subcmd =>
                {
                    AddHeightmapSubcommand(subcmd, "fn", "set biomemap filename", "(full path including filename, or nothing to disable)", args =>
                    {
                        if(string.IsNullOrEmpty(args))
                        {
                            BetterContinents.Settings.DisableBiomemap();
                            Console.instance.Print($"<color=orange>Biomemap disabled!</color>");
                        }
                        else if (!File.Exists(BetterContinents.CleanPath(args)))
                            Console.instance.Print($"<color=red>ERROR: {args} doesn't exist</color>");
                        else
                            BetterContinents.Settings.SetBiomemapPath(args);
                    });
                });
                command.AddSubcommand("s", "spawnmap settings, get more info with 'bc param s help'", subcmdConfig: subcmd =>
                {
                    AddHeightmapSubcommand(subcmd, "fn", "set spawnmap filename", "(full path including filename, or nothing to disable)", args =>
                    {
                        if(string.IsNullOrEmpty(args))
                        {
                            BetterContinents.Settings.DisableSpawnmap();
                            Console.instance.Print($"<color=orange>Spawnmap disabled!</color>");
                            Console.instance.Print($"<color=orange>INFO: Use 'bc regenloc' to update the location spawns in the world</color>");
                        }
                        else if (!File.Exists(BetterContinents.CleanPath(args)))
                            Console.instance.Print($"<color=red>ERROR: {args} doesn't exist</color>");
                        else
                        {
                            BetterContinents.Settings.SetSpawnmapPath(args);
                            Console.instance.Print($"<color=orange>INFO: Use 'bc regenloc' to update the location spawns in the world</color>");
                        }                    
                    });
                });
                command.AddSubcommand("fo", "forest settings, get more info with 'bc param fo help'", subcmdConfig: subcmd =>
                {
                    AddHeightmapSubcommand(subcmd, "sc", "forest scale", "(between 0 and 1)", args => BetterContinents.Settings.ForestScaleFactor = float.Parse(args));
                    AddHeightmapSubcommand(subcmd, "am", "forest amount", "(between 0 and 1)", args => BetterContinents.Settings.ForestAmount = float.Parse(args));
                    AddHeightmapSubcommand(subcmd, "ffo", "forest factor override all trees", "(0 to disable, 1 to enable)", args =>
                    {
                        BetterContinents.Settings.ForestFactorOverrideAllTrees = int.Parse(args) != 0;
                        Console.instance.Print("<color=orange>NOTE: You need to reload the world to apply this change to the forest factor override!</color>");
                    });
                    AddHeightmapSubcommand(subcmd, "fn", "set forestmap filename", "(full path including filename, or nothing to disable)", args =>
                    {
                        if(string.IsNullOrEmpty(args))
                        {
                            BetterContinents.Settings.DisableForestmap();
                            Console.instance.Print($"<color=orange>Forestmap disabled!</color>");
                        }
                        else if (!File.Exists(BetterContinents.CleanPath(args)))
                            Console.instance.Print($"<color=red>ERROR: {args} doesn't exist</color>");
                        else
                            BetterContinents.Settings.SetForestmapPath(args);
                    });
                    AddHeightmapSubcommand(subcmd, "mu", "forestmap multiply", "(between 0 and 1)", args =>
                    {
                        BetterContinents.Settings.ForestmapMultiply = float.Parse(args);
                    });
                    AddHeightmapSubcommand(subcmd, "ad", "forestmap add", "(between 0 and 1)", args =>
                    {
                        BetterContinents.Settings.ForestmapAdd = float.Parse(args);
                    });
                });
                command.AddSubcommand("ri", "ridge settings, get more info with 'bc param ri help'", subcmdConfig: subcmd =>
                {
                    AddHeightmapSubcommand(subcmd, "mh", "ridges max height", "(between 0 and 1)", args =>
                    {
                        BetterContinents.Settings.MaxRidgeHeight = float.Parse(args);
                    });
                    AddHeightmapSubcommand(subcmd, "si", "ridge size", "(between 0 and 1)", args => BetterContinents.Settings.RidgeSize = float.Parse(args));
                    AddHeightmapSubcommand(subcmd, "bl", "ridge blend", "(between 0 and 1)", args => BetterContinents.Settings.RidgeBlend = float.Parse(args));
                    AddHeightmapSubcommand(subcmd, "am", "ridge amount", "(between 0 and 1)", args => BetterContinents.Settings.RidgeAmount = float.Parse(args));
                });
                command.AddSubcommand("st", "start position settings, get more info with 'bc param st help'", subcmdConfig: subcmd =>
                {
                    subcmd.AddSubcommand("os", "override start position", "(0 to disable, 1 to enable)", args =>
                    {
                        BetterContinents.Settings.OverrideStartPosition = int.Parse(args) != 0;
                        Console.instance.Print($"<color=orange>INFO: Use 'bc regenloc' to update the location spawns in the world (including the start location)</color>");
                    });
                    subcmd.AddSubcommand("x", "start position x", "(between -10500 and 10500)", args =>
                    {
                        BetterContinents.Settings.StartPositionX = float.Parse(args);
                        Console.instance.Print($"<color=orange>INFO: Use 'bc regenloc' to update the location spawns in the world (including the start location)</color>");
                    });
                    subcmd.AddSubcommand("y", "start position y", "(between -10500 and 10500)", args =>
                    {
                        BetterContinents.Settings.StartPositionY = float.Parse(args);
                        Console.instance.Print($"<color=orange>INFO: Use 'bc regenloc' to update the location spawns in the world (including the start location)</color>");
                    });
                });
            });
        }

        public static void RunConsoleCommand(string text)
        {
            rootCommand.Run(text);
        }

        private static Command rootCommand = new Command("bc", "", "", null, null);

        public static void AddCommand(string cmd, string desc, Action<string> action = null, Action<Command> configCmd = null, Func<object> getCurrentValue = null)
        {
            rootCommand.AddSubcommand(cmd, desc, action, configCmd, getCurrentValue);
        }

        public static void AddCommand(string cmd, string desc, string args, Action<string> action, Action<Command> configCmd = null, Func<object> getCurrentValue = null)
        {
            rootCommand.AddSubcommand(cmd, desc, args, action, configCmd, getCurrentValue);
        }

        public class Command
        {
            public string cmd;
            public string desc;
            public string args;
            public Action<string> action;
            public Func<object> getCurrentValue;
            public List<Command> subcommands = new List<Command>();
            public Command parent;

            public Command(string cmd, string desc, string args, Action<string> action, Func<object> getCurrentValue)
            {
                this.cmd = cmd;
                this.desc = desc;
                this.args = args;
                this.action = action;
                this.getCurrentValue = getCurrentValue;
                if (cmd != "help")
                {
                    AddSubcommand("help", "get help with this command", _ => this.ShowSubcommandHelp());
                }
            }

            public Command(string cmd, string desc, Action<string> action, Func<string> getCurrentValue) : this(cmd, desc, null, action, getCurrentValue) { }

            public bool Run(string text)
            {
                if (subcommands.Any() && text.StartsWith(cmd + " "))
                {
                    if(subcommands.FirstOrDefault(s => s.Run(text.Substring(cmd.Length).Trim())) != null)
                    {
                        return true;
                    }
                }
                if (action != null && (text == cmd || args != null && text.StartsWith(cmd + " ")))
                {
                    try
                    {
                        action(text.Substring(cmd.Length).Trim());
                    }
                    catch (Exception ex)
                    {
                        Console.instance.Print($"{cmd} failed: {ex.Message}");
                    }

                    return true;
                }
                else if (action == null && text == cmd)
                {
                    ShowSubcommandHelp();
                    
                    return true;
                }

                return false;
            }

            public void AddSubcommand(string cmd, string desc, Action<string> action = null,
                Action<Command> subcmdConfig = null, Func<object> getCurrentValue = null) =>
                AddSubcommand(cmd, desc, null, action, subcmdConfig, getCurrentValue);

            public void AddSubcommand(string cmd, string desc, string args, Action<string> action = null,
                Action<Command> subcmdConfig = null, Func<object> getCurrentValue = null)
            {
                var newSubCmd = new Command(cmd, desc, args, action, getCurrentValue);
                newSubCmd.parent = this;
                subcmdConfig?.Invoke(newSubCmd);
                subcommands.Add(newSubCmd);
            }
            
            public void ShowHelp()
            {
                var cmdStack = new List<string>();
                var curr = this;
                while (curr != null)
                {
                    cmdStack.Insert(0, curr.cmd); 
                    curr = curr.parent;
                }

                var fullCmd = string.Join(" ", cmdStack);
                    
                Console.instance.Print($"    <size=18><b><color=cyan>{fullCmd}</color></b> <color=orange>{args ?? string.Empty}</color> -- <b><color=#55ff55ff>{getCurrentValue?.Invoke() ?? string.Empty}</color></b> -- </size><size=15>{desc}</size>");

                // if (getCurrentValue != null)
                // {
                //     Console.instance.Print($"        <size=15><b><color=#55ff55ff>{getCurrentValue()}</color></b></size>");
                // }
            }

            public void ShowSubcommandHelp()
            {
                ShowHelp();
                foreach (var subcmd in subcommands)
                {
                    subcmd.ShowHelp();
                }
            }
        }

        public static T GetDelegate<T>(Type type, string method) where T : Delegate 
            => AccessTools.MethodDelegate<T>(AccessTools.Method(type, method));
    }
}