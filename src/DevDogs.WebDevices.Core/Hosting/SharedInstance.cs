using System;
using System.Threading.Tasks;

namespace DevDogs.WebDevices.Core.Hosting;

/// <summary>
/// One instance shared by successive holders, disposed only after nobody has held it for a grace period.
/// SimHub ends the plugin and creates a new one on every game switch; holding the device server here keeps
/// it, and every device connection, alive across those switches (docs/adr/ADR-0007). Thread-safe.
/// </summary>
/// <typeparam name="T">The shared resource.</typeparam>
public sealed class SharedInstance<T>
    where T : class, IDisposable
{
    private readonly object _gate = new();
    private readonly TimeSpan _grace;
    private T? _instance;
    private int _holders;
    private int _generation;

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedInstance{T}"/> class.
    /// </summary>
    /// <param name="grace">How long the instance outlives its last holder, waiting for a new one.</param>
    public SharedInstance(TimeSpan grace)
    {
        _grace = grace;
    }

    /// <summary>
    /// Returns the shared instance, creating it if there is none, and counts the caller as a holder.
    /// </summary>
    /// <param name="create">Creates the instance; called only when none exists.</param>
    /// <returns>The shared instance.</returns>
    public T Acquire(Func<T> create)
    {
        lock (_gate)
        {
            _generation++; // cancels a pending disposal
            _holders++;
            return _instance ??= create();
        }
    }

    /// <summary>
    /// Stops counting one holder. When none remain, the instance is disposed after the grace period
    /// unless it is acquired again first. Extra calls are ignored.
    /// </summary>
    public void Release()
    {
        int generation;
        lock (_gate)
        {
            if (_holders == 0)
            {
                return;
            }

            if (--_holders > 0)
            {
                return;
            }

            generation = ++_generation;
        }

        _ = Task.Delay(_grace).ContinueWith(_ => DisposeIfStillIdle(generation), TaskScheduler.Default);
    }

    private void DisposeIfStillIdle(int generation)
    {
        T? idle;
        lock (_gate)
        {
            if (_holders > 0 || _generation != generation)
            {
                return;
            }

            idle = _instance;
            _instance = null;
        }

        idle?.Dispose();
    }
}
