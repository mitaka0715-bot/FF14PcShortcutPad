using System;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using FF14PcShortcutPadDalamud.Models;

namespace FF14PcShortcutPadDalamud.Windows;

internal sealed class PadWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public PadWindow(Plugin plugin)
        : base("FF14 PC Pad##FF14PcShortcutPad")
    {
        this.plugin = plugin;
        Flags = ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse
            | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoCollapse;
    }

    public void Dispose()
    {
    }

    public override void PreDraw()
    {
        BgAlpha = plugin.Configuration.WindowAlpha;
    }

    public override void Draw()
    {
        var config = plugin.Configuration;
        config.EnsureDefaults();

        var page = config.Pages[config.CurrentPageIndex];
        var buttonSize = config.IconSize * ImGuiHelpers.GlobalScale;
        var gap = config.Gap * ImGuiHelpers.GlobalScale;

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap));
        for (var row = 0; row < 4; row++)
        {
            for (var col = 0; col < 4; col++)
            {
                var index = (row * 4) + col;
                DrawCommandButton(page.Buttons[index], index, buttonSize);

                if (col < 3)
                {
                    ImGui.SameLine();
                }
            }
        }

        ImGui.PopStyleVar();
        DrawPageControls(config, (buttonSize * 4f) + (gap * 3f));

        if (plugin.ShouldShowStatus)
        {
            ImGui.TextUnformatted(plugin.LastStatus);
        }
    }

    private void DrawCommandButton(PadButton button, int buttonIndex, float buttonSize)
    {
        var id = $"##PadButton{buttonIndex}";
        var clicked = DrawFixedIconButton(id, button.IconPath, buttonSize);

        if (clicked)
        {
            plugin.ExecuteCommand(button.Command);
        }

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            plugin.ToggleConfigUi();
        }
    }

    private void DrawPageControls(Configuration config, float gridWidth)
    {
        ImGui.Spacing();
        var buttonWidth = 34f * ImGuiHelpers.GlobalScale;
        var centerWidth = Math.Max(48f * ImGuiHelpers.GlobalScale, gridWidth - (buttonWidth * 2f) - (ImGui.GetStyle().ItemSpacing.X * 2f));

        if (ImGui.Button("<##PrevPage", new Vector2(buttonWidth, 24f * ImGuiHelpers.GlobalScale)))
        {
            config.CurrentPageIndex = (config.CurrentPageIndex + Configuration.PageCount - 1) % Configuration.PageCount;
            config.Save();
        }

        ImGui.SameLine();
        var pageText = $"{config.CurrentPageIndex + 1}/{Configuration.PageCount}";
        var centerStart = ImGui.GetCursorPosX();
        ImGui.Dummy(new Vector2(centerWidth, 24f * ImGuiHelpers.GlobalScale));
        var textSize = ImGui.CalcTextSize(pageText);
        ImGui.SetCursorPos(new Vector2(centerStart + ((centerWidth - textSize.X) * 0.5f), ImGui.GetCursorPosY() - (22f * ImGuiHelpers.GlobalScale)));
        ImGui.TextUnformatted(pageText);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (4f * ImGuiHelpers.GlobalScale));

        ImGui.SameLine();
        if (ImGui.Button(">##NextPage", new Vector2(buttonWidth, 24f * ImGuiHelpers.GlobalScale)))
        {
            config.CurrentPageIndex = (config.CurrentPageIndex + 1) % Configuration.PageCount;
            config.Save();
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("設定##OpenConfig"))
        {
            plugin.ToggleConfigUi();
        }
    }

    private static bool DrawFixedIconButton(string id, string iconPath, float size)
    {
        var buttonSize = new Vector2(size, size);
        var pos = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, buttonSize);
        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();

        var end = pos + buttonSize;
        var drawList = ImGui.GetWindowDrawList();

        if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
        {
            try
            {
                var texture = Plugin.TextureProvider.GetFromFile(iconPath);
                if (texture.TryGetWrap(out var wrap, out _) && wrap is not null)
                {
                    var padding = 1f * ImGuiHelpers.GlobalScale;
                    var innerMin = pos + new Vector2(padding, padding);
                    var innerMax = end - new Vector2(padding, padding);
                    drawList.AddImage(wrap.Handle, innerMin, innerMax);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "Failed to draw icon: {Path}", iconPath);
            }
        }
        else if (hovered || active)
        {
            var fill = active
                ? new Vector4(1f, 1f, 1f, 0.16f)
                : new Vector4(1f, 1f, 1f, 0.08f);
            drawList.AddRectFilled(pos, end, ImGui.GetColorU32(fill), 4f * ImGuiHelpers.GlobalScale);
        }

        return clicked;
    }
}
