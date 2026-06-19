using System.Globalization;
using FluentResults;
using GbcNet.App.Configuration.Kdl;
using KdlSharp;

namespace GbcNet.App.Configuration.Sections.Input;

/// <summary>
/// Reads input options from the KDL configuration document.
/// </summary>
internal static class InputOptionsReader
{
    /// <summary>
    /// Reads the input section from the configuration document.
    /// </summary>
    public static Result<InputOptions> Read(KdlDocument document)
    {
        var inputNode = document.ReadRequiredSection(InputOptionsSchema.InputNodeName);
        return inputNode.IsSuccess
            ? ReadInputNode(inputNode.Value)
            : inputNode.ToResult<InputOptions>();
    }

    private static Result<InputOptions> ReadInputNode(KdlNode inputNode)
    {
        var version = KdlNodeReader.ReadRequiredInt32Property(
            inputNode,
            InputOptionsSchema.VersionPropertyName
        );

        if (version.IsFailed)
        {
            return version.ToResult<InputOptions>();
        }

        if (version.Value != InputOptions.SupportedVersion)
        {
            return Result.Fail(
                $"Input config version {version.Value.ToString(CultureInfo.InvariantCulture)} is not supported."
            );
        }

        var activeProfile = KdlNodeReader.ReadOptionalStringProperty(
            inputNode,
            InputOptionsSchema.ActiveProfilePropertyName,
            InputOptionsSchema.DefaultProfileName
        );

        if (activeProfile.IsFailed)
        {
            return activeProfile.ToResult<InputOptions>();
        }

        var profiles = ReadProfiles(inputNode);

        if (profiles.IsFailed)
        {
            return profiles.ToResult<InputOptions>();
        }

        return new InputOptions
        {
            Version = InputOptions.SupportedVersion,
            ActiveProfile = activeProfile.Value,
            Profiles = profiles.Value,
        };
    }

    private static Result<Dictionary<string, InputProfileOptions>> ReadProfiles(KdlNode inputNode)
    {
        var profiles = new Dictionary<string, InputProfileOptions>(StringComparer.Ordinal);

        foreach (var node in inputNode.Children)
        {
            if (
                !string.Equals(
                    node.Name,
                    InputOptionsSchema.ProfileNodeName,
                    StringComparison.Ordinal
                )
            )
            {
                return Result.Fail(
                    $"Input config node '{InputOptionsSchema.InputNodeName}' does not allow child '{node.Name}'."
                );
            }

            var profileName = KdlNodeReader.ReadRequiredStringArgument(node);

            if (profileName.IsFailed)
            {
                return profileName.ToResult<Dictionary<string, InputProfileOptions>>();
            }

            var profile = ReadProfile(node);

            if (profile.IsFailed)
            {
                return profile.ToResult<Dictionary<string, InputProfileOptions>>();
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

    private static Result<InputProfileOptions> ReadProfile(KdlNode profileNode)
    {
        var keyboardBindings = new List<KeyboardInputBindingOptions>();

        foreach (var node in profileNode.Children)
        {
            if (
                !string.Equals(
                    node.Name,
                    InputOptionsSchema.KeyboardNodeName,
                    StringComparison.Ordinal
                )
            )
            {
                return Result.Fail($"Input profile does not allow child '{node.Name}'.");
            }

            var keyboard = ReadKeyboard(node);

            if (keyboard.IsFailed)
            {
                return keyboard.ToResult<InputProfileOptions>();
            }

            keyboardBindings.AddRange(keyboard.Value);
        }

        return new InputProfileOptions { Keyboard = keyboardBindings };
    }

    private static Result<IReadOnlyList<KeyboardInputBindingOptions>> ReadKeyboard(
        KdlNode keyboardNode
    )
    {
        var bindings = new List<KeyboardInputBindingOptions>();

        foreach (var node in keyboardNode.Children)
        {
            if (
                !string.Equals(node.Name, InputOptionsSchema.BindNodeName, StringComparison.Ordinal)
            )
            {
                return Result.Fail($"Keyboard config does not allow child '{node.Name}'.");
            }

            var binding = ReadKeyboardBinding(node);

            if (binding.IsFailed)
            {
                return binding.ToResult<IReadOnlyList<KeyboardInputBindingOptions>>();
            }

            bindings.Add(binding.Value);
        }

        return bindings;
    }

    private static Result<KeyboardInputBindingOptions> ReadKeyboardBinding(KdlNode bindNode)
    {
        var button = KdlNodeReader.ReadRequiredStringArgument(bindNode);
        var key = KdlNodeReader.ReadRequiredStringProperty(
            bindNode,
            InputOptionsSchema.KeyPropertyName
        );

        var validation = Result.Merge(button.ToResult(), key.ToResult());

        if (validation.IsFailed)
        {
            return validation.ToResult<KeyboardInputBindingOptions>();
        }

        return new KeyboardInputBindingOptions(button.Value, key.Value);
    }
}
