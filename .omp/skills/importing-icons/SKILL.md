---
name: importing-icons
description: Find and import Material Design Icons for Avalonia XAML. Use when adding or changing a PathIcon, StreamGeometry, or shared icon resource in GbcNet.
---

# Avalonia Icons

Use the project's shared icon dictionary. Never add an icon package, SVG asset, or local copy of an icon outside that dictionary.

## Source

Use [Material Design Icons](https://pictogrammers.com/library/mdi/). Open the requested icon at:

```text
https://pictogrammers.com/library/mdi/icon/<name>/
```

Copy its SVG `<path d="…">` exactly. Do not simplify, reformat, or hand-edit it.

## Project Convention

* Shared resources live in `emulator/src/GbcNet.App/Shell/Chrome/Icons.axaml`.
* Resource keys use `Icon` plus the MDI icon name in PascalCase:
  * `plus` → `IconPlus`
* Add a `<StreamGeometry>` resource before `</ResourceDictionary>`.
* Consume it with `Data="{StaticResource IconName}"` on `PathIcon`.
* Set size and foreground at the caller, using the surrounding UI's existing resources.

## Required Workflow

1. Read `Icons.axaml`; reuse an existing icon if it matches.
2. Open the requested MDI icon page.
3. Copy its exact geometry path into `Icons.axaml` under the project key convention.
4. Reference the new `StaticResource` from the requested `PathIcon`.
5. Check no duplicate project key exists and run CSharpier plus the affected build.

## Example

```xml
<StreamGeometry x:Key="IconPlus">M19,13H13V19H11V13H5V11H11V5H13V11H19V13Z</StreamGeometry>

<PathIcon
  Width="12"
  Height="12"
  Data="{StaticResource IconPlus}"
  Foreground="{DynamicResource ChromeMutedBrush}"
/>
```
