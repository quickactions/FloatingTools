using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace FloatingTools.App.Services.OpenAI;

public sealed class OpenAiConnectionTester(
    HttpClient httpClient,
    IOpenAiConfigurationProvider configurationProvider)
    : IOpenAiConnectionTester
{
    public async Task<ConnectionTestResult> TestAsync(
        CancellationToken cancellationToken)
    {
        var configuration = configurationProvider.GetConfiguration();
        if (configuration is null)
        {
            return new(false, "API key is not configured.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"models/{Uri.EscapeDataString(configuration.Model)}");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", configuration.ApiKey);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            return response.StatusCode switch
            {
                HttpStatusCode.OK => new(true, "Connection successful."),
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    new(false, "Invalid API key."),
                HttpStatusCode.TooManyRequests =>
                    new(false, "API quota unavailable."),
                _ when (int)response.StatusCode >= 500 =>
                    new(false, "OpenAI is temporarily unavailable."),
                _ => new(false, "Could not verify the OpenAI connection.")
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, "Connection test timed out.");
        }
        catch (HttpRequestException)
        {
            return new(false, "Network error.");
        }
    }
}
