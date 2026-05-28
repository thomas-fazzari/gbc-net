namespace GbcNet.Tests.RomTesting;

internal sealed record RomTestObservation(
    string Source,
    RomTestStatus? Status = null,
    string Output = "",
    byte? StatusCode = null
);
