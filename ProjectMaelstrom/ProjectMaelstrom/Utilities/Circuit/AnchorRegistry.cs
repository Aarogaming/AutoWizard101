using System.Globalization;
using System.Text.Json;
using ProjectMaelstrom.Utilities.Contracts;

namespace ProjectMaelstrom.Utilities.Circuit;

public sealed record AnchorSpec(
    string Id,
    NormalizedRect Bounds,
    string ExpectedOcr,
    ulong FallbackHash,
    int RowCount,
    double RowStrideY,
    IReadOnlyDictionary<string, (double Start, double End)> Columns,
    string Description);

/// <summary>
/// Parser and registry for declarative UI layout anchors (ui_anchors.v1.json).
/// </summary>
public sealed class AnchorRegistry
{
    private readonly Dictionary<string, AnchorSpec> _anchors = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, AnchorSpec> Anchors => _anchors;

    public static AnchorRegistry Load(string json)
    {
        var registry = new AnchorRegistry();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("anchors", out var anchorsObj) || anchorsObj.ValueKind != JsonValueKind.Object)
        {
            return registry;
        }

        foreach (var prop in anchorsObj.EnumerateObject())
        {
            var id = prop.Name;
            var obj = prop.Value;

            double x = 0, y = 0, w = 0, h = 0;
            if (obj.TryGetProperty("rect", out var rectArr) && rectArr.GetArrayLength() == 4)
            {
                x = rectArr[0].GetDouble();
                y = rectArr[1].GetDouble();
                w = rectArr[2].GetDouble();
                h = rectArr[3].GetDouble();
            }

            var expectedOcr = obj.TryGetProperty("expected_ocr", out var ocrProp) ? ocrProp.GetString() ?? "" : "";
            var desc = obj.TryGetProperty("description", out var descProp) ? descProp.GetString() ?? "" : "";

            ulong fallbackHash = 0;
            if (obj.TryGetProperty("fallback_image_hash", out var hashProp) && hashProp.GetString() is { } hashStr && hashStr.Length >= 16)
            {
                // Parse first 16 hex digits (64 bits)
                ulong.TryParse(hashStr[..16], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out fallbackHash);
            }

            int rowCount = obj.TryGetProperty("row_count", out var rcProp) ? rcProp.GetInt32() : 0;
            double rowStrideY = obj.TryGetProperty("row_stride_y", out var rsProp) ? rsProp.GetDouble() : 0.0;

            var cols = new Dictionary<string, (double Start, double End)>(StringComparer.OrdinalIgnoreCase);
            if (obj.TryGetProperty("columns", out var colsObj) && colsObj.ValueKind == JsonValueKind.Object)
            {
                foreach (var c in colsObj.EnumerateObject())
                {
                    if (c.Value.ValueKind == JsonValueKind.Array && c.Value.GetArrayLength() == 2)
                    {
                        cols[c.Name] = (c.Value[0].GetDouble(), c.Value[1].GetDouble());
                    }
                }
            }

            registry._anchors[id] = new AnchorSpec(
                Id: id,
                Bounds: new NormalizedRect(x, y, w, h),
                ExpectedOcr: expectedOcr,
                FallbackHash: fallbackHash,
                RowCount: rowCount,
                RowStrideY: rowStrideY,
                Columns: cols,
                Description: desc);
        }

        return registry;
    }

    public static AnchorRegistry LoadDefault(string? basePath = null)
    {
        string[] candidates =
        [
            Path.Combine(basePath ?? "", "Resources", "ui_anchors.v1.json"),
            Path.Combine(basePath ?? "", "ProjectMaelstrom", "Resources", "ui_anchors.v1.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "ui_anchors.v1.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "ProjectMaelstrom", "ProjectMaelstrom", "Resources", "ui_anchors.v1.json")
        ];

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                var content = File.ReadAllText(path);
                return Load(content);
            }
        }

        // Embedded fallback default anchors
        return Load(FallbackDefaultJson);
    }

    public bool TryGetAnchor(string id, out AnchorSpec spec) => _anchors.TryGetValue(id, out spec!);

    public AnchorSpec GetAnchor(string id)
    {
        if (_anchors.TryGetValue(id, out var spec)) return spec;
        throw new KeyNotFoundException($"UI anchor '{id}' not found in registry.");
    }

    private const string FallbackDefaultJson = """
    {
      "client_version": "2026.11",
      "aspect_ratio": "16:9",
      "anchors": {
        "bazaar_refresh_button": { "rect": [0.285, 0.210, 0.045, 0.038], "expected_ocr": "", "description": "Refresh button" },
        "bazaar_reagent_tab": { "rect": [0.335, 0.210, 0.048, 0.038], "expected_ocr": "", "description": "Reagents tab" },
        "bazaar_item_table": { "rect": [0.260, 0.275, 0.420, 0.440], "row_count": 10, "row_stride_y": 0.044, "columns": { "name": [0.00, 0.58], "quantity": [0.59, 0.74], "price": [0.75, 1.00] }, "description": "Item listings table" },
        "bazaar_buy_more_button": { "rect": [0.415, 0.745, 0.110, 0.042], "expected_ocr": "BUY MORE", "description": "Buy more button" },
        "bazaar_buy_confirm_dialog_button": { "rect": [0.440, 0.580, 0.095, 0.042], "expected_ocr": "BUY", "description": "Confirm purchase button" },
        "bazaar_player_gold_label": { "rect": [0.540, 0.750, 0.120, 0.035], "expected_ocr": "", "description": "Player gold display" },
        "native_idle_banner": { "rect": [0.350, 0.450, 0.300, 0.100], "expected_ocr": "YOU ARE IDLE", "description": "Native idle banner" }
      }
    }
    """;
}
