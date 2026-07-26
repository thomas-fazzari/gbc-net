---
name: importing-icons
description: Find and import Fluent icons for Avalonia XAML. Use when adding or changing a PathIcon, StreamGeometry, or shared icon resource in GbcNet.
---

# Avalonia Icons

Use the project's shared icon dictionary. Never add an icon package, SVG asset, or local copy of an icon outside that dictionary.

## Source

Get Fluent geometry from <http://avaloniaui.github.io/icons.html>.

The page exposes each icon as:

```xml
<StreamGeometry x:Key="timer_regular">…</StreamGeometry>
```

Copy the geometry path exactly. Do not simplify, reformat, or hand-edit it.

## Project Convention

* Shared resources live in `src/GbcNet.App/Shell/Chrome/Icons.axaml`.
* Resource keys use `Icon` plus the source snake-case name in PascalCase:
  * `timer_regular` → `IconTimerRegular`
  * `apps_list_regular` → `IconAppsListRegular`
* Add a `<StreamGeometry>` resource before `</ResourceDictionary>`.
* Consume it with `Data="{StaticResource IconName}"` on `PathIcon`.
* Set size and foreground at the caller, using the surrounding UI's existing resources.

## Required Workflow

1. Read `Icons.axaml`; reuse an existing icon if it matches.
2. Open the icon catalogue in the browser and find the requested source key.
3. Copy its exact geometry path into `Icons.axaml` under the project key convention.
4. Reference the new `StaticResource` from the requested `PathIcon`.
5. Check no duplicate project key exists and run CSharpier plus the affected build.

## Example

```xml
<StreamGeometry x:Key="IconTimerRegular">M12,5 …</StreamGeometry>

<PathIcon
  Width="12"
  Height="12"
  Data="{StaticResource IconTimerRegular}"
  Foreground="{DynamicResource ChromeMutedBrush}"
/>
```
