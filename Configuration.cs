using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Dalamud.Configuration;
using FF14PcShortcutPadDalamud.Models;

namespace FF14PcShortcutPadDalamud;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public const int PageCount = 10;
    public const int ButtonCount = 16;

    private static readonly string[] DefaultShortcuts =
    [
        "9", "/", "*", "+",
        "6", "7", "8", "-",
        "5", "4", "3", "A",
        "2", "1", "0", "B",
    ];

    public int Version { get; set; } = 3;
    public bool MainWindowVisible { get; set; } = true;
    public bool ConfigWindowVisible { get; set; }
    public string MainCommand { get; set; } = "/ffpcpad";
    public string PageNextShortcut { get; set; } = "+";
    public string PagePreviousShortcut { get; set; } = "-";
    public int CurrentPageIndex { get; set; }
    public float IconSize { get; set; } = 46f;
    public float Gap { get; set; } = 3f;
    public float WindowAlpha { get; set; } = 0.05f;
    public Vector4 TextColor { get; set; } = new(1f, 1f, 1f, 1f);
    public string IconDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FF14PcShortcutPad", "icons");
    public List<PadPage> Pages { get; set; } = [];

    public void EnsureDefaults()
    {
        while (Pages.Count < PageCount)
        {
            Pages.Add(CreateDefaultPage());
        }

        if (Pages.Count > PageCount)
        {
            Pages.RemoveRange(PageCount, Pages.Count - PageCount);
        }

        foreach (var page in Pages)
        {
            while (page.Buttons.Count < ButtonCount)
            {
                var shortcut = DefaultShortcuts[page.Buttons.Count];
                page.Buttons.Add(new PadButton { Label = string.Empty, ShortcutText = shortcut });
            }

            if (page.Buttons.Count > ButtonCount)
            {
                page.Buttons.RemoveRange(ButtonCount, page.Buttons.Count - ButtonCount);
            }

            for (var i = 0; i < page.Buttons.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(page.Buttons[i].ShortcutText))
                {
                    page.Buttons[i].ShortcutText = DefaultShortcuts[i];
                }

                if (Version < 2 && page.Buttons[i].Label == DefaultShortcuts[i])
                {
                    page.Buttons[i].Label = string.Empty;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(PageNextShortcut))
        {
            PageNextShortcut = "+";
        }

        if (string.IsNullOrWhiteSpace(PagePreviousShortcut))
        {
            PagePreviousShortcut = "-";
        }

        PageNextShortcut = PageNextShortcut.Trim();
        PagePreviousShortcut = PagePreviousShortcut.Trim();
        Version = Math.Max(Version, 4);
        CurrentPageIndex = Math.Clamp(CurrentPageIndex, 0, PageCount - 1);
        IconSize = Math.Clamp(IconSize, 28f, 96f);
        Gap = Math.Clamp(Gap, 0f, 24f);
        WindowAlpha = Math.Clamp(WindowAlpha, 0f, 1f);
        MainCommand = NormalizeCommand(MainCommand);
    }

    public void Save()
    {
        EnsureDefaults();
        Plugin.PluginInterface.SavePluginConfig(this);
    }

    public static string NormalizeCommand(string command)
    {
        var trimmed = string.IsNullOrWhiteSpace(command) ? "/ffpcpad" : command.Trim();
        return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }

    private static PadPage CreateDefaultPage()
    {
        var page = new PadPage();
        foreach (var shortcut in DefaultShortcuts)
        {
            page.Buttons.Add(new PadButton { Label = string.Empty, ShortcutText = shortcut });
        }

        return page;
    }
}
