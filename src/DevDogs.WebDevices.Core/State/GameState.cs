namespace DevDogs.WebDevices.Core.State;

/// <summary>
/// What the player is doing, as far as device routing cares.
/// </summary>
public enum GameState
{
    /// <summary>No game process is detected and no telemetry is flowing.</summary>
    SystemIdle,

    /// <summary>A game is running but the player is not driving: launching, loading or in menus.</summary>
    GameIdle,

    /// <summary>Telemetry is flowing and the game is not in its menus.</summary>
    Driving,
}
