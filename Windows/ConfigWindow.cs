using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using FF14PcShortcutPadDalamud.Models;

namespace FF14PcShortcutPadDalamud.Windows;

internal sealed class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private int selectedButtonIndex;
    private string iconSearch = string.Empty;
    private string selectedIconCategory = string.Empty;
    private string selectedIconBranch = string.Empty;
    private string selectedIconFolder = string.Empty;
    private string captureTarget = string.Empty;
    private string capturedShortcut = string.Empty;

    public ConfigWindow(Plugin plugin)
        : base("FF14 PC Pad 設定##FF14PcShortcutPadConfig")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(740, 680),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var config = plugin.Configuration;
        config.EnsureDefaults();

        DrawGlobalSettings(config);
        ImGui.Separator();
        DrawPageSelector(config);
        ImGui.Separator();
        DrawButtonEditor(config);
    }

    private void DrawGlobalSettings(Configuration config)
    {
        ImGui.TextUnformatted("全体設定");

        var command = config.MainCommand;
        ImGui.SetNextItemWidth(180 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText("メイン画面呼び出しコマンド", ref command, 64))
        {
            config.MainCommand = Configuration.NormalizeCommand(command);
            config.Save();
            plugin.RefreshMainCommand();
        }

        DrawShortcutCapture("次ページキー", "PageNext", config.PageNextShortcut, value =>
        {
            config.PageNextShortcut = value;
            config.Save();
        });

        ImGui.SameLine();
        DrawShortcutCapture("前ページキー", "PagePrevious", config.PagePreviousShortcut, value =>
        {
            config.PagePreviousShortcut = value;
            config.Save();
        });

        var iconSize = config.IconSize;
        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderFloat("アイコンサイズ", ref iconSize, 28f, 96f, "%.0f"))
        {
            config.IconSize = iconSize;
            config.Save();
        }

        var gap = config.Gap;
        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderFloat("ボタン間隔", ref gap, 0f, 24f, "%.0f"))
        {
            config.Gap = gap;
            config.Save();
        }

        var alpha = config.WindowAlpha;
        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderFloat("メイン背景アルファ", ref alpha, 0f, 1f, "%.2f"))
        {
            config.WindowAlpha = alpha;
            config.Save();
        }

        var iconDirectory = config.IconDirectory;
        ImGui.SetNextItemWidth(430 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText("アイコンフォルダ", ref iconDirectory, 512))
        {
            config.IconDirectory = iconDirectory;
            selectedIconCategory = string.Empty;
            selectedIconBranch = string.Empty;
            selectedIconFolder = string.Empty;
            config.Save();
        }

        DrawIconCategoryPicker(config);
        DrawIconBranchPicker(config);
        DrawIconFolderPicker(config);

        ImGui.SetNextItemWidth(260 * ImGuiHelpers.GlobalScale);
        ImGui.InputText("アイコン検索", ref iconSearch, 128);
    }

    private void DrawPageSelector(Configuration config)
    {
        ImGui.TextUnformatted("ページ");
        for (var i = 0; i < Configuration.PageCount; i++)
        {
            if (i > 0)
            {
                ImGui.SameLine();
            }

            if (ImGui.RadioButton($"{i + 1}##Page{i}", config.CurrentPageIndex == i))
            {
                config.CurrentPageIndex = i;
                config.Save();
            }
        }
    }

    private void DrawButtonEditor(Configuration config)
    {
        var page = config.Pages[config.CurrentPageIndex];
        selectedButtonIndex = Math.Clamp(selectedButtonIndex, 0, page.Buttons.Count - 1);

        ImGui.TextUnformatted("ボタン設定");
        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("編集ボタン", GetButtonSelectorLabel(page.Buttons[selectedButtonIndex], selectedButtonIndex)))
        {
            for (var i = 0; i < page.Buttons.Count; i++)
            {
                if (ImGui.Selectable(GetButtonSelectorLabel(page.Buttons[i], i), selectedButtonIndex == i))
                {
                    selectedButtonIndex = i;
                }
            }

            ImGui.EndCombo();
        }

        DrawPadButtonFields(config, page.Buttons[selectedButtonIndex], "Button");
    }

    private void DrawPadButtonFields(Configuration config, PadButton button, string id)
    {
        DrawShortcutCapture("基本ショートカット", $"{id}Shortcut", button.ShortcutText, value =>
        {
            button.ShortcutText = value;
            config.Save();
        });

        var command = button.Command;
        ImGui.SetNextItemWidth(520 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputTextMultiline($"実行コマンド##{id}Command", ref command, 1024, new Vector2(520 * ImGuiHelpers.GlobalScale, 86 * ImGuiHelpers.GlobalScale)))
        {
            button.Command = command;
            config.Save();
        }

        DrawIconGrid(config, button);
    }

    private void DrawShortcutCapture(string label, string id, string currentValue, Action<string> onSave)
    {
        var target = $"ShortcutCapture_{id}";
        var popupId = $"キー入力##{id}Popup";
        var isCapturing = captureTarget == target;
        var display = string.IsNullOrWhiteSpace(currentValue) ? "未設定" : currentValue;

        ImGui.TextUnformatted($"{label}: {display}");
        ImGui.SameLine();
        if (ImGui.SmallButton($"キー入力##{id}Capture"))
        {
            captureTarget = target;
            capturedShortcut = string.Empty;
            ImGui.OpenPopup(popupId);
        }

        if (!isCapturing)
        {
            return;
        }

        ImGui.OpenPopup(popupId);
        if (!ImGui.BeginPopupModal(popupId, ImGuiWindowFlags.AlwaysAutoResize))
        {
            return;
        }

        if (Plugin.TryCaptureShortcut(out var shortcut))
        {
            capturedShortcut = MergeShortcut(capturedShortcut, shortcut);
        }

        ImGui.TextUnformatted(label);
        var capturedDisplay = string.IsNullOrWhiteSpace(capturedShortcut) ? "キーを押してください" : capturedShortcut;
        ImGui.SetNextItemWidth(180 * ImGuiHelpers.GlobalScale);
        ImGui.InputText($"##{id}CapturedText", ref capturedDisplay, 64, ImGuiInputTextFlags.ReadOnly);

        if (ImGui.Button($"決定##{id}Apply") && !string.IsNullOrWhiteSpace(capturedShortcut))
        {
            onSave(Plugin.NormalizeShortcutText(capturedShortcut));
            captureTarget = string.Empty;
            capturedShortcut = string.Empty;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button($"解除##{id}Clear"))
        {
            onSave(string.Empty);
            captureTarget = string.Empty;
            capturedShortcut = string.Empty;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button($"キャンセル##{id}Cancel"))
        {
            captureTarget = string.Empty;
            capturedShortcut = string.Empty;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private static string MergeShortcut(string current, string incoming)
    {
        var tokens = current.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        foreach (var part in incoming.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = NormalizeShortcutPart(part);
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (IsModifierToken(token))
            {
                if (!tokens.Any(existing => string.Equals(NormalizeShortcutPart(existing), token, StringComparison.OrdinalIgnoreCase)))
                {
                    tokens.Add(token);
                }

                continue;
            }

            tokens.RemoveAll(existing => !IsModifierToken(NormalizeShortcutPart(existing)));
            tokens.Add(token);
        }

        return string.Join("+", tokens.OrderBy(GetShortcutPartOrder));
    }

    private static int GetShortcutPartOrder(string token)
    {
        return NormalizeShortcutPart(token) switch
        {
            "Ctrl" => 0,
            "Alt" => 1,
            "Shift" => 2,
            _ => 3,
        };
    }

    private static bool IsModifierToken(string token)
    {
        var normalized = NormalizeShortcutPart(token);
        return normalized is "Ctrl" or "Alt" or "Shift";
    }

    private static string NormalizeShortcutPart(string token)
    {
        return token.Trim().ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => "Ctrl",
            "ALT" or "MENU" => "Alt",
            "SHIFT" => "Shift",
            _ => token.Trim(),
        };
    }

    private void DrawIconCategoryPicker(Configuration config)
    {
        var categories = GetIconCategories(config.IconDirectory);
        if (categories.Length == 0)
        {
            selectedIconCategory = string.Empty;
            ImGui.TextDisabled("アイコン種類: フォルダが見つかりません");
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedIconCategory) || !categories.Contains(selectedIconCategory, StringComparer.OrdinalIgnoreCase))
        {
            selectedIconCategory = categories[0];
            selectedIconBranch = string.Empty;
            selectedIconFolder = string.Empty;
        }

        ImGui.SetNextItemWidth(320 * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("アイコン種類", GetDisplayName(config.IconDirectory, selectedIconCategory)))
        {
            foreach (var category in categories)
            {
                if (ImGui.Selectable(GetDisplayName(config.IconDirectory, category), selectedIconCategory == category))
                {
                    selectedIconCategory = category;
                    selectedIconBranch = string.Empty;
                    selectedIconFolder = string.Empty;
                    iconSearch = string.Empty;
                }
            }

            ImGui.EndCombo();
        }
    }

    private void DrawIconBranchPicker(Configuration config)
    {
        var category = GetActiveCategoryDirectory(config);
        var branches = GetIconBranches(category);
        if (branches.Length == 0)
        {
            selectedIconBranch = category;
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedIconBranch) || !branches.Contains(selectedIconBranch, StringComparer.OrdinalIgnoreCase))
        {
            selectedIconBranch = branches[0];
            selectedIconFolder = string.Empty;
        }

        ImGui.SetNextItemWidth(360 * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("派生フォルダ", GetDisplayName(category, selectedIconBranch)))
        {
            foreach (var branch in branches)
            {
                if (ImGui.Selectable(GetDisplayName(category, branch), selectedIconBranch == branch))
                {
                    selectedIconBranch = branch;
                    selectedIconFolder = string.Empty;
                    iconSearch = string.Empty;
                }
            }

            ImGui.EndCombo();
        }
    }

    private void DrawIconFolderPicker(Configuration config)
    {
        var branch = GetActiveBranchDirectory(config);
        var folders = GetIconFolders(branch);
        if (folders.Length == 0)
        {
            selectedIconFolder = branch;
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedIconFolder) || !folders.Contains(selectedIconFolder, StringComparer.OrdinalIgnoreCase))
        {
            selectedIconFolder = folders[0];
        }

        ImGui.SetNextItemWidth(420 * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("アイコンフォルダ", GetDisplayName(branch, selectedIconFolder)))
        {
            foreach (var folder in folders)
            {
                if (ImGui.Selectable(GetDisplayName(branch, folder), selectedIconFolder == folder))
                {
                    selectedIconFolder = folder;
                    iconSearch = string.Empty;
                }
            }

            ImGui.EndCombo();
        }
    }

    private void DrawIconGrid(Configuration config, PadButton button)
    {
        var folder = GetActiveIconFolder(config);
        var files = GetDirectIconFiles(folder)
            .Where(file =>
                string.IsNullOrWhiteSpace(iconSearch)
                || Path.GetFileName(file).Contains(iconSearch, StringComparison.OrdinalIgnoreCase))
            .Take(240)
            .ToArray();

        ImGui.TextUnformatted($"アイコン選択: {files.Length}件");
        if (!string.IsNullOrWhiteSpace(button.IconPath))
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("解除##ClearIcon"))
            {
                button.IconPath = string.Empty;
                config.Save();
            }
        }

        var iconSize = 38f * ImGuiHelpers.GlobalScale;
        var cellWidth = iconSize + (18f * ImGuiHelpers.GlobalScale);
        var padding = 28f * ImGuiHelpers.GlobalScale;
        var availableWidth = Math.Max(cellWidth * 4f, ImGui.GetContentRegionAvail().X);
        var columns = 4;
        var tableWidth = Math.Min(availableWidth, (columns * cellWidth) + ImGui.GetStyle().ScrollbarSize + padding);
        var tableSize = new Vector2(tableWidth, 300f * ImGuiHelpers.GlobalScale);

        if (ImGui.BeginTable("IconGrid", columns, ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.NoHostExtendX, tableSize))
        {
            foreach (var file in files)
            {
                ImGui.TableNextColumn();
                ImGui.PushID(file);
                var selected = button.IconPath == file;
                if (selected)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.55f, 0.9f, 0.65f));
                }

                if (DrawIconTile(file, iconSize))
                {
                    button.IconPath = file;
                    config.Save();
                }

                if (selected)
                {
                    ImGui.PopStyleColor();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(GetDisplayName(config.IconDirectory, file));
                }

                ImGui.PopID();
            }

            ImGui.EndTable();
        }
    }

    private string GetActiveCategoryDirectory(Configuration config)
    {
        if (!string.IsNullOrWhiteSpace(selectedIconCategory) && Directory.Exists(selectedIconCategory))
        {
            return selectedIconCategory;
        }

        return config.IconDirectory;
    }

    private string GetActiveBranchDirectory(Configuration config)
    {
        if (!string.IsNullOrWhiteSpace(selectedIconBranch) && Directory.Exists(selectedIconBranch))
        {
            return selectedIconBranch;
        }

        return GetActiveCategoryDirectory(config);
    }

    private string GetActiveIconFolder(Configuration config)
    {
        if (!string.IsNullOrWhiteSpace(selectedIconFolder) && Directory.Exists(selectedIconFolder))
        {
            return selectedIconFolder;
        }

        return GetActiveBranchDirectory(config);
    }

    private static bool DrawIconTile(string path, float size)
    {
        try
        {
            if (File.Exists(path))
            {
                var texture = Plugin.TextureProvider.GetFromFile(path);
                if (texture.TryGetWrap(out var wrap, out _) && wrap is not null)
                {
                    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4f * ImGuiHelpers.GlobalScale, 4f * ImGuiHelpers.GlobalScale));
                    var clicked = ImGui.ImageButton(wrap.Handle, new Vector2(size, size));
                    ImGui.PopStyleVar();
                    return clicked;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Failed to draw icon tile: {Path}", path);
        }

        return ImGui.Button("##MissingIcon", new Vector2(size, size));
    }

    private static string GetButtonSelectorLabel(PadButton button, int index)
    {
        var shortcut = string.IsNullOrWhiteSpace(button.ShortcutText) ? "-" : button.ShortcutText;
        var command = string.IsNullOrWhiteSpace(button.Command) ? "未設定" : button.Command.Split('\n')[0].Trim();
        return $"{index + 1}: {shortcut} / {command}";
    }

    private static string[] GetIconCategories(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return [];
        }

        var directories = Directory.EnumerateDirectories(directory)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return directories.Length > 0 ? directories : [directory];
    }

    private static string[] GetIconFolders(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return [];
        }

        var directories = Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories)
            .Where(dir => GetDirectIconFiles(dir).Length > 0)
            .OrderBy(dir => GetDisplayName(directory, dir), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return GetDirectIconFiles(directory).Length > 0
            ? new[] { directory }.Concat(directories).ToArray()
            : directories;
    }

    private static string[] GetIconBranches(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return [];
        }

        var directDirectories = Directory.EnumerateDirectories(directory)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return directDirectories.Length > 0 ? directDirectories : [directory];
    }

    private static string[] GetDirectIconFiles(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return [];
        }

        var extensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };
        return Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(file => extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GetDisplayName(string directory, string path)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return Path.GetFileName(path);
        }

        try
        {
            return Path.GetRelativePath(directory, path);
        }
        catch
        {
            return Path.GetFileName(path);
        }
    }
}
