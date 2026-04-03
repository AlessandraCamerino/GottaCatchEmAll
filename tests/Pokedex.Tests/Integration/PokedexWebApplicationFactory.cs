using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Pokedex.Api.HttpClients;

namespace Pokedex.Tests.Integration;

// Custom WebApplicationFactory that replaces the real HTTP clients with NSubstitute fakes.
//
// Design decision: we replace IPokeApiClient and ITranslationClient at the DI level
// rather than swapping HttpMessageHandlers. This approach:
//   1. Is simpler — no need to serialise/deserialise JSON in test setup
//   2. Tests the Controller → Service layer end-to-end (routing, status codes, JSON serialisation)
//   3. Keeps the "what the external API returns" concern at a higher, more readable level
//
// The trade-off: we do NOT test the PokeApiClient/TranslationClient HTTP logic itself.
// That is covered separately in unit tests for those classes (if added in the future).
public class PokedexWebApplicationFactory : WebApplicationFactory<Program>
{
    // Exposed so individual tests can configure return values.
    // IPokeApiClient is internal to Pokedex.Api (InternalsVisibleTo grants access to this assembly),
    // so the property must also be internal — C# requires consistency between a member's
    // accessibility and the accessibility of its type.
    internal IPokeApiClient PokeApiClient { get; } = Substitute.For<IPokeApiClient>();
    internal ITranslationClient TranslationClient { get; } = Substitute.For<ITranslationClient>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real typed client registrations and replace with fakes.
            // We remove by the interface type so the DI container resolves our substitutes.
            RemoveService<IPokeApiClient>(services);
            RemoveService<ITranslationClient>(services);

            services.AddSingleton(PokeApiClient);
            services.AddSingleton(TranslationClient);
        });
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(T));
        if (descriptor is not null)
            services.Remove(descriptor);
    }
}
