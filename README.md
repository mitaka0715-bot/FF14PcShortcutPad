# FF14 PC Shortcut Pad

FF14 PC Shortcut Pad is a Dalamud plugin that adds a compact 4x4 in-game shortcut pad for chat commands, macros, and page-based icon buttons.

## Features

- 4x4 icon button pad
- 10 saved pages
- Configurable next/previous page shortcuts
- Configurable per-button shortcut keys
- Supports FF14 chat commands such as `/ac` and `/hotbar`
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

Lines starting with `/` are sent as FF14/Dalamud commands.
Lines without `/` are sent as normal chat text.

```text
/p よろしくお願いします
よろしくお願いします
```

## Dalamud Repo Test

For local testing, add this custom repository URL in Dalamud:

```text
https://raw.githubusercontent.com/mitaka0715-bot/FF14PcShortcutPad/main/repo.json
```

## Notes

Use this plugin according to the FF14, XIVLauncher, and Dalamud rules and policies.
