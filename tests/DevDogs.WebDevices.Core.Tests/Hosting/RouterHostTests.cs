using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DevDogs.WebDevices.Core.Config;
using DevDogs.WebDevices.Core.Hosting;
using DevDogs.WebDevices.Core.State;
using FluentAssertions;
using Xunit;

namespace DevDogs.WebDevices.Core.Tests.Hosting;

public sealed class RouterHostTests : IDisposable
{
    private readonly List<string> _log = [];
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly int _port = FreePort();
    private readonly RouterHost _host;

    public RouterHostTests()
    {
        _host = new RouterHost(Settings(_port), cacheBusterSeed: 1000, () => DateTime.UtcNow, Log);
    }

    public void Dispose()
    {
        _client.Dispose();
        _host.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RouterSettings Settings(int port, string idleDashboard = "Clock") => new()
    {
        Port = port,
        SystemIdleDelayMs = 0,
        Devices =
        {
            new DeviceConfig
            {
                Name = "ipad",
                SystemIdleDashboard = idleDashboard,
                Games = { new GameMapping { GameName = "ETS2", Driving = "Truck" } },
            },
        },
    };

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private void Log(string message)
    {
        lock (_log)
        {
            _log.Add(message);
        }
    }

    private Task<string> GetStateAsync(int port) => _client.GetStringAsync($"http://127.0.0.1:{port}/ipad/state", Ct);

    [Fact]
    public async Task Configure_starts_the_server_on_the_settings_port()
    {
        _host.Configure(Settings(_port)).Listening.Should().BeTrue();

        (await GetStateAsync(_port)).Should().Contain("Clock");
        _log.Should().Contain(m => m.Contains($"Listening on port {_port}"));
    }

    [Fact]
    public async Task Configuring_again_on_the_same_port_keeps_open_device_streams()
    {
        _host.Configure(Settings(_port));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        using var stream = await _client.GetStreamAsync($"http://127.0.0.1:{_port}/ipad/events", cts.Token);
        using var reader = new StreamReader(stream);
        await ReadUntilAsync(reader, "Clock", cts.Token);

        // What a new plugin instance does after a game switch: same settings file, new idle dashboard here.
        _host.Configure(Settings(_port, idleDashboard: "Big clock"));

        await ReadUntilAsync(reader, "Big clock", cts.Token);
        _host.Registry.Get("ipad")!.Connections.Should().Be(1);
    }

    [Fact]
    public async Task A_new_port_moves_the_server()
    {
        _host.Configure(Settings(_port));
        var newPort = FreePort();

        _host.Configure(Settings(newPort)).Port.Should().Be(newPort);

        (await GetStateAsync(newPort)).Should().Contain("Clock");
        var oldPort = async () => await GetStateAsync(_port);
        await oldPort.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task A_port_simhub_uses_is_refused_and_stops_the_server()
    {
        _host.Configure(Settings(_port));

        var status = _host.Configure(Settings(_port), new SimHubPorts(8888, _port));

        status.Listening.Should().BeFalse();
        status.Message.Should().Contain("SimHub's HTTPS port");
        _host.Server.Status.Should().Be(status);
        var request = async () => await GetStateAsync(_port);
        await request.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task Moving_off_a_refused_port_starts_the_server()
    {
        var simHub = new SimHubPorts(8888, _port);
        _host.Configure(Settings(_port), simHub);
        var newPort = FreePort();

        _host.Configure(Settings(newPort), simHub).Listening.Should().BeTrue();

        (await GetStateAsync(newPort)).Should().Contain("Clock");
    }

    [Fact]
    public void Observe_routes_by_the_hosts_own_clock()
    {
        _host.Configure(Settings(_port));

        _host.Observe(new GameSignals("ETS2", true, true, false)).Should().BeTrue();

        _host.Router.GetTarget("ipad")!.Dashboard.Should().Be("Truck");
    }

    [Fact]
    public async Task Dispose_stops_the_server()
    {
        _host.Configure(Settings(_port));

        _host.Dispose();

        var request = async () => await GetStateAsync(_port);
        await request.Should().ThrowAsync<HttpRequestException>();
    }

    private static async Task ReadUntilAsync(StreamReader reader, string text, CancellationToken token)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(token) ?? throw new EndOfStreamException();
            if (line.StartsWith("data: ", StringComparison.Ordinal) && line.Contains($"\"{text}\""))
            {
                return;
            }
        }
    }
}
