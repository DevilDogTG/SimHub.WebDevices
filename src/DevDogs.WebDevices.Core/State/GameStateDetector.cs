namespace DevDogs.WebDevices.Core.State;

/// <summary>
/// Turns SimHub's raw game flags into a <see cref="RouterState"/>.
/// </summary>
/// <remarks>
/// A paused game counts as in menus: ETS2, for one, reports its in-game menu only as paused. Replays count
/// as driving.
/// </remarks>
public static class GameStateDetector
{
    /// <summary>
    /// Returns the routing state for one telemetry frame.
    /// </summary>
    /// <param name="signals">The frame's game flags.</param>
    /// <returns>The routing state.</returns>
    public static RouterState Detect(GameSignals signals)
    {
        if (!signals.ProcessDetected && !signals.GameRunning)
        {
            // SimHub keeps a selected game while idle; leaving it out stops a game change in SimHub from switching devices.
            return RouterState.SystemIdle;
        }

        var gameName = string.IsNullOrWhiteSpace(signals.GameName) ? null : signals.GameName;
        var driving = signals.GameRunning && !signals.InMenu && !signals.Paused;
        var state = driving ? GameState.Driving : GameState.GameIdle;
        return new RouterState(state, gameName);
    }
}
