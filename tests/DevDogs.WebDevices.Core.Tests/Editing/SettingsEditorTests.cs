using System;
using System.Collections.Generic;
using System.Linq;
using DevDogs.WebDevices.Core.Config;
using DevDogs.WebDevices.Core.Devices;
using DevDogs.WebDevices.Core.Editing;
using DevDogs.WebDevices.Core.Routing;
using DevDogs.WebDevices.Core.Server;
using DevDogs.WebDevices.Core.State;
using FluentAssertions;
using Xunit;

namespace DevDogs.WebDevices.Core.Tests.Editing;

public class SettingsEditorTests
{
    private const string CustomCode = "Custom_26c4c1fa";

    private static readonly IReadOnlyList<string> Dashboards = ["Clock", "MYTEC C127", "Truck"];

    private static readonly IReadOnlyList<GameOption> Games =
    [
        new("AssettoCorsaCompetizione", "Assetto Corsa Competizione"),
        new("ETS2", "Euro Truck Simulator 2"),
        new(CustomCode, "TestTaskManager"),
    ];

    private readonly List<RouterSettings> _saved = [];
    private string? _currentGame = "ETS2";

    private static RouterSettings Saved() => new()
    {
        Port = 8889,
        SystemIdleDelayMs = 3000,
        Devices =
        {
            new DeviceConfig
            {
                Name = "ipad",
                HideControls = true,
                SystemIdleDashboard = "Clock",
                Default = new GameDashboards { Driving = "MYTEC C127" },
                Games = { new GameMapping { GameName = "ETS2", Driving = "Truck" } },
            },
            new DeviceConfig { Name = "s8", SystemIdleDashboard = "Clock" },
        },
    };

    private SettingsEditor Create(RouterSettings? settings = null) =>
        new(settings ?? Saved(), Dashboards, Games, () => _currentGame, () => "192.168.10.102", _saved.Add);

    [Fact]
    public void Loads_devices_general_settings_and_friendly_game_names()
    {
        var editor = Create();

        editor.Port.Should().Be(8889);
        editor.SystemIdleDelayMs.Should().Be(3000);
        editor.Devices.Select(d => d.Name).Should().Equal("ipad", "s8");
        editor.SelectedDevice.Should().BeSameAs(editor.Devices[0]);
        var game = editor.Devices[0].Games.Should().ContainSingle().Subject;
        game.GameCode.Should().Be("ETS2");
        game.DisplayName.Should().Be("Euro Truck Simulator 2");
    }

    [Fact]
    public void Round_trips_the_loaded_settings()
    {
        Create().ToSettings().Should().BeEquivalentTo(Saved());
    }

    [Fact]
    public void Starts_clean_and_becomes_dirty_on_edit_and_clean_again_when_undone()
    {
        var editor = Create();
        editor.IsDirty.Should().BeFalse();

        editor.Devices[0].SystemIdleDashboard = "Truck";
        editor.IsDirty.Should().BeTrue();

        editor.Devices[0].SystemIdleDashboard = "Clock";
        editor.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void Edits_inside_game_rows_mark_the_editor_dirty()
    {
        var editor = Create();

        editor.Devices[0].Games[0].GameIdle = "Clock";

        editor.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Adding_devices_picks_unique_names_and_selects_the_new_device()
    {
        var editor = Create();

        editor.AddDeviceCommand.Execute(null);
        editor.AddDeviceCommand.Execute(null);

        editor.Devices.Select(d => d.Name).Should().Equal("ipad", "s8", "device-1", "device-2");
        editor.SelectedDevice!.Name.Should().Be("device-2");
        editor.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void New_devices_hide_simhubs_dashboard_controls()
    {
        // SimHub shows its Fullscreen/Reload/Back panel on every dashboard load, so on a switching
        // device it would reappear after every switch.
        var editor = Create();

        editor.AddDeviceCommand.Execute(null);

        editor.SelectedDevice!.HideControls.Should().BeTrue();
    }

    [Fact]
    public void Removing_a_device_selects_its_neighbour()
    {
        var editor = Create();
        editor.SelectedDevice = editor.Devices[0];

        editor.RemoveDeviceCommand.Execute(null);

        editor.Devices.Select(d => d.Name).Should().Equal("s8");
        editor.SelectedDevice!.Name.Should().Be("s8");
    }

    [Fact]
    public void Remove_is_disabled_without_a_selection()
    {
        var editor = Create(new RouterSettings());

        editor.SelectedDevice.Should().BeNull();
        editor.RemoveDeviceCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Invalid_settings_block_saving()
    {
        var editor = Create();

        editor.Devices[0].Name = "Bad Name";

        editor.Errors.Should().ContainSingle().Which.Should().Contain("Bad Name");
        editor.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void A_port_clash_with_simhub_is_an_error()
    {
        var editor = Create();

        editor.Port = 8888;

        editor.Errors.Should().ContainSingle();
    }

    [Fact]
    public void Unknown_dashboards_are_warnings_that_still_allow_saving()
    {
        var editor = Create();

        editor.Devices[1].SystemIdleDashboard = "Deleted dash";

        editor.Warnings.Should().ContainSingle().Which.Should().Contain("Deleted dash").And.Contain("s8");
        editor.Errors.Should().BeEmpty();
        editor.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Adds_the_picked_game_once_with_its_friendly_name()
    {
        var editor = Create();
        editor.SelectedDevice = editor.Devices[1];
        editor.SelectedGameToAdd = Games[2];

        editor.AddGameCommand.Execute(null);
        editor.AddGameCommand.Execute(null);

        var row = editor.Devices[1].Games.Should().ContainSingle().Subject;
        row.GameCode.Should().Be(CustomCode);
        row.DisplayName.Should().Be("TestTaskManager");
    }

    [Fact]
    public void Adds_the_game_simhub_is_currently_on()
    {
        var editor = Create();
        editor.SelectedDevice = editor.Devices[1];

        editor.AddCurrentGameCommand.Execute(null);

        editor.Devices[1].Games.Single().GameCode.Should().Be("ETS2");
    }

    [Fact]
    public void Add_current_game_is_disabled_when_simhub_has_no_game()
    {
        _currentGame = null;

        Create().AddCurrentGameCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void An_unknown_game_code_shows_the_code_as_its_name()
    {
        _currentGame = "SomeNewGame";
        var editor = Create();

        editor.AddCurrentGameCommand.Execute(null);

        editor.Devices[0].Games.Last().DisplayName.Should().Be("SomeNewGame");
    }

    [Fact]
    public void Removes_a_game_row()
    {
        var editor = Create();
        var row = editor.Devices[0].Games[0];

        editor.RemoveGameCommand.Execute(row);

        editor.Devices[0].Games.Should().BeEmpty();
    }

    [Fact]
    public void Save_hands_over_the_settings_and_leaves_the_editor_clean()
    {
        var editor = Create();
        editor.Devices[0].Name = "ipad-left";

        editor.SaveCommand.Execute(null);

        _saved.Should().ContainSingle().Which.Devices[0].Name.Should().Be("ipad-left");
        editor.IsDirty.Should().BeFalse();
        editor.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Revert_restores_the_last_saved_settings()
    {
        var editor = Create();
        editor.AddDeviceCommand.Execute(null);
        editor.Port = 9000;

        editor.RevertCommand.Execute(null);

        editor.ToSettings().Should().BeEquivalentTo(Saved());
        editor.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void Export_then_import_restores_the_settings_as_unsaved_changes()
    {
        var editor = Create();
        var exported = editor.Export();
        editor.Devices.Clear();

        editor.Import(exported).Should().BeNull();

        editor.ToSettings().Should().BeEquivalentTo(Saved());
        _saved.Should().BeEmpty("importing does not save by itself");
    }

    [Fact]
    public void Importing_something_else_reports_why_and_changes_nothing()
    {
        var editor = Create();

        editor.Import("not json").Should().NotBeNull();

        editor.ToSettings().Should().BeEquivalentTo(Saved());
    }

    [Fact]
    public void Device_urls_use_the_pc_address_port_and_name()
    {
        var editor = Create();
        var device = editor.Devices[0];

        device.Url.Should().Be("http://192.168.10.102:8889/ipad/");

        device.Name = "ipad-left";
        editor.Port = 9000;

        device.Url.Should().Be("http://192.168.10.102:9000/ipad-left/");
    }

    [Fact]
    public void Status_refresh_describes_the_server_state_and_each_device()
    {
        var editor = Create();
        var now = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        var seen = new List<DeviceStatus>
        {
            new("ipad", 1, now.AddSeconds(-2), "192.168.10.20"),
            new("s8", 0, now.AddMinutes(-5), "192.168.10.21"),
        };

        editor.RefreshStatus(
            new RouterState(GameState.Driving, CustomCode),
            new ServerStatus(true, 8889, "Listening on port 8889."),
            seen,
            name => name == "ipad" ? new DeviceTarget("Truck", "/Dash?v=1#Truck") : new DeviceTarget(null, null),
            now);

        editor.ServerText.Should().Be("Listening on port 8889.");
        editor.StateText.Should().Be("Driving · TestTaskManager");
        editor.Devices[0].IsConnected.Should().BeTrue();
        editor.Devices[0].StatusText.Should().Be("Connected from 192.168.10.20");
        editor.Devices[0].ShowingText.Should().Be("Showing Truck");
        editor.Devices[1].IsConnected.Should().BeFalse();
        editor.Devices[1].StatusText.Should().Be("Last seen 5 min ago");
        editor.Devices[1].ShowingText.Should().Be("No dashboard for this state");
    }

    [Fact]
    public void Status_refresh_handles_idle_and_never_seen_devices()
    {
        var editor = Create();

        editor.RefreshStatus(
            RouterState.SystemIdle,
            new ServerStatus(false, 8889, "Port 8889 is already in use by another program. Choose another port."),
            [],
            _ => null,
            DateTime.UtcNow);

        editor.StateText.Should().Be("No game running");
        editor.Devices[0].StatusText.Should().Be("Never connected");
        editor.Devices[0].ShowingText.Should().Be("Not saved yet");
    }

    [Fact]
    public void Status_refresh_never_marks_the_editor_dirty()
    {
        var editor = Create();

        editor.RefreshStatus(RouterState.SystemIdle, new ServerStatus(true, 8889, "ok"), [], _ => null, DateTime.UtcNow);

        editor.IsDirty.Should().BeFalse();
    }
}
