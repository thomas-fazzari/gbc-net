namespace GbcNet.Core.Sm83;

/// <summary>
/// SM83 condition codes encoded in conditional jump, call, and return instructions.
/// </summary>
internal enum ConditionCode : byte
{
    /// <summary>
    /// Z flag is reset.
    /// </summary>
    NotZero = 0,

    /// <summary>
    /// Z flag is set.
    /// </summary>
    Zero = 1,

    /// <summary>
    /// C flag is reset.
    /// </summary>
    NotCarry = 2,

    /// <summary>
    /// C flag is set.
    /// </summary>
    Carry = 3,
}
