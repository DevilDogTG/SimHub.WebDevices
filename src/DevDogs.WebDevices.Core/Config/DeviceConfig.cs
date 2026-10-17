using System.Collections.Generic;

namespace DevDogs.WebDevices.Core.Config;

/// <summary>
/// One browser device and the dashboards it shows in each game state.
/// </summary>
public sealed class DeviceConfig
{
    /// <summary>Gets or sets the name used in the device URL (<c>/&lt;name&gt;/</c>); see <see cref="DeviceNameRules"/>.</summary>
    public string Name { get; set; } = "";

    /// <summary>Gets or sets a value indicating whether SimHub's on-screen dashboard controls are hidden.</summary>
    public bool HideControls { get; set; }

    /// <summary>Gets or sets the dashboard shown when no game process is detected.</summary>
    public string? SystemIdleDashboard { get; set; }

    /// <summary>Gets or sets the dashboards used for games that have no mapping of their own.</summary>
    public GameDashboards Default { get; set; } = new();

    /// <summary>Gets or sets the per-game dashboards, keyed by SimHub's game name.</summary>
    public List<GameMapping> Games { get; set; } = [];
}
