using Pokedex.Api.Models;

namespace Pokedex.Api.Services;

public interface IPokemonService
{
    // Returns basic Pokémon information with the standard description.
    // Throws PokemonNotFoundException if the name is not recognised by PokéAPI.
    Task<PokemonResponse> GetPokemonAsync(string name, CancellationToken ct = default);

    // Returns Pokémon information with a fun translated description.
    // Translation rules:
    //   - cave habitat OR legendary → Yoda
    //   - everything else          → Shakespeare
    //   - translation failure      → falls back to standard description
    Task<PokemonResponse> GetTranslatedPokemonAsync(string name, CancellationToken ct = default);
}
