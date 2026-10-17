using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DevDogs.WebDevices.Core.Config;

/// <summary>
/// What a device name may contain. Names become a URL path segment, so they are limited to lowercase
/// letters, digits, <c>-</c> and <c>_</c>, and a few names are kept free for the plugin's own routes.
/// </summary>
public static class DeviceNameRules
{
    /// <summary>The longest allowed name.</summary>
    public const int MaxLength = 32;

    private static readonly Regex Pattern = new("^[a-z0-9][a-z0-9_-]*$", RegexOptions.CultureInvariant);

    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal) { "admin", "api", "assets", "static" };

    /// <summary>Gets the reason a name is not allowed.</summary>
    public static string Requirement =>
        $"use 1-{MaxLength} lowercase letters, digits, '-' or '_', starting with a letter or digit, " +
        $"and not one of: {string.Join(", ", Reserved)}";

    /// <summary>
    /// Returns whether <paramref name="name"/> can be used as a device name.
    /// </summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><see langword="true"/> when the name is allowed.</returns>
    public static bool IsValid(string? name) =>
        name is { Length: > 0 and <= MaxLength } && Pattern.IsMatch(name) && !Reserved.Contains(name);
}
