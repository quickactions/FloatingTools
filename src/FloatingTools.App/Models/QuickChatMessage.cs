namespace FloatingTools.App.Models;

public sealed class QuickChatMessage
{
    private List<QuickChatAttachment> _attachments = [];

    public Guid Id { get; set; }

    public QuickChatMessageRole Role { get; set; }

    public string? Text { get; set; }

    public List<QuickChatAttachment> Attachments
    {
        get => _attachments;
        set => _attachments = value ?? [];
    }

    public DateTimeOffset CreatedAt { get; set; }

    public QuickChatMessageStatus? Status { get; set; }

    public string? ErrorMessage { get; set; }
}

public enum QuickChatMessageRole
{
    User,
    Assistant
}

public enum QuickChatMessageStatus
{
    InProgress,
    Completed,
    Interrupted,
    Error
}
