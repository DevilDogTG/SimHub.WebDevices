using System;
using System.Diagnostics;
using DevDogs.WebDevices.Core.Config;
using DevDogs.WebDevices.Core.Devices;
using DevDogs.WebDevices.Core.Routing;
using DevDogs.WebDevices.Core.Server;
using DevDogs.WebDevices.Core.State;

namespace DevDogs.WebDevices.Core.Hosting;

/// <summary>
/// Everything that must outlive a single SimHub plugin instance: the router (with its debounce timing),
/// the device registry, the server and the clock they share. Held in a <see cref="SharedInstance{T}"/>
/// so a game switch, which re-creates the plugin, does not drop device connections (docs/adr/ADR-0007).
/// </summary>
public sealed class RouterHost : IDisposable
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    /// <summary>
    /// Initializes a new instance of the <see cref="RouterHost"/> class. The server starts on
    /// <see cref="Configure"/>.
    /// </summary>
    /// <param name="settings">The initial settings.</param>
    /// <param name="cacheBusterSeed">The value dashboard paths count up from; seed with the current time.</param>
    /// <param name="utcNow">The clock for device last-seen times.</param>
    /// <param name="log">Receives diagnostic messages, from any thread; wired once for the host's lifetime.</param>
    public RouterHost(RouterSettings settings, long cacheBusterSeed, Func<DateTime> utcNow, Action<string> log)
    {
        Router = new DeviceRouter(settings, cacheBusterSeed);
        Registry = new DeviceRegistry(utcNow);
        Server = new WebDevicesServer(Router, Registry);
        Server.Log += log;
    }

    /// <summary>Gets the router.</summary>
    public DeviceRouter Router { get; }

    /// <summary>Gets the device registry.</summary>
    public DeviceRegistry Registry { get; }

    /// <summary>Gets the server.</summary>
    public WebDevicesServer Server { get; }

    /// <summary>
    /// Applies settings and makes sure the server listens on the configured port. An unchanged port keeps
    /// the running server, and every open device stream, as they are.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The server status.</returns>
    public ServerStatus Configure(RouterSettings settings)
    {
        Router.ApplySettings(settings);
        var status = Server.Status;
        return status.Listening && status.Port == settings.Port ? status : Server.Start(settings.Port);
    }

    /// <summary>
    /// Records one telemetry frame, timed by the host's own clock so debouncing continues across plugin instances.
    /// </summary>
    /// <param name="signals">The frame's game flags.</param>
    /// <returns><see langword="true"/> when the routed state changed.</returns>
    public bool Observe(GameSignals signals) => Router.Observe(signals, _clock.Elapsed);

    /// <summary>Stops the server.</summary>
    public void Dispose() => Server.Dispose();
}
