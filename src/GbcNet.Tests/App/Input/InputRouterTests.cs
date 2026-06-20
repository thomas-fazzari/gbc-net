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
}
