namespace DevDogs.WebDevices.Core.State;

/// <summary>
/// The parts of SimHub's <c>GameData</c> that decide the <see cref="GameState"/>, copied out so the
/// rules can be tested without SimHub.
/// </summary>
/// <param name="GameName">SimHub's current game (<c>GameData.GameName</c>).</param>
/// <param name="ProcessDetected">Whether the game process is running (<c>GameData.RunningGameProcessDetected</c>).</param>
/// <param name="GameRunning">Whether telemetry is flowing (<c>GameData.GameRunning</c>).</param>
/// <param name="InMenu">Whether the game reports being in its menus (<c>GameData.GameInMenu</c>).</param>
/// <param name="Paused">Whether the game reports being paused (<c>GameData.GamePaused</c>); games such as ETS2
/// report their in-game menu this way instead of through <paramref name="InMenu"/>.</param>
public sealed record GameSignals(string? GameName, bool ProcessDetected, bool GameRunning, bool InMenu, bool Paused = false);
