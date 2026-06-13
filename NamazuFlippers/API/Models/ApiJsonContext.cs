using System.Text.Json.Serialization;
using NamazuFlippers.Core;
using NamazuFlippers.Data;

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
[JsonSerializable(typeof(RawScanResponse))]
[JsonSerializable(typeof(RawScanItem))]
[JsonSerializable(typeof(List<RawScanItem>))]
[JsonSerializable(typeof(RankedOpportunity))]
[JsonSerializable(typeof(RouteStop))]
[JsonSerializable(typeof(ScanWarning))]
[JsonSerializable(typeof(ScanEngineResult))]
[JsonSerializable(typeof(ScanCacheEnvelope))]
[JsonSerializable(typeof(SessionState))]
[JsonSerializable(typeof(FlipPosition))]
[JsonSerializable(typeof(FlipPositionStatus))]
[JsonSerializable(typeof(FlipLedgerEnvelope))]
[JsonSerializable(typeof(Dictionary<int, bool>))]
[JsonSerializable(typeof(List<RankedOpportunity>))]
[JsonSerializable(typeof(List<RouteStop>))]
[JsonSerializable(typeof(List<ScanWarning>))]
[JsonSerializable(typeof(List<FlipPosition>))]
internal sealed partial class ApiJsonContext : JsonSerializerContext
{
}
