using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Schema;
using BepInEx.Configuration;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace BetterContinents
{
    public partial class DebugUtils
    {
        private static Texture CloudTexture;
        private static Texture TransparentTexture;

        private static readonly string[] Bosses =
        {
            "StartTemple", "Eikthyrnir", "GDKing", "GoblinKing", "Bonemass", "Dragonqueen",
            "Vendor_BlackForest"
        };
        
        //private delegate void AddCmdActionDelegate(Type type, string name, string desc, object defaultValue, AcceptableValueBase range, Action<object> setValue, Func<object> getValue);
        //private delegate NoiseStackSettings.NoiseSettings GetSettingsFromArgsDelegate();

        static DebugUtils()
        {
            // ReSharper disable once ObjectCreationAsStatement
            new Terminal.ConsoleCommand("bc", "Root Better Continents command", args => RunConsoleCommand(args.FullLine.Trim()), true, false, true);
            rootCommand = new Command("bc", "Better Continents", "Better Continents command").Subcommands(bc =>
            {
                bc.AddCommand("info", "Dump Info", "print current settings to console", _ =>
                {
                    BetterContinents.Settings.Dump(str =>
                        Console.instance.Print($"<size=15><color=silver>{str}</color></size>"));
                    Console.instance.Print(
                        $"<color=orange>NOTE: these settings don't map exactly to console param function or the config file, as some of them are derived.</color>");
                });

                if (BetterContinents.Settings.AnyImageMap)
                {
                    bc.AddGroup("reload", "Reload", "reload and reapply one or more of the image maps", reload =>
                    {
                        if (BetterContinents.Settings.HasHeightmap)
                            reload.AddCommand("hm", "Heightmap", "Reload the heightmap",
                                HeightmapCommand(_ => BetterContinents.Settings.ReloadHeightmap()));
                        if (BetterContinents.Settings.HasRoughmap)
                            reload.AddCommand("rm", "Roughmap", "Reload the roughmap",
                                HeightmapCommand(_ => BetterContinents.Settings.ReloadRoughmap()));
                        if (BetterContinents.Settings.HasFlatmap)
                            reload.AddCommand("fm", "Flatmap", "Reload the flatmap",
                                HeightmapCommand(_ => BetterContinents.Settings.ReloadFlatmap()));
                        if (BetterContinents.Settings.HasBiomemap)
                            reload.AddCommand("bm", "Biomemap", "Reload the biomemap",
                                HeightmapCommand(_ => BetterContinents.Settings.ReloadBiomemap()));
                        if (BetterContinents.Settings.HasSpawnmap)
                            reload.AddCommand("sm", "Spawnmap", "Reload the spawnmap",
                                HeightmapCommand(_ => BetterContinents.Settings.ReloadSpawnmap()));
                        if (BetterContinents.Settings.HasForestmap)
                            reload.AddCommand("fom", "Forestmap", "Reload the forestmap",
                                HeightmapCommand(_ => BetterContinents.Settings.ReloadForestmap()));
                        if (BetterContinents.Settings.AnyImageMap)
                        {
                            reload.AddCommand("all", "All", "Reload all image maps", HeightmapCommand(_ =>
                            {
                                if (BetterContinents.Settings.HasHeightmap) BetterContinents.Settings.ReloadHeightmap();
                                if (BetterContinents.Settings.HasRoughmap) BetterContinents.Settings.ReloadRoughmap();
                                if (BetterContinents.Settings.HasFlatmap) BetterContinents.Settings.ReloadFlatmap();
                                if (BetterContinents.Settings.HasBiomemap) BetterContinents.Settings.ReloadBiomemap();
                                if (BetterContinents.Settings.HasSpawnmap) BetterContinents.Settings.ReloadSpawnmap();
                                if (BetterContinents.Settings.HasForestmap) BetterContinents.Settings.ReloadForestmap();
                            }));
                        }
                    });
                }

                bc.AddCommand("locs", "Dump locations", "print all location spawn instance counts to the console", _ =>
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
                bc.AddCommand("bosses", "Show bosses", "show pins for bosses, start temple and trader",
                    _ => GameUtils.ShowOnMap(Bosses));
                bc.AddCommand("show", "Show locations", "pin locations matching optional filter on the map", args =>
                {
                    GameUtils.ShowOnMap((args ?? "")
                        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(f => f.Trim())
                        .ToArray());
                });
                bc.AddCommand("hide", "Hide locations", "remove pins matching optional filter from the map", args =>
                {
                    GameUtils.HideOnMap((args ?? "")
                        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(f => f.Trim())
                        .ToArray());
                });
                bc.AddValue<bool>("clouds", "Clouds", "if the map clouds are enabled",
                    defaultValue: true,
                    getter: () =>
                        Minimap.instance.m_mapImageLarge.material.GetTexture("_CloudTex") != TransparentTexture,
                    setter: enable =>
                    {
                        var mat = Minimap.instance.m_mapImageLarge.material;
                        CloudTexture ??= mat.GetTexture("_CloudTex");
                        TransparentTexture ??= UI.CreateFillTexture(new Color32(0, 0, 0, 0));
                        mat.SetTexture("_CloudTex", enable ? TransparentTexture : CloudTexture);
                    });
                bc.AddValue<int>("mapds", "Minimap downscaling", "set minimap downscaling factor (for faster updates)",
                    defaultValue: 2,
                    list: new[] { 0, 1, 2, 3 },
                    getter: () => GameUtils.MinimapDownscalingPower,
                    setter: val =>
                    {
                        GameUtils.MinimapDownscalingPower = Mathf.Clamp(val, 0, 3);
                        GameUtils.FastMinimapRegen();
                    });
                bc.AddCommand("refresh", "Refresh",
                    "resets all vegetation and locations (done automatically on every change)",
                    _ => GameUtils.Refresh());
                bc.AddCommand("despawnall", "Despawn all", "despawn everything",
                    _ => GameUtils.DespawnAll());
                bc.AddCommand("resetall", "Reset all",
                    "reset everything (WARNING: this deletes everything that has been build in the map!)", _ =>
                    {
                        GameUtils.ResetAll();
                        Console.instance.Print($"<color=orange>All constructions removed!</color>");
                    });
                bc.AddCommand("regenloc", "Regenerate locs", "regenerate all locations", _ =>
                {
                    bool prevLocSetting = BetterContinents.ConfigDebugSkipDefaultLocationPlacement.Value;
                    BetterContinents.ConfigDebugSkipDefaultLocationPlacement.Value = false;
                    GameUtils.RegenerateLocations();
                    BetterContinents.ConfigDebugSkipDefaultLocationPlacement.Value = prevLocSetting;
                    Console.instance.Print($"<color=orange>All locations regenerated!</color>");
                });
                bc.AddCommand("scr", "Save map screenshot",
                    "save the minimap to a png, optionally pass resolution, default is 2048", arg =>
                    {
                        var filename = DateTime.Now.ToString("yyyy-dd-M-HH-mm-ss") + ".png";
                        var screenshotDir = Path.Combine(Utils.GetSaveDataPath(FileHelpers.FileSource.Local), "BetterContinents",
                            WorldGenerator.instance.m_world.m_name);
                        var path = Path.Combine(screenshotDir, filename);
                        int size = string.IsNullOrEmpty(arg) ? 2048 : int.Parse(arg);
                        GameUtils.SaveMinimap(path, size);
                        Console.instance.Print($"Map screenshot saved to {path}, size {size} x {size}");
                    });
                bc.AddCommand("savepreset", "Save preset",
                    "save current world settings as a preset, including a thumbnail, pass preset name as argument",
                    arg =>
                    {
                        arg ??= WorldGenerator.instance.m_world.m_name;
                        Presets.Save(BetterContinents.Settings, arg);
                    });

                bc.AddGroup("g", "Global", "global settings, get more info with 'bc g help'",
                    group =>
                    {
                        // AddHeightmapValue(subcmd, "cs", "continent size", 0, new AcceptableValueRange<float>(0, 1), 
                        //     value => BetterContinents.Settings.ContinentSize = value,
                        //     () => BetterContinents.Settings.ContinentSize);
                        // AddHeightmapValue(subcmd, "ma", "mountains amount", 0.5f, new AcceptableValueRange<float>(0, 1),
                        //     value => BetterContinents.Settings.MountainsAmount = value,
                        //     () => BetterContinents.Settings.MountainsAmount);
                        // AddHeightmapValue(subcmd, "oc", "ocean channels", false, 
                        //     args => BetterContinents.Settings.OceanChannelsEnabled = int.Parse(args) != 0,
                        //     () => BetterContinents.Settings.OceanChannelsEnabled);
                        group.AddValue<float>("sl", "Sea level adjustment", "sea level adjustment", 
                            defaultValue: 0.5f, minValue: 0, maxValue: 1,
                            setter: SetHeightmapValue<float>(value => BetterContinents.Settings.SeaLevel = value),
                            getter: () => BetterContinents.Settings.SeaLevel);
                        group.AddValue<bool>("r", "Enable rivers", "whether rivers are enabled",
                            defaultValue: true,
                            setter: SetHeightmapValue<bool>(value => BetterContinents.Settings.RiversEnabled = value),
                            getter: () => BetterContinents.Settings.RiversEnabled);
                        group.AddValue<bool>("me", "Map edge drop off", "whether the map drops away at the boundary",
                            defaultValue: true,
                            setter: SetHeightmapValue<bool>(value => BetterContinents.Settings.MapEdgeDropoff = value),
                            getter: () => BetterContinents.Settings.MapEdgeDropoff);
                        group.AddValue<bool>("mc", "Allow mountains in center",
                            "whether the center of the map (usually the spawn area), is flattened",
                            defaultValue: false,
                            setter: SetHeightmapValue<bool>(value =>
                                BetterContinents.Settings.MountainsAllowedAtCenter = value),
                            getter: () => BetterContinents.Settings.MountainsAllowedAtCenter);
                    });

                bc.AddGroup("h", "Heightmap", "heightmap settings, get more info with 'bc param h help'",
                    group =>
                    {
                        group.AddValue<string>("fn", "Heightmap filename",
                            "set heightmap filename (full path including filename, or nothing to disable)",
                            defaultValue: string.Empty,
                            setter: SetHeightmapValue<string>(path =>
                            {
                                if (string.IsNullOrEmpty(path))
                                {
                                    BetterContinents.Settings.DisableHeightmap();
                                    Console.instance.Print($"<color=orange>Heightmap disabled!</color>");
                                }
                                else if (!File.Exists(BetterContinents.CleanPath(path)))
                                    Console.instance.Print($"<color=red>ERROR: {path} doesn't exist</color>");
                                else
                                    BetterContinents.Settings.SetHeightmapPath(path);
                            }),
                            getter: () => BetterContinents.Settings.GetHeightmapPath());
                        group.AddValue<bool>("ov", "Heightmap Override All",
                            "causes the terrain to conform to the heightmap, ignoring biome specific variance",
                            defaultValue: false,
                            setter: SetHeightmapValue<bool>(value =>
                                BetterContinents.Settings.HeightmapOverrideAll = value),
                            getter: () => BetterContinents.Settings.HeightmapOverrideAll);
                        group.AddValue<float>("am", "Heightmap Amount", "heightmap amount",
                            defaultValue: 1f, minValue: 0, maxValue: 5,
                            setter: SetHeightmapValue<float>(value =>
                                BetterContinents.Settings.HeightmapAmount = value),
                            getter: () => BetterContinents.Settings.HeightmapAmount);
                        group.AddValue<float>("bl", "Heightmap Blend", "heightmap blend",
                            defaultValue: 1f, minValue: 0, maxValue: 1,
                            setter: SetHeightmapValue<float>(value => BetterContinents.Settings.HeightmapBlend = value),
                            getter: () => BetterContinents.Settings.HeightmapBlend);
                        group.AddValue<float>("ad", "Heightmap Add", "heightmap add",
                            defaultValue: 0f, minValue: -1, maxValue: 1,
                            setter: SetHeightmapValue<float>(value => BetterContinents.Settings.HeightmapAdd = value),
                            getter: () => BetterContinents.Settings.HeightmapAdd);
                        group.AddValue<float>("ma", "Heightmap Mask", "heightmap mask",
                            defaultValue: 0f, minValue: 0, maxValue: 1,
                            setter: SetHeightmapValue<float>(value => BetterContinents.Settings.HeightmapMask = value),
                            getter: () => BetterContinents.Settings.HeightmapMask);
                    });

                bc.AddGroup("r", "Roughmap", "roughmap settings, get more info with 'bc param r help'", group =>
                {
                    group.AddValue<string>("fn", "Roughmap Filename",
                        "set roughmap filename (full path including filename, or nothing to disable)",
                        defaultValue: string.Empty,
                        setter: SetHeightmapValue<string>(path =>
                        {
                            if (string.IsNullOrEmpty(path))
                            {
                                BetterContinents.Settings.DisableRoughmap();
                                Console.instance.Print($"<color=orange>Roughmap disabled!</color>");
                            }
                            else if (!File.Exists(BetterContinents.CleanPath(path)))
                                Console.instance.Print($"<color=red>ERROR: {path} doesn't exist</color>");
                            else
                                BetterContinents.Settings.SetRoughmapPath(path);
                        }),
                        getter: () => BetterContinents.Settings.GetRoughmapPath());
                    group.AddValue<float>("bl", "Roughmap Blend", "roughmap blend",
                        defaultValue: 1f, minValue: 0, maxValue: 1,
                        setter: SetHeightmapValue<float>(value => BetterContinents.Settings.RoughmapBlend = value),
                        getter: () => BetterContinents.Settings.RoughmapBlend);
                });
                bc.AddGroup("b", "Biomemap", "biomemap settings, get more info with 'bc param b help'", group =>
                {
                    group.AddValue<string>("fn", "Biomemap Filename",
                        "set biomemap filename (full path including filename, or nothing to disable)",
                        defaultValue: string.Empty,
                        setter: SetHeightmapValue<string>(path =>
                        {
                            if (string.IsNullOrEmpty(path))
                            {
                                BetterContinents.Settings.DisableBiomemap();
                                Console.instance.Print($"<color=orange>Biomemap disabled!</color>");
                            }
                            else if (!File.Exists(BetterContinents.CleanPath(path)))
                                Console.instance.Print($"<color=red>ERROR: {path} doesn't exist</color>");
                            else
                                BetterContinents.Settings.SetBiomemapPath(path);
                        }),
                        getter: () => BetterContinents.Settings.GetBiomemapPath());
                });
                bc.AddGroup("s", "Spawnmap", "spawnmap settings, get more info with 'bc param s help'", group =>
                {
                    group.AddValue<string>("fn", "Spawnmap Filename",
                        "set spawnmap filename (full path including filename, or nothing to disable)",
                        defaultValue: string.Empty,
                        setter: SetHeightmapValue<string>(path =>
                        {
                            if (string.IsNullOrEmpty(path))
                            {
                                BetterContinents.Settings.DisableSpawnmap();
                                Console.instance.Print($"<color=orange>Spawnmap disabled!</color>");
                                Console.instance.Print(
                                    $"<color=orange>INFO: Use 'bc regenloc' to update the location spawns in the world</color>");
                            }
                            else if (!File.Exists(BetterContinents.CleanPath(path)))
                                Console.instance.Print($"<color=red>ERROR: {path} doesn't exist</color>");
                            else
                            {
                                BetterContinents.Settings.SetSpawnmapPath(path);
                                Console.instance.Print(
                                    $"<color=orange>INFO: Use 'bc regenloc' to update the location spawns in the world</color>");
                            }
                        }),
                        getter: () => BetterContinents.Settings.GetSpawnmapPath());
                });
                bc.AddGroup("fo", "Forest", "forest settings, get more info with 'bc param fo help'", group =>
                {
                    group.AddValue<float>("sc", "Forest Scale", "forest scale",
                        defaultValue: 0.5f, minValue: 0f, maxValue: 1f,
                        setter: SetHeightmapValue<float>(value => BetterContinents.Settings.ForestScaleFactor = value),
                        getter: () => BetterContinents.Settings.ForestScaleFactor);
                    group.AddValue<float>("am", "Forest Amount", "forest amount",
                        defaultValue: 0.5f, minValue: 0f, maxValue: 1f,
                        setter: SetHeightmapValue<float>(value => BetterContinents.Settings.ForestAmount = value),
                        getter: () => BetterContinents.Settings.ForestAmount);
                    group.AddValue<bool>("ffo", "Forest Factor Override All", "forest factor override all trees",
                        setter: SetHeightmapValue<bool>(value =>
                        {
                            BetterContinents.Settings.ForestFactorOverrideAllTrees = value;
                            Console.instance.Print(
                                "<color=orange>NOTE: You need to reload the world to apply this change to the forest factor override!</color>");
                        }),
                        getter: () => BetterContinents.Settings.ForestFactorOverrideAllTrees);
                    group.AddValue<string>("fn", "Forestmap Filename",
                        "set forestmap filename (full path including filename, or nothing to disable)",
                        defaultValue: string.Empty,
                        setter: SetHeightmapValue<string>(path =>
                        {
                            if (string.IsNullOrEmpty(path))
                            {
                                BetterContinents.Settings.DisableForestmap();
                                Console.instance.Print($"<color=orange>Forestmap disabled!</color>");
                            }
                            else if (!File.Exists(BetterContinents.CleanPath(path)))
                                Console.instance.Print($"<color=red>ERROR: {path} doesn't exist</color>");
                            else
                                BetterContinents.Settings.SetForestmapPath(path);
                        }),
                        getter: () => BetterContinents.Settings.GetForestmapPath());
                    group.AddValue<float>("mu", "Forestmap Multiply", "forestmap multiply",
                        defaultValue: 1f, minValue: 0f, maxValue: 1f,
                        setter: SetHeightmapValue<float>(value => BetterContinents.Settings.ForestmapMultiply = value),
                        getter: () => BetterContinents.Settings.ForestmapMultiply);
                    group.AddValue<float>("mu", "Forestmap Add", "forestmap add",
                        defaultValue: 0f, minValue: 0f, maxValue: 1f,
                        setter: SetHeightmapValue<float>(value => BetterContinents.Settings.ForestmapAdd = value),
                        getter: () => BetterContinents.Settings.ForestmapAdd);
                });
                // bc.AddGroup("ri", "ridge settings, get more info with 'bc param ri help'", 
                // subcmd =>
                // {
                //     AddHeightmapSubcommand(subcmd, "mh", "ridges max height", "(between 0 and 1)", args =>
                //     {
                //         BetterContinents.Settings.MaxRidgeHeight = float.Parse(args);
                //     });
                //     AddHeightmapSubcommand(subcmd, "si", "ridge size", "(between 0 and 1)", args => BetterContinents.Settings.RidgeSize = float.Parse(args));
                //     AddHeightmapSubcommand(subcmd, "bl", "ridge blend", "(between 0 and 1)", args => BetterContinents.Settings.RidgeBlend = float.Parse(args));
                //     AddHeightmapSubcommand(subcmd, "am", "ridge amount", "(between 0 and 1)", args => BetterContinents.Settings.RidgeAmount = float.Parse(args));
                // });
                bc.AddGroup("st", "Start Position", "start position settings, get more info with 'bc param st help'",
                    group =>
                    {
                        group.AddValue<bool>("os", "Override Start Position", "override start position",
                            setter: SetHeightmapValue<bool>(value =>
                                BetterContinents.Settings.OverrideStartPosition = value),
                            getter: () => BetterContinents.Settings.OverrideStartPosition);
                        group.AddValue<float>("x", "Start Position X", "start position x",
                            defaultValue: 1f, minValue: 0f, maxValue: 1f,
                            setter: SetHeightmapValue<float>(value =>
                            {
                                BetterContinents.Settings.StartPositionX = value;
                                Console.instance.Print(
                                    $"<color=orange>INFO: Use 'bc regenloc' to update the location spawns in the world (including the start location)</color>");
                            }),
                            getter: () => BetterContinents.Settings.StartPositionX);
                        group.AddValue<float>("y", "Start Position Y", "start position y",
                            defaultValue: 1f, minValue: 0f, maxValue: 1f,
                            setter: SetHeightmapValue<float>(value =>
                            {
                                BetterContinents.Settings.StartPositionY = value;
                                Console.instance.Print(
                                    $"<color=orange>INFO: Use 'bc regenloc' to update the location spawns in the world (including the start location)</color>");
                            }),
                            getter: () => BetterContinents.Settings.StartPositionY);
                    });

                void AddNoiseCommands(Command.SubcommandBuilder group, NoiseStackSettings.NoiseSettings settings,
                    bool isWarp = false, bool isMask = false)
                {
                    // Basic
                    group.AddValue("nt", "Noise Type", "noise type",
                        defaultValue: FastNoiseLite.NoiseType.OpenSimplex2,
                        setter: SetHeightmapValue<FastNoiseLite.NoiseType>(value => settings.NoiseType = value),
                        getter: () => settings.NoiseType);
                    group.AddValue("fq", "Frequency X", "frequency x",
                        defaultValue: 0.0005f,
                        setter: SetHeightmapValue<float>(value => settings.Frequency = value),
                        getter: () => settings.Frequency);
                    group.AddValue("asp", "Aspect Ratio", "scales y dimension relative to x",
                        defaultValue: 1,
                        setter: SetHeightmapValue<float>(value => settings.Aspect = value),
                        getter: () => settings.Aspect);

                    // Fractal
                    group.AddValue("ft", "Fractal Type", "fractal type",
                        defaultValue: isWarp ? FastNoiseLite.FractalType.None : FastNoiseLite.FractalType.FBm,
                        list: isWarp
                            ? NoiseStackSettings.NoiseSettings.WarpFractalTypes
                            : NoiseStackSettings.NoiseSettings.NonFractalTypes,
                        setter: SetHeightmapValue<FastNoiseLite.FractalType>(value => settings.FractalType = value),
                        getter: () => settings.FractalType);

                    if (settings.FractalType != FastNoiseLite.FractalType.None)
                    {
                        group.AddValue<int>("fo", "Fractal Octaves", "fractal octaves",
                            defaultValue: 4, minValue: 1, maxValue: 10,
                            setter: SetHeightmapValue<int>(value => settings.FractalOctaves = value),
                            getter: () => settings.FractalOctaves);
                        group.AddValue("fl", "Fractal Lacunarity", "fractal lacunarity",
                            defaultValue: 2, minValue: 0, maxValue: 10,
                            setter: SetHeightmapValue<float>(value => settings.FractalLacunarity = value),
                            getter: () => settings.FractalLacunarity);
                        group.AddValue("fg", "Fractal Gain", "fractal gain",
                            defaultValue: 0.5f, minValue: 0, maxValue: 2,
                            setter: SetHeightmapValue<float>(value => settings.FractalGain = value),
                            getter: () => settings.FractalGain);
                        group.AddValue("ws", "Weighted Strength", "weighted strength",
                            defaultValue: 0, minValue: -2, maxValue: 2,
                            setter: SetHeightmapValue<float>(value => settings.FractalWeightedStrength = value),
                            getter: () => settings.FractalWeightedStrength);
                        if (settings.FractalType == FastNoiseLite.FractalType.PingPong)
                        {
                            group.AddValue("ps", "Ping-Pong Strength", "ping-pong strength",
                                defaultValue: 2, minValue: 0, maxValue: 10,
                                setter: SetHeightmapValue<float>(value => settings.FractalPingPongStrength = value),
                                getter: () => settings.FractalPingPongStrength);
                        }
                    }

                    if (settings.NoiseType == FastNoiseLite.NoiseType.Cellular)
                    {
                        // Cellular
                        group.AddValue("cf", "Cellular Distance Function", "cellular distance function",
                            defaultValue: FastNoiseLite.CellularDistanceFunction.Euclidean,
                            setter: SetHeightmapValue<FastNoiseLite.CellularDistanceFunction>(value =>
                                settings.CellularDistanceFunction = value),
                            getter: () => settings.CellularDistanceFunction);
                        group.AddValue("ct", "Cellular Return Type", "cellular return type",
                            defaultValue: FastNoiseLite.CellularReturnType.Distance2Div,
                            setter: SetHeightmapValue<FastNoiseLite.CellularReturnType>(value =>
                                settings.CellularReturnType = value),
                            getter: () => settings.CellularReturnType);
                        group.AddValue("cj", "Cellular Jitter", "cellular jitter",
                            defaultValue: 1, minValue: 0, maxValue: 2,
                            setter: SetHeightmapValue<float>(value => settings.CellularJitter = value),
                            getter: () => settings.CellularJitter);
                    }

                    if (isWarp)
                    {
                        // Warp
                        group.AddValue("dt", "Domain Warp Type", "domain warp type",
                            defaultValue: FastNoiseLite.DomainWarpType.OpenSimplex2,
                            setter: SetHeightmapValue<FastNoiseLite.DomainWarpType>(value =>
                                settings.DomainWarpType = value),
                            getter: () => settings.DomainWarpType);
                        group.AddValue("da", "Domain Warp Amp", "domain warp amp",
                            defaultValue: 50, minValue: 0, maxValue: 20000,
                            setter: SetHeightmapValue<float>(value => settings.DomainWarpAmp = value),
                            getter: () => settings.DomainWarpAmp);
                    }

                    // Filters
                    group.AddValue("in", "Invert", "invert",
                        setter: SetHeightmapValue<bool>(value => settings.Invert = value),
                        getter: () => settings.Invert);
                    
                    group.AddValue("ust", "Use Smooth Threshold", "use smooth threshold",
                        setter: SetHeightmapValue<bool>(value => settings.UseSmoothThreshold = value),
                        getter: () => settings.UseSmoothThreshold);
                    group.AddValue("sts", "Smooth Threshold Start", "smooth threshold start",
                        defaultValue: 0, minValue: -1, maxValue: 1,
                        getter: () => settings.SmoothThresholdStart,
                        setter: SetHeightmapValue<float>(value => settings.SmoothThresholdStart = value));
                    group.AddValue("ste", "Smooth Threshold End", "smooth threshold end",
                        defaultValue: 1, minValue: -1, maxValue: 1,
                        getter: () => settings.SmoothThresholdEnd,
                        setter: SetHeightmapValue<float>(value => settings.SmoothThresholdEnd = value));
                    
                    group.AddValue("uth", "Use Threshold", "use threshold",
                        setter: SetHeightmapValue<bool>(value => settings.UseThreshold = value),
                        getter: () => settings.UseThreshold);
                    group.AddValue("th", "Threshold", "threshold",
                        defaultValue: 0, minValue: 0, maxValue: 1,
                        getter: () => settings.Threshold,
                        setter: SetHeightmapValue<float>(value => settings.Threshold = value));
                    
                    group.AddValue("ura", "Use Range", "use range",
                        setter: SetHeightmapValue<bool>(value => settings.UseRange = value),
                        getter: () => settings.UseRange);
                    group.AddValue("ras", "Range Start", "range start",
                        defaultValue: 0, minValue: -1, maxValue: 1,
                        getter: () => settings.RangeStart,
                        setter: SetHeightmapValue<float>(value => settings.RangeStart = value));
                    group.AddValue("rae", "Range End", "range end",
                        defaultValue: 1, minValue: -1, maxValue: 1,
                        getter: () => settings.RangeEnd,
                        setter: SetHeightmapValue<float>(value => settings.RangeEnd = value));
                    
                    group.AddValue("uop", "Use Opacity", "use opacity",
                        setter: SetHeightmapValue<bool>(value => settings.UseOpacity = value),
                        getter: () => settings.UseOpacity);
                    group.AddValue("op", "Opacity", "opacity",
                        defaultValue: 1, minValue: 0, maxValue: 1,
                        getter: () => settings.Threshold,
                        setter: SetHeightmapValue<float>(value => settings.Threshold = value));
                    
                    group.AddValue("blm", "Blend Mode", "how to apply this layer to the previous one",
                        defaultValue: BlendOperations.BlendModeType.Overlay,
                        setter: SetHeightmapValue<BlendOperations.BlendModeType>(value =>
                            settings.BlendMode = value),
                        getter: () => settings.BlendMode);
                }

                bc.AddGroup("hl", "Height Layer Settings", "height layer settings",
                    hl =>
                    {
                        var baseNoise = BetterContinents.Settings.BaseHeightNoise;
                        // hl.AddValue<int>("n", "Number of Layers", "set number of layers",
                        //         defaultValue: 1, minValue: 1, maxValue: 5,
                        //         getter: () => baseNoise.NoiseLayers.Count,
                        //         setter: SetHeightmapValue<int>(val => baseNoise.SetNoiseLayerCount(val)))
                        //     .CustomDrawer(cmd =>
                        //     {
                        //         GUILayout.BeginHorizontal();
                        //         if(baseNoise.NoiseLayers.Count > )
                        //         if (GUILayout.Button("-"))
                        //         {
                        //             
                        //         }
                        //         GUILayout.EndHorizontal();
                        //     });

                        hl.AddCommand("add", "Add Layer", "", HeightmapCommand(_ => baseNoise.AddNoiseLayer()));

                        for (int i = 0; i < baseNoise.NoiseLayers.Count; i++)
                        {
                            int index = i;
                            var noiseLayer = baseNoise.NoiseLayers[index];
                            hl.AddGroup(index.ToString(), $"layer {index}", $"layer {index} settings", l =>
                            {
                                l.AddGroup("npreset", "Apply Noise Preset", "", preset =>
                                {
                                    preset.AddCommand("def", "Default", "General noise layer",
                                        HeightmapCommand(_ =>
                                            noiseLayer.noiseSettings = NoiseStackSettings.NoiseSettings.Default()));
                                    preset.AddCommand("ri", "Ridges", "Ridged noise",
                                        HeightmapCommand(_ =>
                                            noiseLayer.noiseSettings = NoiseStackSettings.NoiseSettings.Ridged()));
                                });
                                l.AddGroup("n", "Noise", $"layer {index} noise settings", nm
                                        => AddNoiseCommands(nm, noiseLayer.noiseSettings))
                                    .UIBackgroundColor(new Color32(0xCE, 0xB3, 0xAB, 0x7f));
                                l.AddGroup("nw", "Noise Warp", $"layer {index} noise warp settings", nm =>
                                {
                                    nm.AddValue<bool>("on", "Enabled", $"layer {index} noise warp enabled",
                                        defaultValue: false,
                                        setter: SetHeightmapValue<bool>(value =>
                                        {
                                            if (value && noiseLayer.noiseWarpSettings == null)
                                                noiseLayer.noiseWarpSettings =
                                                    NoiseStackSettings.NoiseSettings.DefaultWarp();
                                            else if (!value)
                                                noiseLayer.noiseWarpSettings = null;
                                        }),
                                        getter: () => noiseLayer.noiseWarpSettings != null);
                                    if (noiseLayer.noiseWarpSettings != null)
                                    {
                                        AddNoiseCommands(nm, noiseLayer.noiseWarpSettings, isWarp: true);
                                    }
                                }).UIBackgroundColor(new Color32(0xCA, 0xAE, 0xA5, 0x7f));
                                l.AddValue<int>(null, $"Noise layer {index} preview", $"Noise layer {index} preview")
                                    .CustomDrawer(_ => DrawNoisePreview(index));

                                if (index > 0)
                                {
                                    l.AddGroup("mpreset", "Apply Mask Preset", "", preset =>
                                    {
                                        preset.AddCommand("def", "Default", "General mask layer", HeightmapCommand(_ =>
                                        {
                                            noiseLayer.maskSettings = NoiseStackSettings.NoiseSettings.Default();
                                            noiseLayer.maskSettings.SmoothThresholdStart = 0.6f;
                                            noiseLayer.maskSettings.SmoothThresholdEnd = 0.75f;
                                        }));
                                        preset.AddCommand("25%", "25%", "About 25% coverage with smooth threshold",
                                            HeightmapCommand(_ =>
                                            {
                                                noiseLayer.maskSettings = NoiseStackSettings.NoiseSettings.Default();
                                                noiseLayer.maskSettings.SmoothThresholdStart = 0.6f;
                                                noiseLayer.maskSettings.SmoothThresholdEnd = 0.75f;
                                            }));
                                        preset.AddCommand("ri", "Ridges", "Warped with smooth threshold",
                                            HeightmapCommand(_ =>
                                            {
                                                noiseLayer.maskSettings = NoiseStackSettings.NoiseSettings.Default();
                                                noiseLayer.maskSettings.SmoothThresholdStart = 0.6f;
                                                noiseLayer.maskSettings.SmoothThresholdEnd = 0.75f;
                                                noiseLayer.maskWarpSettings =
                                                    NoiseStackSettings.NoiseSettings.DefaultWarp();
                                            }));
                                    });

                                    l.AddGroup("m", "Mask", $"layer {index} mask settings", nm =>
                                    {
                                        nm.AddValue<bool>("on", "Enabled", $"layer {index} mask enabled",
                                            defaultValue: false,
                                            setter: SetHeightmapValue<bool>(value =>
                                            {
                                                if (value && noiseLayer.maskSettings == null)
                                                    noiseLayer.maskSettings =
                                                        NoiseStackSettings.NoiseSettings.Default();
                                                else if (!value)
                                                    noiseLayer.maskSettings = noiseLayer.maskWarpSettings = null;
                                            }),
                                            getter: () => noiseLayer.maskSettings != null);
                                        if (noiseLayer.maskSettings != null)
                                        {
                                            AddNoiseCommands(nm, noiseLayer.maskSettings, isMask: true);
                                        }
                                    }).UIBackgroundColor(new Color32(0xBA, 0xA5, 0xFF, 0x7f));
                                    if (noiseLayer.maskSettings != null)
                                    {
                                        l.AddGroup("mw", "Mask Warp", $"layer {index} mask warp settings", nm =>
                                        {
                                            nm.AddValue<bool>("on", "Enabled", $"layer {index} mask warp enabled",
                                                defaultValue: false,
                                                setter: SetHeightmapValue<bool>(value =>
                                                {
                                                    if (value && noiseLayer.maskWarpSettings == null)
                                                        noiseLayer.maskWarpSettings =
                                                            NoiseStackSettings.NoiseSettings.DefaultWarp();
                                                    else if (!value)
                                                        noiseLayer.maskWarpSettings = null;
                                                }),
                                                getter: () => noiseLayer.maskWarpSettings != null);
                                            if (noiseLayer.maskWarpSettings != null)
                                            {
                                                AddNoiseCommands(nm, noiseLayer.maskWarpSettings, isWarp: true);
                                            }
                                        }).UIBackgroundColor(new Color32(0xB1, 0x99, 0xFF, 0x7f));
                                        l.AddValue<int>(null, $"Mask layer {index} preview",
                                                $"Mask layer {index} preview")
                                            .CustomDrawer(_ => DrawMaskPreview(index));
                                    }
                                }

                                l.AddCommand("delete", "Delete Layer", "",
                                        HeightmapCommand(_ => baseNoise.NoiseLayers.Remove(noiseLayer)))
                                    .UIBackgroundColor(new Color(0.5f, 0.1f, 0.1f));
                                if (index > 0 && index < baseNoise.NoiseLayers.Count - 1)
                                {
                                    l.AddCommand("down", "Move Down", "Swap this layer with the one below",
                                        HeightmapCommand(_ =>
                                        {
                                            var other = baseNoise.NoiseLayers[index + 1];
                                            baseNoise.NoiseLayers[index + 1] = noiseLayer;
                                            baseNoise.NoiseLayers[index] = other;
                                        }));
                                }
                            });
                        }

                        hl.AddValue<int>(null, $"Final preview", "preview of final heightmap")
                            .CustomDrawer(_ => DrawNoisePreview(baseNoise.NoiseLayers.Count));
                    }).UIBackgroundColor(new Color32(0xB4, 0x9A, 0x67, 0x7f));

                // AddHeightmapSubcommand(command, "num", "height noise layer count", "(count from 0 to 4)", args => BetterContinents.Settings.BaseHeightNoise.SetNoiseLayerCount(int.Parse(args)));
                // for (int i = 0; i < 4; i++)
                // {
                //     int index = i;
                //     command.AddSubcommand(index.ToString(), $"height layer {index}", subcmdConfig: subcmdLayer =>
                //     {
                //         subcmdLayer.AddSubcommand("n", $"height layer {index} noise", 
                //             subcmdConfig: subcmdLayerPart => AddNoiseCommands(
                //                 (cmd, desc, args, action, getValue) => AddHeightmapSubcommand(subcmdLayerPart, cmd, desc, args, action, getValue),
                //                 () => BetterContinents.Settings.BaseHeightNoise.NoiseLayers[index].noiseSettings));
                //         subcmdLayer.AddSubcommand("nw", $"height layer {index} noise domain warp", 
                //             subcmdConfig: subcmdLayerPart =>
                //             {
                //                 AddHeightmapSubcommand(subcmdLayerPart, "on", $"enable height layer {index} noise domain warp", "", 
                //                     _ => BetterContinents.Settings.BaseHeightNoise.NoiseLayers[index].noiseWarpSettings ??= NoiseStackSettings.NoiseSettings.Default());
                //                 AddHeightmapSubcommand(subcmdLayerPart, "off", $"disable height layer {index} noise domain warp", "", 
                //                     _ => BetterContinents.Settings.BaseHeightNoise.NoiseLayers[index].noiseWarpSettings = null);
                //                 AddNoiseCommands(
                //                     (cmd, desc, args, action, getValue) =>
                //                         AddHeightmapSubcommand(subcmdLayerPart, cmd, desc, args, action,
                //                             getValue),
                //                     () => BetterContinents.Settings.BaseHeightNoise.NoiseLayers[index]
                //                         .noiseWarpSettings);
                //             });
                //         subcmdLayer.AddSubcommand("m", $"height layer {index} mask", 
                //             subcmdConfig: subcmdLayerPart =>
                //             {
                //                 AddHeightmapSubcommand(subcmdLayerPart, "on", $"enable height layer {index} mask", "", 
                //                     _ => BetterContinents.Settings.BaseHeightNoise.NoiseLayers[index].maskSettings ??= NoiseStackSettings.NoiseSettings.Default());
                //                 AddHeightmapSubcommand(subcmdLayerPart, "off", $"disable height layer {index} mask", "", 
                //                     _ =>
                //                     {
                //                         BetterContinents.Settings.BaseHeightNoise.NoiseLayers[index].maskSettings = null;
                //                         BetterContinents.Settings.BaseHeightNoise.NoiseLayers[index].maskWarpSettings = null;
                //                     });
                //                 AddNoiseCommands(
                //                     (cmd, desc, args, action, getValue) =>
                //                         AddHeightmapSubcommand(subcmdLayerPart, cmd, desc, args, action,
                //                             getValue),
                //                     () => BetterContinents.Settings.BaseHeightNoise.NoiseLayers[index].maskSettings);
                //             });
                //         subcmdLayer.AddSubcommand("mw", $"height layer {index} mask domain warp", 
                //             subcmdConfig: subcmdLayerPart =>
                //             {
                //                 AddHeightmapSubcommand(subcmdLayerPart, "on", $"enable height layer {index} mask domain warp", "", 
                //                     _ => BetterContinents.Settings.BaseHeightNoise.NoiseLayers[index].maskWarpSettings ??= NoiseStackSettings.NoiseSettings.Default());
                //                 AddHeightmapSubcommand(subcmdLayerPart, "off", $"disable height layer {index} mask domain warp", "", 
                //                     _ => BetterContinents.Settings.BaseHeightNoise.NoiseLayers[index].maskWarpSettings = null);
                //                 AddNoiseCommands(
                //                     (cmd, desc, args, action, getValue) =>
                //                         AddHeightmapSubcommand(subcmdLayerPart, cmd, desc, args, action,
                //                             getValue),
                //                     () => BetterContinents.Settings.BaseHeightNoise.NoiseLayers[index].maskWarpSettings);
                //             });
                //     });
                // }
            });
        }
        
        public static void RunConsoleCommand(string text)
        {
            rootCommand.Run(text);
        }

        public static T GetDelegate<T>(Type type, string method) where T : Delegate 
            => AccessTools.MethodDelegate<T>(AccessTools.Method(type, method));
        
        private static Action<string> HeightmapCommand(Action<string> command) =>
            value =>
            {
                GameUtils.BeginTerrainChanges();
                command(value);
                BetterContinents.WorldGeneratorPatch.ApplyNoiseSettings();
                noisePreviewTextures = null;
                maskPreviewTextures = null;
                GameUtils.EndTerrainChanges();
            };
        
        private static Action<T> SetHeightmapValue<T>(Action<T> setValue) =>
            value =>
            {
                GameUtils.BeginTerrainChanges();
                setValue(value);
                BetterContinents.WorldGeneratorPatch.ApplyNoiseSettings();
                noisePreviewTextures = null;
                maskPreviewTextures = null;
                GameUtils.EndTerrainChanges();
            };
        
        private static readonly Command rootCommand;
        private static List<Texture> noisePreviewTextures = null;
        private static List<Texture> maskPreviewTextures = null;
        private static readonly List<bool> noisePreviewExpanded = new List<bool>();

        private const int NoisePreviewSize = 512;
        private static (Texture noise, Texture mask) GetPreviewTextures(int layerIndex)
        {
            if (noisePreviewTextures == null)
            {
                var noise = BetterContinents.WorldGeneratorPatch.BaseHeightNoise;
                noisePreviewTextures = new List<Texture>();
                maskPreviewTextures = new List<Texture>();
                for (int i = 0; i < noise.layers.Count; i++)
                {
                    noisePreviewTextures.Add(CreateNoisePreview((x, y) => noise.layers[i].noise.GetNoise(x, y),
                        NoisePreviewSize));
                    maskPreviewTextures.Add(noise.layers[i].mask != null
                        ? CreateNoisePreview((x, y) => noise.layers[i].mask.Value.GetNoise(x, y), NoisePreviewSize)
                        : null);
                }

                noisePreviewTextures.Add(CreateNoisePreview((x, y) => noise.Apply(x, y), NoisePreviewSize));
                maskPreviewTextures.Add(null);
            }

            return (noisePreviewTextures[layerIndex], maskPreviewTextures[layerIndex]);
        }

        private static Texture CreateNoisePreview(Func<float, float, float> noiseFn, int size = 128)
        {
            var tex = new Texture2D(size, size);
            var pixels = new Color32[size * size];
            GameUtils.SimpleParallelFor(4, 0, size, y =>
            {
                float yp = 2f * (y / (float) size - 0.5f) * BetterContinents.WorldSize;
                for (int x = 0; x < size; ++x)
                {
                    float xp = 2f * (x / (float) size - 0.5f) * BetterContinents.WorldSize;
                    byte val = (byte) Mathf.Clamp((int) (noiseFn(xp, yp) * 255f), 0, 255);
                    pixels[y * size + x] = new Color32(val, val, val, byte.MaxValue);
                }
            });

            tex.SetPixels32(pixels);
            tex.Apply(false);
            return tex;
        }
        
        private static void DrawNoisePreview(int i)
        {
            var (noiseTexture, maskTexture) = GetPreviewTextures(i);

            noisePreviewExpanded.Resize(Mathf.Max(noisePreviewExpanded.Count, noisePreviewTextures.Count));
            noisePreviewExpanded[i] = GUILayout.Toggle(noisePreviewExpanded[i], i == noisePreviewTextures.Count - 1 ? "Preview Final" : $"Preview Layer {i} Noise");
            if (noisePreviewExpanded[i])
            {
                GUILayout.Box(noiseTexture);
            }
        }
        
        private static void DrawMaskPreview(int i)
        {
            var (noiseTexture, maskTexture) = GetPreviewTextures(i);
            noisePreviewExpanded.Resize(Mathf.Max(noisePreviewExpanded.Count, maskPreviewTextures.Count));
            noisePreviewExpanded[i] = GUILayout.Toggle(noisePreviewExpanded[i], i == maskPreviewTextures.Count - 1 ? "Preview Final Mask" : $"Preview Layer {i} Mask");
            if (noisePreviewExpanded[i])
            {
                GUILayout.Box(maskTexture);
            }
        }
    }
}