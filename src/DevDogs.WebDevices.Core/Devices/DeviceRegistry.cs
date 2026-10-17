using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DevDogs.WebDevices.Core.Devices;

/// <summary>
/// Tracks which devices are connected and when each was last seen. Thread-safe.
/// </summary>
public sealed class DeviceRegistry
{
    private readonly object _gate = new();
    private readonly Func<DateTime> _utcNow;
    private readonly Dictionary<string, DeviceStatus> _devices = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceRegistry"/> class.
    /// </summary>
    /// <param name="utcNow">The clock.</param>
    public DeviceRegistry(Func<DateTime> utcNow)
    {
        _utcNow = utcNow;
    }

    /// <summary>
    /// Records an open event stream; dispose the result when the stream closes.
    /// </summary>
    /// <param name="deviceName">The device name.</param>
    /// <param name="remoteAddress">The device's network address.</param>
    /// <returns>A handle that records the disconnect when disposed.</returns>
    public IDisposable Connect(string deviceName, string remoteAddress)
    {
        var name = Normalize(deviceName);
        Record(name, remoteAddress, +1);
        return new Connection(this, name, remoteAddress);
    }

    /// <summary>
    /// Records that a device was seen without opening a stream (for example a state poll).
    /// </summary>
    /// <param name="deviceName">The device name.</param>
    /// <param name="remoteAddress">The device's network address.</param>
    public void Touch(string deviceName, string remoteAddress) => Record(Normalize(deviceName), remoteAddress, 0);

    /// <summary>
    /// Drops what is known about a device, for example after it is removed from the settings.
    /// </summary>
    /// <param name="deviceName">The device name.</param>
    public void Forget(string deviceName)
    {
        lock (_gate)
        {
            _devices.Remove(Normalize(deviceName));
        }
    }

    /// <summary>
    /// Returns the status of one device.
    /// </summary>
    /// <param name="deviceName">The device name.</param>
    /// <returns>The status, or <see langword="null"/> when the device has never been seen.</returns>
    public DeviceStatus? Get(string deviceName)
    {
        lock (_gate)
        {
            return _devices.TryGetValue(Normalize(deviceName), out var status) ? status : null;
        }
    }

    /// <summary>
    /// Returns every seen device, ordered by name.
    /// </summary>
    /// <returns>The statuses.</returns>
    public IReadOnlyList<DeviceStatus> Snapshot()
    {
        lock (_gate)
        {
            return _devices.Values.OrderBy(d => d.Name, StringComparer.Ordinal).ToList();
        }
    }

    private static string Normalize(string deviceName) => deviceName.ToLowerInvariant();

    private void Record(string name, string remoteAddress, int connectionDelta)
    {
        lock (_gate)
        {
            var connections = _devices.TryGetValue(name, out var current) ? current.Connections : 0;
            _devices[name] = new DeviceStatus(name, Math.Max(0, connections + connectionDelta), _utcNow(), remoteAddress);
        }
    }

    private sealed class Connection(DeviceRegistry registry, string name, string remoteAddress) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                registry.Record(name, remoteAddress, -1);
            }
        }
    }
}
