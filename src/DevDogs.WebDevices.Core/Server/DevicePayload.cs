using DevDogs.WebDevices.Core.Routing;
using DevDogs.WebDevices.Core.State;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DevDogs.WebDevices.Core.Server;

/// <summary>
/// The JSON the device page receives, both as an event and from the state poll.
/// </summary>
public static class DevicePayload
{
    /// <summary>
    /// Returns the payload as single-line JSON (event stream data must not contain line breaks).
    /// </summary>
    /// <param name="device">The device name.</param>
    /// <param name="state">The current routing state.</param>
    /// <param name="target">What the device should show.</param>
    /// <param name="simHubWebPort">The port of SimHub's web server, which the page loads <c>path</c> from.</param>
    /// <returns>The JSON text.</returns>
    public static string ToJson(string device, RouterState state, DeviceTarget target, int simHubWebPort) =>
        new JObject
        {
            ["device"] = device,
            ["state"] = state.State.ToString(),
            ["game"] = state.GameName,
            ["dashboard"] = target.Dashboard,
            ["path"] = target.Path,
            ["simHubPort"] = simHubWebPort,
        }.ToString(Formatting.None);
}
