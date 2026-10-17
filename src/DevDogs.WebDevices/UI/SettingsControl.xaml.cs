using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DevDogs.WebDevices.Core.Editing;
using Microsoft.Win32;

namespace DevDogs.WebDevices.UI;

/// <summary>
/// The "Web Devices" settings tab. All logic lives in <see cref="SettingsEditor"/>; this only adds what
/// needs WPF: file dialogs, the clipboard and the live-status timer.
/// </summary>
public partial class SettingsControl : UserControl
{
    private readonly SettingsEditor _editor;
    private readonly DispatcherTimer _statusTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsControl"/> class.
    /// </summary>
    /// <param name="editor">The view model.</param>
    /// <param name="refreshStatus">Refreshes the editor's live status; called every second while visible.</param>
    internal SettingsControl(SettingsEditor editor, Action<SettingsEditor> refreshStatus)
    {
        _editor = editor;
        DataContext = editor;
        InitializeComponent();

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += (_, _) => refreshStatus(_editor);
        Loaded += (_, _) =>
        {
            refreshStatus(_editor);
            _statusTimer.Start();
        };
        Unloaded += (_, _) => _statusTimer.Stop();
    }

    private void CopyUrl_Click(object sender, RoutedEventArgs e)
    {
        if (_editor.SelectedDevice is { } device)
        {
            Clipboard.SetText(device.Url);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            FileName = "WebDevices.json",
            Filter = "JSON files (*.json)|*.json",
        };
        if (dialog.ShowDialog() == true)
        {
            File.WriteAllText(dialog.FileName, _editor.Export());
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var problem = _editor.Import(File.ReadAllText(dialog.FileName));
        if (problem is not null)
        {
            MessageBox.Show(problem, "Import failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
