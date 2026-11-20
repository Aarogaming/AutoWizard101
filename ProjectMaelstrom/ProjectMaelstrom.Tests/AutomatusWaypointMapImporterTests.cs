using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class AutomatusWaypointMapImporterTests
{
    [Fact]
    public void ImportsCoordinatesAndPreservesLegacyMarkersAsOpaqueData()
    {
        const string source = "UNICORN_WAY\r\n12,-4,8\r\n0,0,-1\r\n23,7,9\r\n";

        var map = AutomatusWaypointMapImporter.Parse(source, "UNICORN_WAY");

        Assert.Equal("candidate-only", map.Qualification);
        Assert.Equal("automatus-waypoint-map-v1", map.SourceFormat);
        Assert.Equal(3, map.Entries.Count);
        Assert.Equal(new NavigationPoint(12, -4, 8), map.Entries[0].Point);
        Assert.False(map.Entries[0].IsLegacyControlMarker);
        Assert.True(map.Entries[1].IsLegacyControlMarker);
        Assert.Equal(-1, map.Entries[1].OpaqueMarkerCode);
        Assert.Equal(new NavigationPoint(0, 0, -1), map.Entries[1].Point);
        Assert.Equal(64, map.SourceSha256.Length);
    }

    [Fact]
    public void RejectsMismatchedLocationHeader()
    {
        const string source = "UNICORN_WAY\n1,2,3\n4,5,6\n";

        Assert.Throws<InvalidDataException>(() => AutomatusWaypointMapImporter.Parse(source, "COMMONS"));
    }

    [Theory]
    [InlineData("UNICORN_WAY\n1,2\n3,4,5\n")]
    [InlineData("UNICORN_WAY\n1,2,nope\n3,4,5\n")]
    [InlineData("UNICORN_WAY\n10000001,2,3\n4,5,6\n")]
    public void RejectsMalformedOrOutOfRangeWaypointRows(string source)
    {
        Assert.Throws<InvalidDataException>(() => AutomatusWaypointMapImporter.Parse(source, "UNICORN_WAY"));
    }

    [Fact]
    public void RejectsOversizedWaypointCount()
    {
        var source = "UNICORN_WAY\n" + string.Concat(Enumerable.Repeat("1,2,3\n", AutomatusWaypointMapImporter.MaximumEntries + 1));

        Assert.Throws<InvalidDataException>(() => AutomatusWaypointMapImporter.Parse(source, "UNICORN_WAY"));
    }

    [Fact]
    public void RequiresAtLeastTwoRouteEntries()
    {
        Assert.Throws<InvalidDataException>(() => AutomatusWaypointMapImporter.Parse("UNICORN_WAY\n1,2,3\n", "UNICORN_WAY"));
    }
}
