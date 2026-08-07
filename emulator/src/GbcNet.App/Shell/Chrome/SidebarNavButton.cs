// Copyright (C) 2026 thomas-fazzari, Fournux
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace GbcNet.App.Shell.Chrome;

internal sealed class SidebarNavButton : Button
{
    // Reuses the Button.sidebar-item styles defined by each window.
    protected override Type StyleKeyOverride => typeof(Button);

    public static readonly StyledProperty<StreamGeometry?> IconDataProperty =
        AvaloniaProperty.Register<SidebarNavButton, StreamGeometry?>(nameof(IconData));

    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<
        SidebarNavButton,
        string?
    >(nameof(Label));

    public StreamGeometry? IconData
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
        var label = new TextBlock { Classes = { "chrome-label", "sidebar-label" }, Text = Label };
        Grid.SetColumn(label, 1);

        Content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("16,*"),
            ColumnSpacing = 9,
            Children =
            {
                new PathIcon
                {
                    Width = 15,
                    Height = 15,
                    Data = IconData,
                },
                label,
            },
        };
    }
}
