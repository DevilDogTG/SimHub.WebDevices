using System;
using DevDogs.WebDevices.Core.Routing;
using FluentAssertions;
using Xunit;

namespace DevDogs.WebDevices.Core.Tests.Routing;

public class DashUrlTests
{
    [Fact]
    public void Matches_the_url_simhub_itself_links_to()
    {
        // Copied from SimHub v5042's dashboard list on :8888.
        DashUrl.Build("MYTEC C127 - System Info", hideControls: false, cacheBuster: 1792201344489)
            .Should().Be("/Dash?v=1792201344489#MYTEC%20C127%20-%20System%20Info");
    }

    [Fact]
    public void Hiding_controls_adds_the_nocontrols_flag()
    {
        DashUrl.Build("Clock", hideControls: true, cacheBuster: 5)
            .Should().Be("/Dash?v=5#Clock|nocontrols");
    }

    [Theory]
    // Same escaping as JavaScript's encodeURIComponent, which SimHub's own page uses.
    [InlineData("A|B", "A%7CB")] // '|' separates SimHub's flags, so it must not appear raw
    [InlineData("A#B", "A%23B")]
    [InlineData("100%", "100%25")]
    [InlineData("A&B+C", "A%26B%2BC")]
    [InlineData("a-b_c.d!e~f*g'h(i)j", "a-b_c.d!e~f*g'h(i)j")]
    [InlineData("Café", "Caf%C3%A9")]
    public void Escapes_the_dashboard_code_like_simhub_does(string code, string expected)
    {
        DashUrl.Build(code, hideControls: false, cacheBuster: 1).Should().Be("/Dash?v=1#" + expected);
    }

    [Fact]
    public void A_new_cache_buster_gives_a_new_url_so_the_frame_reloads()
    {
        DashUrl.Build("Clock", false, 1).Should().NotBe(DashUrl.Build("Clock", false, 2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_dashboard_code_is_rejected(string code)
    {
        var act = () => DashUrl.Build(code, false, 1);

        act.Should().Throw<ArgumentException>();
    }
}
