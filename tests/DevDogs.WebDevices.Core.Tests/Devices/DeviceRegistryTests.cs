using System;
using DevDogs.WebDevices.Core.Devices;
using FluentAssertions;
using Xunit;

namespace DevDogs.WebDevices.Core.Tests.Devices;

public class DeviceRegistryTests
{
    private DateTime _now = new(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);

    private DeviceRegistry Create() => new(() => _now);

    [Fact]
    public void An_unseen_device_has_no_status()
    {
        Create().Get("ipad").Should().BeNull();
    }

    [Fact]
    public void Connecting_marks_the_device_connected_and_seen()
    {
        var registry = Create();

        using var connection = registry.Connect("ipad", "192.168.1.20");

        registry.Get("ipad").Should().Be(new DeviceStatus("ipad", 1, _now, "192.168.1.20"));
    }

    [Fact]
    public void Disconnecting_keeps_the_last_seen_time_of_the_disconnect()
    {
        var registry = Create();
        var connection = registry.Connect("ipad", "192.168.1.20");
        _now = _now.AddMinutes(5);

        connection.Dispose();

        registry.Get("ipad").Should().Be(new DeviceStatus("ipad", 0, _now, "192.168.1.20"));
    }

    [Fact]
    public void Disposing_twice_counts_once()
    {
        var registry = Create();
        using var other = registry.Connect("ipad", "a");
        var connection = registry.Connect("ipad", "b");

        connection.Dispose();
        connection.Dispose();

        registry.Get("ipad")!.Connections.Should().Be(1);
    }

    [Fact]
    public void Touch_updates_last_seen_without_a_connection()
    {
        var registry = Create();
        _now = _now.AddSeconds(30);

        registry.Touch("ipad", "192.168.1.21");

        registry.Get("ipad").Should().Be(new DeviceStatus("ipad", 0, _now, "192.168.1.21"));
    }

    [Fact]
    public void Names_are_matched_ignoring_case_and_reported_in_lowercase()
    {
        var registry = Create();

        using var connection = registry.Connect("IPad", "a");

        registry.Get("ipad")!.Name.Should().Be("ipad");
    }

    [Fact]
    public void Snapshot_lists_devices_by_name()
    {
        var registry = Create();
        using var s8 = registry.Connect("s8", "a");
        registry.Touch("ipad", "b");

        registry.Snapshot().Should().SatisfyRespectively(
            first => first.Name.Should().Be("ipad"),
            second => second.Name.Should().Be("s8"));
    }

    [Fact]
    public void Forget_removes_a_device()
    {
        var registry = Create();
        registry.Touch("ipad", "a");

        registry.Forget("ipad");

        registry.Get("ipad").Should().BeNull();
    }
}
