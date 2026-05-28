namespace GbcNet.Core.Serial;

/// <summary>
/// Provides the byte latched when a serial transfer starts.
/// </summary>
public sealed class SerialByteTransferredEventArgs(byte value) : EventArgs
{
    /// <summary>
    /// Byte written to SB before the transfer started.
    /// </summary>
    public byte Value { get; } = value;
}
