using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public sealed class JsonQuickChatStore : IQuickChatStore
{
    public const string StorageFileName = "quickchat.json";

    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _storagePath;

    public JsonQuickChatStore()
        : this(GetDefaultStoragePath())
    {
    }

    public JsonQuickChatStore(string storagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);
        _storagePath = storagePath;
    }

    public string StoragePath => _storagePath;

    public static string GetDefaultStoragePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloatingTools",
            StorageFileName);

    public async Task<QuickChatConversationState> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_storagePath))
            {
                return CreateEmptyState();
            }

            var json = await File.ReadAllTextAsync(
                _storagePath,
                Encoding.UTF8,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return CreateEmptyState();
            }

            var state = JsonSerializer.Deserialize<QuickChatConversationState>(
                json,
                SerializerOptions);
            return NormalizeLoadedState(state);
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException)
        {
            return CreateEmptyState();
        }
    }

    public async Task SaveAsync(
        QuickChatConversationState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        var persistableState = CreatePersistableState(state);
        await _writeLock.WaitAsync(cancellationToken);
        var directory = Path.GetDirectoryName(_storagePath);
        var temporaryPath = _storagePath + ".tmp";

        try
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(persistableState, SerializerOptions);
            await File.WriteAllTextAsync(
                temporaryPath,
                json,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
            File.Move(temporaryPath, _storagePath, overwrite: true);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static QuickChatConversationState NormalizeLoadedState(
        QuickChatConversationState? state)
    {
        if (state is null
            || state.SchemaVersion < 0
            || state.SchemaVersion > QuickChatConversationState.CurrentSchemaVersion)
        {
            return CreateEmptyState();
        }

        state.SchemaVersion = QuickChatConversationState.CurrentSchemaVersion;
        state.Messages = state.Messages.OfType<QuickChatMessage>().ToList();
        foreach (var message in state.Messages)
        {
            message.Attachments = message.Attachments
                .OfType<QuickChatAttachment>()
                .Where(HasManagedAssetReference)
                .ToList();
            NormalizeMessageState(message);
        }

        return state;
    }

    private static QuickChatConversationState CreatePersistableState(
        QuickChatConversationState state) =>
        new()
        {
            SchemaVersion = QuickChatConversationState.CurrentSchemaVersion,
            AdditionalInstructions = state.AdditionalInstructions,
            Messages = state.Messages.Select(CloneForPersistence).ToList()
        };

    private static QuickChatMessage CloneForPersistence(QuickChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var attachments = message.Attachments.Select(CloneForPersistence).ToList();
        var status = message.Role == QuickChatMessageRole.User
            ? null
            : message.Status;

        return new QuickChatMessage
        {
            Id = message.Id,
            Role = message.Role,
            Text = message.Text,
            Attachments = attachments,
            CreatedAt = message.CreatedAt,
            Status = status,
            ErrorMessage = status == QuickChatMessageStatus.Error
                ? message.ErrorMessage
                : null
        };
    }

    private static QuickChatAttachment CloneForPersistence(
        QuickChatAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        if (!HasManagedAssetReference(attachment))
        {
            throw new ArgumentException(
                "Quick Chat attachments require a managed relative asset file name.",
                nameof(attachment));
        }

        return new QuickChatAttachment
        {
            Id = attachment.Id,
            Type = attachment.Type,
            AssetFileName = attachment.AssetFileName,
            MediaType = attachment.MediaType,
            Width = attachment.Width,
            Height = attachment.Height
        };
    }

    private static void NormalizeMessageState(QuickChatMessage message)
    {
        if (message.Role == QuickChatMessageRole.User)
        {
            message.Status = null;
            message.ErrorMessage = null;
            return;
        }

        if (message.Status is null or QuickChatMessageStatus.InProgress)
        {
            message.Status = QuickChatMessageStatus.Interrupted;
        }

        if (message.Status != QuickChatMessageStatus.Error)
        {
            message.ErrorMessage = null;
        }
    }

    private static bool HasManagedAssetReference(QuickChatAttachment attachment)
    {
        var fileName = attachment.AssetFileName;
        return !string.IsNullOrWhiteSpace(fileName)
            && !Path.IsPathRooted(fileName)
            && string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal)
            && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    private static QuickChatConversationState CreateEmptyState() => new();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false));
        return options;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // The next save can safely replace a stale temporary file.
        }
    }
}
