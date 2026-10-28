using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DevDogs.WebDevices.Core.Config;

/// <summary>
/// The ports SimHub's own web server uses, which the device server must never take: SimHub's web server
/// fails to start when either is in use, and the dashboards go with it.
/// </summary>
/// <param name="WebPort">SimHub's HTTP port.</param>
/// <param name="HttpsPort">SimHub's HTTPS port, reserved even while HTTPS is off so turning it on later still works.</param>
public sealed record SimHubPorts(int WebPort, int HttpsPort)
{
    /// <summary>Gets SimHub's default ports.</summary>
    public static SimHubPorts Default { get; } = new(8888, 8889);

    /// <summary>
    /// Reads the ports from SimHub's <c>PluginsData\Common\DashStudioSettings_2.json</c>. Never throws: a
    /// missing or unreadable file, or a missing or invalid value, gives SimHub's default for that port.
    /// </summary>
    /// <param name="settingsFile">The path of SimHub's dashboard settings file.</param>
    /// <returns>The ports.</returns>
    public static SimHubPorts Read(string settingsFile)
    {
        JObject settings;
        try
        {
            settings = JObject.Parse(File.ReadAllText(settingsFile));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return Default;
        }

        return new SimHubPorts(
            PortOrDefault(settings, "SimHubWebPort", Default.WebPort),
            PortOrDefault(settings, "SimHubWebHttpsPort", Default.HttpsPort));
    }

    /// <summary>
    /// Explains why the device server must not listen on <paramref name="port"/>.
    /// </summary>
    /// <param name="port">The device port.</param>
    /// <returns>The reason, or <see langword="null"/> when the port is not one of SimHub's.</returns>
    public string? Conflict(int port)
    {
        if (port == WebPort)
        {
            return $"Port {port} is SimHub's own web port. Choose another port.";
        }

        return port == HttpsPort
            ? $"Port {port} is SimHub's HTTPS port; using it stops SimHub's web server when HTTPS is on. Choose another port."
            : null;
    }

    private static int PortOrDefault(JObject settings, string name, int fallback) =>
        settings[name] is { Type: JTokenType.Integer } token && (int)token is > 0 and <= 65535 ? (int)token : fallback;
}
