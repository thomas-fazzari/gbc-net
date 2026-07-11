// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Input;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.Tests.App.Configuration;

public sealed class InputConfigDraftTests
{
    [Fact]
    public void Constructor_RejectsInvalidInputConfig()
    {
        var config = AppConfigurationFile.CreateDefaultInputConfig();
        config.Profiles = new Dictionary<string, InputProfileConfig>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            [InputConfig.DefaultProfileName] = new(),
        };

        Assert.Throws<ArgumentException>(() => new InputConfigDraft(config));
    }

    [Fact]
    public void Constructor_DeepCopiesInputAndKeepsSelectionIndependentFromActive()
    {
        var keyboard = Bindings();
        var config = Config(new InputProfileConfig { Keyboard = keyboard });
        var draft = new InputConfigDraft(config);

        keyboard[0] = keyboard[0] with { KeyName = "D" };
        Assert.True(draft.CreateProfile("arcade").Succeeded);
        Assert.True(draft.SetActiveProfile(InputConfig.DefaultProfileName).Succeeded);

        var built = draft.Build();

        Assert.Equal(InputConfig.DefaultProfileName, draft.ActiveProfileName);
        Assert.Equal("arcade", draft.SelectedProfileName);
        Assert.Equal("Up", built.Profiles[InputConfig.DefaultProfileName].Keyboard[0].KeyName);
    }

    [Fact]
    public void CreateProfile_TrimsNameChecksDuplicatesAndClonesSelectedProfile()
    {
        var draft = new InputConfigDraft(AppConfigurationFile.CreateDefaultInputConfig());

        var result = draft.CreateProfile(" arcade ");
        var duplicate = draft.CreateProfile("ARCADE");
        var blank = draft.CreateProfile("   ");
        var changeClone = draft.SetKeyboardBinding("arcade", JoypadButton.A, Key.C);
        var built = draft.Build();

        Assert.True(result.Succeeded);
        Assert.False(duplicate.Succeeded);
        Assert.False(blank.Succeeded);
        Assert.True(changeClone.Succeeded);
        Assert.Equal("arcade", draft.SelectedProfileName);
        Assert.Equal("Z", KeyFor(built, InputConfig.DefaultProfileName, JoypadButton.A));
        Assert.Equal("C", KeyFor(built, "arcade", JoypadButton.A));
    }

    [Fact]
    public void RenameProfile_AllowsCaseOnlyNonDefaultRenameAndUpdatesActiveSpelling()
    {
        var draft = new InputConfigDraft(AppConfigurationFile.CreateDefaultInputConfig());
        Assert.True(draft.CreateProfile("Arcade").Succeeded);
        Assert.True(draft.SetActiveProfile("arcade").Succeeded);

        var result = draft.RenameProfile("ARCADE", "arcade");
        var built = draft.Build();

        Assert.True(result.Succeeded);
        Assert.Equal("arcade", draft.ActiveProfileName);
        Assert.Equal("arcade", draft.SelectedProfileName);
        Assert.Equal("arcade", built.ActiveProfile);
        Assert.True(built.Profiles.ContainsKey("arcade"));
    }

    [Fact]
    public void RenameProfile_RejectsDefaultBlankMissingAndDuplicateNames()
    {
        var draft = new InputConfigDraft(AppConfigurationFile.CreateDefaultInputConfig());
        Assert.True(draft.CreateProfile("one").Succeeded);
        Assert.True(draft.CreateProfile("two").Succeeded);

        Assert.False(draft.RenameProfile(InputConfig.DefaultProfileName, "base").Succeeded);
        Assert.False(draft.RenameProfile("one", " ").Succeeded);
        Assert.False(draft.RenameProfile("missing", "new").Succeeded);
        Assert.False(draft.RenameProfile("one", "TWO").Succeeded);
    }

    [Fact]
    public void DeleteProfile_RejectsDefaultAndActiveProfiles()
    {
        var draft = new InputConfigDraft(AppConfigurationFile.CreateDefaultInputConfig());
        Assert.True(draft.CreateProfile("arcade").Succeeded);
        Assert.False(draft.DeleteProfile(InputConfig.DefaultProfileName).Succeeded);
        Assert.True(draft.SetActiveProfile("arcade").Succeeded);

        var deleteActive = draft.DeleteProfile("ARCADE");
        Assert.True(draft.SetActiveProfile(InputConfig.DefaultProfileName).Succeeded);
        var deleteInactive = draft.DeleteProfile("arcade");

        Assert.False(deleteActive.Succeeded);
        Assert.True(deleteInactive.Succeeded);
        Assert.Equal(InputConfig.DefaultProfileName, draft.SelectedProfileName);
        Assert.DoesNotContain(
            draft.Profiles,
            profile => string.Equals(profile.Name, "arcade", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void SetActiveProfile_IsExplicitAndCaseInsensitive()
    {
        var draft = new InputConfigDraft(AppConfigurationFile.CreateDefaultInputConfig());
        Assert.True(draft.CreateProfile("Arcade").Succeeded);
        Assert.Equal(InputConfig.DefaultProfileName, draft.ActiveProfileName);

        var missing = draft.SetActiveProfile("missing");
        var active = draft.SetActiveProfile("arcade");

        Assert.False(missing.Succeeded);
        Assert.True(active.Succeeded);
        Assert.Equal("Arcade", draft.ActiveProfileName);
        Assert.Equal("Arcade", draft.SelectedProfileName);
    }

    [Fact]
    public void SetKeyboardBinding_RejectsDuplicateReservedAndUndefinedWithoutMutation()
    {
        var draft = new InputConfigDraft(AppConfigurationFile.CreateDefaultInputConfig());
        var duplicate = draft.SetKeyboardBinding(
            InputConfig.DefaultProfileName,
            JoypadButton.A,
            Key.X
        );
        var reserved = draft.SetKeyboardBinding(
            InputConfig.DefaultProfileName,
            JoypadButton.A,
            Key.Space
        );
        var undefinedKey = draft.SetKeyboardBinding(
            InputConfig.DefaultProfileName,
            JoypadButton.A,
            (Key)999999
        );
        var undefinedButton = draft.SetKeyboardBinding(
            InputConfig.DefaultProfileName,
            (JoypadButton)999999,
            Key.C
        );
        var built = draft.Build();

        Assert.False(duplicate.Succeeded);
        Assert.False(reserved.Succeeded);
        Assert.False(undefinedKey.Succeeded);
        Assert.False(undefinedButton.Succeeded);
        Assert.Equal("Z", KeyFor(built, InputConfig.DefaultProfileName, JoypadButton.A));
    }

    [Fact]
    public void GetKeyboardBinding_ReturnsSelectedProfileKeyWithoutBuildingConfig()
    {
        var draft = new InputConfigDraft(AppConfigurationFile.CreateDefaultInputConfig());
        Assert.True(draft.CreateProfile("arcade").Succeeded);
        Assert.True(draft.SetKeyboardBinding("arcade", JoypadButton.A, Key.C).Succeeded);

        Assert.Equal(Key.C, draft.GetKeyboardBinding("ARCADE", JoypadButton.A));
        Assert.Equal(
            Key.Z,
            draft.GetKeyboardBinding(InputConfig.DefaultProfileName, JoypadButton.A)
        );
    }

    [Fact]
    public void Build_DeepCopiesStrictDtosInDeterministicButtonOrder()
    {
        var source = AppConfigurationFile.CreateDefaultInputConfig();
        var draft = new InputConfigDraft(source);
        Assert.True(draft.CreateProfile("arcade").Succeeded);
        Assert.True(draft.SetKeyboardBinding("arcade", JoypadButton.A, Key.C).Succeeded);

        var built = draft.Build();
        var validation = InputConfigValidator.Validate(built);
        Assert.True(draft.SetKeyboardBinding("arcade", JoypadButton.A, Key.D).Succeeded);
        var rebuilt = draft.Build();

        Assert.Empty(validation);
        Assert.NotSame(
            source.Profiles[InputConfig.DefaultProfileName],
            built.Profiles[InputConfig.DefaultProfileName]
        );
        Assert.NotSame(
            source.Profiles[InputConfig.DefaultProfileName].Keyboard[0],
            built.Profiles[InputConfig.DefaultProfileName].Keyboard[0]
        );
        Assert.Equal(
            InputConfigValidator.RequiredButtons,
            built
                .Profiles["arcade"]
                .Keyboard.Select(binding =>
                    Enum.Parse<JoypadButton>(binding.ButtonName, ignoreCase: true)
                )
        );
        Assert.Equal("C", KeyFor(built, "arcade", JoypadButton.A));
        Assert.Equal("D", KeyFor(rebuilt, "arcade", JoypadButton.A));
    }

    private static InputConfig Config(InputProfileConfig profile) =>
        new()
        {
            Profiles = new Dictionary<string, InputProfileConfig>(StringComparer.OrdinalIgnoreCase)
            {
                [InputConfig.DefaultProfileName] = profile,
            },
        };

    private static string KeyFor(InputConfig config, string profileName, JoypadButton button) =>
        config
            .Profiles[profileName]
            .Keyboard.Single(binding =>
                string.Equals(
                    binding.ButtonName,
                    button.ToString(),
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .KeyName;

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
