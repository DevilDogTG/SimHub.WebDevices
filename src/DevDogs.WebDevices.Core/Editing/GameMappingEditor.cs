using System;
using DevDogs.WebDevices.Core.Config;

namespace DevDogs.WebDevices.Core.Editing;

/// <summary>
/// One game row of a device in the settings tab.
/// </summary>
public sealed class GameMappingEditor : ObservableObject
{
    private readonly Action _changed;
    private string? _gameIdle;
    private string? _driving;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameMappingEditor"/> class.
    /// </summary>
    /// <param name="gameCode">SimHub's game code.</param>
    /// <param name="displayName">The name shown for the game.</param>
    /// <param name="changed">Called after any setting changes.</param>
    public GameMappingEditor(string gameCode, string displayName, Action changed)
    {
        GameCode = gameCode;
        DisplayName = displayName;
        _changed = changed;
    }

    /// <summary>Gets SimHub's game code.</summary>
    public string GameCode { get; }

    /// <summary>Gets the name shown for the game.</summary>
    public string DisplayName { get; }

    /// <summary>Gets or sets the dashboard shown in the game's menus.</summary>
    public string? GameIdle
    {
        get => _gameIdle;
        set
        {
            if (SetProperty(ref _gameIdle, value))
            {
                _changed();
            }
        }
    }

    /// <summary>Gets or sets the dashboard shown while driving.</summary>
    public string? Driving
    {
        get => _driving;
        set
        {
            if (SetProperty(ref _driving, value))
            {
                _changed();
            }
        }
    }

    /// <summary>
    /// Returns the row as settings.
    /// </summary>
    /// <returns>The mapping.</returns>
    public GameMapping ToMapping() => new()
    {
        GameName = GameCode,
        GameIdle = SettingsEditor.Blank(GameIdle),
        Driving = SettingsEditor.Blank(Driving),
    };
}
