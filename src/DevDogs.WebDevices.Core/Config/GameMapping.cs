namespace DevDogs.WebDevices.Core.Config;

/// <summary>
/// The dashboards for one game on one device.
/// </summary>
public sealed class GameMapping : GameDashboards
{
    /// <summary>Gets or sets SimHub's game name (<c>GameData.GameName</c>, for example <c>ETS2</c>), matched ignoring case.</summary>
    public string GameName { get; set; } = "";
}
