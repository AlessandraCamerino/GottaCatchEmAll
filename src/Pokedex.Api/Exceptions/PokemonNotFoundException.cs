namespace Pokedex.Api.Exceptions;

// Custom exception thrown when PokéAPI returns 404 for a given Pokémon name.
//
// Design decision: instead of returning null or a Result<T> from the service,
// we throw a domain-specific exception. The global exception handler in Program.cs
// catches it and maps it to an HTTP 404 ProblemDetails response.
//
// This keeps the controller thin (no null checks) and centralises error-to-HTTP mapping.
public class PokemonNotFoundException(string name)
    : Exception($"Pokémon '{name}' not found.");
