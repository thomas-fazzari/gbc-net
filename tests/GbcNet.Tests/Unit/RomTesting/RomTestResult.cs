// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text;

namespace GbcNet.Tests.Unit.RomTesting;

/// <summary>
/// Holds the aggregated terminal result of one ROM run.
/// </summary>
/// <param name="Status">The final verdict across all result channels.</param>
/// <param name="MachineCycles">The number of emulated M-cycles completed.</param>
/// <param name="Observations">The final snapshot from each result channel.</param>
/// <param name="Diagnostic">An optional runner diagnostic.</param>
internal sealed record RomTestResult(
    RomTestStatus Status,
    int MachineCycles,
    IReadOnlyList<RomTestObservation> Observations,
    string Diagnostic = ""
)
{
    /// <summary>
    /// Formats the status, M-cycle count, diagnostic, and observations for an assertion failure.
    /// </summary>
    public string ToFailureMessage()
    {
        var message = new StringBuilder();
        message
            .Append("Status: ")
            .AppendLine(Status.ToString())
            .Append("Machine cycles: ")
            .AppendLine(MachineCycles.ToString(CultureInfo.InvariantCulture));

        AppendSection(message, "Diagnostic", Diagnostic);

        foreach (var observation in Observations)
        {
            AppendObservation(message, observation);
        }

        return message.ToString();
    }

    private static void AppendObservation(StringBuilder message, RomTestObservation observation)
    {
        if (observation.StatusCode is { } statusCode)
        {
            message
                .Append(observation.Source)
                .Append(" status: 0x")
                .AppendLine(statusCode.ToString("X2", CultureInfo.InvariantCulture));
        }

        AppendSection(message, observation.Source + " output", observation.Output);
    }

    private static void AppendSection(StringBuilder message, string title, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        message.AppendLine(title).AppendLine(value);
    }
}
