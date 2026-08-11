// Copyright (C) 2026 thomas-fazzari, Fournux
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using TablerIcons;

namespace GbcNet.App.Shell.Chrome;

internal sealed class SidebarNavButton : Button
{
    protected override Type StyleKeyOverride => typeof(Button);

    public static readonly StyledProperty<Icons?> IconDataProperty = AvaloniaProperty.Register<
        SidebarNavButton,
        Icons?
    >(nameof(IconData));

    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<
        SidebarNavButton,
        string?
    >(nameof(Label));

    public Icons? IconData
    {
        get => GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UpdateContent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconDataProperty || change.Property == LabelProperty)
        {
            UpdateContent();
        }
    }

    private void UpdateContent()
    {
        var label = new TextBlock
        {
            Classes = { "label" },
            Text = Label,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 1);

        Content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("20,*"),
            ColumnSpacing = 10,
            Children =
            {
                new TablerIcon
                {
                    Width = 20,
                    Height = 20,
                    Icon = IconData,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                label,
            },
        };
    }
}
