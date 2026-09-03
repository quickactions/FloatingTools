namespace FloatingTools.App.Models;

public sealed class AiToolSettings
{
    public bool UseAppCredentials { get; set; } = true;

    public string? Provider { get; set; }

    public string? Model { get; set; }

    public bool IsModelSelectionInitialized { get; set; }
}
