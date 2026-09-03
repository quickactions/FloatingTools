using System.IO;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class JsonQuickChatStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "FloatingTools.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task MissingFile_ReturnsValidEmptyState()
    {
        var state = await CreateStore().LoadAsync();

        Assert.Equal(QuickChatConversationState.CurrentSchemaVersion, state.SchemaVersion);
        Assert.Empty(state.Messages);
        Assert.Null(state.AdditionalInstructions);
    }

    [Fact]
    public async Task EmptyState_RoundTrips()
    {
        var store = CreateStore();

        await store.SaveAsync(new QuickChatConversationState());
        var restored = await store.LoadAsync();

        Assert.Empty(restored.Messages);
        Assert.Null(restored.AdditionalInstructions);
        Assert.False(File.Exists(store.StoragePath + ".tmp"));
    }

    [Fact]
    public async Task UserTextMessage_RoundTripsWithNoGenerationStatus()
    {
        var message = CreateMessage(QuickChatMessageRole.User, "שלום world");

        var restored = await SaveAndLoadSingleAsync(message);

        Assert.Equal(message.Id, restored.Id);
        Assert.Equal(QuickChatMessageRole.User, restored.Role);
        Assert.Equal("שלום world", restored.Text);
        Assert.Null(restored.Status);
        Assert.Null(restored.ErrorMessage);
    }

    [Theory]
    [InlineData(QuickChatMessageStatus.Completed)]
    [InlineData(QuickChatMessageStatus.Interrupted)]
    public async Task AssistantTerminalMessage_RoundTrips(
        QuickChatMessageStatus status)
    {
        var message = CreateMessage(QuickChatMessageRole.Assistant, "response");
        message.Status = status;

        var restored = await SaveAndLoadSingleAsync(message);

        Assert.Equal(status, restored.Status);
        Assert.Equal("response", restored.Text);
        Assert.Null(restored.ErrorMessage);
    }

    [Fact]
    public async Task AssistantErrorMessage_RoundTripsWithErrorText()
    {
        var message = CreateMessage(QuickChatMessageRole.Assistant, "partial");
        message.Status = QuickChatMessageStatus.Error;
        message.ErrorMessage = "Connection failed";

        var restored = await SaveAndLoadSingleAsync(message);

        Assert.Equal(QuickChatMessageStatus.Error, restored.Status);
        Assert.Equal("Connection failed", restored.ErrorMessage);
    }

    [Fact]
    public async Task ImageAttachmentMetadata_RoundTripsWithoutEmbeddedContent()
    {
        var message = CreateMessage(QuickChatMessageRole.User, null);
        var attachment = CreateAttachment();
        message.Attachments.Add(attachment);
        var store = CreateStore();

        await store.SaveAsync(new QuickChatConversationState { Messages = [message] });
        var json = await File.ReadAllTextAsync(store.StoragePath);
        var restored = Assert.Single(
            Assert.Single((await store.LoadAsync()).Messages).Attachments);

        Assert.Equal(attachment.Id, restored.Id);
        Assert.Equal(QuickChatAttachmentType.Image, restored.Type);
        Assert.Equal("managed-image.webp", restored.AssetFileName);
        Assert.Equal("image/webp", restored.MediaType);
        Assert.Equal(1280, restored.Width);
        Assert.Equal(720, restored.Height);
        Assert.DoesNotContain("base64", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:image", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.GetPathRoot(_directory)!, json);
    }

    [Fact]
    public async Task TextAndImageAttachmentMessage_RoundTripsTogether()
    {
        var message = CreateMessage(QuickChatMessageRole.User, "What is shown?");
        message.Attachments.Add(CreateAttachment());

        var restored = await SaveAndLoadSingleAsync(message);

        Assert.Equal("What is shown?", restored.Text);
        Assert.Single(restored.Attachments);
    }

    [Fact]
    public async Task ImageOnlyUserMessage_RoundTrips()
    {
        var message = CreateMessage(QuickChatMessageRole.User, null);
        message.Attachments.Add(CreateAttachment());

        var restored = await SaveAndLoadSingleAsync(message);

        Assert.Null(restored.Text);
        Assert.Single(restored.Attachments);
        Assert.Null(restored.Status);
    }

    [Fact]
    public async Task MissingOrNullAttachments_NormalizeToEmpty()
    {
        Directory.CreateDirectory(_directory);
        var path = GetStoragePath();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var timestamp = DateTimeOffset.Parse("2026-08-31T12:00:00+00:00");
        var json = $$"""
        {
          "schemaVersion": 1,
          "messages": [
            {
              "id": "{{firstId}}",
              "role": "user",
              "text": "missing attachments",
              "createdAt": "{{timestamp:O}}"
            },
            {
              "id": "{{secondId}}",
              "role": "user",
              "text": "null attachments",
              "attachments": null,
              "createdAt": "{{timestamp:O}}"
            }
          ]
        }
        """;
        await File.WriteAllTextAsync(path, json);

        var state = await new JsonQuickChatStore(path).LoadAsync();

        Assert.Equal(2, state.Messages.Count);
        Assert.All(state.Messages, message => Assert.Empty(message.Attachments));
    }

    [Fact]
    public async Task AdditionalInstructions_RoundTripWithoutProductInstructions()
    {
        var store = CreateStore();
        const string instructions = "Always answer in Hebrew.";

        await store.SaveAsync(new QuickChatConversationState
        {
            AdditionalInstructions = instructions
        });
        var json = await File.ReadAllTextAsync(store.StoragePath);
        var restored = await store.LoadAsync();

        Assert.Equal(instructions, restored.AdditionalInstructions);
        Assert.Contains(instructions, json);
        Assert.DoesNotContain("productInstruction", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NullMessagesCollection_NormalizesSafely()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            GetStoragePath(),
            """
            {
              "schemaVersion": 1,
              "messages": null,
              "additionalInstructions": null
            }
            """);

        var restored = await CreateStore().LoadAsync();

        Assert.Empty(restored.Messages);
        Assert.Null(restored.AdditionalInstructions);
    }

    [Theory]
    [InlineData("inProgress")]
    [InlineData(null)]
    public async Task NonTerminalAssistantState_LoadsAsInterrupted(string? status)
    {
        Directory.CreateDirectory(_directory);
        var statusProperty = status is null
            ? string.Empty
            : $",\n    \"status\": \"{status}\"";
        var json = $$"""
        {
          "schemaVersion": 1,
          "messages": [{
            "id": "{{Guid.NewGuid()}}",
            "role": "assistant",
            "text": "partial response",
            "attachments": [],
            "createdAt": "2026-08-31T12:00:00+00:00"{{statusProperty}}
          }]
        }
        """;
        await File.WriteAllTextAsync(GetStoragePath(), json);

        var message = Assert.Single((await CreateStore().LoadAsync()).Messages);

        Assert.Equal(QuickChatMessageStatus.Interrupted, message.Status);
    }

    [Fact]
    public async Task MessageIdsTimestampsAndOrder_ArePreserved()
    {
        var first = CreateMessage(QuickChatMessageRole.User, "first");
        first.CreatedAt = DateTimeOffset.Parse("2026-08-31T09:00:00+03:00");
        var second = CreateMessage(QuickChatMessageRole.Assistant, "second");
        second.CreatedAt = DateTimeOffset.Parse("2026-08-31T09:00:01+03:00");
        second.Status = QuickChatMessageStatus.Completed;
        var store = CreateStore();

        await store.SaveAsync(new QuickChatConversationState
        {
            Messages = [first, second]
        });
        var restored = await store.LoadAsync();

        Assert.Equal([first.Id, second.Id], restored.Messages.Select(item => item.Id));
        Assert.Equal(first.CreatedAt, restored.Messages[0].CreatedAt);
        Assert.Equal(second.CreatedAt, restored.Messages[1].CreatedAt);
    }

    [Fact]
    public async Task Save_WritesOnlyConfiguredQuickChatPath()
    {
        var configuredDirectory = Path.Combine(_directory, "configured");
        var configuredPath = Path.Combine(
            configuredDirectory,
            JsonQuickChatStore.StorageFileName);
        var store = new JsonQuickChatStore(configuredPath);

        await store.SaveAsync(new QuickChatConversationState());

        Assert.Equal(configuredPath, store.StoragePath);
        Assert.True(File.Exists(configuredPath));
        Assert.Equal(
            [JsonQuickChatStore.StorageFileName],
            Directory.GetFiles(configuredDirectory).Select(Path.GetFileName));
        Assert.False(File.Exists(GetStoragePath()));
    }

    [Fact]
    public async Task Save_DoesNotTouchOtherProductPersistenceFiles()
    {
        Directory.CreateDirectory(_directory);
        var sentinels = new[]
        {
            "notes.json",
            "settings.json",
            "translation-history.json"
        };
        foreach (var fileName in sentinels)
        {
            await File.WriteAllTextAsync(Path.Combine(_directory, fileName), fileName);
        }

        await CreateStore().SaveAsync(new QuickChatConversationState());

        foreach (var fileName in sentinels)
        {
            Assert.Equal(
                fileName,
                await File.ReadAllTextAsync(Path.Combine(_directory, fileName)));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ malformed")]
    public async Task EmptyOrMalformedFile_ReturnsEmptyState(string json)
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(GetStoragePath(), json);

        var state = await CreateStore().LoadAsync();

        Assert.Empty(state.Messages);
        Assert.Equal(QuickChatConversationState.CurrentSchemaVersion, state.SchemaVersion);
    }

    [Fact]
    public async Task FutureSchema_ReturnsEmptyCurrentState()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            GetStoragePath(),
            """
            {
              "schemaVersion": 999,
              "messages": [{ "role": "user", "text": "future" }]
            }
            """);

        var state = await CreateStore().LoadAsync();

        Assert.Equal(QuickChatConversationState.CurrentSchemaVersion, state.SchemaVersion);
        Assert.Empty(state.Messages);
    }

    [Fact]
    public async Task OlderSchema_NormalizesToCurrentWithoutMigrationFramework()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            GetStoragePath(),
            """
            {
              "schemaVersion": 0,
              "messages": [],
              "additionalInstructions": "legacy optional instruction"
            }
            """);

        var state = await CreateStore().LoadAsync();

        Assert.Equal(QuickChatConversationState.CurrentSchemaVersion, state.SchemaVersion);
        Assert.Empty(state.Messages);
        Assert.Equal("legacy optional instruction", state.AdditionalInstructions);
    }

    [Fact]
    public async Task Save_RejectsAbsoluteAssetPathsWithoutWritingState()
    {
        var message = CreateMessage(QuickChatMessageRole.User, null);
        message.Attachments.Add(new QuickChatAttachment
        {
            Id = Guid.NewGuid(),
            AssetFileName = Path.Combine(_directory, "image.png"),
            MediaType = "image/png"
        });
        var store = CreateStore();

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(
            new QuickChatConversationState { Messages = [message] }));

        Assert.False(File.Exists(store.StoragePath));
    }

    [Fact]
    public void DefaultPath_IsDedicatedQuickChatFileUnderFloatingTools()
    {
        var path = JsonQuickChatStore.GetDefaultStoragePath();

        Assert.Equal(JsonQuickChatStore.StorageFileName, Path.GetFileName(path));
        Assert.Equal("FloatingTools", Path.GetFileName(Path.GetDirectoryName(path)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private JsonQuickChatStore CreateStore() => new(GetStoragePath());

    private string GetStoragePath() => Path.Combine(
        _directory,
        JsonQuickChatStore.StorageFileName);

    private async Task<QuickChatMessage> SaveAndLoadSingleAsync(
        QuickChatMessage message)
    {
        var store = CreateStore();
        await store.SaveAsync(new QuickChatConversationState { Messages = [message] });
        return Assert.Single((await store.LoadAsync()).Messages);
    }

    private static QuickChatMessage CreateMessage(
        QuickChatMessageRole role,
        string? text) =>
        new()
        {
            Id = Guid.NewGuid(),
            Role = role,
            Text = text,
            CreatedAt = DateTimeOffset.UtcNow
        };

    private static QuickChatAttachment CreateAttachment() =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = QuickChatAttachmentType.Image,
            AssetFileName = "managed-image.webp",
            MediaType = "image/webp",
            Width = 1280,
            Height = 720
        };
}
