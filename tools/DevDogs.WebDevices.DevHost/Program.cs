using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DevDogs.WebDevices.Core.Config;
using DevDogs.WebDevices.Core.Hosting;
using DevDogs.WebDevices.Core.State;

namespace DevDogs.WebDevices.DevHost;

/// <summary>
/// Serves device pages from the real Core server without SimHub, with the game state typed in by hand,
/// so the device page can be tried in browsers and on devices. Dashboards still load from SimHub's own
/// web server, so SimHub should be running.
/// </summary>
public static class Program
{
    private const string Usage = """
        Usage: DevHost [--port <n>] [--settings <file>] [--commands <file>]

          --port      Port to serve devices on (default 8890, so it can run next to the plugin's 8889).
          --settings  Settings JSON in the plugin's format (default: one device "test").
          --commands  A file polled for commands; each write is run and the file emptied.

        Commands (typed or written to the commands file):
          idle             no game running
          menu <game>      game running, not driving
          drive <game>     driving
          reload           re-read the settings file
          quit             stop
        """;

    private static readonly object Gate = new();
    private static GameSignals _signals = new(null, false, false, false);

    public static async Task<int> Main(string[] args)
    {
        string? settingsPath = null;
        string? commandsPath = null;
        int? port = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--settings" when i + 1 < args.Length:
                    settingsPath = args[++i];
                    break;
                case "--commands" when i + 1 < args.Length:
                    commandsPath = args[++i];
                    break;
                case "--port" when i + 1 < args.Length && int.TryParse(args[i + 1], out var p):
                    port = p;
                    i++;
                    break;
                default:
                    Console.WriteLine(Usage);
                    return 2;
            }
        }

        var settings = LoadSettings(settingsPath, port);
        using var host = new RouterHost(settings, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), () => DateTime.UtcNow, Log);
        var status = host.Configure(settings);
        if (!status.Listening)
        {
            return 1;
        }

        foreach (var device in settings.Devices)
        {
            Log($"Device page: http://localhost:{settings.Port}/{device.Name}/");
        }

        using var quit = new CancellationTokenSource();
        var observe = ObserveLoopAsync(host, quit.Token);
        var commands = commandsPath is null ? Task.CompletedTask : CommandFileLoopAsync(commandsPath, host, settingsPath, port, quit);
        _ = Task.Run(() => ConsoleLoop(host, settingsPath, port, quit));

        try
        {
            await Task.Delay(Timeout.Infinite, quit.Token);
        }
        catch (OperationCanceledException)
        {
        }

        await Task.WhenAll(observe, commands);
        return 0;
    }

    private static void Log(string message) => Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {message}");

    private static RouterSettings LoadSettings(string? path, int? port)
    {
        var settings = path is null
            ? new RouterSettings
            {
                SystemIdleDelayMs = 0,
                Devices =
                {
                    new DeviceConfig
                    {
                        Name = "test",
                        SystemIdleDashboard = "MYTEC C127 - System Info",
                        Default = new GameDashboards { Driving = "MYTEC C127" },
                        Games = { new GameMapping { GameName = "ETS2", Driving = "SimHub - Truck Route Advisor" } },
                    },
                },
            }
            : RouterSettingsJson.Deserialize(File.ReadAllText(path));

        settings.Port = port ?? (path is null ? 8890 : settings.Port);
        foreach (var problem in RouterSettingsValidator.Validate(settings))
        {
            Log("Settings problem: " + problem);
        }

        return settings;
    }

    private static async Task ObserveLoopAsync(RouterHost host, CancellationToken quit)
    {
        // Like SimHub's DataUpdate: feed the current flags continuously so debounce delays elapse.
        while (!quit.IsCancellationRequested)
        {
            GameSignals signals;
            lock (Gate)
            {
                signals = _signals;
            }

            if (host.Observe(signals))
            {
                var state = host.Router.State;
                Log($"State: {state.State}{(state.GameName is null ? "" : " (" + state.GameName + ")")}");
            }

            try
            {
                await Task.Delay(100, quit);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static void ConsoleLoop(RouterHost host, string? settingsPath, int? port, CancellationTokenSource quit)
    {
        string? line;
        while ((line = Console.ReadLine()) is not null)
        {
            Run(line, host, settingsPath, port, quit);
        }
    }

    private static async Task CommandFileLoopAsync(string path, RouterHost host, string? settingsPath, int? port, CancellationTokenSource quit)
    {
        File.WriteAllText(path, "");
        while (!quit.IsCancellationRequested)
        {
            try
            {
                var text = File.ReadAllText(path);
                if (text.Trim().Length > 0)
                {
                    File.WriteAllText(path, "");
                    foreach (var line in text.Split('\n'))
                    {
                        Run(line, host, settingsPath, port, quit);
                    }
                }

                await Task.Delay(200, quit.Token);
            }
            catch (IOException)
            {
                // The writer still has the file open; try again on the next pass.
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static void Run(string line, RouterHost host, string? settingsPath, int? port, CancellationTokenSource quit)
    {
        var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return;
        }

        var game = parts.Length > 1 ? parts[1] : null;
        switch (parts[0].ToLowerInvariant())
        {
            case "idle":
                SetSignals(new GameSignals(null, false, false, false));
                break;
            case "menu":
                SetSignals(new GameSignals(game, true, true, true));
                break;
            case "drive":
                SetSignals(new GameSignals(game, true, true, false));
                break;
            case "reload":
                host.Configure(LoadSettings(settingsPath, port));
                Log("Settings reloaded.");
                break;
            case "quit":
                quit.Cancel();
                break;
            default:
                Log($"Unknown command '{line.Trim()}'.");
                Console.WriteLine(Usage);
                break;
        }
    }

    private static void SetSignals(GameSignals signals)
    {
        lock (Gate)
        {
            _signals = signals;
        }

        Log($"Signals: {signals}");
    }
}
