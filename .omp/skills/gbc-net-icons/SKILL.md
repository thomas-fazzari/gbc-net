---
name: gbc-net-icons
description: Use Tabler Icons in Avalonia XAML and C#. Use when adding or changing an icon in GbcNet.
---

# Avalonia Icons

Use `TablerIcons.Avalonia` directly. Icon names match the React names shown on the [Tabler Icons](https://tabler.io/icons) site and use the `Icon` prefix.

- In XAML, declare `xmlns:ti="using:TablerIcons"` and use `<ti:TablerIcon Icon="IconName" />`.
- In C#, use the typed `TablerIcons.Icons` enum and render `TablerIcon` directly.
- Set brush, size, and stroke width with the surrounding design-system resources.
- Do not add local geometry, SVG assets, wrappers, aliases, or another icon package.
