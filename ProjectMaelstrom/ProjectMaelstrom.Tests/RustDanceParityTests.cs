using System.Globalization;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

/// <summary>Native half of the shared labeled Dance observations; no capture or input.</summary>
public sealed class RustDanceParityTests
{
    private static DanceCue Cue(string value)
    {
        if (!Enum.TryParse<DanceCue>(value, true, out var cue) || !Enum.IsDefined(cue))
            throw new InvalidDataException($"Unknown fixture cue: {value}");
        return cue;
    }

    private static string[] List(string value) => value == "-" ? [] : value.Split(',');
    private static int Integer(string value) => int.Parse(value, CultureInfo.InvariantCulture);
    private static double Number(string value) => double.Parse(value, CultureInfo.InvariantCulture);

    [Fact]
    public void SharedDanceObservationsMatchNativeAfterEveryFrame()
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "TestData", "RustParity", "dance-observations-v1.tsv"));
        Assert.Equal("# maelstrom-dance-observations-v1", lines[0]);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        PetDanceSequence? sequence = null;
        var caseId = "";
        var observations = 0;
        var caseObservations = 0;
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var row = line.Split('\t');
            switch (row[0])
            {
                case "case":
                    Assert.Equal(3, row.Length);
                    Assert.True(sequence is null || caseObservations > 0, $"Empty case: {caseId}");
                    caseId = row[1];
                    Assert.True(ids.Add(caseId), $"Duplicate fixture case: {caseId}");
                    sequence = new PetDanceSequence(Integer(row[2]));
                    caseObservations = 0;
                    break;
                case "obs":
                    Assert.Equal(11, row.Length);
                    Assert.NotNull(sequence);
                    var actual = sequence.Observe(Number(row[1]), new(Integer(row[2]), Integer(row[3])),
                        PetDanceSequence.ParsePhase(row[4]), Cue(row[5]), Cue(row[6]), Number(row[7]));
                    var location = $"{caseId}, line {lineIndex + 1}: {actual.Reason}";
                    Assert.True(actual.Cues.SequenceEqual(List(row[8])), $"Cues: {location}");
                    Assert.True(actual.Proposals.SequenceEqual(List(row[9])), $"Proposals: {location}");
                    Assert.True(actual.Rejected == bool.Parse(row[10]), $"Rejected: {location}");
                    observations++;
                    caseObservations++;
                    break;
                default:
                    throw new InvalidDataException($"Unknown fixture operation: {row[0]}");
            }
        }
        Assert.True(caseObservations > 0);
        Assert.Equal(15, ids.Count);
        Assert.Equal(390, observations);
    }
}
