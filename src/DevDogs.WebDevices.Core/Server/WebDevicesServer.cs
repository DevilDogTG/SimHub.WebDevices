using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DevDogs.WebDevices.Core.Devices;
using DevDogs.WebDevices.Core.Routing;

namespace DevDogs.WebDevices.Core.Server;

/// <summary>
/// The HTTP server devices connect to. A minimal HTTP/1.1 server on a plain TCP socket rather than
/// <c>HttpListener</c>, so it runs inside SimHub's process without a URL reservation and is covered by
/// SimHub's own firewall rule (docs/adr/ADR-0006). Every route is a bodiless GET; every response except
/// the event stream closes the connection.
/// </summary>
public sealed class WebDevicesServer : IDisposable
{
    private const int MaxHeadBytes = 8192;
    private const string DevicePageResource = "DevDogs.WebDevices.Core.Server.device.html";

    private static readonly Lazy<byte[]> DevicePage = new(LoadDevicePage);

    private readonly DeviceRouter _router;
    private readonly DeviceRegistry _registry;
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<TcpClient, byte> _clients = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _stopping;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebDevicesServer"/> class. Call <see cref="Start"/> to listen.
    /// </summary>
    /// <param name="router">Supplies each device's target and change notifications.</param>
    /// <param name="registry">Records device connections.</param>
    public WebDevicesServer(DeviceRouter router, DeviceRegistry registry)
    {
        _router = router;
        _registry = registry;
    }

    /// <summary>Raised with diagnostic messages; handlers must be thread-safe.</summary>
    public event Action<string>? Log;

    /// <summary>Gets or sets how often an idle event stream sends a heartbeat, so dead connections are noticed.</summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Gets or sets how long a client may take to send its request.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets the current status.</summary>
    public ServerStatus Status { get; private set; } = new(false, 0, "Not started.");

    /// <summary>
    /// Starts listening on all network interfaces, stopping any previous listener first. Never throws for
    /// network errors; the returned status explains a failure.
    /// </summary>
    /// <param name="port">The TCP port.</param>
    /// <returns>The new status.</returns>
    public ServerStatus Start(int port)
    {
        lock (_gate)
        {
            StopListening();

            try
            {
                _listener = Bind(port);
            }
            catch (SocketException ex)
            {
                Status = new ServerStatus(false, port, BindFailure(port, ex));
                Log?.Invoke(Status.Message);
                return Status;
            }

            _stopping = new CancellationTokenSource();
            _ = AcceptLoopAsync(_listener, _stopping.Token);
            Status = new ServerStatus(true, port, $"Listening on port {port}.");
            Log?.Invoke(Status.Message);
            return Status;
        }
    }

    /// <summary>
    /// Stops listening and closes every open connection.
    /// </summary>
    public void Stop()
    {
        lock (_gate)
        {
            if (_listener is null)
            {
                return;
            }

            var port = Status.Port;
            StopListening();
            Status = new ServerStatus(false, port, "Stopped.");
            Log?.Invoke(Status.Message);
        }
    }

    /// <inheritdoc/>
    public void Dispose() => Stop();

    private static TcpListener Bind(int port)
    {
        try
        {
            // One dual-mode socket accepts both IPv4 and IPv6 clients.
            var listener = new TcpListener(IPAddress.IPv6Any, port);
            listener.Server.DualMode = true;
            listener.Start();
            return listener;
        }
        catch (SocketException ex) when (ex.SocketErrorCode is not (SocketError.AddressAlreadyInUse or SocketError.AccessDenied))
        {
            // IPv6 is disabled on this machine.
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            return listener;
        }
    }

    private static string BindFailure(int port, SocketException ex) => ex.SocketErrorCode switch
    {
        SocketError.AddressAlreadyInUse => $"Port {port} is already in use by another program. Choose another port.",
        SocketError.AccessDenied => $"Port {port} is reserved by Windows (see 'netsh int ipv4 show excludedportrange'). Choose another port.",
        _ => $"Cannot listen on port {port}: {ex.Message}",
    };

    private static byte[] LoadDevicePage()
    {
        using var resource = typeof(WebDevicesServer).Assembly.GetManifestResourceStream(DevicePageResource)
            ?? throw new InvalidOperationException($"Embedded resource '{DevicePageResource}' is missing.");
        using var copy = new MemoryStream();
        resource.CopyTo(copy);
        return copy.ToArray();
    }

    private static string RemoteAddress(TcpClient client)
    {
        if (client.Client.RemoteEndPoint is not IPEndPoint endPoint)
        {
            return "unknown";
        }

        return (endPoint.Address.IsIPv4MappedToIPv6 ? endPoint.Address.MapToIPv4() : endPoint.Address).ToString();
    }

    private static byte[] Html(string title, string body) => Encoding.UTF8.GetBytes(
        "<!DOCTYPE html><html><head><meta charset=\"utf-8\">" +
        "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
        $"<title>{title}</title><style>body{{margin:0;padding:20vh 16px 0;background:#000;color:#ccc;" +
        "font:18px/1.4 -apple-system,\"Segoe UI\",Roboto,sans-serif;text-align:center}</style></head>" +
        $"<body>{body}</body></html>");

    private static async Task WriteTextAsync(Stream stream, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }

    private static async Task RespondAsync(
        Stream stream,
        int status,
        string reason,
        string contentType,
        byte[] body,
        bool includeBody,
        string? extraHeader = null)
    {
        var head = new StringBuilder()
            .Append("HTTP/1.1 ").Append(status).Append(' ').Append(reason).Append("\r\n")
            .Append("Content-Type: ").Append(contentType).Append("\r\n")
            .Append("Content-Length: ").Append(body.Length).Append("\r\n")
            .Append("Cache-Control: no-cache, no-store\r\n")
            .Append("Connection: close\r\n");
        if (extraHeader is not null)
        {
            head.Append(extraHeader).Append("\r\n");
        }

        var headBytes = Encoding.ASCII.GetBytes(head.Append("\r\n").ToString());
        await stream.WriteAsync(headBytes, 0, headBytes.Length).ConfigureAwait(false);
        if (includeBody)
        {
            await stream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
        }

        await stream.FlushAsync().ConfigureAwait(false);
    }

    private static async Task WatchForCloseAsync(Stream stream, CancellationTokenSource closed)
    {
        // Nothing more is expected from an event-stream client; a read completing means it went away.
        try
        {
            var buffer = new byte[256];
            while (await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false) > 0)
            {
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
        {
        }

        try
        {
            closed.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void StopListening()
    {
        if (_listener is null)
        {
            return;
        }

        _stopping?.Cancel();
        _listener.Stop();
        foreach (var client in _clients.Keys)
        {
            client.Close();
        }

        _listener = null;
        _stopping = null;
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            }
            catch (Exception) when (stopping.IsCancellationRequested)
            {
                return;
            }
            catch (SocketException ex)
            {
                Log?.Invoke($"Accept failed: {ex.Message}");
                continue;
            }

            _ = HandleClientAsync(client, stopping);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken stopping)
    {
        _clients[client] = 0;
        try
        {
            client.NoDelay = true;
            var stream = client.GetStream();
            var head = await ReadHeadAsync(client, stream, stopping).ConfigureAwait(false);
            if (head is null || !RequestHead.TryParse(head, out var request))
            {
                await RespondAsync(stream, 400, "Bad Request", "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Bad request"), true)
                    .ConfigureAwait(false);
                return;
            }

            var route = EndpointRoutes.Resolve(request, name => _router.GetTarget(name) is not null);
            await ServeAsync(client, stream, route, request.Method == "GET", stopping).ConfigureAwait(false);
            client.Client.Shutdown(SocketShutdown.Send);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or OperationCanceledException)
        {
            // The client went away, timed out, or the server is stopping.
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Request failed: {ex}");
        }
        finally
        {
            _clients.TryRemove(client, out _);
            client.Close();
        }
    }

    private async Task<string?> ReadHeadAsync(TcpClient client, Stream stream, CancellationToken stopping)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        timeout.CancelAfter(RequestTimeout);

        // Socket reads ignore cancellation on .NET Framework; closing the client is what ends a stalled read.
        using (timeout.Token.Register(client.Close))
        {
            var buffer = new byte[MaxHeadBytes];
            var count = 0;
            while (count < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer, count, buffer.Length - count).ConfigureAwait(false);
                if (read == 0)
                {
                    return null;
                }

                count += read;
                var text = Encoding.ASCII.GetString(buffer, 0, count);
                if (text.IndexOf("\r\n\r\n", StringComparison.Ordinal) >= 0)
                {
                    return text;
                }
            }

            return null;
        }
    }

    private async Task ServeAsync(TcpClient client, Stream stream, Route route, bool includeBody, CancellationToken stopping)
    {
        const string HtmlType = "text/html; charset=utf-8";
        switch (route.Kind)
        {
            case RouteKind.Page:
                await RespondAsync(stream, 200, "OK", HtmlType, DevicePage.Value, includeBody).ConfigureAwait(false);
                break;

            case RouteKind.Redirect:
                await RespondAsync(stream, 301, "Moved Permanently", HtmlType, [], includeBody, $"Location: /{route.Device}/")
                    .ConfigureAwait(false);
                break;

            case RouteKind.State:
                _registry.Touch(route.Device!, RemoteAddress(client));
                var json = Payload(route.Device!) ?? "null";
                await RespondAsync(stream, 200, "OK", "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json), includeBody)
                    .ConfigureAwait(false);
                break;

            case RouteKind.Events:
                await ServeEventsAsync(client, stream, route.Device!, includeBody, stopping).ConfigureAwait(false);
                break;

            case RouteKind.DeviceNotConfigured:
                var name = WebUtility.HtmlEncode(route.Device);
                await RespondAsync(
                    stream,
                    404,
                    "Not Found",
                    HtmlType,
                    Html("Not configured", $"<p>Device <b>{name}</b> is not configured in SimHub.</p><p>Add it in SimHub, then reload this page.</p>"),
                    includeBody).ConfigureAwait(false);
                break;

            case RouteKind.MethodNotAllowed:
                await RespondAsync(stream, 405, "Method Not Allowed", "text/plain; charset=utf-8", [], includeBody, "Allow: GET, HEAD")
                    .ConfigureAwait(false);
                break;

            default:
                await RespondAsync(
                    stream,
                    404,
                    "Not Found",
                    HtmlType,
                    Html("Not found", "<p>Nothing here.</p><p>Open <b>/&lt;device-name&gt;/</b> on this address.</p>"),
                    includeBody).ConfigureAwait(false);
                break;
        }
    }

    private string? Payload(string device)
    {
        var target = _router.GetTarget(device);
        return target is null ? null : DevicePayload.ToJson(device, _router.State, target, _router.SimHubWebPort);
    }

    private async Task ServeEventsAsync(TcpClient client, Stream stream, string device, bool includeBody, CancellationToken stopping)
    {
        const string Head = "HTTP/1.1 200 OK\r\nContent-Type: text/event-stream; charset=utf-8\r\n" +
                            "Cache-Control: no-cache\r\nConnection: keep-alive\r\n\r\n";
        if (!includeBody)
        {
            await WriteTextAsync(stream, Head).ConfigureAwait(false);
            return;
        }

        // Not disposed: a change notification already in flight may still release it after unsubscribing.
        var changed = new SemaphoreSlim(0);
        void OnTargetsChanged(IReadOnlyCollection<string> names)
        {
            foreach (var name in names)
            {
                if (string.Equals(name, device, StringComparison.OrdinalIgnoreCase))
                {
                    changed.Release();
                    return;
                }
            }
        }

        var remote = RemoteAddress(client);
        using var closed = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        _router.TargetsChanged += OnTargetsChanged;
        Log?.Invoke($"Device '{device}' connected from {remote}.");
        try
        {
            using var connection = _registry.Connect(device, remote);
            _ = WatchForCloseAsync(stream, closed);
            await WriteTextAsync(stream, Head + "retry: 2000\n\n").ConfigureAwait(false);

            string? sentPath = null;
            var first = true;
            while (true)
            {
                var target = _router.GetTarget(device);
                if (target is null)
                {
                    await WriteTextAsync(stream, "event: gone\ndata: {}\n\n").ConfigureAwait(false);
                    return;
                }

                if (first || target.Path != sentPath)
                {
                    var payload = DevicePayload.ToJson(device, _router.State, target, _router.SimHubWebPort);
                    await WriteTextAsync(stream, $"event: dash\ndata: {payload}\n\n").ConfigureAwait(false);
                    sentPath = target.Path;
                    first = false;
                }

                if (await changed.WaitAsync(HeartbeatInterval, closed.Token).ConfigureAwait(false))
                {
                    while (changed.Wait(0))
                    {
                        // Coalesce bursts: only the latest target matters.
                    }
                }
                else
                {
                    await WriteTextAsync(stream, ": ping\n\n").ConfigureAwait(false);
                    _registry.Touch(device, remote);
                }
            }
        }
        finally
        {
            _router.TargetsChanged -= OnTargetsChanged;
            Log?.Invoke($"Device '{device}' disconnected.");
        }
    }
}
