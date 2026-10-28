using System;
using System.IO;
using DevDogs.WebDevices.Core.Config;
using FluentAssertions;
using Xunit;

namespace DevDogs.WebDevices.Core.Tests.Config;

public sealed class SimHubPortsTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"DashStudioSettings_{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_file);

    private SimHubPorts ReadJson(string json)
    {
        File.WriteAllText(_file, json);
        return SimHubPorts.Read(_file);
    }

    [Fact]
    public void Reads_the_web_and_https_ports_from_simhubs_settings()
    {
        ReadJson("""{ "SimHubWebPort": 8080, "SimHubWebHttpsEnabled": true, "SimHubWebHttpsPort": 8443 }""")
            .Should().Be(new SimHubPorts(8080, 8443));
    }

    [Fact]
    public void A_missing_file_gives_simhubs_defaults()
    {
        SimHubPorts.Read(_file).Should().Be(new SimHubPorts(8888, 8889));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    public void An_unreadable_file_gives_simhubs_defaults(string json)
    {
        ReadJson(json).Should().Be(SimHubPorts.Default);
    }

    [Fact]
    public void A_missing_or_invalid_value_gives_that_ports_default()
    {
        ReadJson("""{ "SimHubWebPort": "8080", "SimHubWebHttpsPort": 70000 }""").Should().Be(SimHubPorts.Default);
        ReadJson("""{ "SimHubWebPort": 8080 }""").Should().Be(new SimHubPorts(8080, 8889));
    }

    [Fact]
    public void Conflict_explains_a_clash_with_either_port()
    {
        var ports = new SimHubPorts(8888, 8889);

        ports.Conflict(8888).Should().Contain("SimHub's own web port");
        ports.Conflict(8889).Should().Contain("SimHub's HTTPS port");
        ports.Conflict(8890).Should().BeNull();
    }
}
