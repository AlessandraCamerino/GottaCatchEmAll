namespace Pokedex.Api.Models;

// DTO returned by both API endpoints.
//
// Design decision: a single response model is shared between /pokemon and /pokemon/translated.
// Both endpoints return the same shape — only the 'description' field differs (standard vs translated).
// Having one model avoids duplication and makes the contract explicit.
//
// Note: property names are PascalCase in C# but ASP.NET Core serialises them as camelCase by default
// (configured in Program.cs via JsonNamingPolicy.CamelCase), matching the expected JSON output.
public record PokemonResponse(
    string Name,
    string Description,
    string? Habitat,
    bool IsLegendary
);
