// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration;
using KdlSharp;

namespace GbcNet.App.Configuration.Kdl;

/// <summary>
/// Helpers for reading strongly typed values from KDL nodes.
/// </summary>
internal static class KdlNodeReader
{
    public static int ReadRequiredInt32Property(KdlNode node, string propertyName)
    {
        var value = ReadRequiredProperty(node, propertyName);

        if (value.ValueType is not KdlValueType.Number)
        {
            throw new ConfigurationException(
                $"Node '{node.Name}' property '{propertyName}' must be a number."
            );
        }

        return value.AsInt32()
            ?? throw new ConfigurationException(
                $"Node '{node.Name}' property '{propertyName}' must be a number."
            );
    }

    public static string ReadRequiredStringProperty(KdlNode node, string propertyName) =>
        ReadStringValue(
            ReadRequiredProperty(node, propertyName),
            $"Node '{node.Name}' property '{propertyName}' must be a string."
        );

    public static string ReadOptionalStringProperty(
        KdlNode node,
        string propertyName,
        string defaultValue
    )
    {
        var value = node.GetProperty(propertyName);

        return value is null
            ? defaultValue
            : ReadStringValue(
                value,
                $"Node '{node.Name}' property '{propertyName}' must be a string."
            );
    }

    public static string ReadRequiredStringArgument(KdlNode node)
    {
        if (node.Arguments.Count != 1)
        {
            throw new ConfigurationException(
                $"Node '{node.Name}' must define exactly one string argument."
            );
        }

        return ReadStringValue(node.Arguments[0], $"Node '{node.Name}' argument must be a string.");
    }

    private static KdlValue ReadRequiredProperty(KdlNode node, string propertyName) =>
        node.GetProperty(propertyName)
        ?? throw new ConfigurationException(
            $"Node '{node.Name}' must define property '{propertyName}'."
        );

    private static string ReadStringValue(KdlValue value, string errorMessage)
    {
        if (value.ValueType is not KdlValueType.String)
        {
            throw new ConfigurationException(errorMessage);
        }

        return value.AsString() ?? throw new ConfigurationException(errorMessage);
    }
}
