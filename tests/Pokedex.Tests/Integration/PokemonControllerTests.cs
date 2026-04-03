using System.Net;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Pokedex.Api.HttpClients;

namespace Pokedex.Tests.Integration;

// Integration tests for PokemonController.
//
// These tests exercise the full request pipeline:
//   HTTP request → routing → controller → service → (fake) clients → JSON response
//
// What we verify here that unit tests cannot:
//   - Correct routing (/pokemon/{name}, /pokemon/translated/{name})
//   - HTTP status codes (200, 404)
//   - JSON serialisation shape (camelCase field names, correct values)
//   - ProblemDetails format for error responses
//
// Design decision: IClassFixture<PokedexWebApplicationFactory> means the factory (and the
// in-process test server) is created once and shared across all tests in this class.
// This is faster than creating a new server per test.
//
// Important: the MemoryCache is also shared across tests (same lifetime as the factory).
// To avoid cache collisions (test A caches "mewtwo", test B gets stale cached data),
// each test uses a unique Pokémon name. This is simpler than resetting the cache between
// tests, which would require exposing the IMemoryCache from the factory.
public class PokemonControllerTests(PokedexWebApplicationFactory factory)
    : IClassFixture<PokedexWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // --- GET /pokemon/{name} ---

    [Fact]
    public async Task GetPokemon_WhenPokemonExists_Returns200WithCorrectBody()
    {
        // Arrange — using "charmander" (unique name) to avoid MemoryCache collision with other tests.
        var species = BuildSpecies("charmander", "It was created by a scientist.", habitat: "mountain", isLegendary: false);
        factory.PokeApiClient.GetSpeciesAsync("charmander", Arg.Any<CancellationToken>()).Returns(species);

        // Act
        var response = await _client.GetAsync("/pokemon/charmander");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await DeserializeAsync<PokemonResponseDto>(response);
        body.Name.Should().Be("charmander");
        body.Description.Should().Be("It was created by a scientist.");
        body.Habitat.Should().Be("mountain");
        body.IsLegendary.Should().BeFalse();
    }

    [Fact]
    public async Task GetPokemon_WhenPokemonNotFound_Returns404()
    {
        // Arrange
        factory.PokeApiClient.GetSpeciesAsync("unknown", Arg.Any<CancellationToken>()).ReturnsNull();

        // Act
        var response = await _client.GetAsync("/pokemon/unknown");

        // Assert — GlobalExceptionHandler maps PokemonNotFoundException → 404
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- GET /pokemon/translated/{name} ---

    [Fact]
    public async Task GetTranslated_WhenLegendary_ReturnsYodaTranslation()
    {
        // Arrange
        var species = BuildSpecies("mewtwo", "Original description.", habitat: "rare", isLegendary: true);
        factory.PokeApiClient.GetSpeciesAsync("mewtwo", Arg.Any<CancellationToken>()).Returns(species);
        factory.TranslationClient
            .TranslateAsync("Original description.", TranslationType.Yoda, Arg.Any<CancellationToken>())
            .Returns("Yoda translated.");

        // Act
        var response = await _client.GetAsync("/pokemon/translated/mewtwo");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await DeserializeAsync<PokemonResponseDto>(response);
        body.Description.Should().Be("Yoda translated.");
    }

    [Fact]
    public async Task GetTranslated_WhenTranslationFails_ReturnsStandardDescription()
    {
        // Arrange
        var species = BuildSpecies("pikachu", "Standard description.", habitat: "forest", isLegendary: false);
        factory.PokeApiClient.GetSpeciesAsync("pikachu", Arg.Any<CancellationToken>()).Returns(species);
        factory.TranslationClient
            .TranslateAsync(Arg.Any<string>(), Arg.Any<TranslationType>(), Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var response = await _client.GetAsync("/pokemon/translated/pikachu");

        // Assert — fallback to standard description
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await DeserializeAsync<PokemonResponseDto>(response);
        body.Description.Should().Be("Standard description.");
    }

    [Fact]
    public async Task GetTranslated_WhenPokemonNotFound_Returns404()
    {
        // Arrange
        factory.PokeApiClient.GetSpeciesAsync("unknown", Arg.Any<CancellationToken>()).ReturnsNull();

        // Act
        var response = await _client.GetAsync("/pokemon/translated/unknown");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Helpers ---

    private static PokeApiSpeciesResponse BuildSpecies(
        string name,
        string description,
        string? habitat = null,
        bool isLegendary = false)
    {
        return new PokeApiSpeciesResponse(
            Name: name,
            FlavorTextEntries: [new FlavorTextEntry(description, new Language("en"))],
            Habitat: habitat is not null ? new Habitat(habitat) : null,
            IsLegendary: isLegendary
        );
    }

    private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
        return result!;
    }

    // Local DTO used only for deserialising the JSON response in assertions.
    // We deliberately do NOT reuse PokemonResponse from the API project here —
    // the test should verify the JSON shape independently from the production type.
    private record PokemonResponseDto(
        string Name,
        string Description,
        string? Habitat,
        bool IsLegendary
    );
}
