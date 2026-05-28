namespace GbcNet.Tests.RomTesting;

internal sealed record RomTestResult(RomTestStatus Status, string SerialOutput, int MachineCycles);
