// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Input;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.App.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.Tests.App.Input;

public sealed class InputRouterTests
{
    [Fact]
    public void Apply_ReturnsFalseForUnboundInput()
    {
        var updates = new List<(JoypadButton Button, bool Pressed)>();
        InputRouter router = new([], (button, pressed) => updates.Add((button, pressed)));

        var handled = router.Apply(Key.A, pressed: true);

        Assert.False(handled);
        Assert.Empty(updates);
    }

    [Fact]
    public void Apply_DoesNotSendDuplicatePressedStateForSameKey()
    {
        var updates = new List<(JoypadButton Button, bool Pressed)>();
        InputRouter router = new(
            [new InputBinding(Key.A, JoypadButton.A)],
            (button, pressed) => updates.Add((button, pressed))
        );

        Assert.True(router.Apply(Key.A, pressed: true));
        Assert.True(router.Apply(Key.A, pressed: true));

        Assert.Equal([(JoypadButton.A, true)], updates);
    }

    [Fact]
    public void Apply_ReleasesButtonOnlyAfterAllMappedInputsAreReleased()
    {
        var updates = new List<(JoypadButton Button, bool Pressed)>();
        InputRouter router = new(
            [new InputBinding(Key.A, JoypadButton.A), new InputBinding(Key.B, JoypadButton.A)],
            (button, pressed) => updates.Add((button, pressed))
        );

        router.Apply(Key.A, pressed: true);
        router.Apply(Key.B, pressed: true);
        router.Apply(Key.A, pressed: false);
        router.Apply(Key.B, pressed: false);

        Assert.Equal([(JoypadButton.A, true), (JoypadButton.A, false)], updates);
    }

    [Fact]
    public void ReplaceBindings_ReleasesHeldOldButtonAndUsesOnlyNewBindings()
    {
        var updates = new List<(JoypadButton Button, bool Pressed)>();
        InputRouter router = new(
            [new InputBinding(Key.A, JoypadButton.A)],
            (button, pressed) => updates.Add((button, pressed))
        );

        router.Apply(Key.A, pressed: true);
        router.ReplaceBindings([new InputBinding(Key.B, JoypadButton.B)]);

        Assert.False(router.Apply(Key.A, pressed: false));
        Assert.True(router.Apply(Key.B, pressed: true));
        Assert.Equal(
            [(JoypadButton.A, true), (JoypadButton.A, false), (JoypadButton.B, true)],
            updates
        );
    }

    [Fact]
    public void ReplaceBindings_DuplicateKeyLeavesOldBindingsAndHeldStateUntouched()
    {
        var updates = new List<(JoypadButton Button, bool Pressed)>();
        InputRouter router = new(
            [new InputBinding(Key.A, JoypadButton.A)],
            (button, pressed) => updates.Add((button, pressed))
        );

        router.Apply(Key.A, pressed: true);

        Assert.Throws<ArgumentException>(() =>
            router.ReplaceBindings([
                new InputBinding(Key.B, JoypadButton.A),
                new InputBinding(Key.B, JoypadButton.B),
            ])
        );
        Assert.False(router.Apply(Key.B, pressed: true));
        Assert.True(router.Apply(Key.A, pressed: false));
        Assert.Equal([(JoypadButton.A, true), (JoypadButton.A, false)], updates);
    }

    [Fact]
    public void Clear_ReleasesActiveButtonsAndForgetsActiveInputs()
    {
        var updates = new List<(JoypadButton Button, bool Pressed)>();
        InputRouter router = new(
            [new InputBinding(Key.A, JoypadButton.A), new InputBinding(Key.B, JoypadButton.A)],
            (button, pressed) => updates.Add((button, pressed))
        );

        router.Apply(Key.A, pressed: true);
        router.Apply(Key.B, pressed: true);
        router.Clear();
        router.Apply(Key.A, pressed: false);
        router.Apply(Key.B, pressed: false);

        Assert.Equal([(JoypadButton.A, true), (JoypadButton.A, false)], updates);
    }

    [Fact]
    public void FromConfig_ResolvesActiveProfileCaseInsensitively()
    {
        var config = new InputConfig
        {
            ActiveProfile = "ALTERNATE",
            Profiles = new Dictionary<string, InputProfileConfig>(StringComparer.Ordinal)
            {
                ["alternate"] = new()
                {
                    Keyboard = [new KeyboardInputBindingConfig("A", nameof(Key.Z))],
                },
            },
        };

        var binding = Assert.Single(InputMap.FromConfig(config).Bindings);
        Assert.Equal(new InputBinding(Key.Z, JoypadButton.A), binding);
    }
}
