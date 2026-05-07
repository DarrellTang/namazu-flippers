using System.Text.Json.Serialization;

namespace NamazuFlippers.API.Models;

/// <summary>
/// Source-generated JSON serializer context for Saddlebag API types.
/// Uses SnakeCaseLower naming policy to map PascalCase C# properties
/// to snake_case JSON fields (HomeServer → home_server).
/// Registered as partial class — the source generator creates the implementation.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ScanRequest))]
[JsonSerializable(typeof(ScanResponse))]
[JsonSerializable(typeof(ScanItem))]
[JsonSerializable(typeof(List<ScanItem>))]
internal sealed partial class ApiJsonContext : JsonSerializerContext
{
}
