using System;
using System.Linq;
using DevDogs.WebDevices.Core.Config;
using DevDogs.WebDevices.Core.State;

namespace DevDogs.WebDevices.Core.Routing;

/// <summary>
/// Picks the dashboard a device shows in a given state. Blank values fall back, most specific first:
/// <list type="bullet">
/// <item>System idle: the device's system idle dashboard.</item>
/// <item>Driving: the game's driving dashboard, then the default game's, then the system idle dashboard.</item>
/// <item>Game idle: the game's idle dashboard, then the default game's, then whatever driving would show.</item>
/// </list>
/// </summary>
public static class DashboardResolver
{
    /// <summary>
    /// Returns the dashboard code for <paramref name="device"/> in <paramref name="state"/>.
    /// </summary>
    /// <param name="device">The device's settings.</param>
    /// <param name="state">The current routing state.</param>
    /// <returns>The dashboard code, or <see langword="null"/> when nothing applicable is configured.</returns>
    public static string? Resolve(DeviceConfig device, RouterState state)
    {
        var systemIdle = Value(device.SystemIdleDashboard);
        if (state.State == GameState.SystemIdle)
        {
            return systemIdle;
        }

        var game = state.GameName is null
            ? null
            : device.Games.FirstOrDefault(g => string.Equals(g.GameName, state.GameName, StringComparison.OrdinalIgnoreCase));

        var driving = Value(game?.Driving) ?? Value(device.Default.Driving) ?? systemIdle;
        if (state.State == GameState.Driving)
        {
            return driving;
        }

        return Value(game?.GameIdle) ?? Value(device.Default.GameIdle) ?? driving;
    }

    private static string? Value(string? dashboard) => string.IsNullOrWhiteSpace(dashboard) ? null : dashboard;
}
