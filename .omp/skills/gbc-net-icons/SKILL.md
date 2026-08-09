---
name: gbc-net-icons
description: Find and import Material Design Icons for Avalonia XAML. Use when adding or changing a PathIcon, StreamGeometry, or shared icon resource in GbcNet.
---

# Avalonia Icons

Shared icon dictionary: `emulator/src/GbcNet.App/Shell/Chrome/Icons.axaml`. Never add an icon package, SVG asset, or local copy outside it.

Source: [Material Design Icons](https://pictogrammers.com/library/mdi/). Open the requested icon at `https://pictogrammers.com/library/mdi/icon/<name>/` and copy its SVG `<path d="…">` verbatim. Do not simplify, reformat, or hand-edit it.

Add a `<StreamGeometry>` resource before `</ResourceDictionary>`, keyed `Icon` plus the MDI name in PascalCase (e.g. `plus` → `IconPlus`).
Consume with `Data="{StaticResource IconName}"` on `PathIcon`; set size and foreground at the caller from the surrounding UI's existing resources.
Before adding a new key, read the dictionary and reuse an existing icon if one matches.
