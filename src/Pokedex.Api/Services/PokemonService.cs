using Pokedex.Api.Exceptions;
using Pokedex.Api.HttpClients;
using Pokedex.Api.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Pokedex.Api.Services;

// The class is internal: consumers depend on IPokemonService (public interface), not the concrete type.
// The DI container resolves internal classes without issues.
internal class PokemonService(
    IPokeApiClient pokeApiClient,
    ITranslationClient translationClient,
    IMemoryCache cache) : IPokemonService
{
    // Cache TTL for Pokémon species data.
    // Design decision: Pokémon data from PokéAPI is essentially static (it rarely changes),
    // so a long TTL is safe and reduces outbound HTTP calls.
    private static readonly TimeSpan SpeciesCacheDuration = TimeSpan.FromHours(1);

    // Cache TTL for translated descriptions.
    // Translations are deterministic for a given input, so we cache them aggressively.
    // This also mitigates the FunTranslations free-tier rate limit of 5 requests/hour.
    private static readonly TimeSpan TranslationCacheDuration = TimeSpan.FromHours(24);

    public async Task<PokemonResponse> GetPokemonAsync(string name, CancellationToken ct = default)
    {
        var species = await GetSpeciesCachedAsync(name, ct);
        var description = ExtractEnglishDescription(species);
        return new PokemonResponse(species.Name, description, species.Habitat?.Name, species.IsLegendary);
    }

    public async Task<PokemonResponse> GetTranslatedPokemonAsync(string name, CancellationToken ct = default)
    {
        var species = await GetSpeciesCachedAsync(name, ct);
        var description = ExtractEnglishDescription(species);

        var translationType = ResolveTranslationType(species);
        var cacheKey = $"translation:{translationType}:{description}";

        var translated = await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TranslationCacheDuration;
            return await translationClient.TranslateAsync(description, translationType, ct);
        });

        // Fall back to the standard description if translation failed or returned null.
        var finalDescription = translated ?? description;
        return new PokemonResponse(species.Name, finalDescription, species.Habitat?.Name, species.IsLegendary);
    }

    // --- Private helpers ---

    private async Task<PokeApiSpeciesResponse> GetSpeciesCachedAsync(string name, CancellationToken ct)
    {
        var cacheKey = $"species:{name.ToLowerInvariant()}";

        var species = await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = SpeciesCacheDuration;
            return await pokeApiClient.GetSpeciesAsync(name, ct);
        });

        // If PokéAPI returned null (404), throw domain exception.
        // The global exception handler maps this to HTTP 404.
        if (species is null)
            throw new PokemonNotFoundException(name);

        return species;
    }

    private static string ExtractEnglishDescription(PokeApiSpeciesResponse species)
    {
        // PokéAPI returns multiple flavor texts (one per game version, multiple languages).
        // We pick the first English entry and sanitise line-break characters that the API embeds.
        //
        // Design note: in production we might prefer a specific game version (e.g. latest),
        // but for this exercise any English description is acceptable per the spec.
        var raw = species.FlavorTextEntries
            .FirstOrDefault(e => e.Language.Name == "en")
            ?.FlavorText ?? string.Empty;

        // PokéAPI encodes line breaks as \f (form feed) and \n — normalise to a single space.
        return raw.Replace("\f", " ").Replace("\n", " ").Trim();
    }

    private static TranslationType ResolveTranslationType(PokeApiSpeciesResponse species)
    {
        // Translation rule from spec:
        //   cave habitat OR legendary → Yoda
        //   everything else           → Shakespeare
        var isCave = species.Habitat?.Name == "cave";
        return (isCave || species.IsLegendary) ? TranslationType.Yoda : TranslationType.Shakespeare;
    }
}
