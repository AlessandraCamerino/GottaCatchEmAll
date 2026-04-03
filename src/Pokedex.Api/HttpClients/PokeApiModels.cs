using System.Text.Json.Serialization;

namespace Pokedex.Api.HttpClients;

// Internal models used only to deserialise PokéAPI responses.
// These are NOT exposed outside the HttpClients folder — the service works with domain models.
//
// Design decision: we map only the fields we need (name, flavor_text_entries, habitat, is_legendary).
// PokéAPI returns very large JSON payloads; deserialising only what's needed is faster and safer.

internal record PokeApiSpeciesResponse(
    string Name,

    // PokéAPI uses snake_case; JsonPropertyName maps to C# PascalCase.
    [property: JsonPropertyName("flavor_text_entries")]
    List<FlavorTextEntry> FlavorTextEntries,

    Habitat? Habitat,

    [property: JsonPropertyName("is_legendary")]
    bool IsLegendary
);

internal record FlavorTextEntry(
    [property: JsonPropertyName("flavor_text")]
    string FlavorText,

    Language Language
);

internal record Language(string Name);

internal record Habitat(string Name);
