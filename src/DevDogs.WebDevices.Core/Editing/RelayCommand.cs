using System;
using System.Windows.Input;

namespace DevDogs.WebDevices.Core.Editing;

/// <summary>
/// A command that runs a delegate, for buttons in the settings tab.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelayCommand"/> class.
    /// </summary>
    /// <param name="execute">What the command does.</param>
    /// <param name="canExecute">Whether it can run now; always when omitted.</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <inheritdoc/>
    public void Execute(object? parameter) => _execute();

    /// <summary>Tells bound buttons to re-check <see cref="CanExecute"/>.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// A command that runs a delegate with the bound parameter, for buttons inside list rows.
/// </summary>
/// <typeparam name="T">The parameter type.</typeparam>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelayCommand{T}"/> class.
    /// </summary>
    /// <param name="execute">What the command does with its parameter.</param>
    public RelayCommand(Action<T> execute)
    {
        _execute = execute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => parameter is T;

    /// <inheritdoc/>
    public void Execute(object? parameter)
    {
        if (parameter is T value)
        {
            _execute(value);
        }
    }
}
