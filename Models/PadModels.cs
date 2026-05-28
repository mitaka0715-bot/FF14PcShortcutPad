using System;
using System.Collections.Generic;

namespace FF14PcShortcutPadDalamud.Models;

[Serializable]
public sealed class PadPage
{
    public List<PadButton> Buttons { get; set; } = [];
}

[Serializable]
public sealed class PadButton
{
    public string Label { get; set; } = string.Empty;
    public string ShortcutText { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
}
