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
    public void Validate_AcceptsCompleteDefaultConfig()
    {
        var config = AppConfigurationFile.CreateDefaultInputConfig();
        var errors = InputConfigValidator.Validate(config);

        Assert.Empty(errors);
    }

    [Fact]
    public void CreateDefaultInputConfig_UsesCaseInsensitiveProfileLookup()
    {
        var config = AppConfigurationFile.CreateDefaultInputConfig();

        Assert.True(config.Profiles.ContainsKey("DEFAULT"));
    }

    [Fact]
    public void Validate_AcceptsCompleteConfigWithCaseInsensitiveActiveProfile()
    {
        var config = ValidConfig();
        config.ActiveProfile = "DEFAULT";
        config.Profiles = new Dictionary<string, InputProfileConfig>(StringComparer.Ordinal)
        {
            ["default"] = CompleteProfile(),
        };

        var errors = InputConfigValidator.Validate(config);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(0, "version")]
    [InlineData(2, "version")]
    public void Validate_RejectsUnsupportedVersion(int version, string expected)
    {
        var config = ValidConfig();
        config.Version = version;

        var errors = InputConfigValidator.Validate(config);

        Assert.Contains(
            errors,
            error => error.Contains(expected, StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Validate_RejectsEmptyProfiles()
    {
        var config = new InputConfig
        {
            Profiles = new Dictionary<string, InputProfileConfig>(StringComparer.OrdinalIgnoreCase),
        };

        var errors = InputConfigValidator.Validate(config);

        Assert.Contains(
            errors,
            error => error.Contains("at least one profile", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Validate_RejectsMissingDefaultProfile()
    {
        var config = new InputConfig
        {
            ActiveProfile = "custom",
            Profiles = new Dictionary<string, InputProfileConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["custom"] = CompleteProfile(),
            },
        };

        var errors = InputConfigValidator.Validate(config);

        Assert.Contains(
            errors,
            error => error.Contains("default", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Validate_RejectsBlankTrimmedAndDuplicateProfileNames()
    {
        var config = new InputConfig
        {
            Profiles = new Dictionary<string, InputProfileConfig>(StringComparer.Ordinal)
            {
                [InputConfig.DefaultProfileName] = CompleteProfile(),
                [" arcade "] = CompleteProfile(),
                ["Arcade"] = CompleteProfile(),
                [""] = CompleteProfile(),
            },
        };

        var errors = InputConfigValidator.Validate(config);

        Assert.Contains(
            errors,
            error => error.Contains("must be trimmed", StringComparison.Ordinal)
        );
        Assert.Contains(
            errors,
            error => error.Contains("more than once", StringComparison.Ordinal)
        );
        Assert.Contains(
            errors,
            error => error.Contains("must not be blank", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Validate_RejectsMissingActiveProfile()
    {
        var config = ValidConfig();
        config.ActiveProfile = "missing";

        var errors = InputConfigValidator.Validate(config);

        Assert.Contains(
            errors,
            error => error.Contains("does not exist", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Validate_RejectsInvalidInactiveProfile()
    {
        var config = ValidConfig();
        config.Profiles = new Dictionary<string, InputProfileConfig>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            [InputConfig.DefaultProfileName] = CompleteProfile(),
            ["inactive"] = Profile((JoypadButton.A, Key.Z)),
        };

        var errors = InputConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains("inactive", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsProfilesThatDoNotHaveExactlyEightKeyboardBindings()
    {
        var config = ValidConfig();
        config.Profiles = new Dictionary<string, InputProfileConfig>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            [InputConfig.DefaultProfileName] = Profile((JoypadButton.A, Key.Z)),
        };

        var errors = InputConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains("exactly 8", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsDuplicateAndMissingButtons()
    {
        var bindings = Bindings();
        bindings[0] = new KeyboardInputBindingConfig("a", "Up");
        var config = ConfigWithProfile(new InputProfileConfig { Keyboard = bindings });

        var errors = InputConfigValidator.Validate(config);

        Assert.Contains(
            errors,
            error => error.Contains("bound more than once", StringComparison.Ordinal)
        );
        Assert.Contains(
            errors,
            error => error.Contains("missing joypad button", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Validate_RejectsDuplicateReservedNoneAndUndefinedKeys()
    {
        var bindings = Bindings();
        bindings[0] = bindings[0] with { KeyName = "Z" };
        bindings[1] = bindings[1] with { KeyName = "Space" };
        bindings[2] = bindings[2] with { KeyName = "None" };
        bindings[3] = bindings[3] with { KeyName = "999999" };
        var config = ConfigWithProfile(new InputProfileConfig { Keyboard = bindings });

        var errors = InputConfigValidator.Validate(config);

        Assert.Contains(
            errors,
            error => error.Contains("bound more than once", StringComparison.Ordinal)
        );
        Assert.Contains(errors, error => error.Contains("reserved", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("None", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("999999", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsNumericAndUndefinedJoypadButtonText()
    {
        var bindings = Bindings();
        bindings[0] = bindings[0] with { ButtonName = "0" };
        bindings[1] = bindings[1] with { ButtonName = "NotAButton" };
        var config = ConfigWithProfile(new InputProfileConfig { Keyboard = bindings });

        var errors = InputConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains('0', StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("NotAButton", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ReportsMalformedNullMembersWithoutThrowing()
    {
        var config = ConfigWithProfile(
            new InputProfileConfig
            {
                Keyboard =
                [
                    null!,
                    new KeyboardInputBindingConfig(null!, "Z"),
                    new KeyboardInputBindingConfig("a", null!),
                ],
            }
        );

        var exception = Record.Exception(() => InputConfigValidator.Validate(config));
        var errors = InputConfigValidator.Validate(config);

        Assert.Null(exception);
        Assert.NotEmpty(errors);
    }

    [Theory]
    [InlineData(Key.Space, true)]
    [InlineData(Key.Tab, true)]
    [InlineData(Key.Enter, false)]
    public void IsReservedKey_IdentifiesApplicationShortcutKeys(Key key, bool expected)
    {
        Assert.Equal(expected, InputConfigValidator.IsReservedKey(key));
    }

    private static InputConfig ValidConfig() => ConfigWithProfile(CompleteProfile());

    private static InputConfig ConfigWithProfile(InputProfileConfig profile) =>
        new()
        {
            Profiles = new Dictionary<string, InputProfileConfig>(StringComparer.OrdinalIgnoreCase)
            {
                [InputConfig.DefaultProfileName] = profile,
            },
        };

    private static InputProfileConfig CompleteProfile() => new() { Keyboard = Bindings() };

    private static InputProfileConfig Profile(params (JoypadButton Button, Key Key)[] bindings) =>
        new()
        {
            Keyboard =
            [
                .. bindings.Select(binding => new KeyboardInputBindingConfig(
                    binding.Button.ToString(),
                    binding.Key.ToString()
                )),
            ],
        };

    private static List<KeyboardInputBindingConfig> Bindings() =>
        [
            new("up", "Up"),
            new("down", "Down"),
            new("left", "Left"),
            new("right", "Right"),
            new("a", "Z"),
            new("b", "X"),
            new("start", "Enter"),
            new("select", "Back"),
        ];
}
