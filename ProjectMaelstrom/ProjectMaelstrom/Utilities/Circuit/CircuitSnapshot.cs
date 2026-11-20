using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectMaelstrom.Utilities.Contracts;

namespace ProjectMaelstrom.Utilities.Circuit;

/// <summary>
/// Layer 1 Read-Only Sensory Bus packet.
/// Note: Serializes WorldBits as raw ulong + explicit string array to avoid the .NET 9
/// System.Text.Json JsonStringEnumConverter composite flags regression (dotnet/runtime#114770).
/// </summary>
public sealed record CircuitSnapshot(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("timestamp_utc_ticks")] long TimestampUtcTicks,
    [property: JsonPropertyName("world_bits_raw")] ulong WorldBitsRaw,
    [property: JsonPropertyName("active_bit_names")] IReadOnlyList<string> ActiveBitNames,
    [property: JsonPropertyName("registers")] CircuitRegisters Registers,
    [property: JsonPropertyName("anchor_confidences")] IReadOnlyDictionary<string, float> AnchorConfidences)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static CircuitSnapshot FromJson(string json) =>
        JsonSerializer.Deserialize<CircuitSnapshot>(json, JsonOptions)
        ?? throw new InvalidOperationException("Failed to deserialize CircuitSnapshot.");

    public bool HasBit(WorldBits bit) => ((WorldBits)WorldBitsRaw & bit) == bit;
}

public readonly record struct CircuitRegisters(
    [property: JsonPropertyName("gold")] int Gold,
    [property: JsonPropertyName("energy")] int Energy,
    [property: JsonPropertyName("candidate_row_index")] int CandidateRowIndex,
    [property: JsonPropertyName("candidate_unit_price")] int CandidateUnitPrice,
    [property: JsonPropertyName("candidate_quantity")] int CandidateQuantity);
