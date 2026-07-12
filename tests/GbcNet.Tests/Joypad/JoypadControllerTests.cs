// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Interrupts;
using GbcNet.Core.Joypad;

namespace GbcNet.Tests.Joypad;

public sealed class JoypadControllerTests
{
    [Fact]
    public void Read_ReturnsHighBitsSetAndReleasedButtonsWhenNothingIsSelected()
    {
        var interrupts = new InterruptController();
        var joypad = new JoypadController(interrupts);

        joypad.Write(0x30, requestInterruptOnTransition: true);

        Assert.Equal(0xFF, joypad.Read());
    }

    [Fact]
    public void Read_ReturnsPressedDirectionAsLowBitWhenDirectionGroupIsSelected()
    {
        var interrupts = new InterruptController();
        var joypad = new JoypadController(interrupts);
        joypad.Write(0x20, requestInterruptOnTransition: true);

        joypad.SetButtonState(JoypadButton.Right, pressed: true);

        Assert.Equal(0xEE, joypad.Read());
    }

    [Fact]
    public void Read_ReturnsPressedActionAsLowBitWhenActionGroupIsSelected()
    {
        var interrupts = new InterruptController();
        var joypad = new JoypadController(interrupts);
        joypad.Write(0x10, requestInterruptOnTransition: true);

        joypad.SetButtonState(JoypadButton.A, pressed: true);

        Assert.Equal(0xDE, joypad.Read());
    }

    [Fact]
    public void Read_CombinesDirectionAndActionButtonsWhenBothGroupsAreSelected()
    {
        var interrupts = new InterruptController();
        var joypad = new JoypadController(interrupts);
        joypad.Write(0x00, requestInterruptOnTransition: true);

        joypad.SetButtonState(JoypadButton.A, pressed: true);

        Assert.Equal(0xCE, joypad.Read());
    }

    [Fact]
    public void SetButtonState_RequestsJoypadInterruptOnVisibleHighToLowTransition()
    {
        var interrupts = new InterruptController();
        var joypad = new JoypadController(interrupts);
        joypad.Write(0x10, requestInterruptOnTransition: true);

        joypad.SetButtonState(JoypadButton.A, pressed: true);

        Assert.Equal(0b0001_0000, interrupts.InterruptFlag);
    }

    [Fact]
    public void SetButtonState_DoesNotRequestJoypadInterruptWhenButtonIsAlreadyPressed()
    {
        var interrupts = new InterruptController();
        var joypad = new JoypadController(interrupts);
        joypad.Write(0x10, requestInterruptOnTransition: true);
        joypad.SetButtonState(JoypadButton.A, pressed: true);
        interrupts.SetInterruptFlag(0x00);

        joypad.SetButtonState(JoypadButton.A, pressed: true);

        Assert.Equal(0x00, interrupts.InterruptFlag);
    }

    [Fact]
    public void Write_RequestsJoypadInterruptWhenAlreadyPressedButtonBecomesVisible()
    {
        var interrupts = new InterruptController();
        var joypad = new JoypadController(interrupts);
        joypad.Write(0x30, requestInterruptOnTransition: true);
        joypad.SetButtonState(JoypadButton.A, pressed: true);

        joypad.Write(0x10, requestInterruptOnTransition: true);

        Assert.Equal(0b0001_0000, interrupts.InterruptFlag);
    }

    [Fact]
    public void RestoreState_RestoresSelectionAndButtonsWithoutRequestingAnInterrupt()
    {
        var interrupts = new InterruptController();
        var joypad = new JoypadController(interrupts);
        joypad.Write(0x00, requestInterruptOnTransition: false);
        joypad.SetButtonState(JoypadButton.Right, pressed: true);
        joypad.SetButtonState(JoypadButton.B, pressed: true);
        var state = joypad.CaptureState();
        joypad.Write(0x30, requestInterruptOnTransition: false);
        joypad.SetButtonState(JoypadButton.Right, pressed: false);
        joypad.SetButtonState(JoypadButton.B, pressed: false);
        interrupts.SetInterruptFlag(0);

        joypad.RestoreState(state);

        Assert.Equal(0, interrupts.InterruptFlag);

        Assert.Equal(0xCC, joypad.Read());
        joypad.Write(0x20, requestInterruptOnTransition: false);
        Assert.Equal(0xEE, joypad.Read());
        joypad.Write(0x10, requestInterruptOnTransition: false);
        Assert.Equal(0xDD, joypad.Read());
        Assert.Equal(0, interrupts.InterruptFlag);

        joypad.Write(0x00, requestInterruptOnTransition: false);
        joypad.SetButtonState(JoypadButton.Up, pressed: true);

        Assert.Equal((byte)0b0001_0000, interrupts.InterruptFlag);
    }
}
