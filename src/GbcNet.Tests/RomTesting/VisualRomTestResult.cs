using GbcNet.Core.Ppu;

namespace GbcNet.Tests.RomTesting;

internal sealed record VisualRomTestResult(LcdFrame? Frame, int CompletedFrames, int MachineCycles);
