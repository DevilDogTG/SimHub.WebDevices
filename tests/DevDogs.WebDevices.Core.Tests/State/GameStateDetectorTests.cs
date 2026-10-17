using DevDogs.WebDevices.Core.State;
using FluentAssertions;
using Xunit;

namespace DevDogs.WebDevices.Core.Tests.State;

public class GameStateDetectorTests
{
    [Theory]
    // process, running, menu -> state
    [InlineData(false, false, false, GameState.SystemIdle)]
    [InlineData(false, false, true, GameState.SystemIdle)] // a stale menu flag after the game closed
    [InlineData(true, false, false, GameState.GameIdle)] // game launched, telemetry not started yet
    [InlineData(true, false, true, GameState.GameIdle)]
    [InlineData(true, true, true, GameState.GameIdle)] // in the game's menus
    [InlineData(true, true, false, GameState.Driving)]
    [InlineData(false, true, false, GameState.Driving)] // telemetry without process detection (network readers)
    [InlineData(false, true, true, GameState.GameIdle)]
    public void Maps_simhub_signals_to_a_state(bool processDetected, bool gameRunning, bool inMenu, GameState expected)
    {
        var state = GameStateDetector.Detect(new GameSignals("ETS2", processDetected, gameRunning, inMenu));

        state.State.Should().Be(expected);
    }

    [Fact]
    public void System_idle_carries_no_game_so_changing_the_selected_game_in_simhub_does_not_switch_devices()
    {
        var ets2 = GameStateDetector.Detect(new GameSignals("ETS2", false, false, false));
        var acc = GameStateDetector.Detect(new GameSignals("AssettoCorsaCompetizione", false, false, false));

        ets2.Should().Be(RouterState.SystemIdle);
        acc.Should().Be(ets2);
    }

    [Theory]
    [InlineData(true, false, GameState.GameIdle)]
    [InlineData(true, true, GameState.Driving)]
    public void Running_states_keep_the_game_name(bool processDetected, bool gameRunning, GameState expected)
    {
        var state = GameStateDetector.Detect(new GameSignals("ETS2", processDetected, gameRunning, false));

        state.Should().Be(new RouterState(expected, "ETS2"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_game_name_becomes_no_game(string? gameName)
    {
        var state = GameStateDetector.Detect(new GameSignals(gameName, true, true, false));

        state.Should().Be(new RouterState(GameState.Driving, null));
    }
}
