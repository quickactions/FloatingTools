using System.IO;
using System.Text.Json.Nodes;
using FloatingTools.App.Diagnostics;
using FloatingTools.App.Models;

namespace FloatingTools.App.Services.OpenAI;

public sealed class QuickChatOpenAiRequestBuilder(
    IQuickChatImageStore imageStore,
    QuickChatAttachmentContentCache? attachmentContentCache = null)
{
    public const string ProductInstruction =
        "You are the assistant in FloatingTools Quick Chat. "
        + "Help with quick questions and short conversations. "
        + "Answer directly and clearly. "
        + "Follow the user's language unless they request another language.";

    public async Task<JsonObject> BuildAsync(
        QuickChatConversationState conversation,
        string model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        using var timing = DebugAiRequestTiming.Start("quick_chat", "payload");
        timing.SetModel(model);
        timing.SetHasImages(conversation.Messages.Any(
            message => message.Attachments.Count > 0));
        timing.Mark("history_build_started");
        try
        {
            var input = new JsonArray();
            foreach (var message in conversation.Messages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var requestMessage = await CreateMessageAsync(
                    message,
                    timing,
                    cancellationToken);
                if (requestMessage is not null)
                {
                    input.Add(requestMessage);
                }
            }

            timing.Mark("history_built");
            var result = new JsonObject
            {
                ["model"] = model,
                ["instructions"] = CreateInstructions(conversation.AdditionalInstructions),
                ["input"] = input,
                ["stream"] = true,
                ["store"] = false
            };
            timing.Mark("request_payload_built");
            timing.Complete("success");
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            timing.Complete("cancelled");
            throw;
        }
        catch
        {
            timing.Complete("failure");
            throw;
        }
    }

    private async Task<JsonObject?> CreateMessageAsync(
        QuickChatMessage message,
        DebugAiRequestTiming timing,
        CancellationToken cancellationToken)
    {
        if (message.Role == QuickChatMessageRole.Assistant)
        {
            return CreateAssistantMessage(message);
        }

        var content = new JsonArray();
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            content.Add(new JsonObject
            {
                ["type"] = "input_text",
                ["text"] = message.Text
            });
        }

        foreach (var attachment in message.Attachments)
        {
            content.Add(await CreateImageContentAsync(
                attachment,
                timing,
                cancellationToken));
        }

        return content.Count == 0
            ? null
            : new JsonObject
            {
                ["role"] = "user",
                ["content"] = content
            };
    }

    private static JsonObject? CreateAssistantMessage(QuickChatMessage message)
    {
        if (message.Status is QuickChatMessageStatus.Error
            or QuickChatMessageStatus.InProgress
            || string.IsNullOrWhiteSpace(message.Text))
        {
            return null;
        }

        return new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = message.Text
        };
    }

    private async Task<JsonObject> CreateImageContentAsync(
        QuickChatAttachment attachment,
        DebugAiRequestTiming timing,
        CancellationToken cancellationToken)
    {
        if (attachment.Type != QuickChatAttachmentType.Image)
        {
            throw MissingImageException(attachment.AssetFileName);
        }

        if (attachmentContentCache is not null
            && attachmentContentCache.TryGetDataUrl(attachment.AssetFileName, out var cachedDataUrl))
        {
            timing.Mark("image_cache_hit");
            return new JsonObject
            {
                ["type"] = "input_image",
                ["image_url"] = cachedDataUrl,
                ["detail"] = "auto"
            };
        }

        string path;
        try
        {
            path = imageStore.GetAbsolutePath(attachment.AssetFileName);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException)
        {
            throw MissingImageException(attachment.AssetFileName, exception);
        }

        byte[] imageBytes;
        try
        {
            if (!File.Exists(path))
            {
                throw MissingImageException(attachment.AssetFileName);
            }

            imageBytes = await File.ReadAllBytesAsync(path, cancellationToken);
            timing.Mark("image_read");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (QuickChatServiceException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw MissingImageException(attachment.AssetFileName, exception);
        }

        var mediaType = Path.GetExtension(attachment.AssetFileName) switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            _ => throw MissingImageException(attachment.AssetFileName)
        };
        var dataUrl = $"data:{mediaType};base64,{Convert.ToBase64String(imageBytes)}";
        timing.Mark("image_base64_prepared");
        attachmentContentCache?.SetDataUrl(attachment.AssetFileName, dataUrl);
        return new JsonObject
        {
            ["type"] = "input_image",
            ["image_url"] = dataUrl,
            ["detail"] = "auto"
        };
    }

    private static string CreateInstructions(string? additionalInstructions) =>
        string.IsNullOrWhiteSpace(additionalInstructions)
            ? ProductInstruction
            : ProductInstruction
                + "\n\nAdditional user instructions:\n"
                + additionalInstructions.Trim();

    private static QuickChatServiceException MissingImageException(
        string assetFileName,
        Exception? innerException = null) =>
        new(
            QuickChatFailureKind.MissingManagedImage,
            "A managed Quick Chat image is missing or unavailable.",
            innerException);
}
