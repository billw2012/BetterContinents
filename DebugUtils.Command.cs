using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

// Contains heavily modified code from https://github.com/BepInEx/BepInEx.ConfigurationManager
namespace BetterContinents
{
    public partial class DebugUtils
    {
        public class Command
        {
            private enum CommandType
            {
                Group,
                Command,
                Value
            }

            private readonly CommandType commandType;
            private readonly string cmd;
            private readonly string uiName;
            private readonly string desc;
            private readonly Type valueType;
            private readonly Command parent;

            private Color backgroundColor;
            private object defaultValue;
            private KeyValuePair<object, object>? range;
            private List<object> validValues;
            private Action<object> setValue;
            private Func<object> getValue;

            private Action<SubcommandBuilder> subCommandBuilder;
            private Action<Command> customDrawer = null;

            private static readonly Dictionary<Type, Func<string, object>> StringToTypeConverters = new()
            {
                { typeof(string), s => s },
                { typeof(float), s => float.Parse(s) },
                { typeof(int), s => int.Parse(s) },
                { typeof(bool), s => bool.Parse(s) },
                { typeof(float?), s => float.TryParse(s, out var value) ? (object)value : null },
                { typeof(int?), s => int.TryParse(s, out var value) ? (object)value : null },
                { typeof(bool?), s => bool.TryParse(s, out var value) ? (object)value : null },
                // { typeof(Vector2), s =>
                //     {
                //         var parts = s.Split(new [] {' '}, StringSplitOptions.RemoveEmptyEntries);
                //         return new Vector2(float.Parse(parts[0]), float.Parse(parts[1]));
                //     }},
            };

            // public static Dictionary<Type, Func<object, string>> TypeToStringConverters = new Dictionary<Type, Func<object, string>>
            // {
            //     { typeof(float?), o => $"{o ?? }" }
            // };

            private Command(CommandType commandType, Command parent, string cmd, string uiName, string desc, Type valueType = null)
            {
                this.commandType = commandType;
                this.parent = parent;
                this.cmd = cmd;
                this.uiName = uiName;
                this.desc = desc;
                this.valueType = valueType;
            }

            public Command(string cmd, string uiName, string desc) : this(CommandType.Group, null, cmd, uiName, desc) { }

            public Command(Command parent, string cmd, string uiName, string desc, Action<string> command) : this(CommandType.Command, parent, cmd, uiName, desc)
            {
                setValue = args => command((string)args);
            }

            public class SubcommandBuilder
            {
                private readonly Command parent;
                private readonly List<Command> subcommands;
                
                public SubcommandBuilder(Command parent, List<Command> subcommands)
                {
                    this.parent = parent;
                    this.subcommands = subcommands;
                }
                
                public Command AddGroup(string cmd, string uiName, string desc, Action<SubcommandBuilder> group = null)
                {
                    var newCommand = new Command(CommandType.Group, parent, cmd, uiName, desc);
                    subcommands.Add(newCommand);
                    newCommand.Subcommands(group);
                    return newCommand;
                }

                public Command AddCommand(string cmd, string uiName, string desc, Action<string> command)
                {
                    var newCommand = new Command(parent, cmd, uiName, desc, command);
                    subcommands.Add(newCommand);
                    return newCommand;
                }

                public Command AddValue(string name, string uiName, string desc, Type type)
                {
                    var newCommand = new Command(CommandType.Value, parent, name, uiName, desc, type);
                    subcommands.Add(newCommand);
                    return newCommand;                
                }
                
                public Command AddValue<T>(string name, string uiName, string desc, T defaultValue = default, Action<T> setter = null, Func<T> getter = null) where T : IComparable =>
                    AddValue(name, uiName, desc, typeof(T))
                        .Default(defaultValue)
                        .Setter(setter)
                        .Getter(getter);
                
                public Command AddValue<T>(string name, string uiName, string desc, T defaultValue, T minValue, T maxValue, Action<T> setter = null, Func<T> getter = null) where T : IComparable =>
                    AddValue(name, uiName, desc, defaultValue, setter, getter)
                        .Range(minValue, maxValue);

                public Command AddValue<T>(string name, string uiName, string desc, T defaultValue, T[] list, Action<T> setter = null, Func<T> getter = null) where T : IComparable =>
                    AddValue(name, uiName, desc, defaultValue, setter, getter)
                        .List(list);
                 
                public Command AddValueNullable<T>(string name, string uiName, string desc, T? defaultValue = default, Action<T?> setter = null, Func<T?> getter = null) where T : struct, IComparable =>
                    AddValue(name, uiName, desc, typeof(T?))
                        .Default(defaultValue)
                        .Setter(setter)
                        .Getter(getter);
                
                public Command AddValueNullable<T>(string name, string uiName, string desc, T? defaultValue, T minValue, T maxValue, Action<T?> setter = null, Func<T?> getter = null) where T : struct, IComparable =>
                    AddValueNullable<T>(name, uiName, desc, defaultValue, setter, getter)
                        .Range(minValue, maxValue);
               
                public Command AddValueNullable<T>(string name, string uiName, string desc, T? defaultValue, T[] list, Action<T?> setter = null, Func<T?> getter = null) where T : struct, IComparable, IEquatable<T> =>
                    AddValueNullable<T>(name, uiName, desc, defaultValue, setter, getter)
                        .List(list);
            }
            
            public bool Run(string text)
            {
                bool hasArgs = text.StartsWith(cmd + " ");
                if (!hasArgs && text != cmd)
                {
                    return false;
                }

                string args = hasArgs ? text.Substring(cmd.Length).Trim() : string.Empty;

                if (commandType == CommandType.Group)
                {
                    if (!hasArgs)
                    {
                        ShowSubcommandHelp();
                    }
                    else if (!GetSubcommands().Any(subcmd => subcmd.Run(args)))
                    {
                        Console.instance.Print($"<color=red>Error: argument {args} is not recognized as a subcommand of {cmd}</color>");
                        ShowSubcommandHelp();
                    }
                }
                else if (commandType == CommandType.Command)
                {
                    if (args == "help")
                    {
                        ShowSubcommandHelp();
                    }
                    else
                    {
                        setValue(args);
                    }
                }
                else if (commandType == CommandType.Value)
                {
                    if (args == "help")
                    {
                        ShowSubcommandHelp();
                    }
                    else
                    {
                        try
                        {
                            if (hasArgs)
                            {
                                var parser = StringToTypeConverters[valueType];
                                setValue(parser(text.Substring(cmd.Length).Trim()));
                            }
                            else if (getValue == null)
                            {
                                Console.instance.Print($"(value is write only, can't show the current value");
                            }
                            else
                            {
                                Console.instance.Print($"Current value of {cmd} is {GetValueString()}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.instance.Print($"{cmd} failed: {ex.Message}");
                        }
                    }
                }

                return true;
            }

            private List<Command> GetSubcommands()
            {
                var allSubcommands = new List<Command>();
                subCommandBuilder?.Invoke(new SubcommandBuilder(this, allSubcommands));
                return allSubcommands;
            }

            public Command Default(object defaultValue)
            {
                if (defaultValue != null && defaultValue.GetType() != valueType)
                {
                    throw new Exception($"Type of default value must match type of the subcommand {valueType}");
                }
                this.defaultValue = defaultValue;
                return this;
            }

            public Command Range<T>(T from, T to) where T : IComparable
            {
                if (typeof(T) != valueType && typeof(T) != Nullable.GetUnderlyingType(valueType))
                {
                    throw new Exception($"Type of range values must match type of the subcommand {valueType}");
                }

                range = new KeyValuePair<object, object>(from, to);
                return this;
            }
            
            public Command List<T>(params T[] values)
            {
                if (typeof(T) != valueType && typeof(T) != Nullable.GetUnderlyingType(valueType))
                {
                    throw new Exception($"Type of range values must match type of the subcommand {valueType}");
                }

                validValues = values.Cast<object>().ToList();
                return this;
            }
            
            public Command Setter<T>(Action<T> setValue)
            {
                if (setValue != null)
                {
                    if (typeof(T) != valueType)
                    {
                        throw new Exception($"Type of setter parameter must match type of the subcommand {valueType}");
                    }

                    this.setValue = value => setValue((T) value);
                }

                return this;
            }

            public Command Getter<T>(Func<T> getValue)
            {
                if (getValue != null)
                {
                    if (typeof(T) != valueType)
                    {
                        throw new Exception($"Type of getter parameter must match type of the subcommand {valueType}");
                    }

                    this.getValue = () => getValue();
                }

                return this;
            }

            public Command UIBackgroundColor(Color32 color)
            {
                backgroundColor = color;
                return this;
            }

            public Command Subcommands(Action<SubcommandBuilder> builder)
            {
                subCommandBuilder = builder;
                return this;
            }

            public Command CustomDrawer(Action<Command> drawer)
            {
                customDrawer = drawer;
                return this;
            }

            private const string NullValueStr = "(disabled)";
            
            private string GetValueString()
                => getValue == null
                    ? "(not a value)"
                    : $"<size=18><b><color=#55ff55ff>{getValue() ?? NullValueStr}</color></b></size>";
            
            public void ShowHelp()
            {
                var helpString = $"<size=18><b><color=cyan>{GetFullCmdName()}</color></b></size>";
                if (range != null)
                {
                    helpString += $" ({range.Value.Key} - {range.Value.Value})";
                }
                else if (validValues != null)
                {
                    var valuesStr = string.Join(", ", validValues.Select(v => v.ToString()));
                    helpString += $" ({valuesStr})";
                }
                else if (valueType != null)
                {
                    helpString += " " + $"({valueType.Name})";
                }

                if (getValue != null)
                {
                    helpString += $" -- {GetValueString()}";
                }

                helpString += $" -- <size=15>{desc}</size>";

                Console.instance.Print($"    " + helpString);

                // if (getValue != null)
                // {
                //     Console.instance.Print($"        <size=15><b><color=#55ff55ff>{getValue()}</color></b></size>");
                // }
            }

            private string GetFullCmdName()
            {
                var cmdStack = new List<string>();
                var curr = this;
                while (curr != null)
                {
                    cmdStack.Insert(0, curr.cmd);
                    curr = curr.parent;
                }

                var fullCmd = string.Join(" ", cmdStack);
                return fullCmd;
            }

            public void ShowSubcommandHelp()
            {
                ShowHelp();
                foreach (var subcmd in GetSubcommands())
                {
                    subcmd.ShowHelp();
                }
            }

            public override string ToString() => $"{nameof(cmd)}: {cmd}, {nameof(desc)}: {desc}, {nameof(valueType)}: {valueType}";
            
            public static class CmdUI
            {
                private static Rect settingWindowRect;
                private static Vector2 settingWindowScrollPos;
                
                private static readonly Dictionary<Type, Action<Command>> DefaultDrawers = new() {
                    {typeof(bool), DrawBoolField},
                    // {typeof(Color), DrawColor },
                    // {typeof(Vector2), DrawVector2 },
                    // {typeof(Vector3), DrawVector3 },
                    // {typeof(Vector4), DrawVector4 },
                    // {typeof(Quaternion), DrawQuaternion },
                };

                public static void DrawSettingsWindow()
                {
                    settingWindowRect = GUILayout.Window(
                        (ModInfo.Name + "SettingsWindow").GetHashCode(),
                        settingWindowRect,
                        Window,
                        "Better Continents",
                        GUILayout.MinWidth(Mathf.Max(LeftColumnWidth + RightColumnWidth + 100, NoisePreviewSize + 100)),
                        GUILayout.MinHeight(Screen.height - 250)
                        );
                    if (settingWindowRect.Contains(Input.mousePosition))
                    {
                        Input.ResetInputAxes();
                    }
                }

                private static void DrawUI(Command cmd)
                {
                    if (cmd.customDrawer != null)
                    {
                        cmd.customDrawer(cmd);
                        return;
                    }
                    
                    var allSubcommands = cmd.GetSubcommands();
                    var label = new GUIContent(cmd.uiName, cmd.desc);
                    var state = GetUIState(cmd);

                    switch (cmd.commandType)
                    {
                        case CommandType.Group:
                            var groupStyle = GUI.skin.box;
                            if (cmd.backgroundColor != default)
                            {
                                groupStyle = new GUIStyle(GUI.skin.box)
                                {
                                    normal = {background = UI.CreateFillTexture(cmd.backgroundColor)}
                                };
                            }

                            GUILayout.BeginVertical(groupStyle);
                            
                            // We have no parent then we are the root command and should skip the header and just show subcommands always
                            if (cmd.parent != null && DrawGroupHeader(label, state.uiExpanded)) state.uiExpanded = !state.uiExpanded;
                            if (cmd.parent == null || state.uiExpanded)
                            {
                                foreach (var subcmd in allSubcommands)
                                {
                                    DrawUI(subcmd);
                                    GUILayout.Space(2);
                                }
                            }

                            GUILayout.EndVertical();
                            
                            break;
                        case CommandType.Command:
                            var buttonStyle = GUI.skin.button;
                            if (cmd.backgroundColor != default)
                            {
                                buttonStyle = new GUIStyle(GUI.skin.button)
                                {
                                    normal = {background = UI.CreateFillTexture(cmd.backgroundColor)}
                                };
                            }
                            
                            if (GUILayout.Button(label, buttonStyle)) {
                                cmd.setValue(null);
                            }
                            break;
                        case CommandType.Value:
                            var valueStyle = GUIStyle.none;
                            if (cmd.backgroundColor != default)
                            {
                                valueStyle = new GUIStyle(GUIStyle.none)
                                {
                                    normal = {background = UI.CreateFillTexture(cmd.backgroundColor)}
                                };
                            }

                            GUILayout.BeginHorizontal(valueStyle);
                            {
                                GUILayout.Label(label, GUILayout.Width(LeftColumnWidth), GUILayout.MaxWidth(LeftColumnWidth));
                                DrawSettingValue(cmd, state);
                            }
                            GUILayout.EndHorizontal();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                private static bool DrawCurrentDropdown()
                {
                    if (ComboBox.CurrentDropdownDrawer != null)
                    {
                        ComboBox.CurrentDropdownDrawer.Invoke();
                        ComboBox.CurrentDropdownDrawer = null;
                        return true;
                    }

                    return false;
                }

                private static void DrawTooltip(Rect area)
                {
                    if (!string.IsNullOrEmpty(GUI.tooltip))
                    {
                        var currentEvent = Event.current;

                        var style = new GUIStyle
                        {
                            normal = new GUIStyleState {textColor = Color.black, background = Texture2D.whiteTexture},
                            wordWrap = true,
                            alignment = TextAnchor.MiddleCenter
                        };

                        const int width = 400;
                        var height = style.CalcHeight(new GUIContent(GUI.tooltip), 400) + 10;

                        var x = currentEvent.mousePosition.x + width > area.width
                            ? area.width - width
                            : currentEvent.mousePosition.x;

                        var y = currentEvent.mousePosition.y + 25 + height > area.height
                            ? currentEvent.mousePosition.y - height
                            : currentEvent.mousePosition.y + 25;

                        GUI.Box(new Rect(x, y, width, height), GUI.tooltip, style);
                    }
                }

                private static void Window(int _)
                {
                    GUILayout.BeginVertical(new GUIStyle
                        {normal = new GUIStyleState {background = Texture2D.grayTexture}});

                    GUILayout.BeginHorizontal(GUI.skin.box);
                    {
                        GUILayout.Label("Better Continents World Settings", GUILayout.ExpandWidth(true));
                        if (GUILayout.Button("Close", GUILayout.ExpandWidth(false)))
                        {

                        }
                    }
                    GUILayout.EndHorizontal();

                    GUI.DragWindow(new Rect(0, 0, 10000, 20));

                    settingWindowScrollPos = GUILayout.BeginScrollView(settingWindowScrollPos, false, true);

                    GUILayout.BeginVertical();

                    DrawUI(rootCommand);

                    GUILayout.EndVertical();

                    GUILayout.EndScrollView();

                    if (!DrawCurrentDropdown())
                        DrawTooltip(settingWindowRect);

                    GUILayout.EndVertical();
                }

                private class CommandUIState
                {
                    public bool uiExpanded;
                    public ComboBox comboBox;
                    public string stringValue;
                }

                private static Dictionary<string, CommandUIState> commandUIState =
                    new Dictionary<string, CommandUIState>();

                private static CommandUIState GetUIState(Command cmd)
                {
                    var id = cmd.GetFullCmdName();
                    if (!commandUIState.TryGetValue(id, out var state))
                    {
                        state = new CommandUIState();
                        commandUIState.Add(id, state);
                    }

                    return state;
                }

                private static GUIStyle groupHeaderSkin;
                private const float LeftColumnWidth = 150;

                private const int RightColumnWidth = 250;
                // private static Rect SettingWindowRect;

                private static bool DrawGroupHeader(GUIContent title, bool isExpanded)
                {
                    groupHeaderSkin ??= new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.UpperCenter,
                        wordWrap = true,
                        stretchWidth = true,
                        fontSize = 14
                    };
                    if (!isExpanded) title.text += "...";
                    return GUILayout.Button(title, groupHeaderSkin, GUILayout.ExpandWidth(true));
                }
                
                private static void DrawSettingValue(Command cmd, CommandUIState state)
                {
                    if (cmd.customDrawer != null)
                        cmd.customDrawer(cmd);
                    //else if (this.range.HasValue)
                    //    DrawRangeField();
                    else if (cmd.validValues != null)
                        DrawListField(cmd, state);
                    else if (cmd.valueType.IsEnum)
                    {
                        if (cmd.valueType.GetCustomAttributes(typeof(FlagsAttribute), false).Any())
                            DrawFlagsField(cmd, Enum.GetValues(cmd.valueType), RightColumnWidth);
                        else
                            DrawComboboxField(cmd, state, Enum.GetValues(cmd.valueType), settingWindowRect.yMax);
                    }
                    else
                    {
                        DrawFieldBasedOnValueType(cmd, state);
                    }
                }
                
                private static void DrawListField(Command cmd, CommandUIState state)
                {
                    if (cmd.validValues.Count == 0)
                        throw new ArgumentException($"Valid values for {cmd.cmd} is declared but empty, it must have at least one value");

                    if (!cmd.valueType.IsInstanceOfType(cmd.validValues.FirstOrDefault(x => x != null)))
                        throw new ArgumentException($"Valid values for {cmd.cmd} contains a value of the wrong type");

                    DrawComboboxField(cmd, state, cmd.validValues, settingWindowRect.yMax);
                }

                private static void DrawFieldBasedOnValueType(Command cmd, CommandUIState state)
                {
                    if (DefaultDrawers.TryGetValue(cmd.valueType, out var drawMethod))
                        drawMethod(cmd);
                    else
                        DrawUnknownField(cmd, state);
                }
                
                private static void DrawUnknownField(Command cmd, CommandUIState state)
                {
                    if(state.stringValue == null)
                    {
                        var rawValue = cmd.getValue();
                        state.stringValue = rawValue == null ? "" : rawValue.ToString();
                    }

                    var name = cmd.GetFullCmdName();
                    GUI.SetNextControlName(name);
                    state.stringValue = GUILayout.TextField(state.stringValue, GUILayout.MaxWidth(RightColumnWidth));
                    if (GUI.GetNameOfFocusedControl() == name)
                    {
                        if (Event.current.isKey && Event.current.keyCode == KeyCode.Return) //GUILayout.Button("apply"))
                        {
                            cmd.setValue(Convert.ChangeType(state.stringValue, cmd.valueType, CultureInfo.InvariantCulture));
                            Event.current.Use();
                            state.stringValue = null;
                        }
                    }
                    else
                    {
                        state.stringValue = null;
                    }
                    
                    GUILayout.FlexibleSpace();
                }

                private static void DrawBoolField(Command cmd)
                {
                    var boolVal = (bool)cmd.getValue();
                    var result = GUILayout.Toggle(boolVal, boolVal ? "Enabled" : "Disabled", GUILayout.ExpandWidth(true));
                    if (result != boolVal)
                        cmd.setValue(result);
                }

                private static void DrawFlagsField(Command cmd, IList enumValues, int maxWidth)
                {
                    var currentValue = Convert.ToInt64(cmd.getValue());
                    var allValues = enumValues.Cast<Enum>().Select(x => new { name = x.ToString(), val = Convert.ToInt64(x) }).ToArray();

                    // Vertically stack Horizontal groups of the options to deal with the options taking more width than is available in the window
                    GUILayout.BeginVertical(GUILayout.MaxWidth(maxWidth));
                    {
                        for (var index = 0; index < allValues.Length;)
                        {
                            GUILayout.BeginHorizontal();
                            {
                                var currentWidth = 0;
                                for (; index < allValues.Length; index++)
                                {
                                    var value = allValues[index];

                                    // Skip the 0 / none enum value, just uncheck everything to get 0
                                    if (value.val != 0)
                                    {
                                        // Make sure this horizontal group doesn't extend over window width, if it does then start a new horiz group below
                                        var textDimension = (int)GUI.skin.toggle.CalcSize(new GUIContent(value.name)).x;
                                        currentWidth += textDimension;
                                        if (currentWidth > maxWidth)
                                            break;

                                        GUI.changed = false;
                                        var newVal = GUILayout.Toggle((currentValue & value.val) == value.val, value.name,
                                            GUILayout.ExpandWidth(false));
                                        if (GUI.changed)
                                        {
                                            var newValue = newVal ? currentValue | value.val : currentValue & ~value.val;
                                            cmd.setValue(Enum.ToObject(cmd.valueType, newValue));
                                        }
                                    }
                                }
                            }
                            GUILayout.EndHorizontal();
                        }

                        GUI.changed = false;
                    }
                    GUILayout.EndVertical();

                    // Make sure the reset button is properly spaced
                    GUILayout.FlexibleSpace();
                }

                private static void DrawComboboxField(Command cmd, CommandUIState state, IList list, float windowYmax)
                {
                    var buttonText = new GUIContent(cmd.getValue().ToString());
                    var dispRect = GUILayoutUtility.GetRect(buttonText, GUI.skin.button, GUILayout.ExpandWidth(true));

                    if (state.comboBox == null)
                    {
                        state.comboBox = new ComboBox(dispRect, buttonText, list.Cast<object>().Select(v => new GUIContent(v.ToString())).ToArray(), GUI.skin.button);
                    }
                    else
                    {
                        state.comboBox.Rect = dispRect;
                        state.comboBox.ButtonContent = buttonText;
                    }

                    state.comboBox.Show(id =>
                    {
                        if (id >= 0 && id < list.Count)
                            cmd.setValue(list[id]);
                    });
                }
            }
        }
    }
}