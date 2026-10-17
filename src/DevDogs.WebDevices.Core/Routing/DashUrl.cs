using System;
using System.Globalization;
using System.Text;

namespace DevDogs.WebDevices.Core.Routing;

/// <summary>
/// Builds the path of a dashboard on SimHub's own web server, in the form SimHub links to:
/// <c>/Dash?v=&lt;cache buster&gt;#&lt;escaped code&gt;|flag</c> (docs/adr/ADR-0002). The device page
/// prefixes it with SimHub's host and port.
/// </summary>
public static class DashUrl
{
    // Characters JavaScript's encodeURIComponent leaves as they are; SimHub's page escapes codes with it.
    private const string Unescaped = "-_.!~*'()";

    /// <summary>
    /// Returns the dashboard path.
    /// </summary>
    /// <param name="dashboardCode">The dashboard code from SimHub's dashboard list.</param>
    /// <param name="hideControls">Whether to hide SimHub's on-screen controls.</param>
    /// <param name="cacheBuster">
    /// A value that differs per switch. SimHub's page ignores hash-only changes, so the query must change
    /// for a frame to load a new dashboard.
    /// </param>
    /// <returns>The path, query and hash, starting with <c>/Dash</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="dashboardCode"/> is blank.</exception>
    public static string Build(string dashboardCode, bool hideControls, long cacheBuster)
    {
        if (string.IsNullOrWhiteSpace(dashboardCode))
        {
            throw new ArgumentException("A dashboard code is required.", nameof(dashboardCode));
        }

        var url = new StringBuilder("/Dash?v=")
            .Append(cacheBuster.ToString(CultureInfo.InvariantCulture))
            .Append('#');

        foreach (var b in Encoding.UTF8.GetBytes(dashboardCode))
        {
            var c = (char)b;
            if (c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' || Unescaped.IndexOf(c) >= 0)
            {
                url.Append(c);
            }
            else
            {
                url.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        if (hideControls)
        {
            url.Append("|nocontrols");
        }

        return url.ToString();
    }
}
