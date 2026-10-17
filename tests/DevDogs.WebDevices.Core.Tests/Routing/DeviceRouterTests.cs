using System;
using System.Collections.Generic;
using DevDogs.WebDevices.Core.Config;
using DevDogs.WebDevices.Core.Routing;
using DevDogs.WebDevices.Core.State;
using FluentAssertions;
using Xunit;

namespace DevDogs.WebDevices.Core.Tests.Routing;

public class DeviceRouterTests
{
    private static readonly GameSignals NoGame = new("ETS2", false, false, false);
    private static readonly GameSignals Ets2Driving = new("ETS2", true, true, false);

    private static RouterSettings Settings() => new()
    {
        SystemIdleDelayMs = 0,
        SwitchDelayMs = 0,
        Devices =
        {
            new DeviceConfig
            {
                Name = "ipad",
                SystemIdleDashboard = "Clock",
                Games = { new GameMapping { GameName = "ETS2", Driving = "Truck" } },
            },
            new DeviceConfig { Name = "s8", SystemIdleDashboard = "Clock" },
        },
    };

    private static (DeviceRouter Router, List<IReadOnlyCollection<string>> Events) Create(RouterSettings? settings = null)
    {
        var router = new DeviceRouter(settings ?? Settings(), cacheBusterSeed: 1000);
        var events = new List<IReadOnlyCollection<string>>();
        router.TargetsChanged += events.Add;
        return (router, events);
    }

    [Fact]
    public void Starts_in_system_idle_showing_each_system_idle_dashboard()
    {
        var (router, _) = Create();

        router.State.Should().Be(RouterState.SystemIdle);
        router.GetTarget("ipad")!.Dashboard.Should().Be("Clock");
        router.GetTarget("ipad")!.Path.Should().StartWith("/Dash?v=").And.EndWith("#Clock");
    }

    [Fact]
    public void An_unknown_device_has_no_target()
    {
        Create().Router.GetTarget("phone").Should().BeNull();
    }

    [Fact]
    public void Device_names_match_ignoring_case()
    {
        Create().Router.GetTarget("IPAD").Should().NotBeNull();
    }

    [Fact]
    public void A_device_with_nothing_configured_has_an_empty_target()
    {
        var settings = new RouterSettings { Devices = { new DeviceConfig { Name = "blank" } } };

        Create(settings).Router.GetTarget("blank").Should().Be(new DeviceTarget(null, null));
    }

    [Fact]
    public void A_state_change_switches_and_reports_only_the_devices_whose_dashboard_changed()
    {
        var (router, events) = Create();
        var s8Before = router.GetTarget("s8");

        router.Observe(Ets2Driving, TimeSpan.Zero).Should().BeTrue();

        router.State.Should().Be(new RouterState(GameState.Driving, "ETS2"));
        router.GetTarget("ipad")!.Dashboard.Should().Be("Truck");
        router.GetTarget("s8").Should().Be(s8Before, "the s8 shows Clock in every state, so its frame must not reload");
        events.Should().ContainSingle().Which.Should().BeEquivalentTo(["ipad"]);
    }

    [Fact]
    public void No_event_when_nothing_changes()
    {
        var (router, events) = Create();

        router.Observe(NoGame, TimeSpan.Zero).Should().BeFalse();

        events.Should().BeEmpty();
    }

    [Fact]
    public void Returning_to_a_dashboard_gives_a_new_path_so_the_frame_reloads()
    {
        var (router, _) = Create();
        var first = router.GetTarget("ipad")!.Path;

        router.Observe(Ets2Driving, TimeSpan.Zero);
        router.Observe(NoGame, TimeSpan.FromSeconds(1));

        router.GetTarget("ipad")!.Dashboard.Should().Be("Clock");
        router.GetTarget("ipad")!.Path.Should().NotBe(first);
    }

    [Fact]
    public void Switches_are_debounced_with_the_configured_delays()
    {
        var settings = Settings();
        settings.SystemIdleDelayMs = 3000;
        var (router, _) = Create(settings);
        router.Observe(Ets2Driving, TimeSpan.Zero);

        router.Observe(NoGame, TimeSpan.FromSeconds(1)).Should().BeFalse();
        router.Observe(NoGame, TimeSpan.FromSeconds(4)).Should().BeTrue();
    }

    [Fact]
    public void New_settings_report_changed_added_and_removed_devices()
    {
        var (router, events) = Create();
        var settings = Settings();
        settings.Devices[0].SystemIdleDashboard = "Big clock"; // ipad changed
        settings.Devices.RemoveAt(1); // s8 removed
        settings.Devices.Add(new DeviceConfig { Name = "tab", SystemIdleDashboard = "Clock" }); // tab added

        router.ApplySettings(settings);

        router.GetTarget("ipad")!.Dashboard.Should().Be("Big clock");
        router.GetTarget("s8").Should().BeNull();
        router.GetTarget("tab")!.Dashboard.Should().Be("Clock");
        events.Should().ContainSingle().Which.Should().BeEquivalentTo(["ipad", "s8", "tab"]);
    }

    [Fact]
    public void Hiding_controls_changes_the_path()
    {
        var (router, events) = Create();
        var settings = Settings();
        settings.Devices[0].HideControls = true;

        router.ApplySettings(settings);

        router.GetTarget("ipad")!.Path.Should().EndWith("#Clock|nocontrols");
        events.Should().ContainSingle().Which.Should().BeEquivalentTo(["ipad"]);
    }

    [Fact]
    public void New_delays_apply_to_later_switches()
    {
        var (router, _) = Create();
        router.Observe(Ets2Driving, TimeSpan.Zero);
        var settings = Settings();
        settings.SystemIdleDelayMs = 5000;

        router.ApplySettings(settings);

        router.Observe(NoGame, TimeSpan.FromSeconds(1)).Should().BeFalse();
        router.Observe(NoGame, TimeSpan.FromSeconds(6)).Should().BeTrue();
    }

    [Fact]
    public void Later_edits_to_a_settings_object_do_not_leak_into_the_router()
    {
        var settings = Settings();
        var (router, _) = Create(settings);

        settings.Devices[0].SystemIdleDashboard = "Edited";

        router.GetTarget("ipad")!.Dashboard.Should().Be("Clock");
        router.Settings.Devices[0].SystemIdleDashboard.Should().Be("Clock");
    }
}
