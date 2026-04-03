using System.Net;
using System.Text.Json;

namespace Pokedex.Api.HttpClients;

// Typed HTTP client for PokéAPI.
//
// Design decision: typed clients (registered via AddHttpClient<IPokeApiClient, PokeApiClient>)
// are preferred over named clients because:
//   1. No magic strings — the type itself is the key.
//   2. The client is injected directly as a dependency, making it mockable in tests.
//   3. BaseAddress and other configuration are encapsulated here, not scattered around.
//
// The HttpClient instance is injected by IHttpClientFactory (managed lifetime, avoids socket exhaustion).
internal class PokeApiClient(HttpClient httpClient) : IPokeApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<PokeApiSpeciesResponse?> GetSpeciesAsync(string name, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync($"api/v2/pokemon-species/{name.ToLowerInvariant()}", ct);

        // 404 means the Pokémon doesn't exist — return null so the service can throw a domain exception.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<PokeApiSpeciesResponse>(stream, JsonOptions, ct);
    }
}
