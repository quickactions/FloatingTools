namespace FloatingTools.App.Models;

public sealed class QuickChatAttachment
{
    public Guid Id { get; set; }

    public QuickChatAttachmentType Type { get; set; } = QuickChatAttachmentType.Image;

    public string AssetFileName { get; set; } = string.Empty;

    public string MediaType { get; set; } = string.Empty;

    public double Width { get; set; }

    public double Height { get; set; }
}

public enum QuickChatAttachmentType
{
    Image
}
