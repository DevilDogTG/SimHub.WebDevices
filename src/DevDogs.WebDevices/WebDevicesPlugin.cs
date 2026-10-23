using System;
using System.Windows.Controls;
using System.Windows.Media;
using DevDogs.WebDevices.Core.Config;
using DevDogs.WebDevices.Core.Devices;
using DevDogs.WebDevices.Core.Editing;
using DevDogs.WebDevices.Core.Hosting;
using DevDogs.WebDevices.Core.State;
using DevDogs.WebDevices.UI;
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
public class WebDevicesPlugin : IPlugin, IDataPlugin, IWPFSettingsV2
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
    /// Gets the name shown in SimHub's left menu.
    /// </summary>
    public string LeftMenuTitle => "Web Devices";

    /// <summary>
    /// Gets the icon shown in SimHub's left menu.
    /// </summary>
    public ImageSource PictureIcon { get; } = PluginIcon.Create();

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

        var signals = new GameSignals(data.GameName, data.RunningGameProcessDetected, data.GameRunning, data.GameInMenu, data.GamePaused);
        if (_host.Observe(signals))
        {
            var state = _host.Router.State;
            SimHub.Logging.Current.Info($"{LogPrefix}State: {state.State}{(state.GameName is null ? "" : " (" + state.GameName + ")")}");
        }
    }

    /// <summary>
    /// Logs SimHub's raw game flags whenever any of them changes, so per-game detection (process, menu,
    /// pause) can be checked against what the router decided. Replay is not used for routing; it is logged
    /// to see what games report.
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
    /// Returns the "Web Devices" settings tab.
    /// </summary>
    /// <param name="pluginManager">The SimHub plugin manager.</param>
    /// <returns>The tab.</returns>
    public Control GetWPFSettingsControl(PluginManager pluginManager)
    {
        var host = _host;
        var editor = new SettingsEditor(
            Settings,
            SimHubCatalog.Dashboards(),
            SimHubCatalog.Games(),
            () => string.IsNullOrWhiteSpace(pluginManager.GameName) ? null : pluginManager.GameName,
            SimHubCatalog.LanAddress,
            settings =>
            {
                this.SaveCommonSettings(SettingsKey, settings);
                Settings = settings;
                var status = host.Configure(settings);
                SimHub.Logging.Current.Info($"{LogPrefix}Settings saved: {settings.Devices.Count} device(s). {status.Message}");
            });

        return new SettingsControl(
            editor,
            e => e.RefreshStatus(host.Router.State, host.Server.Status, host.Registry.Snapshot(), host.Router.GetTarget, DateTime.UtcNow));
    }

    /// <summary>
    /// Called by SimHub when this instance ends: on every game switch and on exit. The shared host keeps
    /// running for the next instance. Settings are saved only by the settings tab's Save button, never here,
    /// so an unchanged copy can never overwrite newer settings.
    /// </summary>
    /// <param name="pluginManager">The SimHub plugin manager.</param>
    public void End(PluginManager pluginManager)
    {
        SharedHost.Release();
    }
}
