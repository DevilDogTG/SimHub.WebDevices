using System;
using DevDogs.WebDevices.Core.State;
using FluentAssertions;
using Xunit;

namespace DevDogs.WebDevices.Core.Tests.State;

public class SwitchDebouncerTests
{
    private static readonly RouterState Driving = new(GameState.Driving, "ETS2");
    private static readonly RouterState GameIdle = new(GameState.GameIdle, "ETS2");
    private static readonly RouterState SystemIdle = RouterState.SystemIdle;

    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    [Fact]
    public void Starts_at_the_initial_state()
    {
        new SwitchDebouncer(Driving, Ms(3000), Ms(0)).Current.Should().Be(Driving);
    }

    [Fact]
    public void An_unchanged_state_never_switches()
    {
        var debouncer = new SwitchDebouncer(Driving, Ms(0), Ms(0));

        debouncer.Update(Driving, Ms(0)).Should().BeFalse();
        debouncer.Update(new RouterState(GameState.Driving, "ETS2"), Ms(100)).Should().BeFalse();
    }

    [Fact]
    public void A_zero_delay_switches_on_the_same_frame()
    {
        var debouncer = new SwitchDebouncer(SystemIdle, Ms(0), Ms(0));

        debouncer.Update(GameIdle, Ms(0)).Should().BeTrue();
        debouncer.Current.Should().Be(GameIdle);
    }

    [Fact]
    public void Entering_system_idle_waits_for_the_system_idle_delay()
    {
        var debouncer = new SwitchDebouncer(Driving, Ms(3000), Ms(0));

        debouncer.Update(SystemIdle, Ms(1000)).Should().BeFalse();
        debouncer.Update(SystemIdle, Ms(3999)).Should().BeFalse();
        debouncer.Current.Should().Be(Driving);

        debouncer.Update(SystemIdle, Ms(4000)).Should().BeTrue();
        debouncer.Current.Should().Be(SystemIdle);
    }

    [Fact]
    public void Other_switches_wait_for_the_switch_delay()
    {
        var debouncer = new SwitchDebouncer(SystemIdle, Ms(3000), Ms(500));

        debouncer.Update(GameIdle, Ms(0)).Should().BeFalse();
        debouncer.Update(GameIdle, Ms(499)).Should().BeFalse();
        debouncer.Update(GameIdle, Ms(500)).Should().BeTrue();
        debouncer.Current.Should().Be(GameIdle);
    }

    [Fact]
    public void A_game_restart_shorter_than_the_delay_does_not_switch()
    {
        var debouncer = new SwitchDebouncer(Driving, Ms(3000), Ms(0));

        debouncer.Update(SystemIdle, Ms(0)).Should().BeFalse();
        debouncer.Update(Driving, Ms(2000)).Should().BeFalse();
        debouncer.Current.Should().Be(Driving);

        // A second drop starts a fresh wait rather than continuing the first one.
        debouncer.Update(SystemIdle, Ms(2500)).Should().BeFalse();
        debouncer.Update(SystemIdle, Ms(5000)).Should().BeFalse();
        debouncer.Update(SystemIdle, Ms(5500)).Should().BeTrue();
    }

    [Fact]
    public void A_new_candidate_restarts_the_wait()
    {
        var debouncer = new SwitchDebouncer(SystemIdle, Ms(3000), Ms(500));

        debouncer.Update(GameIdle, Ms(0)).Should().BeFalse();
        debouncer.Update(Driving, Ms(400)).Should().BeFalse();
        debouncer.Update(Driving, Ms(500)).Should().BeFalse();
        debouncer.Update(Driving, Ms(900)).Should().BeTrue();
        debouncer.Current.Should().Be(Driving);
    }

    [Fact]
    public void A_different_game_is_a_new_state()
    {
        var debouncer = new SwitchDebouncer(Driving, Ms(3000), Ms(0));
        var acc = new RouterState(GameState.Driving, "AssettoCorsaCompetizione");

        debouncer.Update(acc, Ms(0)).Should().BeTrue();
        debouncer.Current.Should().Be(acc);
    }

    [Fact]
    public void Changed_delays_apply_to_the_next_wait()
    {
        var debouncer = new SwitchDebouncer(Driving, Ms(3000), Ms(0)) { SystemIdleDelay = Ms(100) };

        debouncer.Update(SystemIdle, Ms(0)).Should().BeFalse();
        debouncer.Update(SystemIdle, Ms(100)).Should().BeTrue();
    }

    [Fact]
    public void Negative_delays_are_rejected()
    {
        var act = () => new SwitchDebouncer(Driving, Ms(-1), Ms(0));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
