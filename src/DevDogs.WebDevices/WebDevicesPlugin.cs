using System;
using System.Diagnostics;
using DevDogs.WebDevices.Core.Config;
using DevDogs.WebDevices.Core.Devices;
using DevDogs.WebDevices.Core.Routing;
using DevDogs.WebDevices.Core.Server;
using DevDogs.WebDevices.Core.State;
using GameReaderCommon;
using SimHub.Plugins;

namespace DevDogs.WebDevices;

/// <summary>
/// SimHub entry point. The class name is the prefix of every property, event and action this plugin
/// registers (<c>WebDevicesPlugin.*</c>) and must not change (docs/adr/ADR-0005).
/// </summary>
[PluginName("DevDogs Web Devices")]
[PluginDescription("Switches SimHub web dashboards on each browser device by game and game state.")]
[PluginAuthor("DevDogs")]
public class WebDevicesPlugin : IPlugin, IDataPlugin
{
    /// <summary>The key SimHub stores this plugin's settings under.</summary>
    private const string SettingsKey = "WebDevicesSettings";

    private const string LogPrefix = "[WebDevices] ";

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private DeviceRouter _router = null!; // assigned in Init, which SimHub calls before any other member
    private WebDevicesServer _server = null!; // assigned in Init

    /// <summary>
    /// Gets or sets the SimHub plugin manager; assigned by SimHub before <see cref="Init"/> is called.
    /// </summary>
    public PluginManager PluginManager { get; set; } = null!; // set by SimHub before Init

    /// <summary>
    /// Gets the user settings; loaded in <see cref="Init"/>.
    /// </summary>
    public RouterSettings Settings { get; private set; } = new();

    /// <summary>
    /// Gets the connection state of every device seen since SimHub started.
    /// </summary>
    public DeviceRegistry Devices { get; } = new(() => DateTime.UtcNow);

    /// <summary>
    /// Loads settings and starts the device server.
    /// </summary>
    /// <param name="pluginManager">The SimHub plugin manager.</param>
    public void Init(PluginManager pluginManager)
    {
        Settings = this.ReadCommonSettings(SettingsKey, () => new RouterSettings());
        foreach (var problem in RouterSettingsValidator.Validate(Settings))
        {
            SimHub.Logging.Current.Warn(LogPrefix + "Settings problem: " + problem);
        }

        // Seeding with the time keeps dashboard URLs unique across SimHub restarts.
        _router = new DeviceRouter(Settings, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _server = new WebDevicesServer(_router, Devices);
        _server.Log += message => SimHub.Logging.Current.Info(LogPrefix + message);
        _server.Start(Settings.Port);

        SimHub.Logging.Current.Info($"{LogPrefix}Loaded with {Settings.Devices.Count} device(s).");
    }

    /// <summary>
    /// Called by SimHub on every telemetry frame, whether or not a game is running.
    /// </summary>
    /// <param name="pluginManager">The SimHub plugin manager.</param>
    /// <param name="data">The current game data.</param>
    public void DataUpdate(PluginManager pluginManager, ref GameData data)
    {
        var signals = new GameSignals(data.GameName, data.RunningGameProcessDetected, data.GameRunning, data.GameInMenu);
        if (_router.Observe(signals, _clock.Elapsed))
        {
            var state = _router.State;
            SimHub.Logging.Current.Info($"{LogPrefix}State: {state.State}{(state.GameName is null ? "" : " (" + state.GameName + ")")}");
        }
    }

    /// <summary>
    /// Called once by SimHub when it shuts down.
    /// </summary>
    /// <param name="pluginManager">The SimHub plugin manager.</param>
    public void End(PluginManager pluginManager)
    {
        _server.Dispose();
        this.SaveCommonSettings(SettingsKey, Settings);
    }
}
