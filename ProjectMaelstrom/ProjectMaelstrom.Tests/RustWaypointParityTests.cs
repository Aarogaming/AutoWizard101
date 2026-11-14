using System.Globalization;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

/// <summary>Shared synthetic pose feedback corpus; no input or game access.</summary>
public sealed class RustWaypointParityTests
{
    private static double Number(string value) => double.Parse(value, CultureInfo.InvariantCulture);
    private static int Integer(string value) => int.Parse(value, CultureInfo.InvariantCulture);

    [Fact]
    public void SharedWaypointObservationsMatchNative()
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "TestData", "RustParity", "waypoint-observations-v1.tsv"));
        Assert.Equal("# maelstrom-waypoint-observations-v1", lines[0]);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        WaypointFeedback? controller = null;
        var caseId = "";
        var count = 0;
        var caseCount = 0;
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var row = line.Split('\t');
            switch (row[0])
            {
                case "case":
                    Assert.Equal(6, row.Length);
                    Assert.True(controller is null || caseCount > 0);
                    caseId = row[1];
                    Assert.True(ids.Add(caseId), $"Duplicate case: {caseId}");
                    controller = new WaypointFeedback(new(Number(row[2]), Number(row[3]), Number(row[4]), 0, 0), Integer(row[5]));
                    caseCount = 0;
                    break;
                case "obs":
                    Assert.Equal(11, row.Length);
                    Assert.NotNull(controller);
                    var actual = controller.Observe(new(Number(row[1]), Number(row[2]), Number(row[3]), Number(row[4]),
                        long.Parse(row[5], CultureInfo.InvariantCulture)));
                    var location = $"{caseId}, line {lineIndex + 1}";
                    Assert.True(actual.State == row[6], $"State: {location}");
                    Assert.True(actual.Reason == row[7], $"Reason: {location}");
                    Assert.True(actual.PulseMs == Integer(row[8]), $"Pulse: {location}");
                    Close(actual.Distance, row[9], $"{location}: distance");
                    Close(actual.HeadingError, row[10], $"{location}: heading");
                    count++;
                    caseCount++;
                    break;
                default:
                    throw new InvalidDataException($"Unknown fixture operation: {row[0]}");
            }
        }
        Assert.True(caseCount > 0);
        Assert.Equal(27, ids.Count);
        Assert.Equal(59, count);
    }

    private static void Close(double actual, string expectedText, string location)
    {
        var expected = Number(expectedText);
        var matches = double.IsNaN(expected) ? double.IsNaN(actual) :
            double.IsInfinity(expected) ? actual == expected :
            double.IsFinite(actual) && Math.Abs(actual - expected) <= 1e-9;
        Assert.True(matches, $"{location}: actual={actual}, expected={expected}");
    }
}
