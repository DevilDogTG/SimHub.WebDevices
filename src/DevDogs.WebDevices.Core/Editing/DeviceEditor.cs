using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using DevDogs.WebDevices.Core.Config;

namespace DevDogs.WebDevices.Core.Editing;

/// <summary>
/// One device in the settings tab: its settings plus live connection status.
/// </summary>
public sealed class DeviceEditor : ObservableObject
{
    private readonly Action _changed;
    private readonly Func<string> _urlBase;
    private string _name = "";
    private bool _hideControls;
    private string? _systemIdleDashboard;
    private string? _defaultGameIdle;
    private string? _defaultDriving;
    private bool _isConnected;
    private string _statusText = "";
    private string _showingText = "";

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceEditor"/> class.
    /// </summary>
    /// <param name="changed">Called after any setting changes (not after status updates).</param>
    /// <param name="urlBase">Returns the device URL prefix, such as <c>http://192.168.1.10:8889/</c>.</param>
    public DeviceEditor(Action changed, Func<string> urlBase)
    {
        _changed = changed;
        _urlBase = urlBase;
        Games.CollectionChanged += OnGamesChanged;
    }

    /// <summary>Gets or sets the device name used in its URL.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value ?? ""))
            {
                OnPropertyChanged(nameof(Url));
                _changed();
            }
        }
    }

    /// <summary>Gets the URL to open on the device.</summary>
    public string Url => _urlBase() + Name + "/";

    /// <summary>Gets or sets a value indicating whether SimHub's on-screen dashboard controls are hidden.</summary>
    public bool HideControls
    {
        get => _hideControls;
        set => Set(ref _hideControls, value);
    }

    /// <summary>Gets or sets the dashboard shown when no game is running.</summary>
    public string? SystemIdleDashboard
    {
        get => _systemIdleDashboard;
        set => Set(ref _systemIdleDashboard, value);
    }

    /// <summary>Gets or sets the menu dashboard for games without their own row.</summary>
    public string? DefaultGameIdle
    {
        get => _defaultGameIdle;
        set => Set(ref _defaultGameIdle, value);
    }

    /// <summary>Gets or sets the driving dashboard for games without their own row.</summary>
    public string? DefaultDriving
    {
        get => _defaultDriving;
        set => Set(ref _defaultDriving, value);
    }

    /// <summary>Gets the per-game rows.</summary>
    public ObservableCollection<GameMappingEditor> Games { get; } = [];

    /// <summary>Gets a value indicating whether the device has an open connection.</summary>
    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }

    /// <summary>Gets the connection status line.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>Gets what the device is currently showing.</summary>
    public string ShowingText
    {
        get => _showingText;
        private set => SetProperty(ref _showingText, value);
    }

    /// <summary>
    /// Returns the device as settings.
    /// </summary>
    /// <returns>The device settings.</returns>
    public DeviceConfig ToConfig() => new()
    {
        Name = Name,
        HideControls = HideControls,
        SystemIdleDashboard = SettingsEditor.Blank(SystemIdleDashboard),
        Default = new GameDashboards { GameIdle = SettingsEditor.Blank(DefaultGameIdle), Driving = SettingsEditor.Blank(DefaultDriving) },
        Games = Games.Select(g => g.ToMapping()).ToList(),
    };

    /// <summary>Re-reads <see cref="Url"/> after the port or address changed.</summary>
    internal void RefreshUrl() => OnPropertyChanged(nameof(Url));

    /// <summary>Updates the live status shown for the device.</summary>
    /// <param name="isConnected">Whether a stream is open.</param>
    /// <param name="statusText">The connection status line.</param>
    /// <param name="showingText">What the device shows.</param>
    internal void SetStatus(bool isConnected, string statusText, string showingText)
    {
        IsConnected = isConnected;
        StatusText = statusText;
        ShowingText = showingText;
    }

    private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            _changed();
        }
    }

    private void OnGamesChanged(object? sender, NotifyCollectionChangedEventArgs e) => _changed();
}
