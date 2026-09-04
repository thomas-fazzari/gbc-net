// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Input;
using GbcNet.App.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.Tests.Unit.App.Input;

public sealed class InputRouterTests
{
    [Fact]
    public void Apply_ReturnsFalseForUnboundInput()
    {
        var updates = new List<(JoypadButton Button, bool Pressed)>();
        InputRouter router = new([], [], (button, pressed) => updates.Add((button, pressed)));

        var handled = router.Apply(Key.A, pressed: true);

        handled.Should().BeFalse();
        updates.Should().BeEmpty();
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

        router.Apply(Key.A, pressed: true).Should().BeTrue();
        router.Apply(Key.A, pressed: true).Should().BeTrue();

        updates.Should().Equal((JoypadButton.A, true));
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

        updates.Should().Equal((JoypadButton.A, true), (JoypadButton.A, false));
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
            router.ApplyGamepadButton(control, pressed: true).Should().BeTrue();
            router.ApplyGamepadButton(control, pressed: false).Should().BeTrue();
        }

        updates
            .Should()
            .Equal(
                (JoypadButton.A, true),
                (JoypadButton.A, false),
                (JoypadButton.B, true),
                (JoypadButton.B, false),
                (JoypadButton.Start, true),
                (JoypadButton.Start, false),
                (JoypadButton.Select, true),
                (JoypadButton.Select, false)
            );
    }

    [Fact]
    public void Apply_KeyboardAndGamepadContributionsReleaseSharedButtonAfterFinalSource()
    {
        var updates = new List<(JoypadButton Button, bool Pressed)>();
        InputRouter router = new(
            [new InputBinding(Key.A, JoypadButton.A)],
            [new GamepadBinding(GamepadButton.South, JoypadButton.A)],
            (button, pressed) => updates.Add((button, pressed))
        );

        router.Apply(Key.A, pressed: true);
        router.ApplyGamepadButton(GamepadButton.South, pressed: true);
        router.Apply(Key.A, pressed: false);
        router.ApplyGamepadButton(GamepadButton.South, pressed: false);

        updates.Should().Equal((JoypadButton.A, true), (JoypadButton.A, false));
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
            router.ApplyGamepadDirection(direction, pressed: true).Should().BeTrue();
            router.ApplyGamepadDirection(direction, pressed: false).Should().BeTrue();
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
            router.ApplyGamepadDirection(button, pressed: true).Should().BeFalse();
        }

        updates
            .Should()
            .Equal(
                (JoypadButton.Up, true),
                (JoypadButton.Up, false),
                (JoypadButton.Down, true),
                (JoypadButton.Down, false),
                (JoypadButton.Left, true),
                (JoypadButton.Left, false),
                (JoypadButton.Right, true),
                (JoypadButton.Right, false)
            );
    }

    [Fact]
    public void ApplyGamepadButton_ReleasesSharedButtonAfterFinalContribution()
    {
        var updates = new List<(JoypadButton Button, bool Pressed)>();
        InputRouter router = new(
            [],
            [
                new GamepadBinding(GamepadButton.South, JoypadButton.A),
                new GamepadBinding(GamepadButton.East, JoypadButton.A),
            ],
            (button, pressed) => updates.Add((button, pressed))
        );

        router.ApplyGamepadButton(GamepadButton.South, pressed: true);
        router.ApplyGamepadButton(GamepadButton.East, pressed: true);
        router.ApplyGamepadButton(GamepadButton.South, pressed: false);
        router.ApplyGamepadButton(GamepadButton.East, pressed: false);

        updates.Should().Equal((JoypadButton.A, true), (JoypadButton.A, false));
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
        router.ApplyGamepadButton(GamepadButton.South, pressed: true);
        router.ApplyGamepadButton(GamepadButton.East, pressed: true);
        router.ApplyGamepadDirection(JoypadButton.Up, pressed: true);
        router.Clear();
        router.Apply(Key.A, pressed: false);
        router.Apply(Key.B, pressed: false);
        router.ApplyGamepadButton(GamepadButton.South, pressed: false);
        router.ApplyGamepadButton(GamepadButton.East, pressed: false);
        router.ApplyGamepadDirection(JoypadButton.Up, pressed: false);

        updates.Count.Should().Be(8);
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
            updates.Count(update => update == (button, true)).Should().Be(1);
            updates.Count(update => update == (button, false)).Should().Be(1);
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
        router.ApplyGamepadButton(GamepadButton.South, pressed: true);

        FluentActions
            .Invoking(() =>
                router.ReplaceBindings(
                    [new InputBinding(Key.B, JoypadButton.B)],
                    [
                        new GamepadBinding(GamepadButton.East, JoypadButton.A),
                        new GamepadBinding(GamepadButton.East, JoypadButton.B),
                    ]
                )
            )
            .Should()
            .ThrowExactly<ArgumentException>();

        router.Apply(Key.A, pressed: false).Should().BeTrue();
        router.ApplyGamepadButton(GamepadButton.South, pressed: false).Should().BeTrue();
        router.Apply(Key.B, pressed: true).Should().BeFalse();
        router.ApplyGamepadButton(GamepadButton.East, pressed: true).Should().BeFalse();
        updates
            .Should()
            .Equal(
                (JoypadButton.A, true),
                (JoypadButton.B, true),
                (JoypadButton.A, false),
                (JoypadButton.B, false)
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
        router.ApplyGamepadButton(GamepadButton.South, pressed: true);
        router.ReplaceBindings(
            [new InputBinding(Key.B, JoypadButton.B)],
            [new GamepadBinding(GamepadButton.East, JoypadButton.A)]
        );

        router.Apply(Key.A, pressed: false).Should().BeFalse();
        router.ApplyGamepadButton(GamepadButton.South, pressed: false).Should().BeFalse();
        router.Apply(Key.B, pressed: true).Should().BeTrue();
        router.ApplyGamepadButton(GamepadButton.East, pressed: true).Should().BeTrue();
        updates
            .Should()
            .Equal(
                (JoypadButton.A, true),
                (JoypadButton.B, true),
                (JoypadButton.A, false),
                (JoypadButton.B, false),
                (JoypadButton.B, true),
                (JoypadButton.A, true)
            );
    }
}
