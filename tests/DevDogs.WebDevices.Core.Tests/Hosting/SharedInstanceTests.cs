using System;
using System.Threading;
using System.Threading.Tasks;
using DevDogs.WebDevices.Core.Hosting;
using FluentAssertions;
using Xunit;

namespace DevDogs.WebDevices.Core.Tests.Hosting;

public class SharedInstanceTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromMilliseconds(100);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Resource : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void The_first_acquire_creates_the_instance()
    {
        var shared = new SharedInstance<Resource>(Grace);
        var created = 0;

        var resource = shared.Acquire(() => { created++; return new Resource(); });

        resource.Should().NotBeNull();
        created.Should().Be(1);
    }

    [Fact]
    public async Task Release_then_acquire_within_the_grace_period_keeps_the_same_instance()
    {
        // SimHub ends the old plugin instance and initializes a new one on every game switch.
        var shared = new SharedInstance<Resource>(Grace);
        var first = shared.Acquire(() => new Resource());

        shared.Release();
        var second = shared.Acquire(() => new Resource());
        await Task.Delay(Grace * 3, Ct);

        second.Should().BeSameAs(first);
        first.Disposed.Should().BeFalse();
    }

    [Fact]
    public async Task The_instance_is_disposed_once_released_for_longer_than_the_grace_period()
    {
        var shared = new SharedInstance<Resource>(Grace);
        var first = shared.Acquire(() => new Resource());

        shared.Release();
        await WaitUntilAsync(() => first.Disposed);

        shared.Acquire(() => new Resource()).Should().NotBeSameAs(first);
    }

    [Fact]
    public async Task It_stays_alive_while_any_holder_remains()
    {
        var shared = new SharedInstance<Resource>(Grace);
        var first = shared.Acquire(() => new Resource());
        shared.Acquire(() => new Resource());

        shared.Release();
        await Task.Delay(Grace * 3, Ct);

        first.Disposed.Should().BeFalse();
    }

    [Fact]
    public async Task An_extra_release_is_ignored()
    {
        var shared = new SharedInstance<Resource>(Grace);
        var first = shared.Acquire(() => new Resource());
        shared.Release();
        shared.Release();

        var second = shared.Acquire(() => new Resource());
        await Task.Delay(Grace * 3, Ct);

        second.Should().BeSameAs(first);
        first.Disposed.Should().BeFalse();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            DateTime.UtcNow.Should().BeBefore(deadline, "the condition should become true in time");
            await Task.Delay(10, Ct);
        }
    }
}
