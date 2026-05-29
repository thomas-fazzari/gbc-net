namespace GbcNet.Core.Sm83;

/// <summary>
/// Observes completed CPU instructions for debugger and breakpoint instrumentation.
/// </summary>
internal interface ICpuInstructionObserver
{
    /// <summary>
    /// Called after an instruction has executed.
    /// </summary>
    void OnInstructionExecuted(byte opcode, Registers registers);
}
