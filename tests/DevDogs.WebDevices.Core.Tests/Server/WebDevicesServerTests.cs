using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DevDogs.WebDevices.Core.Config;
using DevDogs.WebDevices.Core.Devices;
using DevDogs.WebDevices.Core.Routing;
using DevDogs.WebDevices.Core.Server;
using DevDogs.WebDevices.Core.State;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DevDogs.WebDevices.Core.Tests.Server;

public sealed class WebDevicesServerTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly DeviceRouter _router;
    private readonly DeviceRegistry _registry = new(() => DateTime.UtcNow);
    private readonly WebDevicesServer _server;
    private readonly HttpClient _client;
    private readonly int _port = FreePort();

    public WebDevicesServerTests()
    {
        _router = new DeviceRouter(Settings(), cacheBusterSeed: 1000);
        _server = new WebDevicesServer(_router, _registry) { HeartbeatInterval = TimeSpan.FromMilliseconds(200) };
        _server.Start(_port).Listening.Should().BeTrue();
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri($"http://127.0.0.1:{_port}"),
            Timeout = Timeout,
        };
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    private static RouterSettings Settings() => new()
    {
        SystemIdleDelayMs = 0,
        Devices =
        {
            new DeviceConfig
            {
                Name = "ipad",
                SystemIdleDashboard = "Clock",
                Games = { new GameMapping { GameName = "ETS2", Driving = "Truck" } },
            },
        },
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static CancellationTokenSource Cts()
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        cts.CancelAfter(Timeout);
        return cts;
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    [Fact]
    public async Task Serves_the_device_page()
    {
        using var response = await _client.GetAsync("/ipad/", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("<iframe").And.Contain("EventSource");
    }

    [Fact]
    public async Task Head_returns_headers_without_a_body()
    {
        using var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/ipad/"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync(Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task Redirects_the_device_url_to_its_trailing_slash_form()
    {
        using var response = await _client.GetAsync("/ipad", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.OriginalString.Should().Be("/ipad/");
    }

    [Fact]
    public async Task An_unknown_device_gets_a_not_configured_page()
    {
        using var response = await _client.GetAsync("/phone/", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("phone").And.Contain("not configured");
    }

    [Fact]
    public async Task Other_paths_are_not_found()
    {
        using var response = await _client.GetAsync("/", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Other_methods_are_rejected()
    {
        using var response = await _client.PostAsync("/ipad/", new StringContent(""), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task A_malformed_request_gets_bad_request()
    {
        using var socket = new TcpClient();
        await socket.ConnectAsync(IPAddress.Loopback, _port, Ct);
        var stream = socket.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes("garbage\r\n\r\n"), Ct);

        var reply = await new StreamReader(stream).ReadLineAsync(Ct);

        reply.Should().StartWith("HTTP/1.1 400");
    }

    [Fact]
    public async Task State_returns_the_current_target_and_marks_the_device_seen()
    {
        var json = JObject.Parse(await _client.GetStringAsync("/ipad/state", Ct));

        json["dashboard"]!.Value<string>().Should().Be("Clock");
        json["state"]!.Value<string>().Should().Be("SystemIdle");
        json["simHubPort"]!.Value<int>().Should().Be(8888);
        _registry.Get("ipad").Should().NotBeNull();
    }

    [Fact]
    public async Task Events_push_the_current_dashboard_then_every_change()
    {
        using var cts = Cts();
        using var stream = await _client.GetStreamAsync("/ipad/events", cts.Token);
        using var reader = new StreamReader(stream);

        var first = await NextEventAsync(reader, cts.Token);
        first.Name.Should().Be("dash");
        first.Data["dashboard"]!.Value<string>().Should().Be("Clock");
        _registry.Get("ipad")!.Connections.Should().Be(1);

        _router.Observe(new GameSignals("ETS2", true, true, false), TimeSpan.Zero);

        var second = await NextEventAsync(reader, cts.Token);
        second.Data["dashboard"]!.Value<string>().Should().Be("Truck");
        second.Data["game"]!.Value<string>().Should().Be("ETS2");
    }

    [Fact]
    public async Task Events_send_heartbeats_while_nothing_changes()
    {
        using var cts = Cts();
        using var stream = await _client.GetStreamAsync("/ipad/events", cts.Token);
        using var reader = new StreamReader(stream);
        await NextEventAsync(reader, cts.Token);

        string? line;
        do
        {
            line = await reader.ReadLineAsync(cts.Token);
        }
        while (line == "");

        line.Should().StartWith(":");
    }

    [Fact]
    public async Task Removing_a_device_sends_gone_and_closes_the_stream()
    {
        using var cts = Cts();
        using var stream = await _client.GetStreamAsync("/ipad/events", cts.Token);
        using var reader = new StreamReader(stream);
        await NextEventAsync(reader, cts.Token);

        _router.ApplySettings(new RouterSettings());

        (await NextEventAsync(reader, cts.Token)).Name.Should().Be("gone");
        (await reader.ReadToEndAsync(cts.Token)).Trim().Should().BeEmpty();
    }

    [Fact]
    public async Task Closing_the_stream_disconnects_the_device()
    {
        using (var cts = Cts())
        using (var stream = await _client.GetStreamAsync("/ipad/events", cts.Token))
        using (var reader = new StreamReader(stream))
        {
            await NextEventAsync(reader, cts.Token);
        }

        await WaitUntilAsync(() => _registry.Get("ipad")!.Connections == 0);
    }

    [Fact]
    public void A_port_in_use_is_reported_not_thrown()
    {
        using var other = new WebDevicesServer(_router, _registry);

        var status = other.Start(_port);

        status.Listening.Should().BeFalse();
        status.Message.Should().Contain(_port.ToString());
    }

    [Fact]
    public async Task Can_restart_on_the_same_port()
    {
        _server.Stop();
        _server.Status.Listening.Should().BeFalse();

        _server.Start(_port).Listening.Should().BeTrue();

        (await _client.GetAsync("/ipad/", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Stopping_ends_open_event_streams()
    {
        using var cts = Cts();
        using var stream = await _client.GetStreamAsync("/ipad/events", cts.Token);
        using var reader = new StreamReader(stream);
        await NextEventAsync(reader, cts.Token);

        _server.Stop();

        var rest = async () => await reader.ReadToEndAsync(cts.Token);
        await rest.Should().NotThrowAsync<OperationCanceledException>();
    }

    private static async Task<(string Name, JObject Data)> NextEventAsync(StreamReader reader, CancellationToken token)
    {
        string? name = null;
        string? data = null;
        while (true)
        {
            var line = await reader.ReadLineAsync(token) ?? throw new EndOfStreamException();
            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                name = line.Substring(7);
            }
            else if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                data = line.Substring(6);
            }
            else if (line == "" && data is not null)
            {
                return (name ?? "message", JObject.Parse(data));
            }
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            DateTime.UtcNow.Should().BeBefore(deadline, "the condition should become true in time");
            await Task.Delay(20, Ct);
        }
    }
}
