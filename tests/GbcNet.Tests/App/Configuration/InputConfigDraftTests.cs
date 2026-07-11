// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Input;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.App.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.Tests.App.Configuration;

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

        Assert.Throws<ArgumentException>(() => new InputConfigDraft(config));
    }

    [Fact]
    public void ProfileLifecycles_AreIndependentAndCloneWithinTheirOwnSection()
    {
        var draft = new InputConfigDraft(AppConfigurationFile.CreateDefaultInputConfig());

        Assert.True(draft.CreateKeyboardProfile(" shared ").Succeeded);
        Assert.True(draft.CreateGamepadProfile("shared").Succeeded);
        Assert.True(draft.SetKeyboardBinding("shared", JoypadButton.A, Key.C).Succeeded);
        Assert.True(
            draft.SetGamepadBinding("shared", JoypadButton.A, GamepadButton.West).Succeeded
        );
        Assert.True(draft.SetActiveKeyboardProfile("shared").Succeeded);

        var built = draft.Build();

        Assert.Equal("shared", draft.ActiveKeyboardProfileName);
        Assert.Equal(InputConfig.DefaultProfileName, draft.ActiveGamepadProfileName);
        Assert.Equal("shared", draft.SelectedKeyboardProfileName);
        Assert.Equal("shared", draft.SelectedGamepadProfileName);
        Assert.Equal("C", KeyboardBindingFor(built, "shared", JoypadButton.A));
        Assert.Equal(
            "Z",
            KeyboardBindingFor(built, InputConfig.DefaultProfileName, JoypadButton.A)
        );
        Assert.Equal("West", GamepadBindingFor(built, "shared", JoypadButton.A));
        Assert.Equal(
            "East",
            GamepadBindingFor(built, InputConfig.DefaultProfileName, JoypadButton.A)
        );
        Assert.True(built.Keyboard.Profiles.ContainsKey("shared"));
        Assert.True(built.Gamepad.Profiles.ContainsKey("shared"));
    }

    [Fact]
    public void KeyboardAndGamepadProfiles_CanUseTheSameNameAndHaveIndependentActiveStates()
    {
        var draft = new InputConfigDraft(AppConfigurationFile.CreateDefaultInputConfig());

        Assert.True(draft.CreateKeyboardProfile("arcade").Succeeded);
        Assert.True(draft.CreateGamepadProfile("arcade").Succeeded);
        Assert.True(draft.SetActiveKeyboardProfile("arcade").Succeeded);
        Assert.True(draft.SetActiveGamepadProfile(InputConfig.DefaultProfileName).Succeeded);

        var built = draft.Build();

        Assert.Equal("arcade", built.Keyboard.ActiveProfile);
        Assert.Equal(InputConfig.DefaultProfileName, built.Gamepad.ActiveProfile);
        Assert.Contains(
            draft.KeyboardProfiles,
            profile => string.Equals(profile.Name, "arcade", StringComparison.Ordinal)
        );
        Assert.Contains(
            draft.GamepadProfiles,
            profile => string.Equals(profile.Name, "arcade", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void DuplicateAssignments_MutateImmediatelyReportBothRowsAndCanBeSwapped()
    {
        var draft = new InputConfigDraft(AppConfigurationFile.CreateDefaultInputConfig());

        Assert.True(
            draft
                .SetKeyboardBinding(InputConfig.DefaultProfileName, JoypadButton.A, Key.X)
                .Succeeded
        );
        Assert.True(
            draft
                .SetGamepadBinding(
                    InputConfig.DefaultProfileName,
                    JoypadButton.A,
                    GamepadButton.South
                )
                .Succeeded
        );

        Assert.Contains(JoypadButton.A, draft.KeyboardBindingConflicts);
        Assert.Contains(JoypadButton.B, draft.KeyboardBindingConflicts);
        Assert.Contains(JoypadButton.A, draft.GamepadBindingConflicts);
        Assert.Contains(JoypadButton.B, draft.GamepadBindingConflicts);
        Assert.Contains(
            draft.Validate(),
            error => error.Contains("more than once", StringComparison.OrdinalIgnoreCase)
        );

        Assert.True(
            draft
                .SetKeyboardBinding(InputConfig.DefaultProfileName, JoypadButton.B, Key.Z)
                .Succeeded
        );
        Assert.True(
            draft
                .SetGamepadBinding(
                    InputConfig.DefaultProfileName,
                    JoypadButton.B,
                    GamepadButton.East
                )
                .Succeeded
        );

        Assert.Empty(draft.KeyboardBindingConflicts);
        Assert.Empty(draft.GamepadBindingConflicts);
        Assert.Empty(draft.Validate());
        Assert.Equal(
            Key.X,
            draft.GetKeyboardBinding(InputConfig.DefaultProfileName, JoypadButton.A)
        );
        Assert.Equal(
            Key.Z,
            draft.GetKeyboardBinding(InputConfig.DefaultProfileName, JoypadButton.B)
        );
        Assert.Equal(
            GamepadButton.South,
            draft.GetGamepadBinding(InputConfig.DefaultProfileName, JoypadButton.A)
        );
        Assert.Equal(
            GamepadButton.East,
            draft.GetGamepadBinding(InputConfig.DefaultProfileName, JoypadButton.B)
        );
    }

    [Fact]
    public void Build_DeepCopiesBothProfileSets()
    {
        var source = AppConfigurationFile.CreateDefaultInputConfig();
        var draft = new InputConfigDraft(source);
        Assert.True(draft.CreateKeyboardProfile("arcade").Succeeded);
        Assert.True(draft.CreateGamepadProfile("arcade").Succeeded);

        var built = draft.Build();
        Assert.True(draft.SetKeyboardBinding("arcade", JoypadButton.A, Key.C).Succeeded);
        Assert.True(
            draft.SetGamepadBinding("arcade", JoypadButton.A, GamepadButton.North).Succeeded
        );
        var rebuilt = draft.Build();

        Assert.Empty(InputConfigValidator.Validate(built));
        Assert.NotSame(
            source.Keyboard.Profiles[InputConfig.DefaultProfileName],
            built.Keyboard.Profiles[InputConfig.DefaultProfileName]
        );
        Assert.NotSame(
            source.Gamepad.Profiles[InputConfig.DefaultProfileName],
            built.Gamepad.Profiles[InputConfig.DefaultProfileName]
        );
        Assert.Equal("East", GamepadBindingFor(built, "arcade", JoypadButton.A));
        Assert.Equal("North", GamepadBindingFor(rebuilt, "arcade", JoypadButton.A));
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
