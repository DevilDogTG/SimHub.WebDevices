namespace DevDogs.WebDevices.Core.Server;

/// <summary>
/// Whether the device server is listening, for the log and the settings tab.
/// </summary>
/// <param name="Listening">Whether devices can connect.</param>
/// <param name="Port">The port it listens on, or tried to.</param>
/// <param name="Message">A human-readable explanation, including why it is not listening.</param>
public sealed record ServerStatus(bool Listening, int Port, string Message);
