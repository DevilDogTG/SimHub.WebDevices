using System;
using System.Diagnostics.CodeAnalysis;

namespace DevDogs.WebDevices.Core.Server;

/// <summary>
/// The parts of an HTTP request line the server routes on. Headers are not needed: every route is a
/// bodiless GET.
/// </summary>
/// <param name="Method">The upper-case method.</param>
/// <param name="Path">The path, without query or fragment.</param>
public sealed record RequestHead(string Method, string Path)
{
    /// <summary>
    /// Parses the request line at the start of <paramref name="head"/>.
    /// </summary>
    /// <param name="head">The request head as received (request line, headers, blank line).</param>
    /// <param name="request">The parsed request when this returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> for a well-formed HTTP/1.x request line with an absolute path.</returns>
    public static bool TryParse(string head, [NotNullWhen(true)] out RequestHead? request)
    {
        request = null;

        var lineEnd = head.IndexOf("\r\n", StringComparison.Ordinal);
        var parts = (lineEnd < 0 ? head : head.Substring(0, lineEnd)).Split(' ');
        if (parts.Length != 3
            || !parts[2].StartsWith("HTTP/1.", StringComparison.Ordinal)
            || !parts[1].StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        var path = parts[1];
        var end = path.IndexOfAny(['?', '#']);
        if (end >= 0)
        {
            path = path.Substring(0, end);
        }

        request = new RequestHead(parts[0].ToUpperInvariant(), path);
        return true;
    }
}
