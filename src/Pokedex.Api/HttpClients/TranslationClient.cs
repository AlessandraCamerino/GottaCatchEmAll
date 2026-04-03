using System.Text.Json;

namespace Pokedex.Api.HttpClients;

// Typed HTTP client for the FunTranslations API.
//
// Design decision: a single client handles both Yoda and Shakespeare translations.
// The two endpoints share the same base URL, auth model, and response shape —
// merging them avoids registering two near-identical typed clients.
//
// API details (from https://funtranslations.mercxry.me/openapi.yaml):
//   Base URL : https://api.funtranslations.mercxry.me/v1
//   Method   : POST
//   Body     : application/x-www-form-urlencoded  { text: "..." }
//   Rate limit: 5 requests/minute (returns 429 on exceeded)
internal class TranslationClient(HttpClient httpClient) : ITranslationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<string?> TranslateAsync(string text, TranslationType type, CancellationToken ct = default)
    {
        var endpoint = type switch
        {
            TranslationType.Yoda        => "yoda",
            TranslationType.Shakespeare => "shakespeare",
            _                           => throw new ArgumentOutOfRangeException(nameof(type))
        };

        // The API accepts application/x-www-form-urlencoded body.
        // FormUrlEncodedContent handles encoding automatically — no need for HttpUtility.UrlEncode.
        var body = new FormUrlEncodedContent([new KeyValuePair<string, string>("text", text)]);
        var response = await httpClient.PostAsync($"translate/{endpoint}", body, ct);

        // If translation fails for any reason (rate limit 429, server error, etc.)
        // we return null so the caller can fall back to the original description.
        // Design note: swallowing non-success here is intentional per spec.
        if (!response.IsSuccessStatusCode)
            return null;

        // Defensive try-catch: if the response body is not valid JSON for any unexpected reason
        // (e.g. a CDN returning an HTML error page with status 200), return null and fall back.
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(ct);
            var result = await JsonSerializer.DeserializeAsync<TranslationResponse>(stream, JsonOptions, ct);
            return result?.Contents?.Translated;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
