using System;
using DevDogs.WebDevices.Core.Config;
using DevDogs.WebDevices.Core.Devices;
using DevDogs.WebDevices.Core.Hosting;
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
    /// <summary>
    /// The key SimHub stores this plugin's settings under; the file is
    /// <c>PluginsData\Common\WebDevicesPlugin.WebDevicesSettings.json</c> (SimHub prefixes the class name).
    /// </summary>
    private const string SettingsKey = "WebDevicesSettings";

    private const string LogPrefix = "[WebDevices] ";

    /// <summary>
    /// The host shared by successive plugin instances. SimHub ends this plugin and creates a new instance on
    /// every game switch; the host, and every device connection, survives that (docs/adr/ADR-0007). The grace
    /// period only matters if the plugin is ended without a successor; on SimHub exit the process ends anyway.
    /// </summary>
    private static readonly SharedInstance<RouterHost> SharedHost = new(TimeSpan.FromSeconds(10));

    private RouterHost _host = null!; // assigned in Init, which SimHub calls before any other member

    // The raw flags last logged, so they are logged only when they change rather than every frame.
    private (string? Game, bool Process, bool Running, bool Menu, bool Paused, bool Replay)? _lastFlags;

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
    public DeviceRegistry Devices => _host.Registry;

    /// <summary>
    /// Loads settings and attaches to the shared host, starting the device server if it is not running.
    /// </summary>
    /// <param name="pluginManager">The SimHub plugin manager.</param>
    public void Init(PluginManager pluginManager)
    {
        Settings = this.ReadCommonSettings(SettingsKey, () => new RouterSettings());
        foreach (var problem in RouterSettingsValidator.Validate(Settings))
        {
            SimHub.Logging.Current.Warn(LogPrefix + "Settings problem: " + problem);
        }

        var reused = true;
        _host = SharedHost.Acquire(() =>
        {
            reused = false;

            // Seeding with the time keeps dashboard URLs unique across SimHub restarts.
            return new RouterHost(
                Settings,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                () => DateTime.UtcNow,
                message => SimHub.Logging.Current.Info(LogPrefix + message));
        });
        _host.Configure(Settings);

        SimHub.Logging.Current.Info(
            $"{LogPrefix}Loaded with {Settings.Devices.Count} device(s){(reused ? "; server kept running" : "")}.");
    }

    /// <summary>
    /// Called by SimHub on every telemetry frame, whether or not a game is running.
    /// </summary>
    /// <param name="pluginManager">The SimHub plugin manager.</param>
    /// <param name="data">The current game data.</param>
    public void DataUpdate(PluginManager pluginManager, ref GameData data)
    {
        LogFlagChanges(data);

        var signals = new GameSignals(data.GameName, data.RunningGameProcessDetected, data.GameRunning, data.GameInMenu);
        if (_host.Observe(signals))
        {
            var state = _host.Router.State;
            SimHub.Logging.Current.Info($"{LogPrefix}State: {state.State}{(state.GameName is null ? "" : " (" + state.GameName + ")")}");
        }
    }

    /// <summary>
    /// Logs SimHub's raw game flags whenever any of them changes, so per-game detection (process, menu,
    /// pause) can be checked against what the router decided. Paused and replay are not used for routing
    /// yet; they are logged to see what games report.
    /// </summary>
    /// <param name="data">The current game data.</param>
    private void LogFlagChanges(GameData data)
    {
        var flags = (data.GameName, data.RunningGameProcessDetected, data.GameRunning, data.GameInMenu,
            data.GamePaused, data.GameReplay);
        if (_lastFlags == flags)
        {
            return;
        }

        _lastFlags = flags;
        SimHub.Logging.Current.Info(
            $"{LogPrefix}Flags: game={flags.GameName ?? "(none)"} process={flags.RunningGameProcessDetected} " +
            $"running={flags.GameRunning} menu={flags.GameInMenu} paused={flags.GamePaused} " +
            $"replay={flags.GameReplay}");
    }

    /// <summary>
    /// Called by SimHub when this instance ends: on every game switch and on exit. The shared host keeps
    /// running for the next instance. Settings are not saved here: nothing changes them at runtime yet, and
    /// saving would overwrite edits made to the file while SimHub runs.
    /// </summary>
    /// <param name="pluginManager">The SimHub plugin manager.</param>
    public void End(PluginManager pluginManager)
    {
        SharedHost.Release();
    }
}
