using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using FF14PcShortcutPadDalamud.Windows;

namespace FF14PcShortcutPadDalamud;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    internal Configuration Configuration { get; }
    internal WindowSystem WindowSystem { get; } = new("FF14PcShortcutPadDalamud");

    private readonly PadWindow padWindow;
    private readonly ConfigWindow configWindow;
    private readonly HashSet<VirtualKey> pressedKeys = [];
    private static VirtualKey[]? captureKeys;
    private string registeredCommand = string.Empty;
    private DateTime statusUntil = DateTime.MinValue;

    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLeftShift = 0xA0;
    private const int VkRightShift = 0xA1;
    private const int VkLeftControl = 0xA2;
    private const int VkRightControl = 0xA3;
    private const int VkLeftMenu = 0xA4;
    private const int VkRightMenu = 0xA5;

    private readonly struct ShortcutBinding(VirtualKey key, bool ctrl, bool alt, bool shift)
    {
        public VirtualKey Key { get; } = key;
        public bool Ctrl { get; } = ctrl;
        public bool Alt { get; } = alt;
        public bool Shift { get; } = shift;
    }

    internal string LastStatus { get; private set; } = string.Empty;
    internal bool ShouldShowStatus => DateTime.UtcNow < statusUntil && !string.IsNullOrWhiteSpace(LastStatus);
    private static bool IsInPlayableArea => ClientState.IsLoggedIn && ClientState.TerritoryType != 0;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.EnsureDefaults();

        padWindow = new PadWindow(this) { IsOpen = Configuration.MainWindowVisible };
        configWindow = new ConfigWindow(this) { IsOpen = Configuration.ConfigWindowVisible };
        WindowSystem.AddWindow(padWindow);
        WindowSystem.AddWindow(configWindow);

        RegisterMainCommand();

        Framework.Update += OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw += DrawWindows;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
    }

    public void Dispose()
    {
        Configuration.ConfigWindowVisible = configWindow.IsOpen;
        Configuration.Save();

        PluginInterface.UiBuilder.Draw -= DrawWindows;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        Framework.Update -= OnFrameworkUpdate;

        UnregisterMainCommand();
        WindowSystem.RemoveAllWindows();
        padWindow.Dispose();
        configWindow.Dispose();
    }

    internal void ToggleMainUi()
    {
        Configuration.MainWindowVisible = !Configuration.MainWindowVisible;
        padWindow.IsOpen = Configuration.MainWindowVisible && IsInPlayableArea;
        Configuration.Save();
    }

    internal void ToggleConfigUi()
    {
        configWindow.Toggle();
        Configuration.ConfigWindowVisible = configWindow.IsOpen;
        Configuration.Save();
    }

    internal void RefreshMainCommand()
    {
        RegisterMainCommand();
    }

    private void DrawWindows()
    {
        padWindow.IsOpen = Configuration.MainWindowVisible && IsInPlayableArea;
        WindowSystem.Draw();
    }

    internal unsafe void ExecuteCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            SetStatus("空コマンド");
            return;
        }

        var lines = command
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var text = line.Trim();
            ExecuteGameCommand(text);
            SetStatus($"送信: {text}");
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!IsInPlayableArea)
        {
            pressedKeys.Clear();
            return;
        }

        var hasNextKey = TryShortcutToBinding(Configuration.PageNextShortcut, out var nextKey);
        var hasPreviousKey = TryShortcutToBinding(Configuration.PagePreviousShortcut, out var previousKey);

        if (hasNextKey)
        {
            HandlePageKey(nextKey, 1);
        }

        if (hasPreviousKey && (!hasNextKey || !SameBinding(previousKey, nextKey)))
        {
            HandlePageKey(previousKey, -1);
        }

        var page = Configuration.Pages[Configuration.CurrentPageIndex];
        for (var i = 0; i < page.Buttons.Count; i++)
        {
            var button = page.Buttons[i];
            if (!TryShortcutToBinding(button.ShortcutText, out var binding))
            {
                continue;
            }

            if ((hasNextKey && SameBinding(binding, nextKey)) || (hasPreviousKey && SameBinding(binding, previousKey)))
            {
                continue;
            }

            var isPressed = IsShortcutPressed(binding);
            if (isPressed && pressedKeys.Add(binding.Key))
            {
                SetStatus($"キー: {button.ShortcutText}");
                ExecuteCommand(button.Command);
            }
            else if (!isPressed)
            {
                pressedKeys.Remove(binding.Key);
            }
        }
    }

    private void HandlePageKey(ShortcutBinding binding, int direction)
    {
        var isPressed = IsShortcutPressed(binding);
        if (isPressed && pressedKeys.Add(binding.Key))
        {
            Configuration.CurrentPageIndex = (Configuration.CurrentPageIndex + direction + Configuration.PageCount) % Configuration.PageCount;
            Configuration.Save();
            SetStatus($"Page: {Configuration.CurrentPageIndex + 1}/{Configuration.PageCount}");
        }
        else if (!isPressed)
        {
            pressedKeys.Remove(binding.Key);
        }
    }

    private static unsafe void ExecuteGameCommand(string command)
    {
        var uiModule = UIModule.Instance();
        var shell = RaptureShellModule.Instance();
        if (uiModule is null || shell is null)
        {
            Log.Warning("FF14 shell was not available for command: {Command}", command);
            return;
        }

        using var text = new Utf8String(command);
        shell->ExecuteCommandInner(&text, uiModule);
    }

    private void SetStatus(string message)
    {
        LastStatus = message;
        statusUntil = DateTime.UtcNow.AddSeconds(2);
    }

    private void RegisterMainCommand()
    {
        var command = Configuration.NormalizeCommand(Configuration.MainCommand);
        if (registeredCommand.Equals(command, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        UnregisterMainCommand();
        registeredCommand = command;
        CommandManager.AddHandler(registeredCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open FF14 PC Shortcut Pad.",
        });
    }

    private void UnregisterMainCommand()
    {
        if (string.IsNullOrWhiteSpace(registeredCommand))
        {
            return;
        }

        CommandManager.RemoveHandler(registeredCommand);
        registeredCommand = string.Empty;
    }

    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("config", StringComparison.OrdinalIgnoreCase))
        {
            ToggleConfigUi();
            return;
        }

        ToggleMainUi();
    }

    internal static bool TryCaptureShortcut(out string shortcut)
    {
        shortcut = string.Empty;
        var ctrl = IsModifierDown("CONTROL", "CTRL");
        var alt = IsModifierDown("MENU", "ALT");
        var shift = IsModifierDown("SHIFT");

        foreach (var key in GetCaptureKeys())
        {
            if (IsPhysicalKeyDown(key) || IsKeyDown(key) || IsImGuiKeyPressed(key))
            {
                shortcut = FormatShortcut(new ShortcutBinding(key, ctrl, alt, shift));
                return true;
            }
        }

        if (ctrl)
        {
            shortcut = "Ctrl";
            return true;
        }

        if (alt)
        {
            shortcut = "Alt";
            return true;
        }

        if (shift)
        {
            shortcut = "Shift";
            return true;
        }

        return false;
    }

    internal static string NormalizeShortcutText(string shortcut)
    {
        return TryShortcutToBinding(shortcut, out var binding)
            ? FormatShortcut(binding)
            : shortcut.Trim();
    }

    private static bool TryShortcutToBinding(string shortcut, out ShortcutBinding binding)
    {
        binding = default;
        if (string.IsNullOrWhiteSpace(shortcut))
        {
            return false;
        }

        var ctrl = false;
        var alt = false;
        var shift = false;
        var keyText = string.Empty;
        var trimmed = shortcut.Trim();
        var modifierText = trimmed;
        if (trimmed.EndsWith("Num+", StringComparison.OrdinalIgnoreCase))
        {
            keyText = "Num+";
            modifierText = trimmed[..^4].TrimEnd('+');
        }
        else if (trimmed.EndsWith("+", StringComparison.Ordinal) && trimmed != "+")
        {
            keyText = "+";
            modifierText = trimmed[..^1].TrimEnd('+');
        }

        var parts = string.IsNullOrWhiteSpace(modifierText)
            ? []
            : modifierText.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0 && trimmed == "+")
        {
            keyText = "+";
        }

        foreach (var part in parts)
        {
            switch (part.Trim().ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    ctrl = true;
                    break;
                case "ALT":
                    alt = true;
                    break;
                case "SHIFT":
                    shift = true;
                    break;
                default:
                    keyText = part.Trim();
                    break;
            }
        }

        if (!TryShortcutToKey(keyText, ctrl || alt || shift, out var key))
        {
            return false;
        }

        binding = new ShortcutBinding(key, ctrl, alt, shift);
        return true;
    }

    private static bool IsShortcutPressed(ShortcutBinding binding)
    {
        return (IsPhysicalKeyDown(binding.Key) || IsKeyDown(binding.Key))
            && IsModifierDown("CONTROL", "CTRL") == binding.Ctrl
            && IsModifierDown("MENU", "ALT") == binding.Alt
            && IsModifierDown("SHIFT") == binding.Shift;
    }

    private static bool SameBinding(ShortcutBinding left, ShortcutBinding right)
    {
        return left.Key == right.Key
            && left.Ctrl == right.Ctrl
            && left.Alt == right.Alt
            && left.Shift == right.Shift;
    }

    private static bool TryShortcutToKey(string shortcut, bool preferMainDigit, out VirtualKey key)
    {
        key = VirtualKey.NO_KEY;
        var text = shortcut.Trim().ToUpperInvariant();
        if (text.Length == 1 && char.IsDigit(text[0]) && preferMainDigit && TryFindMainDigitKey(text[0], out key))
        {
            return true;
        }

        key = text switch
        {
            "0" => VirtualKey.NUMPAD0,
            "1" => VirtualKey.NUMPAD1,
            "2" => VirtualKey.NUMPAD2,
            "3" => VirtualKey.NUMPAD3,
            "4" => VirtualKey.NUMPAD4,
            "5" => VirtualKey.NUMPAD5,
            "6" => VirtualKey.NUMPAD6,
            "7" => VirtualKey.NUMPAD7,
            "8" => VirtualKey.NUMPAD8,
            "9" => VirtualKey.NUMPAD9,
            "/" => VirtualKey.DIVIDE,
            "*" => VirtualKey.MULTIPLY,
            "+" => VirtualKey.ADD,
            "-" => VirtualKey.SUBTRACT,
            "NUM0" => VirtualKey.NUMPAD0,
            "NUM1" => VirtualKey.NUMPAD1,
            "NUM2" => VirtualKey.NUMPAD2,
            "NUM3" => VirtualKey.NUMPAD3,
            "NUM4" => VirtualKey.NUMPAD4,
            "NUM5" => VirtualKey.NUMPAD5,
            "NUM6" => VirtualKey.NUMPAD6,
            "NUM7" => VirtualKey.NUMPAD7,
            "NUM8" => VirtualKey.NUMPAD8,
            "NUM9" => VirtualKey.NUMPAD9,
            "NUM/" => VirtualKey.DIVIDE,
            "NUM*" => VirtualKey.MULTIPLY,
            "NUM+" => VirtualKey.ADD,
            "NUM-" => VirtualKey.SUBTRACT,
            "A" => VirtualKey.A,
            "B" => VirtualKey.B,
            _ => VirtualKey.NO_KEY,
        };

        if (key != VirtualKey.NO_KEY)
        {
            return true;
        }

        return Enum.TryParse(text, true, out key) && key != VirtualKey.NO_KEY;
    }

    private static bool TryFindMainDigitKey(char digit, out VirtualKey key)
    {
        foreach (var value in Enum.GetValues<VirtualKey>())
        {
            var name = value.ToString().ToUpperInvariant();
            if (name == $"D{digit}" || name == $"KEY_{digit}" || name == $"KEY{digit}" || name == $"VK_{digit}")
            {
                key = value;
                return true;
            }
        }

        key = VirtualKey.NO_KEY;
        return false;
    }

    private static bool IsModifierDown(params string[] names)
    {
        if (names.Any(name => name.Equals("CTRL", StringComparison.OrdinalIgnoreCase) || name.Equals("CONTROL", StringComparison.OrdinalIgnoreCase)) && IsPhysicalControlDown())
        {
            return true;
        }

        if (names.Any(name => name.Equals("ALT", StringComparison.OrdinalIgnoreCase) || name.Equals("MENU", StringComparison.OrdinalIgnoreCase)) && IsPhysicalAltDown())
        {
            return true;
        }

        if (names.Any(name => name.Equals("SHIFT", StringComparison.OrdinalIgnoreCase)) && IsPhysicalShiftDown())
        {
            return true;
        }

        var io = ImGui.GetIO();
        if (names.Any(name => name.Equals("CTRL", StringComparison.OrdinalIgnoreCase) || name.Equals("CONTROL", StringComparison.OrdinalIgnoreCase)) && io.KeyCtrl)
        {
            return true;
        }

        if (names.Any(name => name.Equals("ALT", StringComparison.OrdinalIgnoreCase) || name.Equals("MENU", StringComparison.OrdinalIgnoreCase)) && io.KeyAlt)
        {
            return true;
        }

        if (names.Any(name => name.Equals("SHIFT", StringComparison.OrdinalIgnoreCase)) && io.KeyShift)
        {
            return true;
        }

        foreach (var key in GetValidKeys())
        {
            var keyName = key.ToString().ToUpperInvariant();
            if (names.Any(name => keyName == name || keyName.Contains(name, StringComparison.OrdinalIgnoreCase)) && (IsKeyDown(key) || IsImGuiKeyDown(key)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsKeyDown(VirtualKey key)
    {
        try
        {
            return KeyState[key];
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsPhysicalKeyDown(VirtualKey key)
    {
        return IsPhysicalKeyDown((int)key);
    }

    private static bool IsPhysicalKeyDown(int keyCode)
    {
        return (GetAsyncKeyState(keyCode) & 0x8000) != 0;
    }

    private static bool IsPhysicalControlDown()
    {
        return IsPhysicalKeyDown(VkControl) || IsPhysicalKeyDown(VkLeftControl) || IsPhysicalKeyDown(VkRightControl);
    }

    private static bool IsPhysicalAltDown()
    {
        return IsPhysicalKeyDown(VkMenu) || IsPhysicalKeyDown(VkLeftMenu) || IsPhysicalKeyDown(VkRightMenu);
    }

    private static bool IsPhysicalShiftDown()
    {
        return IsPhysicalKeyDown(VkShift) || IsPhysicalKeyDown(VkLeftShift) || IsPhysicalKeyDown(VkRightShift);
    }

    private static bool IsImGuiKeyPressed(VirtualKey key)
    {
        var imguiKey = ImGuiHelpers.VirtualKeyToImGuiKey(key);
        return imguiKey != ImGuiKey.None && ImGui.IsKeyPressed(imguiKey, false);
    }

    private static bool IsImGuiKeyDown(VirtualKey key)
    {
        var imguiKey = ImGuiHelpers.VirtualKeyToImGuiKey(key);
        return imguiKey != ImGuiKey.None && ImGui.IsKeyDown(imguiKey);
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private static VirtualKey[] GetCaptureKeys()
    {
        return captureKeys ??= GetValidKeys()
            .Where(key => key != VirtualKey.NO_KEY && !IsModifierKey(key))
            .OrderBy(FormatKey)
            .ToArray();
    }

    private static IEnumerable<VirtualKey> GetValidKeys()
    {
        return KeyState.GetValidVirtualKeys().Select(key => (VirtualKey)key);
    }

    private static bool IsModifierKey(VirtualKey key)
    {
        var name = key.ToString().ToUpperInvariant();
        return name.Contains("SHIFT", StringComparison.Ordinal)
            || name.Contains("CONTROL", StringComparison.Ordinal)
            || name.Contains("CTRL", StringComparison.Ordinal)
            || name.Contains("MENU", StringComparison.Ordinal)
            || name.Contains("ALT", StringComparison.Ordinal);
    }

    private static string FormatShortcut(ShortcutBinding binding)
    {
        var parts = new List<string>();
        if (binding.Ctrl)
        {
            parts.Add("Ctrl");
        }

        if (binding.Alt)
        {
            parts.Add("Alt");
        }

        if (binding.Shift)
        {
            parts.Add("Shift");
        }

        parts.Add(FormatKey(binding.Key));
        return string.Join("+", parts);
    }

    private static string FormatKey(VirtualKey key)
    {
        var name = key.ToString().ToUpperInvariant();
        if (name.StartsWith("NUMPAD", StringComparison.Ordinal) && name.Length == 7 && char.IsDigit(name[6]))
        {
            return "Num" + name[6];
        }

        if (name == "DIVIDE")
        {
            return "Num/";
        }

        if (name == "MULTIPLY")
        {
            return "Num*";
        }

        if (name == "ADD")
        {
            return "Num+";
        }

        if (name == "SUBTRACT")
        {
            return "Num-";
        }

        if (name.Length == 1)
        {
            return name;
        }

        if (name.Length == 2 && name[0] == 'D' && char.IsDigit(name[1]))
        {
            return name[1].ToString();
        }

        if (name.StartsWith("KEY_", StringComparison.Ordinal) && name.Length == 5 && char.IsDigit(name[4]))
        {
            return name[4].ToString();
        }

        return key.ToString();
    }
}
