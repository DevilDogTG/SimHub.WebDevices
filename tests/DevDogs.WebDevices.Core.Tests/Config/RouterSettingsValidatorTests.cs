using System.Collections.Generic;
using DevDogs.WebDevices.Core.Config;
using FluentAssertions;
using Xunit;

namespace DevDogs.WebDevices.Core.Tests.Config;

public class RouterSettingsValidatorTests
{
    [Fact]
    public void Defaults_are_valid()
    {
        RouterSettingsValidator.Validate(new RouterSettings()).Should().BeEmpty();
    }

    [Fact]
    public void A_fully_configured_device_is_valid()
    {
        var settings = new RouterSettings
        {
            Devices =
            {
                new DeviceConfig
                {
                    Name = "ipad-left",
                    SystemIdleDashboard = "Clock",
                    Default = new GameDashboards { Driving = "Generic" },
                    Games =
                    {
                        new GameMapping { GameName = "ETS2", GameIdle = "Truck menu", Driving = "Truck" },
                        new GameMapping { GameName = "AssettoCorsaCompetizione", Driving = "ACC" },
                    },
                },
            },
        };

        RouterSettingsValidator.Validate(settings).Should().BeEmpty();
    }

    [Theory]
    [InlineData("ipad")]
    [InlineData("ipad-left")]
    [InlineData("s8_1")]
    [InlineData("a")]
    [InlineData("abcdefghijklmnopqrstuvwxyz012345")]
    public void Url_safe_device_names_are_valid(string name)
    {
        DeviceNameRules.IsValid(name).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("iPad")]
    [InlineData("ipad left")]
    [InlineData("-ipad")]
    [InlineData("ipad/left")]
    [InlineData("ipad.left")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456")]
    [InlineData("admin")]
    [InlineData("api")]
    public void Invalid_or_reserved_device_names_are_rejected(string name)
    {
        DeviceNameRules.IsValid(name).Should().BeFalse();

        var settings = new RouterSettings { Devices = { new DeviceConfig { Name = name } } };
        RouterSettingsValidator.Validate(settings).Should().ContainSingle();
    }

    [Fact]
    public void Duplicate_device_names_are_rejected()
    {
        var settings = new RouterSettings
        {
            Devices = { new DeviceConfig { Name = "ipad" }, new DeviceConfig { Name = "ipad" } },
        };

        RouterSettingsValidator.Validate(settings).Should().ContainSingle().Which.Should().Contain("ipad");
    }

    [Fact]
    public void Duplicate_game_names_in_one_device_are_rejected_ignoring_case()
    {
        var device = new DeviceConfig
        {
            Name = "ipad",
            Games = { new GameMapping { GameName = "ETS2" }, new GameMapping { GameName = "ets2" } },
        };

        RouterSettingsValidator.Validate(new RouterSettings { Devices = { device } })
            .Should().ContainSingle().Which.Should().Contain("ets2");
    }

    [Fact]
    public void The_same_game_on_two_devices_is_allowed()
    {
        var settings = new RouterSettings
        {
            Devices =
            {
                new DeviceConfig { Name = "a", Games = { new GameMapping { GameName = "ETS2" } } },
                new DeviceConfig { Name = "b", Games = { new GameMapping { GameName = "ETS2" } } },
            },
        };

        RouterSettingsValidator.Validate(settings).Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void A_game_mapping_needs_a_game_name(string gameName)
    {
        var device = new DeviceConfig { Name = "ipad", Games = { new GameMapping { GameName = gameName } } };

        RouterSettingsValidator.Validate(new RouterSettings { Devices = { device } }).Should().ContainSingle();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    [InlineData(65536)]
    public void Port_must_be_a_non_privileged_tcp_port(int port)
    {
        RouterSettingsValidator.Validate(new RouterSettings { Port = port }).Should().ContainSingle();
    }

    [Fact]
    public void Port_must_differ_from_simhub_web_port()
    {
        var settings = new RouterSettings { Port = 8888, SimHubWebPort = 8888 };

        RouterSettingsValidator.Validate(settings).Should().ContainSingle();
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(60001, 0)]
    [InlineData(0, 60001)]
    public void Delays_must_be_between_zero_and_sixty_seconds(int systemIdleDelayMs, int switchDelayMs)
    {
        var settings = new RouterSettings { SystemIdleDelayMs = systemIdleDelayMs, SwitchDelayMs = switchDelayMs };

        RouterSettingsValidator.Validate(settings).Should().ContainSingle();
    }

    [Fact]
    public void Every_problem_is_reported()
    {
        var settings = new RouterSettings
        {
            Port = 0,
            Devices = new List<DeviceConfig> { new() { Name = "Bad Name" }, new() { Name = "" } },
        };

        RouterSettingsValidator.Validate(settings).Should().HaveCount(3);
    }
}
