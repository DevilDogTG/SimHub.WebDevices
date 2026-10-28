using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using DevDogs.WebDevices.Core.Config;
using DevDogs.WebDevices.Core.Editing;

namespace DevDogs.WebDevices.UI;

/// <summary>
/// What the plugin needs from SimHub and the PC: installed dashboards, known games, SimHub's own web ports
/// and the address devices use to reach this PC.
/// </summary>
internal static class SimHubCatalog
{
    /// <summary>
    /// Returns the installed dashboard codes.
    /// </summary>
    /// <returns>The codes, sorted.</returns>
    public static IReadOnlyList<string> Dashboards() =>
        DashboardCatalog.Scan(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DashTemplates"));

    /// <summary>
    /// Returns the games SimHub shows in its game list, by name. Empty if SimHub's game list cannot be read;
    /// the tab then still offers "Add current game".
    /// </summary>
    /// <returns>The games.</returns>
    public static IReadOnlyList<GameOption> Games()
    {
        try
        {
            return GamesFromSimHub();
        }
        catch (Exception ex) when (ex is MissingMemberException or TypeLoadException or InvalidOperationException)
        {
            SimHub.Logging.Current.Warn("[WebDevices] Could not read SimHub's game list: " + ex.Message);
            return [];
        }
    }

    /// <summary>
    /// Returns the ports SimHub's own web server uses, read from its settings (SimHub's defaults if unreadable).
    /// </summary>
    /// <returns>The ports.</returns>
    public static SimHubPorts WebPorts() =>
        SimHubPorts.Read(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PluginsData", "Common", "DashStudioSettings_2.json"));

    /// <summary>
    /// Returns the IPv4 address of the network adapter that has a default gateway, which is the one other
    /// devices on the home network reach this PC through.
    /// </summary>
    /// <returns>The address, or <c>localhost</c> when none is found.</returns>
    public static string LanAddress()
    {
        try
        {
            var address = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                .Select(n => n.GetIPProperties())
                .Where(p => p.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork
                                                        && !g.Address.Equals(System.Net.IPAddress.Any)))
                .SelectMany(p => p.UnicastAddresses)
                .Select(u => u.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            return address?.ToString() ?? "localhost";
        }
        catch (NetworkInformationException)
        {
            return "localhost";
        }
    }

    // SimHub's game list is not a documented plugin API: kept in its own method so a missing member fails
    // here, when this method is compiled, and is caught by the caller instead of breaking the whole tab.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IReadOnlyList<GameOption> GamesFromSimHub() =>
        SimHub.Plugins.Configuration.GetGamesSnapshot()
            .Where(g => g.IsShown && !string.IsNullOrWhiteSpace(g.Code))
            .Select(g => new GameOption(g.Code, string.IsNullOrWhiteSpace(g.Name) ? g.Code : g.Name))
            .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}
