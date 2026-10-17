using System;
using System.Collections.Generic;
using System.IO;
using DevDogs.WebDevices.Core.Editing;
using FluentAssertions;
using Xunit;

namespace DevDogs.WebDevices.Core.Tests.Editing;

public class EditingBasicsTests
{
    private sealed class Sample : ObservableObject
    {
        private string _name = "";

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
    }

    [Fact]
    public void SetProperty_raises_property_changed_only_for_a_new_value()
    {
        var sample = new Sample();
        var raised = new List<string?>();
        sample.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sample.Name = "ipad";
        sample.Name = "ipad";

        raised.Should().Equal("Name");
    }

    [Fact]
    public void RelayCommand_runs_its_action_when_allowed()
    {
        var runs = 0;
        var allowed = false;
        var command = new RelayCommand(() => runs++, () => allowed);

        command.CanExecute(null).Should().BeFalse();
        allowed = true;
        command.CanExecute(null).Should().BeTrue();
        command.Execute(null);

        runs.Should().Be(1);
    }

    [Fact]
    public void RelayCommand_announces_can_execute_changes()
    {
        var command = new RelayCommand(() => { });
        var raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        command.RaiseCanExecuteChanged();

        raised.Should().Be(1);
    }

    [Fact]
    public void RelayCommand_of_T_passes_its_parameter()
    {
        string? received = null;
        var command = new RelayCommand<string>(value => received = value);

        command.Execute("ETS2");

        received.Should().Be("ETS2");
    }
}

public sealed class DashboardCatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "webdevices-dash-" + Guid.NewGuid().ToString("N"));

    public DashboardCatalogTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Dashboard(string folder, string? file = null)
    {
        var dir = Directory.CreateDirectory(Path.Combine(_root, folder));
        File.WriteAllText(Path.Combine(dir.FullName, (file ?? folder) + ".djson"), "{}");
    }

    [Fact]
    public void Lists_folders_that_hold_a_dashboard_of_the_same_name_sorted_ignoring_case()
    {
        Dashboard("MYTEC C127 - System Info");
        Dashboard("clock");
        Dashboard("Ressources", file: "something-else");
        Directory.CreateDirectory(Path.Combine(_root, "_Library"));
        File.WriteAllText(Path.Combine(_root, "loose.djson"), "{}");

        DashboardCatalog.Scan(_root).Should().Equal("clock", "MYTEC C127 - System Info");
    }

    [Fact]
    public void A_missing_folder_gives_an_empty_list()
    {
        DashboardCatalog.Scan(Path.Combine(_root, "nope")).Should().BeEmpty();
    }
}
