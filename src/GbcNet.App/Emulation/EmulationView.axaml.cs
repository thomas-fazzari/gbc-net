// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;

namespace GbcNet.App.Emulation;

internal sealed partial class EmulationView : UserControl
{
    public EmulationView()
    {
        InitializeComponent();
    }

    public Image Screen => ScreenImage;
}
