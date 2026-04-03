using Microsoft.AspNetCore.Mvc;
using Pokedex.Api.Models;
using Pokedex.Api.Services;

namespace Pokedex.Api.Controllers;

// Design decision: the controller is intentionally thin.
// It handles only HTTP concerns: routing, input validation, status codes, and delegating to the service.
// No business logic lives here — that belongs in PokemonService.
//
// [ApiController] gives us automatic model validation and ProblemDetails error responses.
[ApiController]
[Route("pokemon")]
public class PokemonController(IPokemonService pokemonService) : ControllerBase
{
    // GET /pokemon/{name}
    // Returns standard Pokémon information.
    [HttpGet("{name}")]
    [ProducesResponseType<PokemonResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPokemon(string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new ProblemDetails { Title = "Invalid name", Status = 400, Detail = "Pokémon name cannot be empty." });

        var pokemon = await pokemonService.GetPokemonAsync(name, ct);
        return Ok(pokemon);
    }

    // GET /pokemon/translated/{name}
    // Returns Pokémon information with a fun translated description (Yoda or Shakespeare).
    // Falls back to the standard description if translation is unavailable.
    [HttpGet("translated/{name}")]
    [ProducesResponseType<PokemonResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTranslated(string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new ProblemDetails { Title = "Invalid name", Status = 400, Detail = "Pokémon name cannot be empty." });

        var pokemon = await pokemonService.GetTranslatedPokemonAsync(name, ct);
        return Ok(pokemon);
    }
}
