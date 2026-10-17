using System;
using DevDogs.WebDevices.Core.Config;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DevDogs.WebDevices.Core.Tests.Config;

public class RouterSettingsJsonTests
{
    [Fact]
    public void Round_trips_every_setting()
    {
        var settings = new RouterSettings
        {
            Port = 9000,
            SimHubWebPort = 8890,
            SystemIdleDelayMs = 5000,
            SwitchDelayMs = 250,
            Devices =
            {
                new DeviceConfig
                {
                    Name = "ipad",
                    HideControls = true,
                    SystemIdleDashboard = "Clock",
                    Default = new GameDashboards { GameIdle = "Menu", Driving = "Generic" },
                    Games = { new GameMapping { GameName = "ETS2", GameIdle = "Truck menu", Driving = "Truck" } },
                },
            },
        };

        var copy = RouterSettingsJson.Deserialize(RouterSettingsJson.Serialize(settings));

        copy.Should().BeEquivalentTo(settings);
    }

    [Fact]
    public void Missing_fields_take_their_defaults()
    {
        var settings = RouterSettingsJson.Deserialize("""{ "Devices": [ { "Name": "ipad" } ] }""");

        settings.Port.Should().Be(8889);
        settings.SimHubWebPort.Should().Be(8888);
        settings.Devices.Should().ContainSingle().Which.Default.Should().NotBeNull();
        settings.Devices[0].Games.Should().BeEmpty();
    }

    [Fact]
    public void Unknown_fields_are_ignored()
    {
        var settings = RouterSettingsJson.Deserialize("""{ "Port": 9001, "FutureSetting": true }""");

        settings.Port.Should().Be(9001);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[]")]
    public void Malformed_input_throws_a_format_exception(string json)
    {
        var act = () => RouterSettingsJson.Deserialize(json);

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Output_is_indented_and_carries_the_schema_version()
    {
        var json = RouterSettingsJson.Serialize(new RouterSettings());

        json.Should().Contain(Environment.NewLine);
        JObject.Parse(json)["Version"]!.Value<int>().Should().Be(RouterSettings.CurrentVersion);
    }
}
