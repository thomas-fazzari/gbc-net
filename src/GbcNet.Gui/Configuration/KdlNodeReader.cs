using FluentResults;
using KdlSharp;

namespace GbcNet.Gui.Configuration;

/// <summary>
/// Helpers for reading strongly typed values from KDL nodes.
/// </summary>
internal static class KdlNodeReader
{
    public static Result<int> ReadRequiredInt32Property(KdlNode node, string propertyName)
    {
        Result<KdlValue> value = ReadRequiredProperty(node, propertyName);

        if (value.IsFailed)
        {
            return value.ToResult<int>();
        }

        if (value.Value.ValueType is not KdlValueType.Number)
        {
            return Result.Fail($"Node '{node.Name}' property '{propertyName}' must be a number.");
        }

        int? intValue = value.Value.AsInt32();

        return intValue.HasValue
            ? Result.Ok(intValue.Value)
            : Result.Fail($"Node '{node.Name}' property '{propertyName}' must be a number.");
    }

    public static Result<string> ReadRequiredStringProperty(KdlNode node, string propertyName)
    {
        Result<KdlValue> value = ReadRequiredProperty(node, propertyName);

        return value.IsSuccess
            ? ReadStringValue(
                value.Value,
                $"Node '{node.Name}' property '{propertyName}' must be a string."
            )
            : value.ToResult<string>();
    }

    public static Result<string> ReadOptionalStringProperty(
        KdlNode node,
        string propertyName,
        string defaultValue
    )
    {
        KdlValue? value = node.GetProperty(propertyName);

        return value is null
            ? defaultValue
            : ReadStringValue(
                value,
                $"Node '{node.Name}' property '{propertyName}' must be a string."
            );
    }

    public static Result<string> ReadRequiredStringArgument(KdlNode node)
    {
        if (node.Arguments.Count != 1)
        {
            return Result.Fail($"Node '{node.Name}' must define exactly one string argument.");
        }

        return ReadStringValue(node.Arguments[0], $"Node '{node.Name}' argument must be a string.");
    }

    private static Result<KdlValue> ReadRequiredProperty(KdlNode node, string propertyName)
    {
        KdlValue? value = node.GetProperty(propertyName);

        return value is not null
            ? Result.Ok(value)
            : Result.Fail($"Node '{node.Name}' must define property '{propertyName}'.");
    }

    private static Result<string> ReadStringValue(KdlValue value, string errorMessage)
    {
        if (value.ValueType is not KdlValueType.String)
        {
            return Result.Fail(errorMessage);
        }

        string? stringValue = value.AsString();

        return stringValue is not null ? Result.Ok(stringValue) : Result.Fail(errorMessage);
    }
}
