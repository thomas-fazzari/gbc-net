// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using GbcNet.App.Shell.Chrome;

namespace GbcNet.App.Emulation;

internal sealed partial class EmulationView : UserControl
{
    public EmulationView()
    {
        InitializeComponent();
        ViewportBackground.Background = AppChrome.Brush(AppChrome.Bg);
        ScreenBackground.Background = AppChrome.Brush(AppChrome.Bg);
    }

    public Image Screen => ScreenImage;
}
