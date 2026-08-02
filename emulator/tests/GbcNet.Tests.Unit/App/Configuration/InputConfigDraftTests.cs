// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Input;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.App.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.Tests.Unit.App.Configuration;

public sealed class InputConfigDraftTests
{
    [Fact]
    public void Constructor_RejectsInvalidInputConfig()
    {
        var config = AppConfigurationFile.CreateDefaultInputConfig();
        config.Keyboard = new KeyboardInputConfig
        {
            ActiveProfile = InputConfig.DefaultProfileName,
            Profiles = new Dictionary<string, KeyboardProfileConfig>(StringComparer.Ordinal)
            {
                [InputConfig.DefaultProfileName] = new(),
            },
        };

        FluentActions
            .Invoking(() => new InputConfigDraft(config))
            .Should()
            .ThrowExactly<ArgumentException>();
    }

    [Fact]
    public void ProfileLifecycles_AreIndependentAndCloneWithinTheirOwnSection()
    {
        var draft = new InputConfigDraft(AppConfigurationFile.CreateDefaultInputConfig());

        draft.CreateKeyboardProfile(" shared ").Succeeded.Should().BeTrue();
        draft.CreateGamepadProfile("shared").Succeeded.Should().BeTrue();
        draft.SetKeyboardBinding("shared", JoypadButton.A, Key.C).Succeeded.Should().BeTrue();
        draft
            .SetGamepadBinding("shared", JoypadButton.A, GamepadButton.West)
            .Succeeded.Should()
            .BeTrue();
        draft.SetActiveKeyboardProfile("shared").Succeeded.Should().BeTrue();

        var built = draft.Build();

        draft.ActiveKeyboardProfileName.Should().Be("shared");
        draft.ActiveGamepadProfileName.Should().Be(InputConfig.DefaultProfileName);
        draft.SelectedKeyboardProfileName.Should().Be("shared");
        draft.SelectedGamepadProfileName.Should().Be("shared");
        KeyboardBindingFor(built, "shared", JoypadButton.A).Should().Be("C");
        KeyboardBindingFor(built, InputConfig.DefaultProfileName, JoypadButton.A).Should().Be("Z");
        GamepadBindingFor(built, "shared", JoypadButton.A).Should().Be("West");
        GamepadBindingFor(built, InputConfig.DefaultProfileName, JoypadButton.A)
            .Should()
            .Be("East");
        built.Keyboard.Profiles.ContainsKey("shared").Should().BeTrue();
        built.Gamepad.Profiles.ContainsKey("shared").Should().BeTrue();
    }

    [Fact]
    public void KeyboardAndGamepadProfiles_CanUseTheSameNameAndHaveIndependentActiveStates()
    {
        var draft = new InputConfigDraft(AppConfigurationFile.CreateDefaultInputConfig());

        draft.CreateKeyboardProfile("arcade").Succeeded.Should().BeTrue();
        draft.CreateGamepadProfile("arcade").Succeeded.Should().BeTrue();
        draft.SetActiveKeyboardProfile("arcade").Succeeded.Should().BeTrue();
        draft.SetActiveGamepadProfile(InputConfig.DefaultProfileName).Succeeded.Should().BeTrue();

        var built = draft.Build();

        built.Keyboard.ActiveProfile.Should().Be("arcade");
        built.Gamepad.ActiveProfile.Should().Be(InputConfig.DefaultProfileName);
        draft
            .KeyboardProfiles.Should()
            .Contain(profile => string.Equals(profile.Name, "arcade", StringComparison.Ordinal));
        draft
            .GamepadProfiles.Should()
            .Contain(profile => string.Equals(profile.Name, "arcade", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateAssignments_MutateImmediatelyReportBothRowsAndCanBeSwapped()
    {
        var draft = new InputConfigDraft(AppConfigurationFile.CreateDefaultInputConfig());

        draft
            .SetKeyboardBinding(InputConfig.DefaultProfileName, JoypadButton.A, Key.X)
            .Succeeded.Should()
            .BeTrue();

        draft
            .SetGamepadBinding(InputConfig.DefaultProfileName, JoypadButton.A, GamepadButton.South)
            .Succeeded.Should()
            .BeTrue();

        draft.KeyboardBindingConflicts.Should().Contain(JoypadButton.A);
        draft.KeyboardBindingConflicts.Should().Contain(JoypadButton.B);
        draft.GamepadBindingConflicts.Should().Contain(JoypadButton.A);
        draft.GamepadBindingConflicts.Should().Contain(JoypadButton.B);
        draft
            .Validate()
            .Should()
            .Contain(error => error.Contains("more than once", StringComparison.OrdinalIgnoreCase));

        draft
            .SetKeyboardBinding(InputConfig.DefaultProfileName, JoypadButton.B, Key.Z)
            .Succeeded.Should()
            .BeTrue();

        draft
            .SetGamepadBinding(InputConfig.DefaultProfileName, JoypadButton.B, GamepadButton.East)
            .Succeeded.Should()
            .BeTrue();

        draft.KeyboardBindingConflicts.Should().BeEmpty();
        draft.GamepadBindingConflicts.Should().BeEmpty();
        draft.Validate().Should().BeEmpty();
        draft.GetKeyboardBinding(InputConfig.DefaultProfileName, JoypadButton.A).Should().Be(Key.X);
        draft.GetKeyboardBinding(InputConfig.DefaultProfileName, JoypadButton.B).Should().Be(Key.Z);
        draft
            .GetGamepadBinding(InputConfig.DefaultProfileName, JoypadButton.A)
            .Should()
            .Be(GamepadButton.South);
        draft
            .GetGamepadBinding(InputConfig.DefaultProfileName, JoypadButton.B)
            .Should()
            .Be(GamepadButton.East);
    }

    [Fact]
    public void Build_DeepCopiesBothProfileSets()
    {
        var source = AppConfigurationFile.CreateDefaultInputConfig();
        var draft = new InputConfigDraft(source);
        draft.CreateKeyboardProfile("arcade").Succeeded.Should().BeTrue();
        draft.CreateGamepadProfile("arcade").Succeeded.Should().BeTrue();

        var built = draft.Build();
        draft.SetKeyboardBinding("arcade", JoypadButton.A, Key.C).Succeeded.Should().BeTrue();
        draft
            .SetGamepadBinding("arcade", JoypadButton.A, GamepadButton.North)
            .Succeeded.Should()
            .BeTrue();
        var rebuilt = draft.Build();

        InputConfigValidator.Validate(built).Should().BeEmpty();
        built
            .Keyboard.Profiles[InputConfig.DefaultProfileName]
            .Should()
            .NotBeSameAs(source.Keyboard.Profiles[InputConfig.DefaultProfileName]);
        built
            .Gamepad.Profiles[InputConfig.DefaultProfileName]
            .Should()
            .NotBeSameAs(source.Gamepad.Profiles[InputConfig.DefaultProfileName]);
        GamepadBindingFor(built, "arcade", JoypadButton.A).Should().Be("East");
        GamepadBindingFor(rebuilt, "arcade", JoypadButton.A).Should().Be("North");
    }

    private static string KeyboardBindingFor(
        InputConfig config,
        string profileName,
        JoypadButton button
    ) =>
        config
            .Keyboard.Profiles[profileName]
            .Bindings.Single(binding =>
                string.Equals(binding.ButtonName, button.ToString(), StringComparison.Ordinal)
            )
            .KeyName;

    private static string GamepadBindingFor(
        InputConfig config,
        string profileName,
        JoypadButton button
    ) =>
        config
            .Gamepad.Profiles[profileName]
            .Bindings.Single(binding =>
                string.Equals(binding.ButtonName, button.ToString(), StringComparison.Ordinal)
            )
            .ControlName;
}
