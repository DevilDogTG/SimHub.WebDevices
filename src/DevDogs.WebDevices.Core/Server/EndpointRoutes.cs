using System;
using System.Text.RegularExpressions;
using DevDogs.WebDevices.Core.Config;

namespace DevDogs.WebDevices.Core.Server;

/// <summary>What a request is asking for.</summary>
public enum RouteKind
{
    /// <summary>The device page (<c>/&lt;device&gt;/</c>).</summary>
    Page,

    /// <summary>The device's event stream (<c>/&lt;device&gt;/events</c>).</summary>
    Events,

    /// <summary>The device's current target as JSON (<c>/&lt;device&gt;/state</c>), the polling fallback.</summary>
    State,

    /// <summary>The device URL without its trailing slash; redirected so the page's relative URLs resolve.</summary>
    Redirect,

    /// <summary>A well-formed device URL for a device that is not in the settings.</summary>
    DeviceNotConfigured,

    /// <summary>Anything else.</summary>
    NotFound,

    /// <summary>A method other than GET or HEAD.</summary>
    MethodNotAllowed,
}

/// <summary>
/// The result of routing a request.
/// </summary>
/// <param name="Kind">What the request is asking for.</param>
/// <param name="Device">The lower-case device name, for device routes.</param>
public sealed record Route(RouteKind Kind, string? Device);

/// <summary>
/// Maps request paths to routes.
/// </summary>
public static class EndpointRoutes
{
    private static readonly Regex DevicePath = new("^/([^/]+)(/(events|state)?)?$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Routes one request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="isKnownDevice">Returns whether a lower-case device name is configured.</param>
    /// <returns>The route.</returns>
    public static Route Resolve(RequestHead request, Func<string, bool> isKnownDevice)
    {
        if (request.Method is not ("GET" or "HEAD"))
        {
            return new Route(RouteKind.MethodNotAllowed, null);
        }

        var match = DevicePath.Match(request.Path);
        var device = match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
        if (device is null || !DeviceNameRules.IsValid(device))
        {
            return new Route(RouteKind.NotFound, null);
        }

        if (!isKnownDevice(device))
        {
            return new Route(RouteKind.DeviceNotConfigured, device);
        }

        var kind = match.Groups[2].Value switch
        {
            "" => RouteKind.Redirect,
            "/" => RouteKind.Page,
            "/events" => RouteKind.Events,
            _ => RouteKind.State,
        };
        return new Route(kind, device);
    }
}
