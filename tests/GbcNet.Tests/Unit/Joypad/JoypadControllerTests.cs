// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Interrupts;
using GbcNet.Core.Joypad;

namespace GbcNet.Tests.Unit.Joypad;

public sealed class JoypadControllerTests
{
    [Fact]
    public void Read_ReturnsHighBitsSetAndReleasedButtonsWhenNothingIsSelected()
    {
        var interrupts = new InterruptController();
        var joypad = new JoypadController(interrupts);

        joypad.Write(0x30, requestInterruptOnTransition: true);

        joypad.Read().Should().Be(0xFF);
    }

    [Fact]
    public void Read_ReturnsPressedDirectionAsLowBitWhenDirectionGroupIsSelected()
    {
        var interrupts = new InterruptController();
        var joypad = new JoypadController(interrupts);
        joypad.Write(0x20, requestInterruptOnTransition: true);

        joypad.SetButtonState(JoypadButton.Right, pressed: true);

        joypad.Read().Should().Be(0xEE);
    }

    [Fact]
    public void Read_ReturnsPressedActionAsLowBitWhenActionGroupIsSelected()
    {
        var interrupts = new InterruptController();
        var joypad = new JoypadController(interrupts);
        joypad.Write(0x10, requestInterruptOnTransition: true);

        joypad.SetButtonState(JoypadButton.A, pressed: true);

        joypad.Read().Should().Be(0xDE);
    }

    [Fact]
    public void Read_CombinesDirectionAndActionButtonsWhenBothGroupsAreSelected()
    {
        var interrupts = new InterruptController();
        var joypad = new JoypadController(interrupts);
        joypad.Write(0x00, requestInterruptOnTransition: true);

        joypad.SetButtonState(JoypadButton.A, pressed: true);

        joypad.Read().Should().Be(0xCE);
    }

    [Fact]
    public void SetButtonState_RequestsJoypadInterruptOnVisibleHighToLowTransition()
    {
        var interrupts = new InterruptController();
        var joypad = new JoypadController(interrupts);
        joypad.Write(0x10, requestInterruptOnTransition: true);

        joypad.SetButtonState(JoypadButton.A, pressed: true);

        interrupts.InterruptFlag.Should().Be(0b0001_0000);
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

        interrupts.InterruptFlag.Should().Be(0x00);
    }

    [Fact]
    public void Write_RequestsJoypadInterruptWhenAlreadyPressedButtonBecomesVisible()
    {
        var interrupts = new InterruptController();
        var joypad = new JoypadController(interrupts);
        joypad.Write(0x30, requestInterruptOnTransition: true);
        joypad.SetButtonState(JoypadButton.A, pressed: true);

        joypad.Write(0x10, requestInterruptOnTransition: true);

        interrupts.InterruptFlag.Should().Be(0b0001_0000);
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

        interrupts.InterruptFlag.Should().Be(0);

        joypad.Read().Should().Be(0xCC);
        joypad.Write(0x20, requestInterruptOnTransition: false);
        joypad.Read().Should().Be(0xEE);
        joypad.Write(0x10, requestInterruptOnTransition: false);
        joypad.Read().Should().Be(0xDD);
        interrupts.InterruptFlag.Should().Be(0);

        joypad.Write(0x00, requestInterruptOnTransition: false);
        joypad.SetButtonState(JoypadButton.Up, pressed: true);

        interrupts.InterruptFlag.Should().Be(0b0001_0000);
    }
}
