using CommunityToolkit.Mvvm.ComponentModel;
using FloatingTools.App.Models;
using FloatingTools.App.SharedUi.Direction;
using System.Text;
using System.Windows;

namespace FloatingTools.App.ViewModels;

public sealed record QuickChatAttachmentPresentation(
    Guid Id,
    QuickChatAttachmentType Type,
    string AssetFileName,
    string MediaType,
    double Width,
    double Height,
    string RuntimePath);

public sealed record QuickChatParagraphPresentation(
    string Text,
    FlowDirection FlowDirection,
    TextAlignment TextAlignment);

public sealed class QuickChatPendingAttachmentViewModel(
    QuickChatAttachment attachment,
    string runtimePath = "")
{
    public QuickChatAttachment Attachment { get; } = attachment
        ?? throw new ArgumentNullException(nameof(attachment));

    public Guid Id => Attachment.Id;

    public string AssetFileName => Attachment.AssetFileName;

    public string MediaType => Attachment.MediaType;

    public double Width => Attachment.Width;

    public double Height => Attachment.Height;

    public string RuntimePath { get; } = runtimePath;
}

public sealed class QuickChatMessageViewModel : ObservableObject
{
    private readonly Func<string, string> _resolveRuntimePath;
    private string? _text;
    private QuickChatMessageStatus? _status;
    private string? _errorMessage;
    private IReadOnlyList<QuickChatAttachmentPresentation> _attachments = [];
    private IReadOnlyList<QuickChatParagraphPresentation> _paragraphs = [];

    public QuickChatMessageViewModel(
        QuickChatMessage message,
        Func<string, string>? resolveRuntimePath = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        Id = message.Id;
        Role = message.Role;
        _resolveRuntimePath = resolveRuntimePath ?? (assetFileName => assetFileName);
        UpdateFrom(message);
    }

    public Guid Id { get; }

    public QuickChatMessageRole Role { get; }

    public bool IsUser => Role == QuickChatMessageRole.User;

    public bool IsAssistant => Role == QuickChatMessageRole.Assistant;

    public string? Text
    {
        get => _text;
        private set
        {
            if (SetProperty(ref _text, value))
            {
                OnPropertyChanged(nameof(HasText));
                OnPropertyChanged(nameof(CanCopy));
            }
        }
    }

    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    public bool CanCopy => IsAssistant && HasText;

    public IReadOnlyList<QuickChatParagraphPresentation> Paragraphs
    {
        get => _paragraphs;
        private set => SetProperty(ref _paragraphs, value);
    }

    public QuickChatMessageStatus? Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(CanRetry));
                OnPropertyChanged(nameof(IsCompleted));
                OnPropertyChanged(nameof(IsInProgress));
                OnPropertyChanged(nameof(IsInterrupted));
                OnPropertyChanged(nameof(IsError));
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public IReadOnlyList<QuickChatAttachmentPresentation> Attachments
    {
        get => _attachments;
        private set
        {
            if (SetProperty(ref _attachments, value))
            {
                OnPropertyChanged(nameof(HasAttachments));
            }
        }
    }

    public bool HasAttachments => Attachments.Count > 0;

    public bool IsCompleted => Status == QuickChatMessageStatus.Completed;

    public bool IsInProgress => Status == QuickChatMessageStatus.InProgress;

    public bool IsInterrupted => Status == QuickChatMessageStatus.Interrupted;

    public bool IsError => Status == QuickChatMessageStatus.Error;

    public bool CanRetry => IsAssistant && IsError;

    public void UpdateFrom(QuickChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Id != Id || message.Role != Role)
        {
            throw new ArgumentException(
                "A presentation message can only update from the same domain message.",
                nameof(message));
        }

        Text = message.Text;
        var paragraphs = CreateLogicalParagraphs(message.Text);
        if (!Paragraphs.SequenceEqual(paragraphs))
        {
            Paragraphs = paragraphs;
        }

        Status = message.Status;
        ErrorMessage = message.ErrorMessage;
        var attachments = message.Attachments
            .Select(attachment => new QuickChatAttachmentPresentation(
                attachment.Id,
                attachment.Type,
                attachment.AssetFileName,
                attachment.MediaType,
                attachment.Width,
                attachment.Height,
                _resolveRuntimePath(attachment.AssetFileName)))
            .ToArray();
        if (!Attachments.SequenceEqual(attachments))
        {
            Attachments = attachments;
        }
    }

    private static IReadOnlyList<QuickChatParagraphPresentation> CreateLogicalParagraphs(
        string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        // Adjacent non-empty source lines remain one logical WPF text flow. Empty
        // source lines delimit paragraphs and remain explicit spacing units.
        var normalized = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return SplitLogicalParagraphs(normalized)
            .Select(paragraph =>
            {
                var resolution = TextDirectionResolver.Resolve(paragraph);
                return new QuickChatParagraphPresentation(
                    NormalizePresentationMarkdown(paragraph),
                    resolution.ToFlowDirection(),
                    resolution.ToPhysicalTextAlignment());
            })
            .ToArray();
    }

    private static IReadOnlyList<string> SplitLogicalParagraphs(string text)
    {
        var paragraphs = new List<string>();
        var current = new StringBuilder();
        foreach (var line in text.Split('\n'))
        {
            if (line.Length == 0)
            {
                FlushCurrentParagraph();
                paragraphs.Add(string.Empty);
                continue;
            }

            if (current.Length > 0)
            {
                current.Append('\n');
            }

            current.Append(line);
        }

        FlushCurrentParagraph();
        return paragraphs;

        void FlushCurrentParagraph()
        {
            if (current.Length == 0)
            {
                return;
            }

            paragraphs.Add(current.ToString());
            current.Clear();
        }
    }

    private static string NormalizePresentationMarkdown(string paragraph)
    {
        if (paragraph.Length == 0)
        {
            return paragraph;
        }

        var lines = paragraph.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = NormalizeSimpleBullet(lines[index]);
            line = RemovePairedDelimiter(line, "**");
            line = RemovePairedDelimiter(line, "`");
            lines[index] = RemovePairedDelimiter(line, "*");
        }

        return string.Join('\n', lines);
    }

    private static string NormalizeSimpleBullet(string line)
    {
        var contentIndex = 0;
        while (contentIndex < line.Length && char.IsWhiteSpace(line[contentIndex]))
        {
            contentIndex++;
        }

        return contentIndex + 1 < line.Length
            && line[contentIndex] is '-' or '*'
            && line[contentIndex + 1] == ' '
                ? string.Concat(line.AsSpan(0, contentIndex), "• ", line.AsSpan(contentIndex + 2))
                : line;
    }

    private static string RemovePairedDelimiter(string text, string delimiter)
    {
        var searchFrom = 0;
        while (true)
        {
            var opening = text.IndexOf(delimiter, searchFrom, StringComparison.Ordinal);
            if (opening < 0)
            {
                return text;
            }

            var closing = text.IndexOf(
                delimiter,
                opening + delimiter.Length,
                StringComparison.Ordinal);
            if (closing < 0)
            {
                return text;
            }

            text = text.Remove(closing, delimiter.Length)
                .Remove(opening, delimiter.Length);
            searchFrom = opening;
        }
    }
}
