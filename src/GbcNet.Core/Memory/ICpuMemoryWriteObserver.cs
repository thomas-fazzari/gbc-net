namespace GbcNet.Core.Memory;

/// <summary>
/// Observes accepted CPU writes for debugger and watchpoint instrumentation.
/// </summary>
internal interface ICpuMemoryWriteObserver
{
    /// <summary>
    /// Called after a CPU-visible write has been routed by the memory bus.
    /// </summary>
    void OnCpuMemoryWritten(ushort address, byte value);
}
