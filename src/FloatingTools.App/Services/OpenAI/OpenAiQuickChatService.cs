using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FloatingTools.App.Diagnostics;
using FloatingTools.App.Models;

namespace FloatingTools.App.Services.OpenAI;

public sealed class OpenAiQuickChatService(
    HttpClient httpClient,
    IOpenAiConfigurationProvider configurationProvider,
    QuickChatOpenAiRequestBuilder requestBuilder)
    : IQuickChatOpenAiService
{
    public async IAsyncEnumerable<QuickChatStreamEvent> StreamResponseAsync(
        QuickChatConversationState conversation,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        using var timing = DebugAiRequestTiming.Start("quick_chat", "openai_stream");
        timing.SetHasImages(conversation.Messages.Any(
            message => message.Attachments.Count > 0));
        var configuration = configurationProvider.GetConfiguration()
            ?? throw new QuickChatServiceException(
                QuickChatFailureKind.NotConfigured,
                "OpenAI is not configured for Quick Chat.");
        timing.SetModel(configuration.Model);
        timing.Mark("configuration_resolved");
        var requestBody = await requestBuilder.BuildAsync(
            conversation,
            configuration.Model,
            cancellationToken);
        timing.Mark("payload_ready");
        var serializedPayload = requestBody.ToJsonString();
        timing.Mark("payload_serialized");
        using var request = new HttpRequestMessage(HttpMethod.Post, "responses")
        {
            Content = new StringContent(
                serializedPayload,
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", configuration.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        timing.Mark("request_prepared");

        timing.Mark("http_request_started");
        using var response = await SendAsync(request, cancellationToken);
        timing.Mark("response_headers_received");
        if (!response.IsSuccessStatusCode)
        {
            timing.Complete("http_failure");
            throw new QuickChatServiceException(
                QuickChatFailureKind.HttpFailure,
                "The Quick Chat request could not be completed.",
                statusCode: response.StatusCode);
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(
            cancellationToken);
        timing.Mark("response_stream_opened");
        using var reader = new StreamReader(responseStream, Encoding.UTF8);
        var data = new StringBuilder();
        var receivedStreamLine = false;
        var receivedTextDelta = false;
        while (true)
        {
            var line = await ReadLineAsync(reader, cancellationToken);
            if (!receivedStreamLine)
            {
                receivedStreamLine = true;
                timing.Mark("first_stream_line_received");
            }

            if (line is null)
            {
                timing.Complete("invalid_response");
                throw InvalidResponseException();
            }

            if (line.Length == 0)
            {
                if (data.Length == 0)
                {
                    continue;
                }

                var parsed = ParseEvent(data.ToString());
                data.Clear();
                if (parsed.Kind == ParsedEventKind.TextDelta)
                {
                    if (!receivedTextDelta)
                    {
                        receivedTextDelta = true;
                        timing.Mark("first_text_delta_received");
                    }

                    yield return new QuickChatStreamEvent(
                        QuickChatStreamEventKind.TextDelta,
                        parsed.Text);
                }
                else if (parsed.Kind == ParsedEventKind.Completed)
                {
                    timing.Mark("stream_completed");
                    timing.Complete("success");
                    yield return new QuickChatStreamEvent(
                        QuickChatStreamEventKind.Completed);
                    yield break;
                }

                continue;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                if (data.Length > 0)
                {
                    data.Append('\n');
                }

                data.Append(line.AsSpan(5).TrimStart());
            }
        }
    }

    public async Task<string> GetResponseAsync(
        QuickChatConversationState conversation,
        CancellationToken cancellationToken = default)
    {
        var text = new StringBuilder();
        await foreach (var item in StreamResponseAsync(conversation, cancellationToken))
        {
            if (item.Kind == QuickChatStreamEventKind.TextDelta)
            {
                text.Append(item.Text);
            }
        }

        return text.ToString();
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new QuickChatServiceException(
                QuickChatFailureKind.HttpFailure,
                "The Quick Chat request timed out.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new QuickChatServiceException(
                QuickChatFailureKind.HttpFailure,
                "The Quick Chat service is unavailable.",
                exception);
        }
    }

    private static async Task<string?> ReadLineAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        try
        {
            return await reader.ReadLineAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or HttpRequestException)
        {
            throw new QuickChatServiceException(
                QuickChatFailureKind.HttpFailure,
                "The Quick Chat response stream ended unexpectedly.",
                exception);
        }
    }

    private static ParsedEvent ParseEvent(string data)
    {
        try
        {
            using var document = JsonDocument.Parse(data);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var typeElement)
                || typeElement.ValueKind != JsonValueKind.String)
            {
                throw InvalidResponseException();
            }

            return typeElement.GetString() switch
            {
                "response.output_text.delta" => ParseDelta(root),
                "response.completed" => new ParsedEvent(ParsedEventKind.Completed),
                "error" or "response.failed" or "response.incomplete" =>
                    throw new QuickChatServiceException(
                        QuickChatFailureKind.HttpFailure,
                        "The Quick Chat provider could not complete the response."),
                _ => new ParsedEvent(ParsedEventKind.Ignored)
            };
        }
        catch (JsonException exception)
        {
            throw new QuickChatServiceException(
                QuickChatFailureKind.InvalidResponse,
                "The Quick Chat provider returned an invalid streaming response.",
                exception);
        }
    }

    private static ParsedEvent ParseDelta(JsonElement root)
    {
        if (!root.TryGetProperty("delta", out var delta)
            || delta.ValueKind != JsonValueKind.String)
        {
            throw InvalidResponseException();
        }

        return new ParsedEvent(ParsedEventKind.TextDelta, delta.GetString());
    }

    private static QuickChatServiceException InvalidResponseException() =>
        new(
            QuickChatFailureKind.InvalidResponse,
            "The Quick Chat provider returned an invalid streaming response.");

    private enum ParsedEventKind
    {
        Ignored,
        TextDelta,
        Completed
    }

    private sealed record ParsedEvent(
        ParsedEventKind Kind,
        string? Text = null);
}
