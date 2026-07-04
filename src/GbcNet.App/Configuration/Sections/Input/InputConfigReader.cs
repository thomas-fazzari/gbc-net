// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using FluentResults;
using GbcNet.App.Configuration.Kdl;
using KdlSharp;

namespace GbcNet.App.Configuration.Sections.Input;

/// <summary>
/// Reads input config from the KDL configuration document.
/// </summary>
internal static class InputConfigReader
{
    /// <summary>
    /// Reads the input section from the configuration document.
    /// </summary>
    public static Result<InputConfig> Read(KdlDocument document)
    {
        var inputNode = document.ReadRequiredSection(InputConfigSchema.InputNodeName);
        return inputNode.IsSuccess
            ? ReadInputNode(inputNode.Value)
            : inputNode.ToResult<InputConfig>();
    }

    private static Result<InputConfig> ReadInputNode(KdlNode inputNode)
    {
        var version = KdlNodeReader.ReadRequiredInt32Property(
            inputNode,
            InputConfigSchema.VersionPropertyName
        );

        if (version.IsFailed)
        {
            return version.ToResult<InputConfig>();
        }

        if (version.Value != InputConfig.SupportedVersion)
        {
            return Result.Fail(
                $"Input config version {version.Value.ToString(CultureInfo.InvariantCulture)} is not supported."
            );
        }

        var activeProfile = KdlNodeReader.ReadOptionalStringProperty(
            inputNode,
            InputConfigSchema.ActiveProfilePropertyName,
            InputConfigSchema.DefaultProfileName
        );

        if (activeProfile.IsFailed)
        {
            return activeProfile.ToResult<InputConfig>();
        }

        var profiles = ReadProfiles(inputNode);

        if (profiles.IsFailed)
        {
            return profiles.ToResult<InputConfig>();
        }

        return new InputConfig
        {
            Version = InputConfig.SupportedVersion,
            ActiveProfile = activeProfile.Value,
            Profiles = profiles.Value,
        };
    }

    private static Result<Dictionary<string, InputProfileConfig>> ReadProfiles(KdlNode inputNode)
    {
        var profiles = new Dictionary<string, InputProfileConfig>(StringComparer.Ordinal);

        foreach (var node in inputNode.Children)
        {
            if (
                !string.Equals(
                    node.Name,
                    InputConfigSchema.ProfileNodeName,
                    StringComparison.Ordinal
                )
            )
            {
                return Result.Fail(
                    $"Input config node '{InputConfigSchema.InputNodeName}' does not allow child '{node.Name}'."
                );
            }

            var profileName = KdlNodeReader.ReadRequiredStringArgument(node);

            if (profileName.IsFailed)
            {
                return profileName.ToResult<Dictionary<string, InputProfileConfig>>();
            }

            var profile = ReadProfile(node);

            if (profile.IsFailed)
            {
                return profile.ToResult<Dictionary<string, InputProfileConfig>>();
            }

            if (!profiles.TryAdd(profileName.Value, profile.Value))
            {
                return Result.Fail($"Input config has duplicate profile '{profileName.Value}'.");
            }
        }

        return profiles.Count == 0
            ? Result.Fail("Input config must contain at least one profile.")
            : profiles;
    }

    private static Result<InputProfileConfig> ReadProfile(KdlNode profileNode)
    {
        var keyboardBindings = new List<KeyboardInputBindingConfig>();

        foreach (var node in profileNode.Children)
        {
            if (
                !string.Equals(
                    node.Name,
                    InputConfigSchema.KeyboardNodeName,
                    StringComparison.Ordinal
                )
            )
            {
                return Result.Fail($"Input profile does not allow child '{node.Name}'.");
            }

            var keyboard = ReadKeyboard(node);

            if (keyboard.IsFailed)
            {
                return keyboard.ToResult<InputProfileConfig>();
            }

            keyboardBindings.AddRange(keyboard.Value);
        }

        return new InputProfileConfig { Keyboard = keyboardBindings };
    }

    private static Result<IReadOnlyList<KeyboardInputBindingConfig>> ReadKeyboard(
        KdlNode keyboardNode
    )
    {
        var bindings = new List<KeyboardInputBindingConfig>();

        foreach (var node in keyboardNode.Children)
        {
            if (!string.Equals(node.Name, InputConfigSchema.BindNodeName, StringComparison.Ordinal))
            {
                return Result.Fail($"Keyboard config does not allow child '{node.Name}'.");
            }

            var binding = ReadKeyboardBinding(node);

            if (binding.IsFailed)
            {
                return binding.ToResult<IReadOnlyList<KeyboardInputBindingConfig>>();
            }

            bindings.Add(binding.Value);
        }

        return bindings;
    }

    private static Result<KeyboardInputBindingConfig> ReadKeyboardBinding(KdlNode bindNode)
    {
        var button = KdlNodeReader.ReadRequiredStringArgument(bindNode);
        var key = KdlNodeReader.ReadRequiredStringProperty(
            bindNode,
            InputConfigSchema.KeyPropertyName
        );

        var validation = Result.Merge(button.ToResult(), key.ToResult());

        if (validation.IsFailed)
        {
            return validation.ToResult<KeyboardInputBindingConfig>();
        }

        return new KeyboardInputBindingConfig(button.Value, key.Value);
    }
}
