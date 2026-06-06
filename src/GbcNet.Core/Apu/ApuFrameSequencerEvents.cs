namespace GbcNet.Core.Apu;

/// <summary>
/// Frame sequencer events produced by one DIV-APU tick.
/// </summary>
internal readonly record struct ApuFrameSequencerEvents(bool Length, bool Sweep, bool Envelope);
