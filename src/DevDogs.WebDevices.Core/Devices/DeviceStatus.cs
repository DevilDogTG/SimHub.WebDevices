using System;

namespace DevDogs.WebDevices.Core.Devices;

/// <summary>
/// What is known about a device's connection.
/// </summary>
/// <param name="Name">The device name, in lowercase.</param>
/// <param name="Connections">How many live event streams the device has open (usually 0 or 1).</param>
/// <param name="LastSeenUtc">When the device last connected, disconnected or polled.</param>
/// <param name="LastAddress">The network address the device last used.</param>
public sealed record DeviceStatus(string Name, int Connections, DateTime LastSeenUtc, string LastAddress);
