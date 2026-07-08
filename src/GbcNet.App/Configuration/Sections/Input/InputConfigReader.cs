// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
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
    public static InputConfig Read(KdlDocument document) =>
        ReadInputNode(document.ReadRequiredSection(InputConfigSchema.InputNodeName));

    private static InputConfig ReadInputNode(KdlNode inputNode)
    {
        var version = KdlNodeReader.ReadRequiredInt32Property(
            inputNode,
            InputConfigSchema.VersionPropertyName
        );

        if (version != InputConfig.SupportedVersion)
        {
            throw new ConfigurationException(
                $"Input config version {version.ToString(CultureInfo.InvariantCulture)} is not supported."
            );
        }

        return new InputConfig
        {
            Version = InputConfig.SupportedVersion,
            ActiveProfile = KdlNodeReader.ReadOptionalStringProperty(
                inputNode,
                InputConfigSchema.ActiveProfilePropertyName,
                InputConfigSchema.DefaultProfileName
            ),
            Profiles = ReadProfiles(inputNode),
        };
    }

    private static Dictionary<string, InputProfileConfig> ReadProfiles(KdlNode inputNode)
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
                throw new ConfigurationException(
                    $"Input config node '{InputConfigSchema.InputNodeName}' does not allow child '{node.Name}'."
                );
            }

            var profileName = KdlNodeReader.ReadRequiredStringArgument(node);
            var profile = ReadProfile(node);

            if (!profiles.TryAdd(profileName, profile))
            {
                throw new ConfigurationException(
                    $"Input config has duplicate profile '{profileName}'."
                );
            }
        }

        return profiles.Count == 0
            ? throw new ConfigurationException("Input config must contain at least one profile.")
            : profiles;
    }

    private static InputProfileConfig ReadProfile(KdlNode profileNode)
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
                throw new ConfigurationException(
                    $"Input profile does not allow child '{node.Name}'."
                );
            }

            keyboardBindings.AddRange(ReadKeyboard(node));
        }

        return new InputProfileConfig { Keyboard = keyboardBindings };
    }

    private static List<KeyboardInputBindingConfig> ReadKeyboard(KdlNode keyboardNode)
    {
        var bindings = new List<KeyboardInputBindingConfig>();

        foreach (var node in keyboardNode.Children)
        {
            if (!string.Equals(node.Name, InputConfigSchema.BindNodeName, StringComparison.Ordinal))
            {
                throw new ConfigurationException(
                    $"Keyboard config does not allow child '{node.Name}'."
                );
            }

            bindings.Add(ReadKeyboardBinding(node));
        }

        return bindings;
    }

    private static KeyboardInputBindingConfig ReadKeyboardBinding(KdlNode bindNode) =>
        new(
            KdlNodeReader.ReadRequiredStringArgument(bindNode),
            KdlNodeReader.ReadRequiredStringProperty(bindNode, InputConfigSchema.KeyPropertyName)
        );
}
