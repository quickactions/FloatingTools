namespace FloatingTools.App.Models;

public sealed class QuickChatConversationState
{
    public const int CurrentSchemaVersion = 1;

    private List<QuickChatMessage> _messages = [];

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<QuickChatMessage> Messages
    {
        get => _messages;
        set => _messages = value ?? [];
    }

    public string? AdditionalInstructions { get; set; }
}
