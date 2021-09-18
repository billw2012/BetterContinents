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

        public static void InitConsole()
        {
            // ReSharper disable once ObjectCreationAsStatement
            new Terminal.ConsoleCommand("bc", "Root Better Continents command", args => RunConsoleCommand(args.FullLine.Trim()), true, false, true);
            
            AddCommand("info", "print current settings to console", _ =>
            {
                BetterContinents.Settings.Dump(str => Console.instance.Print($"<size=15><color=silver>{str}</color></size>"));
                Console.instance.Print($"<color=orange>NOTE: these settings don't map exactly to console param function or the config file, as some of them are derived.</color>");
            });

            AddCommand("reload", "reload and reapply one or more of the image maps (e.g. 'bc reload hm rm' to reload height map and roughmap)", "hm/rm/fm/bm/sm/fom/all", args =>
            {
                string[] maps = args.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
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
                if (maps.Contains("rm") || all)
                {
                    BetterContinents.Settings.ReloadRoughmap();
                }
                if (maps.Contains("fm") || all)
                {
                    BetterContinents.Settings.ReloadFlatmap();
                }
                if (maps.Contains("bm") || all)
                {
                    BetterContinents.Settings.ReloadBiomemap();
                }
                if (maps.Contains("sm") || all)
                {
                    BetterContinents.Settings.ReloadSpawnmap();
                }
                if (maps.Contains("fom") || all)
                {
                    BetterContinents.Settings.ReloadForestmap();
                }
            
                if (affectingHeight)
                {
                    GameUtils.EndHeightChanges();
                }
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
            // AddCommand("show", "pin all locations on the map", _ =>
            // {
            //     GameUtils.ShowOnMap();
            // });
            AddCommand("show", "pin locations matching optional filter on the map", "(optional filter)", args =>
            {
                GameUtils.ShowOnMap(args
                    .Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim())
                    .ToArray());
            });
            // AddCommand("hide", "remove all pins from the map",  _ =>
            // {
            //     GameUtils.HideOnMap();
            // });
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
                GameUtils.SaveMinimap(string.IsNullOrEmpty(arg) ? 2048 : int.Parse(arg));
            });

            void AddHeightmapSubcommand(Command command, string cmd, string desc, string args, Action<string> action)
            {
                command.AddSubcommand(cmd, desc, args, args2 =>
                {
                    GameUtils.BeginHeightChanges();
                    action(args2);
                    GameUtils.EndHeightChanges();
                });            
            }

            AddCommand("param", "set parameters directly", configCmd: command =>
            {
                command.AddSubcommand("g", "global settings, get more info with 'bc param g help'", subcmdConfig: subcmd => { 
                    AddHeightmapSubcommand(subcmd, "cs", "continent size", "(between 0 and 1)", args => BetterContinents.Settings.SetContinentSize(float.Parse(args)));            
                    AddHeightmapSubcommand(subcmd, "ma", "mountains amount", "(between 0 and 1)", args => BetterContinents.Settings.SetMountainsAmount(float.Parse(args)));
                    AddHeightmapSubcommand(subcmd, "sl", "sea level adjustment", "(between 0 and 1)", args => BetterContinents.Settings.SetSeaLevelAdjustment(float.Parse(args)));
                    AddHeightmapSubcommand(subcmd, "oc", "ocean channels", "(0 to disable, 1 to enable)", args => BetterContinents.Settings.SetOceanChannelsEnabled(int.Parse(args) != 0));
                    AddHeightmapSubcommand(subcmd, "r", "rivers", "(0 to disable, 1 to enable)", args => BetterContinents.Settings.SetRiversEnabled(int.Parse(args) != 0));
                    AddHeightmapSubcommand(subcmd, "me", "map edge drop off", "(0 to disable, 1 to enable)", args => BetterContinents.Settings.SetMapEdgeDropoff(int.Parse(args) != 0));
                    AddHeightmapSubcommand(subcmd, "mc", "mountains allowed at center", "(0 to disable, 1 to enable)", args => BetterContinents.Settings.SetMountainsAllowedAtCenter(int.Parse(args) != 0));
                });
                command.AddSubcommand("h", "heightmap settings, get more info with 'bc param h help'", subcmdConfig: subcmd =>
                {
                    AddHeightmapSubcommand(subcmd, "fn", "set heightmap filename", "(full path including filename, or nothing to disable)", args =>
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
                    AddHeightmapSubcommand(subcmd, "ov", "heightmap override all", "(0 to disable, 1 to enable)", args => BetterContinents.Settings.SetHeightmapOverrideAll(int.Parse(args) != 0));
                    AddHeightmapSubcommand(subcmd, "am", "heightmap amount", "(between 0 and 5)", args => BetterContinents.Settings.SetHeightmapAmount(float.Parse(args)));
                    AddHeightmapSubcommand(subcmd, "bl", "heightmap blend", "(between 0 and 1)", args => BetterContinents.Settings.SetHeightmapBlend(float.Parse(args)));
                    AddHeightmapSubcommand(subcmd, "ad", "heightmap add", "(between -1 and 1)", args => BetterContinents.Settings.SetHeightmapAdd(float.Parse(args)));
                    AddHeightmapSubcommand(subcmd, "ma", "heightmap mask", "(between 0 and 1)", args => BetterContinents.Settings.SetHeightmapMask(float.Parse(args)));
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
                    AddHeightmapSubcommand(subcmd, "bl", "roughmap blend", "(between 0 and 1)", args => BetterContinents.Settings.SetRoughmapBlend(float.Parse(args)));
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

                    AddHeightmapSubcommand(subcmd, "u", "use roughmap inverted for flat", "(0 to disable, 1 to enable)", args => BetterContinents.Settings.SetUseRoughInvertedForFlat(int.Parse(args) != 0));
                    AddHeightmapSubcommand(subcmd, "bl", "flatmap blend", "(between 0 and 1)", args => BetterContinents.Settings.SetFlatmapBlend(float.Parse(args)));
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
                    AddHeightmapSubcommand(subcmd, "sc", "forest scale", "(between 0 and 1)", args => BetterContinents.Settings.SetForestScale(float.Parse(args)));
                    AddHeightmapSubcommand(subcmd, "am", "forest amount", "(between 0 and 1)", args => BetterContinents.Settings.SetForestAmount(float.Parse(args)));
                    AddHeightmapSubcommand(subcmd, "ffo", "forest factor override all trees", "(0 to disable, 1 to enable)", args =>
                    {
                        BetterContinents.Settings.SetForestFactorOverrideAllTrees(int.Parse(args) != 0);
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
                    AddHeightmapSubcommand(subcmd, "mu", "forestmap multiply", "(between 0 and 1)", args => BetterContinents.Settings.SetForestmapMultiply(float.Parse(args)));
                    AddHeightmapSubcommand(subcmd, "ad", "forestmap add", "(between 0 and 1)", args => BetterContinents.Settings.SetForestmapAdd(float.Parse(args)));
                });
                command.AddSubcommand("ri", "ridge settings, get more info with 'bc param ri help'", subcmdConfig: subcmd =>
                {
                    AddHeightmapSubcommand(subcmd, "mh", "ridges max height", "(between 0 and 1)", args => BetterContinents.Settings.SetMaxRidgeHeight(float.Parse(args)));
                    AddHeightmapSubcommand(subcmd, "si", "ridge size", "(between 0 and 1)", args => BetterContinents.Settings.SetRidgeSize(float.Parse(args)));
                    AddHeightmapSubcommand(subcmd, "bl", "ridge blend", "(between 0 and 1)", args => BetterContinents.Settings.SetRidgeBlend(float.Parse(args)));
                    AddHeightmapSubcommand(subcmd, "am", "ridge amount", "(between 0 and 1)", args => BetterContinents.Settings.SetRidgeAmount(float.Parse(args)));
                });
                command.AddSubcommand("st", "start position settings, get more info with 'bc param st help'", subcmdConfig: subcmd =>
                {
                    subcmd.AddSubcommand("os", "override start position", "(0 to disable, 1 to enable)", args =>
                    {
                        BetterContinents.Settings.SetOverrideStartPosition(int.Parse(args) != 0);
                        Console.instance.Print($"<color=orange>INFO: Use 'bc regenloc' to update the location spawns in the world (including the start location)</color>");
                    });
                    subcmd.AddSubcommand("x", "start position x", "(between -10500 and 10500)", args =>
                    {
                        BetterContinents.Settings.SetStartPositionX(float.Parse(args));
                        Console.instance.Print($"<color=orange>INFO: Use 'bc regenloc' to update the location spawns in the world (including the start location)</color>");
                    });
                    subcmd.AddSubcommand("y", "start position y", "(between -10500 and 10500)", args =>
                    {
                        BetterContinents.Settings.SetStartPositionY(float.Parse(args));
                        Console.instance.Print($"<color=orange>INFO: Use 'bc regenloc' to update the location spawns in the world (including the start location)</color>");
                    });
                });
            });
        }
        
        public static void RunConsoleCommand(string text)
        {
            rootCommand.Run(text);
        }

        private static readonly Command rootCommand = new Command("bc", "", "", null);

        public static void AddCommand(string cmd, string desc, Action<string> action = null, Action<Command> configCmd = null)
        {
            rootCommand.AddSubcommand(cmd, desc, action, configCmd);
        }

        public static void AddCommand(string cmd, string desc, string args, Action<string> action, Action<Command> configCmd = null)
        {
            rootCommand.AddSubcommand(cmd, desc, args, action, configCmd);
        }

        public class Command
        {
            public string cmd;
            public string desc;
            public string args;
            public Action<string> action;
            public List<Command> subcommands = new List<Command>();
            public Command parent;

            public Command(string cmd, string desc, string args, Action<string> action)
            {
                this.cmd = cmd;
                this.desc = desc;
                this.args = args;
                this.action = action;
                if (cmd != "help")
                {
                    AddSubcommand("help", "get help with this command", _ => this.ShowSubcommandHelp());
                }
            }

            public Command(string cmd, string desc, Action<string> action) : this(cmd, desc, null, action) { }

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
                Action<Command> subcmdConfig = null) =>
                AddSubcommand(cmd, desc, null, action, subcmdConfig);

            public void AddSubcommand(string cmd, string desc, string args, Action<string> action = null,
                Action<Command> subcmdConfig = null)
            {
                var newSubCmd = new Command(cmd, desc, args, action);
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
                Console.instance.Print(args != null
                    ? $"    <size=18><b><color=cyan>{fullCmd}</color></b> <color=orange>{args}</color> -- </size><size=15>{desc}</size>"
                    : $"    <size=18><b><color=cyan>{fullCmd}</color></b> -- </size><size=15>{desc}</size>");
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