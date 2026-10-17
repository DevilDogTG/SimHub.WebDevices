using DevDogs.WebDevices.Core.Config;
using DevDogs.WebDevices.Core.Routing;
using DevDogs.WebDevices.Core.State;
using FluentAssertions;
using Xunit;

namespace DevDogs.WebDevices.Core.Tests.Routing;

public class DashboardResolverTests
{
    private static readonly RouterState Ets2Driving = new(GameState.Driving, "ETS2");
    private static readonly RouterState Ets2Idle = new(GameState.GameIdle, "ETS2");
    private static readonly RouterState AccDriving = new(GameState.Driving, "AssettoCorsaCompetizione");
    private static readonly RouterState AccIdle = new(GameState.GameIdle, "AssettoCorsaCompetizione");

    private static DeviceConfig FullDevice() => new()
    {
        Name = "ipad",
        SystemIdleDashboard = "Clock",
        Default = new GameDashboards { GameIdle = "Default menu", Driving = "Default drive" },
        Games = { new GameMapping { GameName = "ETS2", GameIdle = "Truck menu", Driving = "Truck" } },
    };

    [Fact]
    public void System_idle_uses_the_system_idle_dashboard()
    {
        DashboardResolver.Resolve(FullDevice(), RouterState.SystemIdle).Should().Be("Clock");
    }

    [Fact]
    public void A_mapped_game_uses_its_own_dashboards()
    {
        DashboardResolver.Resolve(FullDevice(), Ets2Driving).Should().Be("Truck");
        DashboardResolver.Resolve(FullDevice(), Ets2Idle).Should().Be("Truck menu");
    }

    [Fact]
    public void Game_names_match_ignoring_case()
    {
        DashboardResolver.Resolve(FullDevice(), new RouterState(GameState.Driving, "ets2")).Should().Be("Truck");
    }

    [Fact]
    public void An_unmapped_game_uses_the_default_game_dashboards()
    {
        DashboardResolver.Resolve(FullDevice(), AccDriving).Should().Be("Default drive");
        DashboardResolver.Resolve(FullDevice(), AccIdle).Should().Be("Default menu");
    }

    [Fact]
    public void An_unknown_game_uses_the_default_game_dashboards()
    {
        DashboardResolver.Resolve(FullDevice(), new RouterState(GameState.Driving, null)).Should().Be("Default drive");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_game_value_falls_back_to_the_default_game(string? blank)
    {
        var device = FullDevice();
        device.Games[0].Driving = blank;
        device.Games[0].GameIdle = blank;

        DashboardResolver.Resolve(device, Ets2Driving).Should().Be("Default drive");
        DashboardResolver.Resolve(device, Ets2Idle).Should().Be("Default menu");
    }

    [Fact]
    public void Game_idle_without_any_idle_dashboard_uses_the_game_driving_dashboard()
    {
        var device = FullDevice();
        device.Games[0].GameIdle = null;
        device.Default.GameIdle = null;

        DashboardResolver.Resolve(device, Ets2Idle).Should().Be("Truck");
        DashboardResolver.Resolve(device, AccIdle).Should().Be("Default drive");
    }

    [Fact]
    public void Driving_without_any_driving_dashboard_uses_the_system_idle_dashboard()
    {
        var device = new DeviceConfig { Name = "ipad", SystemIdleDashboard = "Clock" };

        DashboardResolver.Resolve(device, AccDriving).Should().Be("Clock");
        DashboardResolver.Resolve(device, AccIdle).Should().Be("Clock");
    }

    [Fact]
    public void Nothing_configured_resolves_to_no_dashboard()
    {
        var device = new DeviceConfig { Name = "ipad" };

        DashboardResolver.Resolve(device, RouterState.SystemIdle).Should().BeNull();
        DashboardResolver.Resolve(device, Ets2Driving).Should().BeNull();
        DashboardResolver.Resolve(device, Ets2Idle).Should().BeNull();
    }

    [Fact]
    public void A_blank_system_idle_dashboard_resolves_to_no_dashboard()
    {
        var device = new DeviceConfig { Name = "ipad", SystemIdleDashboard = " " };

        DashboardResolver.Resolve(device, RouterState.SystemIdle).Should().BeNull();
    }
}
