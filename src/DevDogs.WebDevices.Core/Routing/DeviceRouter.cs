using System;
using System.Collections.Generic;
using DevDogs.WebDevices.Core.Config;
using DevDogs.WebDevices.Core.State;

namespace DevDogs.WebDevices.Core.Routing;

/// <summary>
/// Keeps every device's <see cref="DeviceTarget"/> in step with the game state and the settings.
/// Thread-safe: fed from SimHub's telemetry thread, read from HTTP threads, reconfigured from the UI.
/// </summary>
public sealed class DeviceRouter
{
    private readonly object _gate = new();
    private readonly SwitchDebouncer _debouncer;
    private RouterSettings _settings;
    private Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private long _cacheBuster;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceRouter"/> class, starting in system idle.
    /// </summary>
    /// <param name="settings">The settings; copied, so later edits to the object have no effect.</param>
    /// <param name="cacheBusterSeed">
    /// The value dashboard paths count up from. Seed with the current time so paths stay unique across restarts.
    /// </param>
    public DeviceRouter(RouterSettings settings, long cacheBusterSeed)
    {
        _settings = Copy(settings);
        _cacheBuster = cacheBusterSeed;
        _debouncer = new SwitchDebouncer(RouterState.SystemIdle, TimeSpan.Zero, TimeSpan.Zero);
        ApplyDelays();
        Rebuild();
    }

    /// <summary>
    /// Raised, outside any lock, with the names of the devices whose target changed (including removed devices).
    /// Handlers run on the caller's thread and must return quickly.
    /// </summary>
    public event Action<IReadOnlyCollection<string>>? TargetsChanged;

    /// <summary>Gets the debounced state devices are showing.</summary>
    public RouterState State
    {
        get
        {
            lock (_gate)
            {
                return _debouncer.Current;
            }
        }
    }

    /// <summary>Gets the port of SimHub's web server, which devices load dashboards from.</summary>
    public int SimHubWebPort
    {
        get
        {
            lock (_gate)
            {
                return _settings.SimHubWebPort;
            }
        }
    }

    /// <summary>Gets a copy of the settings in use.</summary>
    public RouterSettings Settings
    {
        get
        {
            lock (_gate)
            {
                return Copy(_settings);
            }
        }
    }

    /// <summary>
    /// Records one telemetry frame.
    /// </summary>
    /// <param name="signals">The frame's game flags.</param>
    /// <param name="now">A monotonic timestamp.</param>
    /// <returns><see langword="true"/> when the debounced state changed on this frame.</returns>
    public bool Observe(GameSignals signals, TimeSpan now)
    {
        IReadOnlyCollection<string> changed;
        lock (_gate)
        {
            if (!_debouncer.Update(GameStateDetector.Detect(signals), now))
            {
                return false;
            }

            changed = Rebuild();
        }

        Raise(changed);
        return true;
    }

    /// <summary>
    /// Replaces the settings and recomputes every target. The port is not applied here; the HTTP server owns it.
    /// </summary>
    /// <param name="settings">The new settings; copied.</param>
    public void ApplySettings(RouterSettings settings)
    {
        IReadOnlyCollection<string> changed;
        lock (_gate)
        {
            _settings = Copy(settings);
            ApplyDelays();
            changed = Rebuild();
        }

        Raise(changed);
    }

    /// <summary>
    /// Returns what <paramref name="deviceName"/> should show.
    /// </summary>
    /// <param name="deviceName">The device name from the URL, matched ignoring case.</param>
    /// <returns>The target, or <see langword="null"/> when no such device is configured.</returns>
    public DeviceTarget? GetTarget(string deviceName)
    {
        lock (_gate)
        {
            return _entries.TryGetValue(deviceName, out var entry) ? entry.Target : null;
        }
    }

    private static RouterSettings Copy(RouterSettings settings) =>
        RouterSettingsJson.Deserialize(RouterSettingsJson.Serialize(settings));

    private void ApplyDelays()
    {
        _debouncer.SystemIdleDelay = TimeSpan.FromMilliseconds(Math.Max(0, _settings.SystemIdleDelayMs));
        _debouncer.SwitchDelay = TimeSpan.FromMilliseconds(Math.Max(0, _settings.SwitchDelayMs));
    }

    private List<string> Rebuild()
    {
        var state = _debouncer.Current;
        var entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        var changed = new List<string>();

        foreach (var device in _settings.Devices)
        {
            if (entries.ContainsKey(device.Name))
            {
                continue; // duplicate names are rejected by validation; the first one wins if one slips through
            }

            var dashboard = DashboardResolver.Resolve(device, state);
            if (_entries.TryGetValue(device.Name, out var previous)
                && previous.Dashboard == dashboard
                && previous.HideControls == device.HideControls)
            {
                entries[device.Name] = previous;
                continue;
            }

            var path = dashboard is null ? null : DashUrl.Build(dashboard, device.HideControls, ++_cacheBuster);
            entries[device.Name] = new Entry(dashboard, device.HideControls, new DeviceTarget(dashboard, path));
            changed.Add(device.Name);
        }

        foreach (var name in _entries.Keys)
        {
            if (!entries.ContainsKey(name))
            {
                changed.Add(name);
            }
        }

        _entries = entries;
        return changed;
    }

    private void Raise(IReadOnlyCollection<string> changed)
    {
        if (changed.Count > 0)
        {
            TargetsChanged?.Invoke(changed);
        }
    }

    private sealed record Entry(string? Dashboard, bool HideControls, DeviceTarget Target);
}
