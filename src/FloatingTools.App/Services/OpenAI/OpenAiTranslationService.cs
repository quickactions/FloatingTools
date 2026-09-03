using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FloatingTools.App.Diagnostics;
using FloatingTools.App.Models;

namespace FloatingTools.App.Services.OpenAI;

public sealed class OpenAiTranslationService : ITranslationService
{
    public const string ProviderName = "OpenAI";
    public const int AlternativeMaxOutputTokens = 96;

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IOpenAiConfigurationProvider _configurationProvider;
    private readonly ConcurrentDictionary<TranslationCacheKey, TranslationResult>
        _translationCache = new();
    private readonly ConcurrentDictionary<AlternativeCacheKey, AlternativeCacheValue>
        _alternativeCache = new();

    public OpenAiTranslationService(
        HttpClient httpClient,
        IOpenAiConfigurationProvider configurationProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configurationProvider = configurationProvider
            ?? throw new ArgumentNullException(nameof(configurationProvider));
    }

    public async Task<TranslationResult> TranslateAsync(
        string text,
        string? sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguage);

        using var timing = DebugAiRequestTiming.Start("translation", "request");
        var configuration = _configurationProvider.GetConfiguration()
            ?? throw new TranslationProviderNotConfiguredException();
        timing.SetModel(configuration.Model);
        timing.Mark("configuration_resolved");
        var detectedLanguage = NormalizeSourceLanguage(sourceLanguage, text);
        var cacheKey = new TranslationCacheKey(
            NormalizeCacheText(text),
            detectedLanguage.ToLowerInvariant(),
            targetLanguage.ToLowerInvariant(),
            configuration.Model);
        if (_translationCache.TryGetValue(cacheKey, out var cachedResult))
        {
            Debug.WriteLine(
                $"FloatingTools translation cache hit: model={configuration.Model}; "
                + $"direction={detectedLanguage}->{targetLanguage}");
            timing.Mark("cache_hit");
            timing.Complete("cache_hit");
            return cachedResult;
        }

        var requestBody = CreateRequestBody(
            configuration.Model,
            text,
            detectedLanguage,
            targetLanguage);
        timing.Mark("payload_built");

        using var request = new HttpRequestMessage(HttpMethod.Post, "responses")
        {
            Content = JsonContent.Create(
                requestBody,
                options: SerializerOptions)
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", configuration.ApiKey);
        timing.Mark("request_prepared");

        HttpResponseMessage response;
        try
        {
            timing.Mark("http_request_started");
            response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            timing.Mark("response_headers_received");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            timing.Complete("cancelled");
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timing.Complete("timeout");
            throw new TranslationServiceException(
                TranslationFailureKind.Timeout,
                "Translation timed out. Please try again.");
        }
        catch (HttpRequestException exception)
        {
            timing.Complete("network_failure");
            throw new TranslationServiceException(
                TranslationFailureKind.NetworkUnavailable,
                "No network connection. Check your connection and try again.",
                exception);
        }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                timing.Complete("http_failure");
                throw CreateStatusException(response.StatusCode);
            }

            OpenAiResponse? responseBody;
            try
            {
                responseBody = await response.Content.ReadFromJsonAsync<OpenAiResponse>(
                    SerializerOptions,
                    cancellationToken);
                timing.Mark("response_body_deserialized");
            }
            catch (JsonException exception)
            {
                timing.Complete("invalid_response");
                Debug.WriteLine(
                    "FloatingTools translation response invalid: "
                    + $"reason=response_json; path={exception.Path ?? "<unknown>"}");
                throw new TranslationServiceException(
                    TranslationFailureKind.InvalidResponse,
                    "The translation provider returned an invalid response. Please try again.",
                    exception);
            }

            ThrowIfIncomplete(responseBody);

            var outputText = responseBody?.Output?
                .SelectMany(item => item.Content ?? [])
                .FirstOrDefault(content => content.Type == "output_text")
                ?.Text;
            var payload = DeserializeTranslationPayload(outputText);
            var correctionStatus = ParseCorrectionStatus(payload.CorrectionStatus);
            var correctedSourceText = NormalizeCorrection(
                payload.CorrectedSourceText,
                text,
                correctionStatus);

            if (correctionStatus != TranslationCorrectionStatus.Ambiguous
                && string.IsNullOrWhiteSpace(payload.TranslatedText))
            {
                throw EmptyResponseException();
            }

            var result = new TranslationResult(
                payload.TranslatedText?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(payload.DetectedSourceLanguage)
                    ? detectedLanguage
                    : payload.DetectedSourceLanguage,
                ProviderName,
                correctedSourceText: correctedSourceText,
                correctionStatus: correctionStatus,
                targetLanguage: string.IsNullOrWhiteSpace(payload.TargetLanguage)
                    ? targetLanguage
                    : payload.TargetLanguage);
            _translationCache.TryAdd(cacheKey, result);
            timing.Mark("result_ready");
            timing.Complete("success");
            return result;
        }
    }

    public async Task<string?> TranslateAlternativeAsync(
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        string primaryTranslation,
        IReadOnlyList<string> existingAlternatives,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceText);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLanguage);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguage);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryTranslation);
        ArgumentNullException.ThrowIfNull(existingAlternatives);

        using var timing = DebugAiRequestTiming.Start("translation", "alternative");
        var configuration = _configurationProvider.GetConfiguration()
            ?? throw new TranslationProviderNotConfiguredException();
        timing.SetModel(configuration.Model);
        timing.Mark("configuration_resolved");
        var cacheKey = new AlternativeCacheKey(
            NormalizeCacheText(sourceText),
            sourceLanguage.ToLowerInvariant(),
            targetLanguage.ToLowerInvariant(),
            configuration.Model,
            NormalizeCacheText(primaryTranslation),
            CreateAlternativeSetKey(existingAlternatives));
        if (_alternativeCache.TryGetValue(cacheKey, out var cachedValue))
        {
            Debug.WriteLine(
                $"FloatingTools alternative cache hit: model={configuration.Model}; "
                + $"direction={sourceLanguage}->{targetLanguage}");
            timing.Mark("cache_hit");
            timing.Complete("cache_hit");
            return cachedValue.Translation;
        }

        var requestBody = CreateAlternativeRequestBody(
            configuration.Model,
            sourceText,
            sourceLanguage,
            targetLanguage,
            primaryTranslation,
            existingAlternatives);
        timing.Mark("payload_built");
        using var request = new HttpRequestMessage(HttpMethod.Post, "responses")
        {
            Content = JsonContent.Create(requestBody, options: SerializerOptions)
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", configuration.ApiKey);
        timing.Mark("request_prepared");

        HttpResponseMessage response;
        try
        {
            timing.Mark("http_request_started");
            response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            timing.Mark("response_headers_received");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            timing.Complete("cancelled");
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timing.Complete("timeout");
            throw new TranslationServiceException(
                TranslationFailureKind.Timeout,
                "Alternative translation timed out. Please try again.");
        }
        catch (HttpRequestException exception)
        {
            timing.Complete("network_failure");
            throw new TranslationServiceException(
                TranslationFailureKind.NetworkUnavailable,
                "No network connection. Check your connection and try again.",
                exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                timing.Complete("http_failure");
                throw CreateStatusException(response.StatusCode);
            }

            OpenAiResponse? responseBody;
            try
            {
                responseBody = await response.Content.ReadFromJsonAsync<OpenAiResponse>(
                    SerializerOptions,
                    cancellationToken);
                timing.Mark("response_body_deserialized");
            }
            catch (JsonException exception)
            {
                timing.Complete("invalid_response");
                Debug.WriteLine(
                    "FloatingTools alternative response invalid: "
                    + $"reason=response_json; path={exception.Path ?? "<unknown>"}");
                throw new TranslationServiceException(
                    TranslationFailureKind.InvalidResponse,
                    "The translation provider returned an invalid response. Please try again.",
                    exception);
            }

            ThrowIfIncomplete(responseBody);
            var outputText = responseBody?.Output?
                .SelectMany(item => item.Content ?? [])
                .FirstOrDefault(content => content.Type == "output_text")
                ?.Text;
            var alternative = NormalizeAlternative(
                DeserializeAlternativePayload(outputText).AlternativeTranslation,
                primaryTranslation,
                existingAlternatives);
            _alternativeCache.TryAdd(
                cacheKey,
                new AlternativeCacheValue(alternative));
            timing.Mark("result_ready");
            timing.Complete("success");
            return alternative;
        }
    }

    private static object CreateRequestBody(
        string model,
        string text,
        string sourceLanguage,
        string targetLanguage) =>
        new
        {
            model,
            instructions =
                $"Primary task: translate {LanguageName(sourceLanguage)} to "
                + $"{LanguageName(targetLanguage)}. Translation takes priority over spelling "
                + "classification. Decide in order: (1) understandable valid input => none and "
                + "translate; (2) one overwhelmingly likely spelling or typing correction => "
                + "confident, return the minimal correction, and translate it; (3) genuinely "
                + "unclear overall intent => ambiguous with both text fields null. For input with "
                + "two or more words, if the overall text is understandable, you MUST translate "
                + "it and correctionStatus MUST be none or confident. Never use ambiguous merely "
                + "because input is multiline, long, technical, contains uncommon words, or has "
                + "one unfamiliar word. Reserve ambiguous primarily for very short input whose "
                + "intended word genuinely cannot be determined. "
                + "Do not require absolute certainty for an obvious typo. Preserve uncertain names, "
                + "brands, abbreviations, usernames, identifiers, technical terms, and uncommon "
                + "valid words. Never rewrite style, grammar, tone, or meaning. Examples: helo => "
                + "confident/hello; realy => confident/really; recieve => confident/receive; "
                + "really => none; repository => none; dotnet => none; aello => ambiguous. "
                + "An understandable sentence with one clear typo is confident, not ambiguous: "
                + "I realy need to submit the project tomorrow => confident/I really need to "
                + "submit the project tomorrow, then translate. Valid multi-word example: Only "
                + "return ambiguous if the meaning of the overall input genuinely cannot be "
                + "determined. => none and translate. Valid multiline example: Inputs containing "
                + "line breaks / should still be treated as one translation request. => none and "
                + "translate, preserving line order and meaning. Treat all lines as one complete "
                + "request; line breaks alone can never make it ambiguous.",
            input = text,
            reasoning = new { effort = "none" },
            max_output_tokens = 240,
            store = false,
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "translation_result",
                    strict = true,
                    schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            translatedText = new
                            {
                                type = new[] { "string", "null" }
                            },
                            correctedSourceText = new
                            {
                                type = new[] { "string", "null" }
                            },
                            correctionStatus = new
                            {
                                type = "string",
                                @enum = new[] { "none", "confident", "ambiguous" }
                            },
                            detectedSourceLanguage = new
                            {
                                type = "string",
                                @enum = new[] { "he", "en" }
                            },
                            targetLanguage = new
                            {
                                type = "string",
                                @enum = new[] { "he", "en" }
                            }
                        },
                        required = new[]
                        {
                            "translatedText",
                            "correctedSourceText",
                            "correctionStatus",
                            "detectedSourceLanguage",
                            "targetLanguage"
                        },
                        additionalProperties = false
                    }
                }
            }
        };

    private static object CreateAlternativeRequestBody(
        string model,
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        string primaryTranslation,
        IReadOnlyList<string> existingAlternatives) =>
        new
        {
            model,
            instructions = CreateAlternativeInstructions(
                sourceText,
                sourceLanguage,
                targetLanguage),
            input = $"Source text:\n{sourceText}\n\nPrimary translation:\n"
                + $"{primaryTranslation}\n\nAlready returned alternatives:\n"
                + FormatExistingAlternatives(existingAlternatives),
            reasoning = new { effort = "none" },
            max_output_tokens = AlternativeMaxOutputTokens,
            store = false,
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "alternative_translation_result",
                    strict = true,
                    schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            alternativeTranslation = new
                            {
                                type = new[] { "string", "null" }
                            }
                        },
                        required = new[] { "alternativeTranslation" },
                        additionalProperties = false
                    }
                }
            }
        };

    private static string CreateAlternativeInstructions(
        string sourceText,
        string sourceLanguage,
        string targetLanguage)
    {
        var commonInstructions =
            $"Return exactly one useful alternative {LanguageName(targetLanguage)} "
            + $"translation for the supplied {LanguageName(sourceLanguage)} source. Return only "
            + "the translation: no explanation, definition, or list. Do not return the primary "
            + "translation or any previously returned alternative. Differences only in punctuation, "
            + "whitespace, or capitalization are not alternatives. Reject trivial grammatical or "
            + "morphological variations that do not add meaningful translation value. Return null "
            + "rather than a low-value alternative.";

        if (TranslationInputLimits.CountWords(sourceText) == 1)
        {
            return commonInstructions
                + " The source contains exactly one word. Treat the primary translation and every "
                + "existing alternative as semantic senses already represented. Return the next "
                + "most common DISTINCT dictionary sense of the source word. Do not return a synonym, "
                + "paraphrase, grammatical form, or wording that expresses essentially the same sense. "
                + "Prefer null when no genuinely distinct useful sense remains. Examples: can with "
                + "primary יכול => prefer פחית, never להיות מסוגל or מסוגל; bank with primary בנק => "
                + "גדת נהר; light with primary אור => קל; right with primary נכון => ימין.";
        }

        return commonInstructions
            + " The source is a phrase or sentence; preserve the same contextual meaning and return "
            + "a different natural translation of that meaning; never switch to an unrelated "
            + "dictionary sense because one source word has multiple meanings.";
    }

    private static string NormalizeSourceLanguage(
        string? sourceLanguage,
        string text) =>
        string.IsNullOrWhiteSpace(sourceLanguage)
            ? TranslationDirectionResolver.Resolve(text).SourceLanguage
            : sourceLanguage;

    private static string NormalizeCacheText(string text) =>
        text.Trim().Normalize(NormalizationForm.FormC);

    private static string LanguageName(string languageCode) =>
        languageCode.Equals(
            TranslationDirectionResolver.HebrewLanguageCode,
            StringComparison.OrdinalIgnoreCase)
            ? "Hebrew"
            : "English";

    private static TranslationPayload DeserializeTranslationPayload(
        string? outputText)
    {
        if (string.IsNullOrWhiteSpace(outputText))
        {
            Debug.WriteLine(
                "FloatingTools translation response invalid: reason=missing_output_text");
            throw EmptyResponseException();
        }

        TranslationPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<TranslationPayload>(
                outputText,
                SerializerOptions);
        }
        catch (JsonException exception)
        {
            Debug.WriteLine(
                "FloatingTools translation response invalid: "
                + $"reason=structured_json; path={exception.Path ?? "<unknown>"}");
            throw new TranslationServiceException(
                TranslationFailureKind.InvalidResponse,
                "The translation provider returned an invalid response. Please try again.",
                exception);
        }

        if (payload is null)
        {
            throw InvalidResponseException();
        }

        return payload;
    }

    private static AlternativePayload DeserializeAlternativePayload(string? outputText)
    {
        if (string.IsNullOrWhiteSpace(outputText))
        {
            throw EmptyResponseException();
        }

        try
        {
            return JsonSerializer.Deserialize<AlternativePayload>(
                    outputText,
                    SerializerOptions)
                ?? throw InvalidResponseException();
        }
        catch (JsonException exception)
        {
            Debug.WriteLine(
                "FloatingTools alternative response invalid: "
                + $"reason=structured_json; path={exception.Path ?? "<unknown>"}");
            throw new TranslationServiceException(
                TranslationFailureKind.InvalidResponse,
                "The translation provider returned an invalid response. Please try again.",
                exception);
        }
    }

    private static string? NormalizeAlternative(
        string? alternativeTranslation,
        string primaryTranslation,
        IReadOnlyList<string> existingAlternatives)
    {
        if (alternativeTranslation is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(alternativeTranslation))
        {
            throw InvalidResponseException();
        }

        var alternative = alternativeTranslation.Trim();
        var normalizedAlternative = NormalizeForComparison(alternative);
        if (normalizedAlternative.Length == 0)
        {
            throw InvalidResponseException();
        }

        if (new[] { primaryTranslation }
            .Concat(existingAlternatives)
            .Any(existing => normalizedAlternative.Equals(
                NormalizeForComparison(existing),
                StringComparison.OrdinalIgnoreCase)))
        {
            throw InvalidResponseException();
        }

        return alternative;
    }

    private static string FormatExistingAlternatives(
        IReadOnlyList<string> existingAlternatives) =>
        existingAlternatives.Count == 0
            ? "(none)"
            : string.Join(
                "\n",
                existingAlternatives.Select(alternative => $"- {alternative}"));

    private static string CreateAlternativeSetKey(
        IReadOnlyList<string> existingAlternatives) =>
        string.Join(
            '\u001F',
            existingAlternatives.Select(NormalizeCacheText));

    private static string NormalizeForComparison(string text) =>
        string.Concat(text.Normalize(NormalizationForm.FormC)
            .Where(char.IsLetterOrDigit));

    private static void ThrowIfIncomplete(OpenAiResponse? response)
    {
        if (!string.Equals(
                response?.Status,
                "incomplete",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var reason = response?.IncompleteDetails?.Reason switch
        {
            "max_output_tokens" => "max_output_tokens",
            "content_filter" => "content_filter",
            _ => "unknown"
        };
        Debug.WriteLine(
            $"FloatingTools translation response incomplete: reason={reason}");

        var message = reason == "max_output_tokens"
            ? "The translation response was incomplete because its output limit was reached."
            : "The translation provider returned an incomplete response.";
        throw new TranslationServiceException(
            TranslationFailureKind.IncompleteResponse,
            message);
    }

    private static string? NormalizeCorrection(
        string? correctedSourceText,
        string originalSourceText,
        TranslationCorrectionStatus correctionStatus)
    {
        if (correctionStatus != TranslationCorrectionStatus.Confident)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(correctedSourceText))
        {
            throw InvalidResponseException();
        }

        var correction = correctedSourceText.Trim();
        if (correction.Equals(originalSourceText.Trim(), StringComparison.Ordinal))
        {
            throw InvalidResponseException();
        }

        return correction;
    }

    private static TranslationCorrectionStatus ParseCorrectionStatus(string? status) =>
        status?.ToLowerInvariant() switch
        {
            "none" => TranslationCorrectionStatus.None,
            "confident" => TranslationCorrectionStatus.Confident,
            "ambiguous" => TranslationCorrectionStatus.Ambiguous,
            _ => throw InvalidResponseException()
        };

    private static TranslationServiceException EmptyResponseException() =>
        new(
            TranslationFailureKind.InvalidResponse,
            "The translation provider returned an empty translation. Please try again.");

    private static TranslationServiceException InvalidResponseException() =>
        new(
            TranslationFailureKind.InvalidResponse,
            "The translation provider returned an invalid response. Please try again.");

    private static TranslationServiceException CreateStatusException(
        HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new(
                TranslationFailureKind.Unauthorized,
                "The OpenAI API key is invalid or is not authorized."),
            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => new(
                TranslationFailureKind.Timeout,
                "Translation timed out. Please try again."),
            HttpStatusCode.TooManyRequests => new(
                TranslationFailureKind.QuotaExceeded,
                "The OpenAI quota or rate limit was exceeded. Please try again later."),
            _ when (int)statusCode >= 500 => new(
                TranslationFailureKind.ProviderUnavailable,
                "The translation provider is temporarily unavailable. Please try again later."),
            _ => new(
                TranslationFailureKind.ProviderUnavailable,
                "The translation request could not be completed. Please try again.")
        };

    private sealed record OpenAiResponse(
        string? Status,
        [property: JsonPropertyName("incomplete_details")]
        OpenAiIncompleteDetails? IncompleteDetails,
        IReadOnlyList<OpenAiOutputItem>? Output);

    private sealed record OpenAiIncompleteDetails(string? Reason);

    private sealed record OpenAiOutputItem(
        IReadOnlyList<OpenAiContentItem>? Content);

    private sealed record OpenAiContentItem(string Type, string? Text);

    private sealed record TranslationPayload(
        string? TranslatedText,
        string? CorrectedSourceText,
        string? CorrectionStatus,
        string? DetectedSourceLanguage,
        string? TargetLanguage);

    private sealed record AlternativePayload(string? AlternativeTranslation);

    private sealed record TranslationCacheKey(
        string SourceText,
        string SourceLanguage,
        string TargetLanguage,
        string Model);

    private sealed record AlternativeCacheKey(
        string SourceText,
        string SourceLanguage,
        string TargetLanguage,
        string Model,
        string PrimaryTranslation,
        string ExistingAlternatives);

    private sealed record AlternativeCacheValue(string? Translation);

}
