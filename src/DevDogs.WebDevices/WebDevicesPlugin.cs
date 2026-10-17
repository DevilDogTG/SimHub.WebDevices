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
    /// Gets or sets the SimHub plugin manager; assigned by SimHub before <see cref="Init"/> is called.
    /// </summary>
    public PluginManager PluginManager { get; set; } = null!; // set by SimHub before Init

    /// <summary>
    /// Called once by SimHub when the plugin is loaded.
    /// </summary>
    /// <param name="pluginManager">The SimHub plugin manager.</param>
    public void Init(PluginManager pluginManager)
    {
        SimHub.Logging.Current.Info("[WebDevices] loaded");
    }

    /// <summary>
    /// Called by SimHub on every telemetry frame, whether or not a game is running.
    /// </summary>
    /// <param name="pluginManager">The SimHub plugin manager.</param>
    /// <param name="data">The current game data.</param>
    public void DataUpdate(PluginManager pluginManager, ref GameData data)
    {
    }

    /// <summary>
    /// Called once by SimHub when it shuts down.
    /// </summary>
    /// <param name="pluginManager">The SimHub plugin manager.</param>
    public void End(PluginManager pluginManager)
    {
        SimHub.Logging.Current.Info("[WebDevices] stopped");
    }
}
