using System.Globalization;
using System.Text;

namespace GbcNet.Tests.RomTesting;

internal sealed record RomTestResult(
    RomTestStatus Status,
    int MachineCycles,
    IReadOnlyList<RomTestObservation> Observations,
    string Diagnostic = ""
)
{
    public static RomTestResult TimedOut(
        int machineCycles,
        IReadOnlyList<RomTestObservation> observations
    ) => new(RomTestStatus.TimedOut, machineCycles, observations);

    public static RomTestResult FromObservations(
        RomTestStatus status,
        int machineCycles,
        IReadOnlyList<RomTestObservation> observations,
        string diagnostic = ""
    ) => new(status, machineCycles, observations, diagnostic);

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
