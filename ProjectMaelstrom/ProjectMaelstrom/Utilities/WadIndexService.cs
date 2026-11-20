using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Lightweight WAD indexer: lists local .wad files and writes a manifest for downstream matching.
/// Does not parse assets yet; safe for quick lookups.
/// </summary>
internal static class WadIndexService
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Build an index of .wad files under the known Wizard101 install root.
    /// </summary>
    public static string BuildIndex()
    {
        var root = GetDefaultWizardInstall();
        string? wadDir = null;
        if (root != null)
        {
            var gameData = Path.Combine(root, "Data", "GameData");
            wadDir = Directory.Exists(gameData) ? gameData : Path.Combine(root, "Wads");
        }
        var list = new List<WadEntry>();

        if (wadDir != null && Directory.Exists(wadDir))
        {
            foreach (var path in Directory.GetFiles(wadDir, "*.wad", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var info = new FileInfo(path);
                    list.Add(new WadEntry
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        SizeBytes = info.Length,
                        ModifiedUtc = info.LastWriteTimeUtc
                    });
                }
                catch (Exception ex)
                {
                    DevTelemetry.Log("WAD", $"Failed to index {path}: {ex.Message}");
                }
            }
        }
        else
        {
            DevTelemetry.Log("WAD", "Wads folder not found; set Wizard101 directory in settings.");
        }

        var manifest = new WadManifest
        {
            BasePath = wadDir ?? "unknown",
            Entries = list.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList()
        };

        var outDir = Path.Combine(AppContext.BaseDirectory, "logs", "wads");
        Directory.CreateDirectory(outDir);
        var outPath = Path.Combine(outDir, "wad_index.json");
        File.WriteAllText(outPath, JsonSerializer.Serialize(manifest, _jsonOptions));
        DevTelemetry.Log("WAD", $"Indexed {manifest.Entries.Count} wads to {outPath}");
        return outPath;
    }

    /// <summary>
    /// In-process WAD asset manifest generator using native WadArchiveReader.
    /// Writes per-wad asset manifests to logs/wads/assets_{wad}.json.
    /// </summary>
    public static void BuildAssetManifests()
    {
        var wadIndexPath = Path.Combine(AppContext.BaseDirectory, "logs", "wads", "wad_index.json");
        if (!File.Exists(wadIndexPath))
        {
            BuildIndex();
        }

        var manifest = LoadIndex(wadIndexPath);
        if (manifest == null || manifest.Entries.Count == 0) return;

        var outDir = Path.Combine(AppContext.BaseDirectory, "logs", "wads");
        Directory.CreateDirectory(outDir);

        foreach (var entry in manifest.Entries)
        {
            try
            {
                var outPath = Path.Combine(outDir, $"assets_{Path.GetFileNameWithoutExtension(entry.Name)}.json");
                if (File.Exists(outPath)) continue; // avoid rework

                using var reader = new WadArchiveReader(entry.FullPath);
                var assets = reader.Entries.Values
                    .Where(e => e.Path.Length > 1 && HasUsefulExtension(e.Path))
                    .Take(1000)
                    .Select(e => new WadAssetEntry
                    {
                        Name = Path.GetFileName(e.Path),
                        Path = e.Path,
                        Size = e.Size
                    })
                    .ToList();

                var assetIndex = new WadAssetIndex
                {
                    WadName = entry.Name,
                    Entries = assets
                };

                File.WriteAllText(outPath, JsonSerializer.Serialize(assetIndex, _jsonOptions));
                DevTelemetry.Log("WAD", $"Asset manifest saved for {entry.Name} ({assets.Count} entries)");
            }
            catch (Exception ex)
            {
                DevTelemetry.Log("WAD", $"Asset manifest error for {entry.Name}: {ex.Message}");
            }
        }
    }

    private static WadManifest? LoadIndex(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<WadManifest>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetDefaultWizardInstall()
    {
        var candidates = new[]
        {
            @"C:\ProgramData\KingsIsle Entertainment\Wizard101",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "KingsIsle Entertainment", "Wizard101"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KingsIsle Entertainment", "Wizard101")
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static bool HasUsefulExtension(string path)
    {
        var lower = path.ToLowerInvariant();
        return lower.EndsWith(".png") ||
               lower.EndsWith(".jpg") ||
               lower.EndsWith(".jpeg") ||
               lower.EndsWith(".dds") ||
               lower.EndsWith(".json") ||
               lower.EndsWith(".txt") ||
               lower.EndsWith(".xml") ||
               lower.EndsWith(".csv") ||
               lower.EndsWith(".bmp") ||
               lower.EndsWith(".tga") ||
               lower.EndsWith(".ttf") ||
               lower.EndsWith(".otf");
    }

    private sealed class WadEntry
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public long SizeBytes { get; set; }
        public DateTime ModifiedUtc { get; set; }
    }

    private sealed class WadManifest
    {
        public string BasePath { get; set; } = "";
        public List<WadEntry> Entries { get; set; } = new();
    }
}
