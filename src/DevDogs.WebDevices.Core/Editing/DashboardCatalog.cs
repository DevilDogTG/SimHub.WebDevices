using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DevDogs.WebDevices.Core.Editing;

/// <summary>
/// Lists the dashboards installed in SimHub. Each lives in <c>DashTemplates\&lt;code&gt;\&lt;code&gt;.djson</c>,
/// and the folder name is the code SimHub's web server uses in dashboard URLs.
/// </summary>
public static class DashboardCatalog
{
    /// <summary>
    /// Returns the dashboard codes found under <paramref name="dashTemplatesFolder"/>, sorted ignoring case.
    /// </summary>
    /// <param name="dashTemplatesFolder">SimHub's <c>DashTemplates</c> folder.</param>
    /// <returns>The codes; empty when the folder does not exist.</returns>
    public static IReadOnlyList<string> Scan(string dashTemplatesFolder)
    {
        if (!Directory.Exists(dashTemplatesFolder))
        {
            return [];
        }

        return Directory.EnumerateDirectories(dashTemplatesFolder)
            .Select(Path.GetFileName)
            .Where(name => File.Exists(Path.Combine(dashTemplatesFolder, name, name + ".djson")))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
