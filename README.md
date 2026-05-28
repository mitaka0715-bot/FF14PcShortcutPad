# FF14 PC Shortcut Pad

FF14 PC Shortcut Pad is a Dalamud plugin that adds a compact 4x4 in-game shortcut pad for FF14 commands, chat text, other Dalamud plugin commands, and page-based icon buttons.

## Features

- 4x4 icon button pad
- 10 saved pages
- Configurable next/previous page shortcuts
- Configurable per-button shortcut keys
- Supports FF14 commands such as `/ac`, `/hotbar`, `/fc`, `/sh`, and `/p`
- Can call other Dalamud plugin commands such as `/cammy` and `/vbm`
- Sends plain chat text when a command line does not start with `/`
- Selects PNG/JPG/WEBP icons from a user-provided icon folder
- Adjustable icon size, button spacing, and main window background alpha
- Japanese settings UI

## Commands

```text
/ffpcpad
/ffpcpad config
```

## Icons

This repository does not include official FF14 icon assets. Use a separate icon extraction tool or your own images, then set the generated `icons` folder in the plugin settings.

## Command Text

Lines starting with `/` are sent as FF14 or Dalamud commands. Lines without `/` are sent as normal `/say` chat text.

```text
/ac スプリント
/fc こんにちは
/sh よろしくお願いします
/p よろしくお願いします
/cammy
/vbm
```

## Notes About Chat Commands

- `/p` and `/party` work when you are in a party. If you are not in a party, FF14 may not show the same error behavior as manual chat input.
- Plain text without `/` is sent as `/say TEXT`.
- Other plugin commands are useful shortcuts: for example, set a button to `/cammy` or `/vbm` to open those plugins directly.

## Dalamud Repo

Add this custom repository URL in Dalamud:

```text
https://raw.githubusercontent.com/mitaka0715-bot/FF14PcShortcutPad/main/repo.json
```

## Notes

Use this plugin according to the FF14, XIVLauncher, and Dalamud rules and policies.
