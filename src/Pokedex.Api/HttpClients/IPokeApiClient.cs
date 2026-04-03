namespace Pokedex.Api.HttpClients;

// Abstraction over the PokéAPI HTTP call.
//
// Design decision: we define an interface so that in unit tests we can substitute
// the real HTTP call with a fake (NSubstitute). Without this interface we would
// need to mock HttpClient directly, which is fragile and verbose.
//
// The interface is internal because PokeApiSpeciesResponse (its return type) is also internal —
// these are implementation details of the HttpClients layer and should not leak outside the assembly.
// The DI container resolves internal types at runtime without issues.
internal interface IPokeApiClient
{
    // Returns null if the Pokémon is not found (HTTP 404).
    // Any other non-success status code throws an HttpRequestException.
    Task<PokeApiSpeciesResponse?> GetSpeciesAsync(string name, CancellationToken ct = default);
}
