using System;
using System.Collections.Generic;

namespace DevDogs.WebDevices.Core.Config;

/// <summary>
/// Checks settings before they are saved or imported.
/// </summary>
public static class RouterSettingsValidator
{
    private const int MinPort = 1024;
    private const int MaxPort = 65535;
    private const int MaxDelayMs = 60_000;

    /// <summary>
    /// Returns every problem found in <paramref name="settings"/>; an empty list means the settings are valid.
    /// </summary>
    /// <param name="settings">The settings to check.</param>
    /// <returns>One human-readable message per problem.</returns>
    public static IReadOnlyList<string> Validate(RouterSettings settings)
    {
        var errors = new List<string>();

        if (settings.Port is < MinPort or > MaxPort)
        {
            errors.Add($"Port {settings.Port} must be between {MinPort} and {MaxPort}.");
        }
        else if (settings.Port == settings.SimHubWebPort)
        {
            errors.Add($"Port {settings.Port} is SimHub's own web port; choose another.");
        }

        if (settings.SimHubWebPort is < 1 or > MaxPort)
        {
            errors.Add($"SimHub web port {settings.SimHubWebPort} must be between 1 and {MaxPort}.");
        }

        CheckDelay(errors, "System idle delay", settings.SystemIdleDelayMs);
        CheckDelay(errors, "Switch delay", settings.SwitchDelayMs);

        var deviceNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var device in settings.Devices)
        {
            if (!DeviceNameRules.IsValid(device.Name))
            {
                errors.Add($"Device name '{device.Name}' is not allowed: {DeviceNameRules.Requirement}.");
            }
            else if (!deviceNames.Add(device.Name))
            {
                errors.Add($"Device name '{device.Name}' is used more than once.");
            }

            CheckGames(errors, device);
        }

        return errors;
    }

    private static void CheckDelay(List<string> errors, string label, int delayMs)
    {
        if (delayMs is < 0 or > MaxDelayMs)
        {
            errors.Add($"{label} {delayMs} ms must be between 0 and {MaxDelayMs} ms.");
        }
    }

    private static void CheckGames(List<string> errors, DeviceConfig device)
    {
        var gameNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in device.Games)
        {
            if (string.IsNullOrWhiteSpace(game.GameName))
            {
                errors.Add($"Device '{device.Name}': a game mapping has no game name.");
            }
            else if (!gameNames.Add(game.GameName))
            {
                errors.Add($"Device '{device.Name}': game '{game.GameName}' is mapped more than once.");
            }
        }
    }
}
