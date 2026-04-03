namespace Pokedex.Api.HttpClients;

internal enum TranslationType
{
    Yoda,
    Shakespeare
}

// Abstraction over the FunTranslations API.
//
// Design decision: returns null instead of throwing when translation fails.
// The spec explicitly says "if you can't translate, use the standard description".
// Returning null lets the service apply the fallback without catching exceptions in business logic.
//
// Both the interface and TranslationType are internal — they are implementation details
// of the HttpClients layer and should not leak into the public API surface.
internal interface ITranslationClient
{
    Task<string?> TranslateAsync(string text, TranslationType type, CancellationToken ct = default);
}
