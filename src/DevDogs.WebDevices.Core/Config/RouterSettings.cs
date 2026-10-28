using System.Collections.Generic;

namespace DevDogs.WebDevices.Core.Config;

/// <summary>
/// Every user setting of the plugin. Stored by SimHub's common settings storage and used as the
/// JSON export format (docs/adr/ADR-0004), so properties are mutable and default-initialised.
/// </summary>
public sealed class RouterSettings
{
    /// <summary>The current export format version.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Gets or sets the format version, so later releases can migrate older exports.</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Gets or sets the TCP port devices connect to.</summary>
    public int Port { get; set; } = 8890;

    /// <summary>Gets or sets the port of SimHub's own web server, which serves the dashboards.</summary>
    public int SimHubWebPort { get; set; } = 8888;

    /// <summary>
    /// Gets or sets how long, in milliseconds, the game must stay gone before devices switch to the
    /// system idle dashboard. Longer than other switches so a game restart does not flicker every device.
    /// </summary>
    public int SystemIdleDelayMs { get; set; } = 3000;

    /// <summary>Gets or sets how long, in milliseconds, any other new state must hold before devices switch.</summary>
    public int SwitchDelayMs { get; set; }

    /// <summary>Gets or sets the configured devices.</summary>
    public List<DeviceConfig> Devices { get; set; } = [];
}
