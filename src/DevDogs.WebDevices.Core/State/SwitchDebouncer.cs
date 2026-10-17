using System;

namespace DevDogs.WebDevices.Core.State;

/// <summary>
/// Holds back a state change until the new state has lasted long enough, so brief drops (a game
/// restart, a loading screen) do not flicker every device. Fed once per telemetry frame; not thread-safe.
/// </summary>
public sealed class SwitchDebouncer
{
    private TimeSpan _systemIdleDelay;
    private TimeSpan _switchDelay;
    private RouterState? _candidate;
    private TimeSpan _candidateSince;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwitchDebouncer"/> class.
    /// </summary>
    /// <param name="initial">The state devices start in.</param>
    /// <param name="systemIdleDelay">How long system idle must last before switching to it.</param>
    /// <param name="switchDelay">How long any other new state must last before switching to it.</param>
    public SwitchDebouncer(RouterState initial, TimeSpan systemIdleDelay, TimeSpan switchDelay)
    {
        Current = initial;
        SystemIdleDelay = systemIdleDelay;
        SwitchDelay = switchDelay;
    }

    /// <summary>Gets the state devices should currently show.</summary>
    public RouterState Current { get; private set; }

    /// <summary>Gets or sets how long system idle must last before switching to it.</summary>
    public TimeSpan SystemIdleDelay
    {
        get => _systemIdleDelay;
        set => _systemIdleDelay = NotNegative(value, nameof(SystemIdleDelay));
    }

    /// <summary>Gets or sets how long any other new state must last before switching to it.</summary>
    public TimeSpan SwitchDelay
    {
        get => _switchDelay;
        set => _switchDelay = NotNegative(value, nameof(SwitchDelay));
    }

    /// <summary>
    /// Records the state seen in one frame.
    /// </summary>
    /// <param name="observed">The state detected this frame.</param>
    /// <param name="now">A monotonic timestamp (for example from a <see cref="System.Diagnostics.Stopwatch"/>).</param>
    /// <returns><see langword="true"/> when <see cref="Current"/> changed on this call.</returns>
    public bool Update(RouterState observed, TimeSpan now)
    {
        if (observed == Current)
        {
            _candidate = null;
            return false;
        }

        if (observed != _candidate)
        {
            _candidate = observed;
            _candidateSince = now;
        }

        var delay = observed.State == GameState.SystemIdle ? _systemIdleDelay : _switchDelay;
        if (now - _candidateSince < delay)
        {
            return false;
        }

        Current = observed;
        _candidate = null;
        return true;
    }

    private static TimeSpan NotNegative(TimeSpan value, string name) =>
        value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(name, value, "A delay cannot be negative.");
}
