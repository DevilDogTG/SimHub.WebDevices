using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using DevDogs.WebDevices.Core.Config;
using DevDogs.WebDevices.Core.Devices;
using DevDogs.WebDevices.Core.Routing;
using DevDogs.WebDevices.Core.Server;
using DevDogs.WebDevices.Core.State;

namespace DevDogs.WebDevices.Core.Editing;

/// <summary>
/// The settings tab's view model: edits a copy of the settings, validates it, and hands it over on Save.
/// The devices and the general settings are saved and reverted separately, each against the other's
/// saved values. Live status is refreshed separately and never counts as an edit.
/// </summary>
public sealed class SettingsEditor : ObservableObject
{
    private readonly Func<string?> _currentGame;
    private readonly Func<string> _hostAddress;
    private readonly Action<RouterSettings> _save;
    private readonly SimHubPorts? _simHubPorts;
    private readonly Dictionary<string, string> _gameNames;
    private readonly HashSet<string> _dashboardSet;
    private RouterSettings _saved;
    private string _savedJson;
    private bool _loading;
    private DeviceEditor? _selectedDevice;
    private GameOption? _selectedGameToAdd;
    private int _port;
    private int _simHubWebPort;
    private int _systemIdleDelayMs;
    private int _switchDelayMs;
    private bool _isDevicesDirty;
    private bool _isGeneralDirty;
    private IReadOnlyList<string> _deviceErrors = [];
    private IReadOnlyList<string> _generalErrors = [];
    private IReadOnlyList<string> _warnings = [];
    private string _serverText = "";
    private string _stateText = "";

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsEditor"/> class.
    /// </summary>
    /// <param name="saved">The settings currently in effect.</param>
    /// <param name="dashboards">The installed dashboard codes.</param>
    /// <param name="games">The games SimHub knows.</param>
    /// <param name="currentGame">Returns the code of SimHub's current game, if any.</param>
    /// <param name="hostAddress">Returns the PC address devices use to reach SimHub.</param>
    /// <param name="save">Persists and applies settings.</param>
    /// <param name="simHubPorts">SimHub's own web ports, which the device port must not use; unchecked when omitted.</param>
    public SettingsEditor(
        RouterSettings saved,
        IReadOnlyList<string> dashboards,
        IReadOnlyList<GameOption> games,
        Func<string?> currentGame,
        Func<string> hostAddress,
        Action<RouterSettings> save,
        SimHubPorts? simHubPorts = null)
    {
        Dashboards = dashboards;
        Games = games;
        _currentGame = currentGame;
        _hostAddress = hostAddress;
        _save = save;
        _simHubPorts = simHubPorts;
        _dashboardSet = new HashSet<string>(dashboards, StringComparer.OrdinalIgnoreCase);
        _gameNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in games)
        {
            _gameNames[game.Code] = game.Name;
        }

        _saved = Copy(saved);
        _savedJson = RouterSettingsJson.Serialize(_saved);
        AddDeviceCommand = new RelayCommand(AddDevice);
        RemoveDeviceCommand = new RelayCommand(RemoveDevice, () => SelectedDevice is not null);
        AddGameCommand = new RelayCommand(() => AddGame(SelectedGameToAdd?.Code), () => SelectedDevice is not null && SelectedGameToAdd is not null);
        AddCurrentGameCommand = new RelayCommand(() => AddGame(_currentGame()), () => SelectedDevice is not null && _currentGame() is not null);
        RemoveGameCommand = new RelayCommand<GameMappingEditor>(row => SelectedDevice?.Games.Remove(row));
        SaveDevicesCommand = new RelayCommand(() => Save(DevicesToSave()), () => IsDevicesDirty && DeviceErrors.Count == 0);
        RevertDevicesCommand = new RelayCommand(() => Load(() => LoadDevices(_saved)), () => IsDevicesDirty);
        SaveGeneralCommand = new RelayCommand(() => Save(GeneralToSave()), () => IsGeneralDirty && GeneralErrors.Count == 0);
        RevertGeneralCommand = new RelayCommand(() => Load(() => LoadGeneral(_saved)), () => IsGeneralDirty);

        Devices.CollectionChanged += OnDevicesChanged;
        Load(() => LoadAll(saved));
    }

    /// <summary>Gets the installed dashboard codes, for the pickers.</summary>
    public IReadOnlyList<string> Dashboards { get; }

    /// <summary>Gets the games SimHub knows, for the game picker.</summary>
    public IReadOnlyList<GameOption> Games { get; }

    /// <summary>Gets the devices being edited.</summary>
    public ObservableCollection<DeviceEditor> Devices { get; } = [];

    /// <summary>Gets or sets the device shown in the editor.</summary>
    public DeviceEditor? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                RefreshCommands();
            }
        }
    }

    /// <summary>Gets or sets the game picked to add to the selected device.</summary>
    public GameOption? SelectedGameToAdd
    {
        get => _selectedGameToAdd;
        set
        {
            if (SetProperty(ref _selectedGameToAdd, value))
            {
                RefreshCommands();
            }
        }
    }

    /// <summary>Gets or sets the port devices connect to.</summary>
    public int Port
    {
        get => _port;
        set
        {
            if (SetProperty(ref _port, value))
            {
                foreach (var device in Devices)
                {
                    device.RefreshUrl();
                }

                Changed();
            }
        }
    }

    /// <summary>Gets or sets SimHub's own web server port.</summary>
    public int SimHubWebPort
    {
        get => _simHubWebPort;
        set => Set(ref _simHubWebPort, value);
    }

    /// <summary>Gets or sets how long, in milliseconds, the game must be gone before switching to system idle.</summary>
    public int SystemIdleDelayMs
    {
        get => _systemIdleDelayMs;
        set => Set(ref _systemIdleDelayMs, value);
    }

    /// <summary>Gets or sets how long, in milliseconds, any other new state must hold before switching.</summary>
    public int SwitchDelayMs
    {
        get => _switchDelayMs;
        set => Set(ref _switchDelayMs, value);
    }

    /// <summary>Gets a value indicating whether the devices have unsaved changes.</summary>
    public bool IsDevicesDirty
    {
        get => _isDevicesDirty;
        private set => SetProperty(ref _isDevicesDirty, value);
    }

    /// <summary>Gets a value indicating whether the general settings have unsaved changes.</summary>
    public bool IsGeneralDirty
    {
        get => _isGeneralDirty;
        private set => SetProperty(ref _isGeneralDirty, value);
    }

    /// <summary>Gets the problems that block saving the devices.</summary>
    public IReadOnlyList<string> DeviceErrors
    {
        get => _deviceErrors;
        private set => SetProperty(ref _deviceErrors, value);
    }

    /// <summary>Gets the problems that block saving the general settings.</summary>
    public IReadOnlyList<string> GeneralErrors
    {
        get => _generalErrors;
        private set => SetProperty(ref _generalErrors, value);
    }

    /// <summary>Gets device problems that do not block saving, such as dashboards that are not installed.</summary>
    public IReadOnlyList<string> Warnings
    {
        get => _warnings;
        private set => SetProperty(ref _warnings, value);
    }

    /// <summary>Gets the server status line.</summary>
    public string ServerText
    {
        get => _serverText;
        private set => SetProperty(ref _serverText, value);
    }

    /// <summary>Gets the current game state line.</summary>
    public string StateText
    {
        get => _stateText;
        private set => SetProperty(ref _stateText, value);
    }

    /// <summary>Gets the command that adds a device.</summary>
    public RelayCommand AddDeviceCommand { get; }

    /// <summary>Gets the command that removes the selected device.</summary>
    public RelayCommand RemoveDeviceCommand { get; }

    /// <summary>Gets the command that adds <see cref="SelectedGameToAdd"/> to the selected device.</summary>
    public RelayCommand AddGameCommand { get; }

    /// <summary>Gets the command that adds SimHub's current game to the selected device.</summary>
    public RelayCommand AddCurrentGameCommand { get; }

    /// <summary>Gets the command that removes a game row (the row is the parameter).</summary>
    public RelayCommand<GameMappingEditor> RemoveGameCommand { get; }

    /// <summary>Gets the command that saves and applies the devices, keeping the saved general settings.</summary>
    public RelayCommand SaveDevicesCommand { get; }

    /// <summary>Gets the command that discards unsaved device changes.</summary>
    public RelayCommand RevertDevicesCommand { get; }

    /// <summary>Gets the command that saves and applies the general settings, keeping the saved devices.</summary>
    public RelayCommand SaveGeneralCommand { get; }

    /// <summary>Gets the command that discards unsaved general settings changes.</summary>
    public RelayCommand RevertGeneralCommand { get; }

    /// <summary>
    /// Returns the edited settings.
    /// </summary>
    /// <returns>The settings.</returns>
    public RouterSettings ToSettings() => WithDevices(EditedDevices());

    /// <summary>
    /// Returns the edited settings as export JSON.
    /// </summary>
    /// <returns>The JSON text.</returns>
    public string Export() => RouterSettingsJson.Serialize(ToSettings());

    /// <summary>
    /// Loads settings from export JSON as unsaved changes on both tabs.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <returns><see langword="null"/> on success, otherwise why the text could not be read.</returns>
    public string? Import(string json)
    {
        try
        {
            var settings = RouterSettingsJson.Deserialize(json);
            Load(() => LoadAll(settings));
            return null;
        }
        catch (FormatException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Updates the live status lines. Never marks the editor dirty.
    /// </summary>
    /// <param name="state">The routed state.</param>
    /// <param name="server">The server status.</param>
    /// <param name="seen">The devices the server has seen.</param>
    /// <param name="target">Returns what a saved device shows, or <see langword="null"/> for an unsaved one.</param>
    /// <param name="nowUtc">The current time.</param>
    public void RefreshStatus(
        RouterState state,
        ServerStatus server,
        IReadOnlyList<DeviceStatus> seen,
        Func<string, DeviceTarget?> target,
        DateTime nowUtc)
    {
        ServerText = server.Message;
        StateText = state.State switch
        {
            GameState.SystemIdle => "No game running",
            GameState.GameIdle => WithGame("Game idle", state.GameName),
            _ => WithGame("Driving", state.GameName),
        };

        foreach (var device in Devices)
        {
            var status = seen.FirstOrDefault(s => string.Equals(s.Name, device.Name, StringComparison.OrdinalIgnoreCase));
            var statusText = status switch
            {
                null => "Never connected",
                { Connections: > 0 } => "Connected from " + status.LastAddress,
                _ => "Last seen " + Ago(nowUtc - status.LastSeenUtc),
            };

            var showing = target(device.Name) switch
            {
                null => "Not saved yet",
                { Dashboard: null } => "No dashboard for this state",
                var t => "Showing " + t.Dashboard,
            };

            device.SetStatus(status?.Connections > 0, statusText, showing);
        }

        AddCurrentGameCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Returns <see langword="null"/> for a blank value, so empty text boxes save as "not set".</summary>
    /// <param name="value">The edited text.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    internal static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value!.Trim();

    private static string Ago(TimeSpan elapsed) => elapsed.TotalSeconds switch
    {
        < 10 => "just now",
        < 60 => $"{(int)elapsed.TotalSeconds} s ago",
        < 3600 => $"{(int)elapsed.TotalMinutes} min ago",
        < 86400 => $"{(int)elapsed.TotalHours} h ago",
        _ => $"{(int)elapsed.TotalDays} d ago",
    };

    private string GameName(string code) => _gameNames.TryGetValue(code, out var name) ? name : code;

    private string WithGame(string label, string? gameCode) => gameCode is null ? label : label + " · " + GameName(gameCode);

    private string UrlBase() => $"http://{_hostAddress()}:{Port.ToString(CultureInfo.InvariantCulture)}/";

    private static RouterSettings Copy(RouterSettings settings) => RouterSettingsJson.Deserialize(RouterSettingsJson.Serialize(settings));

    private List<DeviceConfig> EditedDevices() => Devices.Select(d => d.ToConfig()).ToList();

    private RouterSettings WithDevices(List<DeviceConfig> devices) => new()
    {
        Port = Port,
        SimHubWebPort = SimHubWebPort,
        SystemIdleDelayMs = SystemIdleDelayMs,
        SwitchDelayMs = SwitchDelayMs,
        Devices = devices,
    };

    /// <summary>Returns what the devices tab saves: the edited devices with the saved general settings.</summary>
    private RouterSettings DevicesToSave() => new()
    {
        Port = _saved.Port,
        SimHubWebPort = _saved.SimHubWebPort,
        SystemIdleDelayMs = _saved.SystemIdleDelayMs,
        SwitchDelayMs = _saved.SwitchDelayMs,
        Devices = EditedDevices(),
    };

    /// <summary>Returns what the general tab saves: the edited general settings with the saved devices.</summary>
    private RouterSettings GeneralToSave() => WithDevices(_saved.Devices);

    /// <summary>Applies loaded values without reacting to each one, then re-checks everything once.</summary>
    private void Load(Action apply)
    {
        _loading = true;
        try
        {
            apply();
        }
        finally
        {
            _loading = false;
        }

        Changed();
    }

    private void LoadAll(RouterSettings settings)
    {
        LoadGeneral(settings);
        LoadDevices(settings);
    }

    private void LoadGeneral(RouterSettings settings)
    {
        Port = settings.Port;
        SimHubWebPort = settings.SimHubWebPort;
        SystemIdleDelayMs = settings.SystemIdleDelayMs;
        SwitchDelayMs = settings.SwitchDelayMs;
    }

    private void LoadDevices(RouterSettings settings)
    {
        var selectedName = SelectedDevice?.Name;
        Devices.Clear();
        foreach (var config in settings.Devices)
        {
            var device = NewDevice();
            device.Name = config.Name;
            device.HideControls = config.HideControls;
            device.SystemIdleDashboard = config.SystemIdleDashboard;
            device.DefaultGameIdle = config.Default.GameIdle;
            device.DefaultDriving = config.Default.Driving;
            foreach (var game in config.Games)
            {
                var row = NewGameRow(game.GameName);
                row.GameIdle = game.GameIdle;
                row.Driving = game.Driving;
                device.Games.Add(row);
            }

            Devices.Add(device);
        }

        SelectedDevice = Devices.FirstOrDefault(d => d.Name == selectedName) ?? Devices.FirstOrDefault();
    }

    private DeviceEditor NewDevice() => new(Changed, UrlBase);

    private GameMappingEditor NewGameRow(string code) => new(code, GameName(code), Changed);

    private void AddDevice()
    {
        var number = 1;
        while (Devices.Any(d => d.Name == $"device-{number}"))
        {
            number++;
        }

        var device = NewDevice();
        device.Name = $"device-{number}";

        // SimHub shows its dashboard controls on every dashboard load, so on a device that switches
        // dashboards they would keep reappearing.
        device.HideControls = true;
        Devices.Add(device);
        SelectedDevice = device;
    }

    private void RemoveDevice()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        var index = Devices.IndexOf(SelectedDevice);
        Devices.RemoveAt(index);
        SelectedDevice = Devices.Count == 0 ? null : Devices[Math.Min(index, Devices.Count - 1)];
    }

    private void AddGame(string? code)
    {
        if (SelectedDevice is null || string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        if (SelectedDevice.Games.Any(g => string.Equals(g.GameCode, code, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        SelectedDevice.Games.Add(NewGameRow(code!));
    }

    private void Save(RouterSettings settings)
    {
        _save(settings);
        _saved = Copy(settings);
        _savedJson = RouterSettingsJson.Serialize(_saved);
        Changed();
    }

    private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            Changed();
        }
    }

    private void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Changed();

    private void Changed()
    {
        if (_loading)
        {
            return;
        }

        var devices = DevicesToSave();
        var general = GeneralToSave();
        DeviceErrors = RouterSettingsValidator.Validate(devices);
        GeneralErrors = RouterSettingsValidator.Validate(general, _simHubPorts);
        Warnings = DashboardWarnings(devices);
        IsDevicesDirty = RouterSettingsJson.Serialize(devices) != _savedJson;
        IsGeneralDirty = RouterSettingsJson.Serialize(general) != _savedJson;
        RefreshCommands();
    }

    private List<string> DashboardWarnings(RouterSettings settings)
    {
        var warnings = new List<string>();
        foreach (var device in settings.Devices)
        {
            var used = new[] { device.SystemIdleDashboard, device.Default.GameIdle, device.Default.Driving }
                .Concat(device.Games.SelectMany(g => new[] { g.GameIdle, g.Driving }));
            foreach (var dashboard in used.Where(d => d is not null && !_dashboardSet.Contains(d)).Distinct())
            {
                warnings.Add($"Device '{device.Name}': dashboard '{dashboard}' is not installed in SimHub.");
            }
        }

        return warnings;
    }

    private void RefreshCommands()
    {
        RemoveDeviceCommand.RaiseCanExecuteChanged();
        AddGameCommand.RaiseCanExecuteChanged();
        AddCurrentGameCommand.RaiseCanExecuteChanged();
        SaveDevicesCommand.RaiseCanExecuteChanged();
        RevertDevicesCommand.RaiseCanExecuteChanged();
        SaveGeneralCommand.RaiseCanExecuteChanged();
        RevertGeneralCommand.RaiseCanExecuteChanged();
    }
}
