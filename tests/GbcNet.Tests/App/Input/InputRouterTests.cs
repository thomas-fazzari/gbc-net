// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Input;
using GbcNet.App.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.Tests.App.Input;

public sealed class InputRouterTests
{
    [Fact]
    public void Apply_ReturnsFalseForUnboundInput()
    {
        var updates = new List<(JoypadButton Button, bool Pressed)>();
        InputRouter router = new([], [], (button, pressed) => updates.Add((button, pressed)));

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
            [],
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
            [],
            (button, pressed) => updates.Add((button, pressed))
        );

        router.Apply(Key.A, pressed: true);
        router.Apply(Key.B, pressed: true);
        router.Apply(Key.A, pressed: false);
        router.Apply(Key.B, pressed: false);

        Assert.Equal([(JoypadButton.A, true), (JoypadButton.A, false)], updates);
    }

    [Fact]
    public void ApplyGamepadButton_MapsActionTransitions()
    {
        var updates = new List<(JoypadButton Button, bool Pressed)>();
        InputRouter router = new(
            [],
            [
                new GamepadBinding(GamepadButton.South, JoypadButton.A),
                new GamepadBinding(GamepadButton.East, JoypadButton.B),
                new GamepadBinding(GamepadButton.Start, JoypadButton.Start),
                new GamepadBinding(GamepadButton.Back, JoypadButton.Select),
            ],
            (button, pressed) => updates.Add((button, pressed))
        );

        foreach (
            var control in new[]
            {
                GamepadButton.South,
                GamepadButton.East,
                GamepadButton.Start,
                GamepadButton.Back,
            }
        )
        {
            Assert.True(router.ApplyGamepadButton(1, control, pressed: true));
            Assert.True(router.ApplyGamepadButton(1, control, pressed: false));
        }

        Assert.Equal(
            [
                (JoypadButton.A, true),
                (JoypadButton.A, false),
                (JoypadButton.B, true),
                (JoypadButton.B, false),
                (JoypadButton.Start, true),
                (JoypadButton.Start, false),
                (JoypadButton.Select, true),
                (JoypadButton.Select, false),
            ],
            updates
        );
    }

    [Fact]
    public void Apply_KeyboardAndTwoGamepadsReleaseSharedButtonAfterFinalSource()
    {
        var updates = new List<(JoypadButton Button, bool Pressed)>();
        InputRouter router = new(
            [new InputBinding(Key.A, JoypadButton.A)],
            [new GamepadBinding(GamepadButton.South, JoypadButton.A)],
            (button, pressed) => updates.Add((button, pressed))
        );

        router.Apply(Key.A, pressed: true);
        router.ApplyGamepadButton(1, GamepadButton.South, pressed: true);
        router.ApplyGamepadButton(2, GamepadButton.South, pressed: true);
        router.Apply(Key.A, pressed: false);
        router.ApplyGamepadButton(1, GamepadButton.South, pressed: false);
        router.ApplyGamepadButton(2, GamepadButton.South, pressed: false);

        Assert.Equal([(JoypadButton.A, true), (JoypadButton.A, false)], updates);
    }

    [Fact]
    public void ApplyGamepadDirection_AcceptsOnlyDpadDirections()
    {
        var updates = new List<(JoypadButton Button, bool Pressed)>();
        InputRouter router = new([], [], (button, pressed) => updates.Add((button, pressed)));

        foreach (
            var direction in new[]
            {
                JoypadButton.Up,
                JoypadButton.Down,
                JoypadButton.Left,
                JoypadButton.Right,
            }
        )
        {
            Assert.True(router.ApplyGamepadDirection(1, direction, pressed: true));
            Assert.True(router.ApplyGamepadDirection(1, direction, pressed: false));
        }

        foreach (
            var button in new[]
            {
                JoypadButton.A,
                JoypadButton.B,
                JoypadButton.Start,
                JoypadButton.Select,
            }
        )
        {
            Assert.False(router.ApplyGamepadDirection(1, button, pressed: true));
        }
        Assert.Equal(
            [
                (JoypadButton.Up, true),
                (JoypadButton.Up, false),
                (JoypadButton.Down, true),
                (JoypadButton.Down, false),
                (JoypadButton.Left, true),
                (JoypadButton.Left, false),
                (JoypadButton.Right, true),
                (JoypadButton.Right, false),
            ],
            updates
        );
    }

    [Fact]
    public void ApplyGamepadInput_IsIdempotentPerDevice()
    {
        var updates = new List<(JoypadButton Button, bool Pressed)>();
        InputRouter router = new(
            [],
            [new GamepadBinding(GamepadButton.South, JoypadButton.A)],
            (button, pressed) => updates.Add((button, pressed))
        );

        Assert.True(router.ApplyGamepadButton(1, GamepadButton.South, pressed: true));
        Assert.True(router.ApplyGamepadButton(1, GamepadButton.South, pressed: true));
        Assert.True(router.ApplyGamepadButton(1, GamepadButton.South, pressed: false));
        Assert.True(router.ApplyGamepadButton(1, GamepadButton.South, pressed: false));
        Assert.True(router.ApplyGamepadDirection(1, JoypadButton.Up, pressed: true));
        Assert.True(router.ApplyGamepadDirection(1, JoypadButton.Up, pressed: true));
        Assert.True(router.ApplyGamepadDirection(1, JoypadButton.Up, pressed: false));
        Assert.True(router.ApplyGamepadDirection(1, JoypadButton.Up, pressed: false));

        Assert.Equal(
            [
                (JoypadButton.A, true),
                (JoypadButton.A, false),
                (JoypadButton.Up, true),
                (JoypadButton.Up, false),
            ],
            updates
        );
    }

    [Fact]
    public void ReleaseGamepad_ReleasesOnlyThatDevicesContributions()
    {
        var updates = new List<(JoypadButton Button, bool Pressed)>();
        InputRouter router = new(
            [],
            [new GamepadBinding(GamepadButton.South, JoypadButton.A)],
            (button, pressed) => updates.Add((button, pressed))
        );

        router.ApplyGamepadButton(1, GamepadButton.South, pressed: true);
        router.ApplyGamepadDirection(1, JoypadButton.Up, pressed: true);
        router.ApplyGamepadButton(2, GamepadButton.South, pressed: true);
        router.ApplyGamepadDirection(2, JoypadButton.Up, pressed: true);
        router.ReleaseGamepad(1);
        router.ReleaseGamepad(2);

        Assert.Equal(
            [
                (JoypadButton.A, true),
                (JoypadButton.Up, true),
                (JoypadButton.A, false),
                (JoypadButton.Up, false),
            ],
            updates
        );
    }

    [Fact]
    public void Clear_ReleasesEveryActiveButtonOnceAndForgetsActiveInputs()
    {
        var updates = new List<(JoypadButton Button, bool Pressed)>();
        InputRouter router = new(
            [new InputBinding(Key.A, JoypadButton.A), new InputBinding(Key.B, JoypadButton.B)],
            [
                new GamepadBinding(GamepadButton.South, JoypadButton.A),
                new GamepadBinding(GamepadButton.East, JoypadButton.Select),
            ],
            (button, pressed) => updates.Add((button, pressed))
        );

        router.Apply(Key.A, pressed: true);
        router.Apply(Key.B, pressed: true);
        router.ApplyGamepadButton(1, GamepadButton.South, pressed: true);
        router.ApplyGamepadButton(1, GamepadButton.East, pressed: true);
        router.ApplyGamepadDirection(1, JoypadButton.Up, pressed: true);
        router.Clear();
        router.Apply(Key.A, pressed: false);
        router.Apply(Key.B, pressed: false);
        router.ApplyGamepadButton(1, GamepadButton.South, pressed: false);
        router.ApplyGamepadButton(1, GamepadButton.East, pressed: false);
        router.ApplyGamepadDirection(1, JoypadButton.Up, pressed: false);

        Assert.Equal(8, updates.Count);
        foreach (
            var button in new[]
            {
                JoypadButton.A,
                JoypadButton.B,
                JoypadButton.Select,
                JoypadButton.Up,
            }
        )
        {
            Assert.Equal(1, updates.Count(update => update == (button, true)));
            Assert.Equal(1, updates.Count(update => update == (button, false)));
        }
    }

    [Fact]
    public void ReplaceBindings_LookupFailureLeavesCurrentBindingsAndStateUntouched()
    {
        var updates = new List<(JoypadButton Button, bool Pressed)>();
        InputRouter router = new(
            [new InputBinding(Key.A, JoypadButton.A)],
            [new GamepadBinding(GamepadButton.South, JoypadButton.B)],
            (button, pressed) => updates.Add((button, pressed))
        );

        router.Apply(Key.A, pressed: true);
        router.ApplyGamepadButton(1, GamepadButton.South, pressed: true);

        Assert.Throws<ArgumentException>(() =>
            router.ReplaceBindings(
                [new InputBinding(Key.B, JoypadButton.B)],
                [
                    new GamepadBinding(GamepadButton.East, JoypadButton.A),
                    new GamepadBinding(GamepadButton.East, JoypadButton.B),
                ]
            )
        );

        Assert.True(router.Apply(Key.A, pressed: false));
        Assert.True(router.ApplyGamepadButton(1, GamepadButton.South, pressed: false));
        Assert.False(router.Apply(Key.B, pressed: true));
        Assert.False(router.ApplyGamepadButton(1, GamepadButton.East, pressed: true));
        Assert.Equal(
            [
                (JoypadButton.A, true),
                (JoypadButton.B, true),
                (JoypadButton.A, false),
                (JoypadButton.B, false),
            ],
            updates
        );
    }

    [Fact]
    public void ReplaceBindings_ClearsHeldInputsAndInstallsNewKeyboardAndGamepadMaps()
    {
        var updates = new List<(JoypadButton Button, bool Pressed)>();
        InputRouter router = new(
            [new InputBinding(Key.A, JoypadButton.A)],
            [new GamepadBinding(GamepadButton.South, JoypadButton.B)],
            (button, pressed) => updates.Add((button, pressed))
        );

        router.Apply(Key.A, pressed: true);
        router.ApplyGamepadButton(1, GamepadButton.South, pressed: true);
        router.ReplaceBindings(
            [new InputBinding(Key.B, JoypadButton.B)],
            [new GamepadBinding(GamepadButton.East, JoypadButton.A)]
        );

        Assert.False(router.Apply(Key.A, pressed: false));
        Assert.False(router.ApplyGamepadButton(1, GamepadButton.South, pressed: false));
        Assert.True(router.Apply(Key.B, pressed: true));
        Assert.True(router.ApplyGamepadButton(1, GamepadButton.East, pressed: true));
        Assert.Equal(
            [
                (JoypadButton.A, true),
                (JoypadButton.B, true),
                (JoypadButton.A, false),
                (JoypadButton.B, false),
                (JoypadButton.B, true),
                (JoypadButton.A, true),
            ],
            updates
        );
    }
}
