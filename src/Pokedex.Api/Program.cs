using Pokedex.Api.Exceptions;
using Pokedex.Api.HttpClients;
using Pokedex.Api.Services;
using Scalar.AspNetCore;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// --- Controllers ---
// Design decision: we use AddControllers (not AddControllersWithViews or AddMvc) because
// this is a pure API — no Razor views needed. Keeps the dependency surface minimal.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialise all response properties as camelCase (e.g. IsLegendary → isLegendary).
        // This matches the expected JSON output in the spec without requiring [JsonPropertyName]
        // attributes on every response model.
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// --- In-memory cache ---
// Design decision: IMemoryCache is sufficient for a single-instance deployment.
builder.Services.AddMemoryCache();

// --- Typed HTTP clients ---
// Design decision: typed clients are registered with IHttpClientFactory under the hood.
// This avoids socket exhaustion (no manual new HttpClient()) and keeps base URLs configured centrally.
// Base URLs are read from appsettings.json so they can be overridden per environment without recompiling.
builder.Services.AddHttpClient<IPokeApiClient, PokeApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ExternalApis:PokeApiBaseUrl"]!);
});

builder.Services.AddHttpClient<ITranslationClient, TranslationClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ExternalApis:TranslationApiBaseUrl"]!);
});

// --- Application services ---
builder.Services.AddScoped<IPokemonService, PokemonService>();

// --- OpenAPI + Scalar ---
// Design decision: we use Microsoft.AspNetCore.OpenApi (built-in to .NET 9) to generate
// the OpenAPI document, and Scalar as the UI renderer.
// This is the recommended approach for .NET 9+ — Swashbuckle is no longer the default
// and Microsoft is investing in this native integration instead.
builder.Services.AddOpenApi();

// --- ProblemDetails (RFC 7807) ---
// Design decision: instead of wrapping every action in try/catch, we register a global
// exception handler that maps our domain exceptions to the appropriate HTTP status codes.
// This keeps controllers clean and error handling centralised.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

// Expose the OpenAPI JSON document at /openapi/v1.json
// and the Scalar UI at /scalar/v1
app.MapOpenApi();
app.MapScalarApiReference();

// HTTPS redirection only outside Development to avoid port-resolution warnings
// when running locally or in the test host.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.MapControllers();

app.Run();

// Needed for WebApplicationFactory in integration tests to access the Program class.
public partial class Program { }
