using System.Text.Json.Serialization;

namespace Pokedex.Api.HttpClients;

// Internal models to deserialise FunTranslations API responses.
// Example response:
// {
//   "success": { "total": 1 },
//   "contents": { "translated": "...", "text": "...", "translation": "yoda" }
// }

internal record TranslationResponse(
    TranslationContents? Contents
);

internal record TranslationContents(
    [property: JsonPropertyName("translated")]
    string Translated
);
