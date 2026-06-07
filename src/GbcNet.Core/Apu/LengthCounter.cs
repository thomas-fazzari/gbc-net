namespace GbcNet.Core.Apu;

/// <summary>
/// Shared APU length counter that disables a channel when enabled and expired.
/// </summary>
internal sealed class LengthCounter(int maxLength)
{
    private int _counter;
    private bool _enabled;

    /// <summary>
    /// Loads the hardware initial length as remaining ticks until expiry.
    /// </summary>
    public void WriteInitialLength(byte value)
    {
        _counter = maxLength - value;
    }

    /// <summary>
    /// Reloads full length on trigger only if the counter already expired.
    /// </summary>
    public void TriggerReloadIfExpired()
    {
        if (_counter == 0)
        {
            _counter = maxLength;
        }
    }

    /// <summary>
    /// Sets whether frame-sequencer length clocks affect this counter.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
    }

    /// <summary>
    /// Clocks length once and returns true when this tick expired it.
    /// </summary>
    public bool Clock()
    {
        if (!_enabled || _counter == 0)
        {
            return false;
        }

        _counter--;
        return _counter == 0;
    }

    /// <summary>
    /// Clears length state on APU power-off.
    /// </summary>
    public void PowerOff()
    {
        _counter = 0;
        _enabled = false;
    }
}
