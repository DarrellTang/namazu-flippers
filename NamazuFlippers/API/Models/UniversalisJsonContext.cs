using System.Text.Json.Serialization;

namespace NamazuFlippers.API.Models;

/// <summary>
/// Source-generated JSON serializer context for Universalis API types.
/// Uses CamelCase naming policy (NOT the snake_case policy on <see cref="ApiJsonContext"/>,
/// which would mangle Universalis's native camelCase field names like "listingsCount").
/// Registered as partial class — the source generator creates the implementation.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(UniversalisMultiResponse))]
[JsonSerializable(typeof(UniversalisItem))]
[JsonSerializable(typeof(UniversalisListing))]
[JsonSerializable(typeof(UniversalisHistory))]
[JsonSerializable(typeof(System.Collections.Generic.Dictionary<string, UniversalisItem>))]
internal sealed partial class UniversalisJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
