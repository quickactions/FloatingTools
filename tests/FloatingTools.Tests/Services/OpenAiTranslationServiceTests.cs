using System.Net;
using System.Net.Http;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.Services.Ai;
using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.Tests.Services;

public sealed class OpenAiTranslationServiceTests
{
    [Fact]
    public async Task Success_ReturnsStructuredTranslationAndProviderMetadata()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var service = CreateService(async (request, cancellationToken) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse(
                HttpStatusCode.OK,
                ResponseWithTranslation("שלום", "en"));
        });

        var result = await service.TranslateAsync(
            "Hello",
            "en",
            "he",
            CancellationToken.None);

        Assert.Equal("שלום", result.MainTranslation);
        Assert.Equal("en", result.DetectedLanguage);
        Assert.Equal(OpenAiTranslationService.ProviderName, result.Provider);
        Assert.Equal("Bearer", capturedRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("test-key", capturedRequest.Headers.Authorization.Parameter);
        var instructions = GetInstructions(capturedBody!);
        Assert.Contains("gpt-5.6-luna", capturedBody);
        Assert.Contains("translation_result", capturedBody);
        Assert.Contains("correctedSourceText", capturedBody);
        Assert.Contains("translatedText", capturedBody);
        Assert.Contains("correctionStatus", capturedBody);
        Assert.Contains("detectedSourceLanguage", capturedBody);
        Assert.Contains("targetLanguage", capturedBody);
        Assert.Contains("Primary task: translate", instructions);
        Assert.Contains("Decide in order", instructions);
        Assert.Contains("Translation takes priority over spelling classification", instructions);
        Assert.Contains("two or more words", instructions);
        Assert.Contains("you MUST translate it", instructions);
        Assert.Contains("MUST be none or confident", instructions);
        Assert.Contains("Never use ambiguous merely because input is multiline", instructions);
        Assert.Contains("Reserve ambiguous primarily for very short input", instructions);
        Assert.Contains("helo => confident/hello", instructions);
        Assert.Contains("realy => confident/really", instructions);
        Assert.Contains("really => none", instructions);
        Assert.Contains("repository => none", instructions);
        Assert.Contains("dotnet => none", instructions);
        Assert.Contains("aello => ambiguous", instructions);
        Assert.Contains("understandable sentence with one clear typo", instructions);
        Assert.Contains("Valid multi-word example", instructions);
        Assert.Contains("Valid multiline example", instructions);
        Assert.Contains("line breaks alone can never make it ambiguous", instructions);
        Assert.Contains("\"effort\":\"none\"", capturedBody);
        Assert.Contains("\"max_output_tokens\":240", capturedBody);
    }

    [Fact]
    public async Task SharedAiConfiguration_UsesPinnedTranslationModelWithoutChangingRequestBehavior()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var settings = new AppSettings
        {
            Ai = new AiSettings
            {
                DefaultModel = "future-application-model",
                Translation = new AiToolSettings { Model = "translation-pinned-model" }
            }
        };
        var configurationProvider = new AiOpenAiConfigurationProvider(
            new AiConfigurationResolver(
                settings,
                new SharedCredentialStores("application-key")),
            () => null);
        var client = new HttpClient(new StubHttpMessageHandler(
            async (request, cancellationToken) =>
            {
                capturedRequest = request;
                capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                return JsonResponse(
                    HttpStatusCode.OK,
                    ResponseWithTranslation("שלום", "en"));
            }))
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        var service = new OpenAiTranslationService(client, configurationProvider);

        var result = await service.TranslateAsync("Hello", "en", "he", CancellationToken.None);

        Assert.Equal("שלום", result.MainTranslation);
        Assert.Equal("application-key", capturedRequest!.Headers.Authorization!.Parameter);
        Assert.Contains("\"model\":\"translation-pinned-model\"", capturedBody);
        Assert.Contains("translation_result", capturedBody);
    }

    [Fact]
    public async Task EnglishTypo_ReturnsCorrectionAndTranslationOfCorrectedMeaning()
    {
        var service = CreateService((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            ResponseWithTranslation(
                "אני באמת אוהב את זה",
                "en",
                "I really like this"))));

        var result = await service.TranslateAsync(
            "I realy like this",
            "en",
            "he",
            CancellationToken.None);

        Assert.Equal("I really like this", result.CorrectedSourceText);
        Assert.Equal("אני באמת אוהב את זה", result.MainTranslation);
        Assert.Equal("en", result.DetectedLanguage);
        Assert.Equal(TranslationCorrectionStatus.Confident, result.CorrectionStatus);
    }

    [Fact]
    public async Task HebrewTypo_ReturnsCorrectionAndEnglishTranslation()
    {
        string? capturedBody = null;
        var service = CreateService(async (request, cancellationToken) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse(
                HttpStatusCode.OK,
                ResponseWithTranslation(
                    "I like reading books",
                    "he",
                    "אני אוהב לקרוא ספרים"));
        });

        var result = await service.TranslateAsync(
            "אני אוהב לקרו ספרים",
            "he",
            "en",
            CancellationToken.None);

        Assert.Equal("אני אוהב לקרוא ספרים", result.CorrectedSourceText);
        Assert.Equal("I like reading books", result.MainTranslation);
        Assert.Contains("translate Hebrew to English", GetInstructions(capturedBody!));
        Assert.Equal(TranslationCorrectionStatus.Confident, result.CorrectionStatus);
    }

    [Fact]
    public async Task CorrectText_DoesNotReturnCorrection()
    {
        var service = CreateService((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            ResponseWithTranslation("שלום", "en"))));

        var result = await service.TranslateAsync(
            "Hello",
            "en",
            "he",
            CancellationToken.None);

        Assert.Null(result.CorrectedSourceText);
        Assert.Equal(TranslationCorrectionStatus.None, result.CorrectionStatus);
    }

    [Fact]
    public async Task AmbiguousText_IsNotAggressivelyCorrected()
    {
        var service = CreateService((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            ResponseWithTranslation("ראיתי אותה מתכופפת", "en"))));

        var result = await service.TranslateAsync(
            "I saw her duck",
            "en",
            "he",
            CancellationToken.None);

        Assert.Null(result.CorrectedSourceText);
    }

    [Fact]
    public async Task CorrectionIdenticalToOriginal_IsSuppressed()
    {
        var service = CreateService((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            ResponseWithTranslation(
                "שלום",
                "en",
                "Hello",
                correctionStatus: "none"))));

        var result = await service.TranslateAsync(
            "Hello",
            "en",
            "he",
            CancellationToken.None);

        Assert.Null(result.CorrectedSourceText);
    }

    [Fact]
    public async Task EmptyCorrectedSourceText_IsTreatedAsNull()
    {
        var service = CreateService((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            ResponseWithTranslation(
                "שלום",
                "en",
                "   ",
                correctionStatus: "none"))));

        var result = await service.TranslateAsync(
            "Hello",
            "en",
            "he",
            CancellationToken.None);

        Assert.Null(result.CorrectedSourceText);
        Assert.Equal("שלום", result.MainTranslation);
    }

    [Theory]
    [InlineData("helo", "hello")]
    [InlineData("realy", "really")]
    public async Task ClearEnglishTypo_IsCorrectedConfidently(
        string source,
        string correction)
    {
        var service = CreateService((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            ResponseWithTranslation(
                "שלום",
                "en",
                correction,
                correctionStatus: "confident"))));

        var result = await service.TranslateAsync(
            source,
            "en",
            "he",
            CancellationToken.None);

        Assert.Equal(TranslationCorrectionStatus.Confident, result.CorrectionStatus);
        Assert.Equal(correction, result.CorrectedSourceText);
        Assert.Equal("שלום", result.MainTranslation);
        Assert.Equal("he", result.TargetLanguage);
    }

    [Theory]
    [InlineData("aello")]
    [InlineData("aellow")]
    [InlineData("dotnat")]
    public async Task ShortUnknownInput_RemainsUncorrectedAndAmbiguous(string source)
    {
        var service = CreateService((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            ResponseWithTranslation(
                translation: null,
                detectedLanguage: "en",
                correctionStatus: "ambiguous"))));

        var result = await service.TranslateAsync(
            source,
            "en",
            "he",
            CancellationToken.None);

        Assert.Equal(TranslationCorrectionStatus.Ambiguous, result.CorrectionStatus);
        Assert.Null(result.CorrectedSourceText);
        Assert.Equal(string.Empty, result.MainTranslation);
    }

    [Theory]
    [InlineData("dotnet")]
    [InlineData("GitHub")]
    [InlineData("NuGet")]
    [InlineData("WPF")]
    [InlineData("API")]
    [InlineData("repository")]
    public async Task ValidTechnicalTerm_RemainsUnchanged(string source)
    {
        var service = CreateService((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            ResponseWithTranslation("תרגום", "en"))));

        var result = await service.TranslateAsync(
            source,
            "en",
            "he",
            CancellationToken.None);

        Assert.Equal(TranslationCorrectionStatus.None, result.CorrectionStatus);
        Assert.Null(result.CorrectedSourceText);
        Assert.Equal("תרגום", result.MainTranslation);
    }

    [Fact]
    public async Task ValidHebrewWord_RemainsUnchanged()
    {
        var service = CreateService((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            ResponseWithTranslation("repository", "he"))));

        var result = await service.TranslateAsync(
            "מאגר",
            "he",
            "en",
            CancellationToken.None);

        Assert.Equal(TranslationCorrectionStatus.None, result.CorrectionStatus);
        Assert.Null(result.CorrectedSourceText);
        Assert.Equal("repository", result.MainTranslation);
    }

    [Fact]
    public async Task RepeatedNormalizedRequest_ReturnsCachedResultWithoutSecondApiCall()
    {
        var requestCount = 0;
        var service = CreateService((_, _) =>
        {
            requestCount++;
            return Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                ResponseWithTranslation("שלום", "en")));
        });

        var first = await service.TranslateAsync(
            "Hello",
            "en",
            "he",
            CancellationToken.None);
        var second = await service.TranslateAsync(
            "  Hello  ",
            "en",
            "he",
            CancellationToken.None);

        Assert.Equal(1, requestCount);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task CacheKey_IncludesTranslationDirection()
    {
        var requestCount = 0;
        var service = CreateService((_, _) =>
        {
            requestCount++;
            return Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                ResponseWithTranslation("Result", "en")));
        });

        await service.TranslateAsync("term", "en", "he", CancellationToken.None);
        await service.TranslateAsync("term", "en", "en", CancellationToken.None);

        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task CacheKey_IncludesConfiguredModel()
    {
        var requestCount = 0;
        var configurationProvider = new MutableConfigurationProvider(
            new OpenAiTranslationConfiguration("test-key", "model-a"));
        var client = new HttpClient(new StubHttpMessageHandler((_, _) =>
        {
            requestCount++;
            return Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                ResponseWithTranslation("שלום", "en")));
        }))
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        var service = new OpenAiTranslationService(client, configurationProvider);

        await service.TranslateAsync("Hello", "en", "he", CancellationToken.None);
        configurationProvider.Configuration =
            new OpenAiTranslationConfiguration("test-key", "model-b");
        await service.TranslateAsync("Hello", "en", "he", CancellationToken.None);

        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task MultilineInput_IsSentAsOneUnchangedRequest()
    {
        string? capturedBody = null;
        var service = CreateService(async (request, cancellationToken) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse(
                HttpStatusCode.OK,
                ResponseWithTranslation("Translated lines", "en"));
        });
        const string source = "First line\nSecond line with context";

        var result = await service.TranslateAsync(
            source,
            "en",
            "he",
            CancellationToken.None);

        using var document = System.Text.Json.JsonDocument.Parse(capturedBody!);
        Assert.Equal(source, document.RootElement.GetProperty("input").GetString());
        Assert.Equal("Translated lines", result.MainTranslation);
    }

    [Fact]
    public async Task MaxOutputTokenIncompleteResponse_IsDistinguishedFromMalformedResponse()
    {
        var incompleteResponse = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = "incomplete",
            incomplete_details = new { reason = "max_output_tokens" },
            output = Array.Empty<object>()
        });
        var service = CreateService((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            incompleteResponse)));

        var exception = await Assert.ThrowsAsync<TranslationServiceException>(
            () => service.TranslateAsync(
                "A longer but valid sentence",
                "en",
                "he",
                CancellationToken.None));

        Assert.Equal(
            TranslationFailureKind.IncompleteResponse,
            exception.FailureKind);
        Assert.Contains("output limit", exception.Message);
    }

    [Fact]
    public async Task Alternative_SingleWord_ReturnsOneDifferentCommonMeaning()
    {
        string? capturedBody = null;
        var service = CreateService(async (request, cancellationToken) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse(
                HttpStatusCode.OK,
                ResponseWithAlternative("פחית"));
        });

        var alternative = await service.TranslateAlternativeAsync(
            "can",
            "en",
            "he",
            "יכול",
            [],
            CancellationToken.None);

        Assert.Equal("פחית", alternative);
        Assert.Contains("alternative_translation_result", capturedBody);
        Assert.Contains("alternativeTranslation", capturedBody);
        Assert.Contains("\"effort\":\"none\"", capturedBody);
        Assert.Contains(
            $"\"max_output_tokens\":{OpenAiTranslationService.AlternativeMaxOutputTokens}",
            capturedBody);
        var instructions = GetInstructions(capturedBody!);
        Assert.Contains("exactly one word", instructions);
        Assert.Contains("DISTINCT dictionary sense", instructions);
        Assert.Contains("prefer פחית", instructions);
        Assert.Contains("never להיות מסוגל or מסוגל", instructions);
    }

    [Theory]
    [InlineData("can", "יכול", "פחית")]
    [InlineData("bank", "בנק", "גדת נהר")]
    [InlineData("light", "אור", "קל")]
    public async Task Alternative_SingleWordPromptPrefersDistinctCommonSense(
        string source,
        string primary,
        string providerAlternative)
    {
        string? capturedBody = null;
        var service = CreateService(async (request, cancellationToken) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse(
                HttpStatusCode.OK,
                ResponseWithAlternative(providerAlternative));
        });

        var alternative = await service.TranslateAlternativeAsync(
            source,
            "en",
            "he",
            primary,
            [],
            CancellationToken.None);

        Assert.Equal(providerAlternative, alternative);
        var instructions = GetInstructions(capturedBody!);
        Assert.Contains("semantic senses already represented", instructions);
        Assert.Contains("next most common DISTINCT dictionary sense", instructions);
        Assert.Contains("Prefer null", instructions);
    }

    [Fact]
    public async Task Alternative_Sentence_PromptRequiresSameContextualMeaning()
    {
        string? capturedBody = null;
        var service = CreateService(async (request, cancellationToken) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse(HttpStatusCode.OK, ResponseWithAlternative("קלטתי"));
        });

        var alternative = await service.TranslateAlternativeAsync(
            "I got it",
            "en",
            "he",
            "הבנתי",
            [],
            CancellationToken.None);

        Assert.Equal("קלטתי", alternative);
        var instructions = GetInstructions(capturedBody!);
        var input = GetInput(capturedBody!);
        Assert.Contains("preserve the same contextual meaning", instructions);
        Assert.Contains("never switch to an unrelated dictionary sense", instructions);
        Assert.Contains("trivial grammatical or morphological variations", instructions);
        Assert.Contains("I got it", input);
        Assert.Contains("הבנתי", input);
    }

    [Theory]
    [InlineData("יכול", "יכול")]
    [InlineData("  ", "יכול")]
    [InlineData("Hello!", "hello")]
    public async Task Alternative_UnusableNonNullResult_IsRejected(
        string providerAlternative,
        string primaryTranslation)
    {
        var service = CreateService((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            ResponseWithAlternative(providerAlternative))));

        var exception = await Assert.ThrowsAsync<TranslationServiceException>(() =>
            service.TranslateAlternativeAsync(
                "source",
                "en",
                "he",
                primaryTranslation,
                [],
                CancellationToken.None));

        Assert.Equal(TranslationFailureKind.InvalidResponse, exception.FailureKind);
    }

    [Fact]
    public async Task Alternative_ExplicitNullMeansNoMoreAlternatives()
    {
        var service = CreateService((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            ResponseWithAlternative(null))));

        var alternative = await service.TranslateAlternativeAsync(
            "source",
            "en",
            "he",
            "primary",
            [],
            CancellationToken.None);

        Assert.Null(alternative);
    }

    [Fact]
    public async Task Alternative_ExistingAlternativeCannotBeReturnedAgain()
    {
        var service = CreateService((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            ResponseWithAlternative("פחית!"))));

        var exception = await Assert.ThrowsAsync<TranslationServiceException>(() =>
            service.TranslateAlternativeAsync(
                "can",
                "en",
                "he",
                "יכול",
                ["פחית"],
                CancellationToken.None));

        Assert.Equal(TranslationFailureKind.InvalidResponse, exception.FailureKind);
    }

    [Fact]
    public async Task AlternativeRequest_SuppliesPreviouslyReturnedAlternatives()
    {
        string? capturedBody = null;
        var service = CreateService(async (request, cancellationToken) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse(HttpStatusCode.OK, ResponseWithAlternative("שימורים"));
        });

        await service.TranslateAlternativeAsync(
            "can",
            "en",
            "he",
            "יכול",
            ["פחית"],
            CancellationToken.None);

        var input = GetInput(capturedBody!);
        Assert.Contains("Already returned alternatives", input);
        Assert.Contains("פחית", input);
        Assert.Contains(
            "Do not return the primary translation",
            GetInstructions(capturedBody!));
    }

    [Fact]
    public async Task AlternativeCache_PreventsSecondApiRequest()
    {
        var requestCount = 0;
        var service = CreateService((_, _) =>
        {
            requestCount++;
            return Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                ResponseWithAlternative("קלטתי")));
        });

        var first = await service.TranslateAlternativeAsync(
            "I got it", "en", "he", "הבנתי", [], CancellationToken.None);
        var second = await service.TranslateAlternativeAsync(
            "I got it", "en", "he", "הבנתי", [], CancellationToken.None);

        Assert.Equal("קלטתי", first);
        Assert.Equal(first, second);
        Assert.Equal(1, requestCount);
    }

    [Fact]
    public async Task AlternativeCache_DifferentPrimaryTranslationUsesDifferentKey()
    {
        var requestCount = 0;
        var service = CreateService((_, _) =>
        {
            requestCount++;
            return Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                ResponseWithAlternative($"Alternative {requestCount}")));
        });

        await service.TranslateAlternativeAsync(
            "I got it", "en", "he", "הבנתי", [], CancellationToken.None);
        await service.TranslateAlternativeAsync(
            "I got it", "en", "he", "קלטתי", [], CancellationToken.None);

        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task AlternativeCache_ExistingAlternativesUseDifferentKeys()
    {
        var requestCount = 0;
        var service = CreateService((_, _) =>
        {
            requestCount++;
            return Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                ResponseWithAlternative($"Alternative {requestCount}")));
        });

        await service.TranslateAlternativeAsync(
            "I got it", "en", "he", "הבנתי", [], CancellationToken.None);
        await service.TranslateAlternativeAsync(
            "I got it", "en", "he", "הבנתי", ["קלטתי"], CancellationToken.None);

        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task Alternative_UsesLiveConfiguredTranslationModel()
    {
        var requestBodies = new List<string>();
        var configurationProvider = new MutableConfigurationProvider(
            new OpenAiTranslationConfiguration("test-key", "model-a"));
        var client = new HttpClient(new StubHttpMessageHandler(
            async (request, cancellationToken) =>
            {
                requestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
                return JsonResponse(
                    HttpStatusCode.OK,
                    ResponseWithAlternative($"alternative-{requestBodies.Count}"));
            }))
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        var service = new OpenAiTranslationService(client, configurationProvider);

        await service.TranslateAlternativeAsync(
            "source", "en", "he", "primary", [], CancellationToken.None);
        configurationProvider.Configuration =
            new OpenAiTranslationConfiguration("test-key", "model-b");
        await service.TranslateAlternativeAsync(
            "source", "en", "he", "primary", [], CancellationToken.None);

        Assert.Contains("\"model\":\"model-a\"", requestBodies[0]);
        Assert.Contains("\"model\":\"model-b\"", requestBodies[1]);
    }

    [Fact]
    public async Task Unauthorized_ReturnsFriendlyTypedFailure()
    {
        var service = CreateService((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        var exception = await Assert.ThrowsAsync<TranslationServiceException>(
            () => service.TranslateAsync(
                "Hello",
                "en",
                "he",
                CancellationToken.None));

        Assert.Equal(TranslationFailureKind.Unauthorized, exception.FailureKind);
        Assert.Contains("API key", exception.Message);
    }

    [Fact]
    public async Task Timeout_ReturnsFriendlyTypedFailure()
    {
        var service = CreateService((_, _) =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException()));

        var exception = await Assert.ThrowsAsync<TranslationServiceException>(
            () => service.TranslateAsync(
                "Hello",
                "en",
                "he",
                CancellationToken.None));

        Assert.Equal(TranslationFailureKind.Timeout, exception.FailureKind);
    }

    [Fact]
    public async Task CallerCancellation_IsNotConvertedToProviderFailure()
    {
        var service = CreateService(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        });
        using var cancellation = new CancellationTokenSource();

        var task = service.TranslateAsync("Hello", "en", "he", cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task MissingApiKey_DoesNotSendRequest()
    {
        var requestWasSent = false;
        var service = CreateService(
            (_, _) =>
            {
                requestWasSent = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            },
            isConfigured: false);

        await Assert.ThrowsAsync<TranslationProviderNotConfiguredException>(
            () => service.TranslateAsync(
                "Hello",
                "en",
                "he",
                CancellationToken.None));

        Assert.False(requestWasSent);
    }

    [Fact]
    public async Task EmptyTranslation_ReturnsInvalidResponseFailure()
    {
        var service = CreateService((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            ResponseWithTranslation("   ", "en"))));

        var exception = await Assert.ThrowsAsync<TranslationServiceException>(
            () => service.TranslateAsync(
                "Hello",
                "en",
                "he",
                CancellationToken.None));

        Assert.Equal(TranslationFailureKind.InvalidResponse, exception.FailureKind);
        Assert.Contains("empty translation", exception.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, TranslationFailureKind.QuotaExceeded)]
    [InlineData(HttpStatusCode.ServiceUnavailable, TranslationFailureKind.ProviderUnavailable)]
    public async Task ProviderStatus_IsMappedToFriendlyFailure(
        HttpStatusCode statusCode,
        TranslationFailureKind expectedFailure)
    {
        var service = CreateService((_, _) => Task.FromResult(
            new HttpResponseMessage(statusCode)));

        var exception = await Assert.ThrowsAsync<TranslationServiceException>(
            () => service.TranslateAsync(
                "Hello",
                "en",
                "he",
                CancellationToken.None));

        Assert.Equal(expectedFailure, exception.FailureKind);
    }

    [Fact]
    public async Task NetworkUnavailable_ReturnsFriendlyTypedFailure()
    {
        var service = CreateService((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException()));

        var exception = await Assert.ThrowsAsync<TranslationServiceException>(
            () => service.TranslateAsync(
                "Hello",
                "en",
                "he",
                CancellationToken.None));

        Assert.Equal(
            TranslationFailureKind.NetworkUnavailable,
            exception.FailureKind);
    }

    private static OpenAiTranslationService CreateService(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
        bool isConfigured = true)
    {
        var client = new HttpClient(new StubHttpMessageHandler(send))
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        var configuration = isConfigured
            ? new OpenAiTranslationConfiguration("test-key", "gpt-5.6-luna")
            : null;

        return new OpenAiTranslationService(
            client,
            new StubConfigurationProvider(configuration));
    }

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string json) =>
        new(statusCode)
        {
            Content = new StringContent(
                json,
                System.Text.Encoding.UTF8,
                "application/json")
        };

    private static string GetInstructions(string requestBody)
    {
        using var document = System.Text.Json.JsonDocument.Parse(requestBody);
        return document.RootElement.GetProperty("instructions").GetString()!;
    }

    private static string GetInput(string requestBody)
    {
        using var document = System.Text.Json.JsonDocument.Parse(requestBody);
        return document.RootElement.GetProperty("input").GetString()!;
    }

    private static string ResponseWithTranslation(
        string? translation,
        string detectedLanguage,
        string? correctedSourceText = null,
        string? correctionStatus = null,
        string? targetLanguage = null)
    {
        correctionStatus ??= string.IsNullOrWhiteSpace(correctedSourceText)
            ? "none"
            : "confident";
        targetLanguage ??= detectedLanguage == "he" ? "en" : "he";
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            translatedText = translation,
            correctedSourceText,
            correctionStatus,
            detectedSourceLanguage = detectedLanguage,
            targetLanguage
        });
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            output = new[]
            {
                new
                {
                    content = new[]
                    {
                        new { type = "output_text", text = payload }
                    }
                }
            }
        });
    }

    private static string ResponseWithAlternative(string? alternativeTranslation)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            alternativeTranslation
        });
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            output = new[]
            {
                new
                {
                    content = new[]
                    {
                        new { type = "output_text", text = payload }
                    }
                }
            }
        });
    }

    private sealed class StubConfigurationProvider(
        OpenAiTranslationConfiguration? configuration)
        : IOpenAiConfigurationProvider
    {
        public OpenAiTranslationConfiguration? GetConfiguration() => configuration;
    }

    private sealed class MutableConfigurationProvider(
        OpenAiTranslationConfiguration? configuration)
        : IOpenAiConfigurationProvider
    {
        public OpenAiTranslationConfiguration? Configuration { get; set; } =
            configuration;

        public OpenAiTranslationConfiguration? GetConfiguration() => Configuration;
    }

    private sealed class SharedCredentialStores(string applicationKey)
        : IAiCredentialStoreProvider
    {
        private readonly ISecureApiKeyStore _application =
            new InMemorySecureApiKeyStore(applicationKey);

        public ISecureApiKeyStore GetStore(AiCredentialScope scope) => scope switch
        {
            AiCredentialScope.Application => _application,
            AiCredentialScope.Translation => new InMemorySecureApiKeyStore(),
            AiCredentialScope.QuickChat => new InMemorySecureApiKeyStore(),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
        };
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
