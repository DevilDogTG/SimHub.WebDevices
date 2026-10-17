namespace DevDogs.WebDevices.Core.Config;

/// <summary>
/// The dashboards shown while a game is running. An empty value falls back as described in
/// <see cref="Routing.DashboardResolver"/>.
/// </summary>
public class GameDashboards
{
    /// <summary>Gets or sets the dashboard shown while the game runs but the player is not driving (menus, loading).</summary>
    public string? GameIdle { get; set; }

    /// <summary>Gets or sets the dashboard shown while driving.</summary>
    public string? Driving { get; set; }
}
