using System.Globalization;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

/// <summary>Native half of the shared synthetic Rust/native conformance corpus. No game access.</summary>
public sealed class RustComponentParityTests
{
    private static int Integer(string value) => int.Parse(value, CultureInfo.InvariantCulture);

    private static Dictionary<string, int> Entries(string value) => value == "-" ? [] :
        value.Split('|').Select(entry => entry.Split('=')).ToDictionary(parts => parts[0], parts => Integer(parts[1]));

    [Fact]
    public void SharedComponentCorpusMatchesNative()
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "TestData", "RustParity", "component-parity-v1.tsv"));
        Assert.Equal("# maelstrom-component-parity-v1", lines[0]);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in lines.Where(line => line.Length > 0 && !line.StartsWith('#')))
        {
            var row = line.Split('\t');
            Assert.True(row.Length >= 3, $"Invalid fixture row: {line}");
            var id = row[1];
            Assert.True(ids.Add(id), $"Duplicate fixture ID: {id}");
            var columns = row[0] switch
            {
                "target" or "pedestal" => 5,
                "hand" or "codec" => 6,
                "enchant" => 8,
                "reject-target" or "reject-pedestal" => 3,
                "reject-hand" => 4,
                _ => throw new InvalidDataException($"Unknown fixture operation: {row[0]}")
            };
            Assert.Equal(columns, row.Length);
            switch (row[0])
            {
                case "target":
                    AssertPoint(CombatGeometry.CalculateTargetCoordinates(Integer(row[2])), row[3], row[4], id);
                    break;
                case "pedestal":
                    AssertPoint(CombatGeometry.GetEnemyPedestalCoordinates(Integer(row[2])), row[3], row[4], id);
                    break;
                case "hand":
                    AssertPoint(CombatGeometry.GetHandCardCoordinates(Integer(row[2]), Integer(row[3])), row[4], row[5], id);
                    break;
                case "enchant":
                    var enchant = new CombatCardGeometry("Epic", Integer(row[2]), Integer(row[3]));
                    var spell = new CombatCardGeometry("Tempest", Integer(row[4]), Integer(row[5]), Integer(row[6]));
                    var (first, second, result) = CombatGeometry.CalculateEnchantGeometry(enchant, spell);
                    AssertPoint(first, row[2], row[3], id);
                    AssertPoint(second, row[4], row[5], id);
                    Assert.Equal(new CombatCardGeometry("Epic-Tempest", Integer(row[7]), spell.SpellY, spell.CardWidth), result);
                    break;
                case "reject-target":
                    Assert.Throws<ArgumentOutOfRangeException>(() => CombatGeometry.CalculateTargetCoordinates(Integer(row[2])));
                    break;
                case "reject-pedestal":
                    Assert.Throws<ArgumentOutOfRangeException>(() => CombatGeometry.GetEnemyPedestalCoordinates(Integer(row[2])));
                    break;
                case "reject-hand":
                    Assert.Throws<ArgumentOutOfRangeException>(() => CombatGeometry.GetHandCardCoordinates(Integer(row[2]), Integer(row[3])));
                    break;
                case "codec":
                    var decoded = DeckEncoder.Decode(row[2]);
                    AssertEntries(decoded.Normal, Entries(row[3]), id);
                    AssertEntries(decoded.Tc, Entries(row[4]), id);
                    AssertEntries(decoded.Item, Entries(row[5]), id);
                    break;
            }
        }
        Assert.Equal(56, ids.Count);
    }

    private static void AssertPoint(System.Drawing.Point point, string x, string y, string id) =>
        Assert.True(point.X == Integer(x) && point.Y == Integer(y), $"Coordinate mismatch: {id}");

    private static void AssertEntries(Dictionary<string, int> actual, Dictionary<string, int> expected, string id)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var (key, value) in expected)
        {
            // Check retained spelling as well as value, independently of the native comparer.
            Assert.Contains(key, actual.Keys);
            Assert.True(actual[key] == value, $"Entry mismatch: {id}/{key}");
        }
    }
}
