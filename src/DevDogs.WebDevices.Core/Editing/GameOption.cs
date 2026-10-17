namespace DevDogs.WebDevices.Core.Editing;

/// <summary>
/// A game SimHub knows: the code it reports as <c>GameData.GameName</c> and the name people recognise
/// (custom games report codes like <c>Custom_&lt;guid&gt;</c>).
/// </summary>
/// <param name="Code">SimHub's game code.</param>
/// <param name="Name">The display name.</param>
public sealed record GameOption(string Code, string Name)
{
    /// <inheritdoc/>
    public override string ToString() => Name;
}
