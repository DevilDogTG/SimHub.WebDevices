namespace DevDogs.WebDevices.Core.Routing;

/// <summary>
/// What one device should show right now.
/// </summary>
/// <param name="Dashboard">The dashboard code, or <see langword="null"/> when nothing is configured for the state.</param>
/// <param name="Path">
/// The dashboard path on SimHub's web server (see <see cref="DashUrl"/>), or <see langword="null"/> with no dashboard.
/// A new path means the device frame must reload.
/// </param>
public sealed record DeviceTarget(string? Dashboard, string? Path);
