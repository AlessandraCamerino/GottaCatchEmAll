using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Pokedex.Api.Exceptions;
using Pokedex.Api.HttpClients;
using Pokedex.Api.Services;

namespace Pokedex.Tests.Unit;

// Unit tests for PokemonService.
//
// Design decision: we use a real MemoryCache instance rather than mocking IMemoryCache.
// Mocking IMemoryCache is complex because GetOrCreateAsync is an extension method that calls
// TryGetValue + CreateEntry under the hood — mocking extension methods with NSubstitute
// requires brittle setup. A real in-memory cache has no I/O, no side effects, and is fast:
// it behaves identically in tests and production.
//
// IPokeApiClient and ITranslationClient ARE mocked — they represent external HTTP calls
// that we don't want to make in unit tests.
public class PokemonServiceTests
{
    private readonly IPokeApiClient _pokeApiClient = Substitute.For<IPokeApiClient>();
    private readonly ITranslationClient _translationClient = Substitute.For<ITranslationClient>();

    // Real MemoryCache — see class comment above.
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    private PokemonService CreateSut() => new(_pokeApiClient, _translationClient, _cache);

    // --- GetPokemonAsync ---

    [Fact]
    public async Task GetPokemonAsync_WhenPokemonExists_ReturnsCorrectResponse()
    {
        // Arrange
        var species = BuildSpecies("mewtwo", "A very strong pokemon.", habitat: "rare", isLegendary: true);
        _pokeApiClient.GetSpeciesAsync("mewtwo").Returns(species);

        // Act
        var result = await CreateSut().GetPokemonAsync("mewtwo");

        // Assert
        result.Name.Should().Be("mewtwo");
        result.Description.Should().Be("A very strong pokemon.");
        result.Habitat.Should().Be("rare");
        result.IsLegendary.Should().BeTrue();
    }

    [Fact]
    public async Task GetPokemonAsync_WhenPokemonNotFound_ThrowsPokemonNotFoundException()
    {
        // Arrange
        _pokeApiClient.GetSpeciesAsync("unknown").ReturnsNull();

        // Act
        var act = () => CreateSut().GetPokemonAsync("unknown");

        // Assert
        await act.Should().ThrowAsync<PokemonNotFoundException>()
            .WithMessage("*unknown*");
    }

    [Fact]
    public async Task GetPokemonAsync_SanitisesFormFeedAndNewlinesInDescription()
    {
        // Arrange — PokéAPI embeds \f (form feed) and \n as line separators in flavor text.
        var species = BuildSpecies("bulbasaur", "A strange\fseed\nPokémon.", habitat: "grassland");
        _pokeApiClient.GetSpeciesAsync("bulbasaur").Returns(species);

        // Act
        var result = await CreateSut().GetPokemonAsync("bulbasaur");

        // Assert
        result.Description.Should().Be("A strange seed Pokémon.");
    }

    // --- GetTranslatedPokemonAsync: translation type selection ---

    [Fact]
    public async Task GetTranslatedPokemonAsync_WhenLegendary_UsesYodaTranslation()
    {
        // Arrange
        var species = BuildSpecies("mewtwo", "Original description.", habitat: "rare", isLegendary: true);
        _pokeApiClient.GetSpeciesAsync("mewtwo").Returns(species);
        _translationClient
            .TranslateAsync("Original description.", TranslationType.Yoda)
            .Returns("Yoda translated.");

        // Act
        var result = await CreateSut().GetTranslatedPokemonAsync("mewtwo");

        // Assert
        result.Description.Should().Be("Yoda translated.");
        await _translationClient.Received(1)
            .TranslateAsync("Original description.", TranslationType.Yoda, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTranslatedPokemonAsync_WhenCaveHabitat_UsesYodaTranslation()
    {
        // Arrange — cave habitat forces Yoda regardless of legendary status
        var species = BuildSpecies("zubat", "Lives in caves.", habitat: "cave", isLegendary: false);
        _pokeApiClient.GetSpeciesAsync("zubat").Returns(species);
        _translationClient
            .TranslateAsync("Lives in caves.", TranslationType.Yoda)
            .Returns("In caves, it lives.");

        // Act
        var result = await CreateSut().GetTranslatedPokemonAsync("zubat");

        // Assert
        result.Description.Should().Be("In caves, it lives.");
        await _translationClient.Received(1)
            .TranslateAsync("Lives in caves.", TranslationType.Yoda, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTranslatedPokemonAsync_WhenNotCaveAndNotLegendary_UsesShakespeareTranslation()
    {
        // Arrange
        var species = BuildSpecies("bulbasaur", "A grass Pokémon.", habitat: "grassland", isLegendary: false);
        _pokeApiClient.GetSpeciesAsync("bulbasaur").Returns(species);
        _translationClient
            .TranslateAsync("A grass Pokémon.", TranslationType.Shakespeare)
            .Returns("A grass Pokémon, forsooth.");

        // Act
        var result = await CreateSut().GetTranslatedPokemonAsync("bulbasaur");

        // Assert
        result.Description.Should().Be("A grass Pokémon, forsooth.");
        await _translationClient.Received(1)
            .TranslateAsync("A grass Pokémon.", TranslationType.Shakespeare, Arg.Any<CancellationToken>());
    }

    // --- GetTranslatedPokemonAsync: fallback behaviour ---

    [Fact]
    public async Task GetTranslatedPokemonAsync_WhenTranslationFails_FallsBackToStandardDescription()
    {
        // Arrange — TranslationClient returns null on failure (rate limit, server error, etc.)
        var species = BuildSpecies("pikachu", "An electric mouse.", habitat: "forest", isLegendary: false);
        _pokeApiClient.GetSpeciesAsync("pikachu").Returns(species);
        _translationClient
            .TranslateAsync(Arg.Any<string>(), Arg.Any<TranslationType>())
            .ReturnsNull();

        // Act
        var result = await CreateSut().GetTranslatedPokemonAsync("pikachu");

        // Assert — spec says: if translation fails, use the standard description
        result.Description.Should().Be("An electric mouse.");
    }

    [Fact]
    public async Task GetTranslatedPokemonAsync_WhenPokemonNotFound_ThrowsPokemonNotFoundException()
    {
        // Arrange
        _pokeApiClient.GetSpeciesAsync("unknown").ReturnsNull();

        // Act
        var act = () => CreateSut().GetTranslatedPokemonAsync("unknown");

        // Assert
        await act.Should().ThrowAsync<PokemonNotFoundException>();
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
            FlavorTextEntries:
            [
                // Include a non-English entry to verify we pick only English descriptions
                new FlavorTextEntry("Descripción en español.", new Language("es")),
                new FlavorTextEntry(description, new Language("en"))
            ],
            Habitat: habitat is not null ? new Habitat(habitat) : null,
            IsLegendary: isLegendary
        );
    }
}
