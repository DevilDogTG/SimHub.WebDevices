namespace DevDogs.WebDevices.Core.State;

/// <summary>
/// The state devices are routed by: the game state plus, while a game runs, which game.
/// Value equality, so an unchanged state never triggers a switch.
/// </summary>
/// <param name="State">The game state.</param>
/// <param name="GameName">The running game, or <see langword="null"/> when idle or unknown.</param>
public sealed record RouterState(GameState State, string? GameName)
{
    /// <summary>Gets the state when no game is running.</summary>
    public static RouterState SystemIdle { get; } = new(GameState.SystemIdle, null);
}
