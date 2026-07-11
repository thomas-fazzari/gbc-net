// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Input;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.Tests.App.Configuration;

public sealed class InputConfigValidatorTests
{
    [Fact]
    public void CreateDefaultInputConfig_StrictlyValidatesBothSections()
    {
        var config = AppConfigurationFile.CreateDefaultInputConfig();

        Assert.Empty(InputConfigValidator.Validate(config));
        Assert.Equal(InputConfig.DefaultProfileName, config.Keyboard.ActiveProfile);
        Assert.Equal(InputConfig.DefaultProfileName, config.Gamepad.ActiveProfile);
        Assert.True(config.Keyboard.Profiles.ContainsKey(InputConfig.DefaultProfileName));
        Assert.True(config.Gamepad.Profiles.ContainsKey(InputConfig.DefaultProfileName));
    }

    [Fact]
    public void CreateDefaultInputConfig_UsesExpectedGamepadMappings()
    {
        var bindings = AppConfigurationFile
            .CreateDefaultInputConfig()
            .Gamepad.Profiles[InputConfig.DefaultProfileName]
            .Bindings.ToDictionary(
                binding => binding.ButtonName,
                binding => binding.ControlName,
                StringComparer.Ordinal
            );

        Assert.Equal("East", bindings["A"]);
        Assert.Equal("South", bindings["B"]);
        Assert.Equal("Start", bindings["Start"]);
        Assert.Equal("Back", bindings["Select"]);
    }

    [Fact]
    public void Validate_AcceptsIndependentKeyboardAndGamepadProfilesWithSharedNames()
    {
        var config = AppConfigurationFile.CreateDefaultInputConfig();
        config.Keyboard = new KeyboardInputConfig
        {
            ActiveProfile = "keyboard-only",
            Profiles = new Dictionary<string, KeyboardProfileConfig>(StringComparer.Ordinal)
            {
                [InputConfig.DefaultProfileName] = CompleteKeyboardProfile(),
                ["shared"] = CompleteKeyboardProfile(),
                ["keyboard-only"] = CompleteKeyboardProfile(),
            },
        };
        config.Gamepad = new GamepadInputConfig
        {
            ActiveProfile = "shared",
            Profiles = new Dictionary<string, GamepadProfileConfig>(StringComparer.Ordinal)
            {
                [InputConfig.DefaultProfileName] = CompleteGamepadProfile(),
                ["shared"] = CompleteGamepadProfile(),
            },
        };

        var errors = InputConfigValidator.Validate(config);

        Assert.Empty(errors);
        Assert.Equal("keyboard-only", config.Keyboard.ActiveProfile);
        Assert.Equal("shared", config.Gamepad.ActiveProfile);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Validate_RejectsUnsupportedVersion(int version)
    {
        var config = AppConfigurationFile.CreateDefaultInputConfig();
        config.Version = version;

        var errors = InputConfigValidator.Validate(config);

        Assert.Contains(
            errors,
            error => error.Contains("version", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Validate_RejectsInvalidKeyboardProfiles()
    {
        var config = AppConfigurationFile.CreateDefaultInputConfig();
        config.Keyboard = new KeyboardInputConfig
        {
            ActiveProfile = "missing",
            Profiles = new Dictionary<string, KeyboardProfileConfig>(StringComparer.Ordinal)
            {
                [InputConfig.DefaultProfileName] = new()
                {
                    Bindings = [new("A", "Z"), new("B", "Z")],
                },
            },
        };

        var errors = InputConfigValidator.Validate(config);

        Assert.Contains(
            errors,
            error => error.Contains("exactly 8", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Contains(
            errors,
            error => error.Contains("more than once", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Contains(
            errors,
            error => error.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Validate_AcceptsAllowedDistinctGamepadControls()
    {
        var config = AppConfigurationFile.CreateDefaultInputConfig();
        config.Gamepad = new GamepadInputConfig
        {
            ActiveProfile = InputConfig.DefaultProfileName,
            Profiles = new Dictionary<string, GamepadProfileConfig>(StringComparer.Ordinal)
            {
                [InputConfig.DefaultProfileName] = new()
                {
                    Bindings =
                    [
                        new("A", "North"),
                        new("B", "West"),
                        new("Start", "LeftShoulder"),
                        new("Select", "RightShoulder"),
                    ],
                },
            },
        };

        Assert.Empty(InputConfigValidator.Validate(config));
    }

    [Fact]
    public void Validate_RejectsGamepadProfilesWithoutFourAllowedDistinctControls()
    {
        var config = AppConfigurationFile.CreateDefaultInputConfig();
        config.Gamepad = new GamepadInputConfig
        {
            ActiveProfile = InputConfig.DefaultProfileName,
            Profiles = new Dictionary<string, GamepadProfileConfig>(StringComparer.Ordinal)
            {
                [InputConfig.DefaultProfileName] = new()
                {
                    Bindings =
                    [
                        new("A", "East"),
                        new("B", "East"),
                        new("Start", "Start"),
                        new("Up", "North"),
                    ],
                },
            },
        };

        var errors = InputConfigValidator.Validate(config);

        Assert.Contains(
            errors,
            error => error.Contains("control 'East' more than once", StringComparison.Ordinal)
        );
        Assert.Contains(
            errors,
            error => error.Contains("cannot bind joypad button 'Up'", StringComparison.Ordinal)
        );
        Assert.Contains(
            errors,
            error => error.Contains("missing joypad button 'Select'", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Validate_ReportsMalformedNullSectionsWithoutThrowing()
    {
        var config = new InputConfig
        {
            Version = InputConfig.SupportedVersion,
            Keyboard = null!,
            Gamepad = null!,
        };

        var exception = Record.Exception(() => InputConfigValidator.Validate(config));
        var errors = InputConfigValidator.Validate(config);

        Assert.Null(exception);
        Assert.Contains(
            errors,
            error => error.Contains("Keyboard input config is malformed", StringComparison.Ordinal)
        );
        Assert.Contains(
            errors,
            error => error.Contains("Gamepad input config is malformed", StringComparison.Ordinal)
        );
    }

    [Theory]
    [InlineData(Key.Space, true)]
    [InlineData(Key.Tab, true)]
    [InlineData(Key.Enter, false)]
    public void IsReservedKey_IdentifiesApplicationShortcutKeys(Key key, bool expected)
    {
        Assert.Equal(expected, InputConfigValidator.IsReservedKey(key));
    }

    private static KeyboardProfileConfig CompleteKeyboardProfile() =>
        new()
        {
            Bindings =
            [
                new("Up", "Up"),
                new("Down", "Down"),
                new("Left", "Left"),
                new("Right", "Right"),
                new("A", "Z"),
                new("B", "X"),
                new("Start", "Enter"),
                new("Select", "Back"),
            ],
        };

    private static GamepadProfileConfig CompleteGamepadProfile() =>
        new()
        {
            Bindings =
            [
                new("A", "East"),
                new("B", "South"),
                new("Start", "Start"),
                new("Select", "Back"),
            ],
        };
}
