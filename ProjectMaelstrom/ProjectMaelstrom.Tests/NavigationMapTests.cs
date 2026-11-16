using System.Buffers.Binary;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class NavigationMapTests
{
    private static byte[] Encode((float X, float Y, float Z, short Index)[] points, params NavigationEdge[] edges)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((short)Math.Max(0, points.Length - 1));
        writer.Write((short)points.Length);
        writer.Write((short)17);
        foreach (var point in points)
        {
            writer.Write(point.X);
            writer.Write(point.Y);
            writer.Write(point.Z);
            writer.Write(point.Index);
        }
        writer.Write(edges.Length);
        foreach (var edge in edges)
        {
            writer.Write((short)edge.From);
            writer.Write((short)edge.To);
        }
        return stream.ToArray();
    }

    private static byte[] Line() => Encode([(0, 0, 0, 0), (3, 4, 0, 1), (3, 4, 12, 2)], new NavigationEdge(0, 1), new(1, 2));

    [Fact]
    public void ParsesExactLocalSchemaAndPreservesOpaqueHeader()
    {
        var data = Line();
        var map = NavigationMap.Parse(data);
        Assert.Equal(2, map.HeaderCount);
        Assert.Equal(17, map.HeaderUnknown);
        Assert.Equal(3, map.VertexRecords);
        Assert.Equal(0, map.SkippedVertexRecords);
        Assert.Equal(new NavigationPoint(3, 4, 12), map.Points[2]);
        Array.Clear(data);
        Assert.Equal(3, map.Points.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<NavigationPoint>)map.Points)[0] = new(9, 9, 9));
        Assert.Throws<NotSupportedException>(() => ((IList<NavigationEdge>)map.Edges)[0] = new(2, 0));
    }

    [Fact]
    public void PreservesLegacySkippedRecordBehaviorWithoutKeepingItsCoordinates()
    {
        var map = NavigationMap.Parse(Encode([(0, 0, 0, 0), (99, 99, 99, 2), (3, 4, 0, 1)], new NavigationEdge(0, 1)));
        Assert.Equal(1, map.SkippedVertexRecords);
        Assert.Equal(2, map.Points.Count);
        Assert.Equal(5, map.FindPath(0, 1).Distance);
    }

    [Fact]
    public void RejectsEveryTruncationAndTrailingPortalLikeData()
    {
        var data = Line();
        for (int length = 0; length < data.Length; length++)
            Assert.Throws<InvalidDataException>(() => NavigationMap.Parse(data[..length]));
        Assert.Throws<InvalidDataException>(() => NavigationMap.Parse([.. data, 0, 0, 0, 0]));
        Assert.Throws<InvalidDataException>(() => NavigationMap.Parse(new byte[NavigationMap.MaximumBytes + 1]));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void RejectsNonfiniteCoordinatesEvenInSkippedRecords(float value)
    {
        Assert.Throws<InvalidDataException>(() => NavigationMap.Parse(Encode([(value, 0, 0, 0)])));
        Assert.Throws<InvalidDataException>(() => NavigationMap.Parse(Encode([(0, 0, 0, 0), (0, value, 0, 0)])));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void RejectsOutOfRangeVertexIndex(short index)
    {
        Assert.Throws<InvalidDataException>(() => NavigationMap.Parse(Encode([(0, 0, 0, index)])));
    }

    [Fact]
    public void RejectsInvalidCountsAndMissingEdgeVertices()
    {
        foreach (int offset in new[] { 0, 2 })
        {
            var data = Line();
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset, 2), -1);
            Assert.Throws<InvalidDataException>(() => NavigationMap.Parse(data));
        }
        foreach (int count in new[] { -1, NavigationMap.MaximumEdges + 1, int.MaxValue })
        {
            var data = Line();
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(48, 4), count);
            Assert.Throws<InvalidDataException>(() => NavigationMap.Parse(data));
        }
        Assert.Throws<InvalidDataException>(() => NavigationMap.Parse(Encode([(0, 0, 0, 0)], new NavigationEdge(0, 1))));
        Assert.Throws<InvalidDataException>(() => NavigationMap.Parse(Encode([(0, 0, 0, 0)], new NavigationEdge(-1, 0))));
    }

    [Fact]
    public void DirectedPathUsesThreeDimensionalDistanceAndNeverInventsReverseEdges()
    {
        var map = NavigationMap.Parse(Line());
        var path = map.FindPath(0, 2);
        Assert.True(path.Found);
        Assert.Equal(new[] { 0, 1, 2 }, path.Vertices);
        Assert.Equal(17, path.Distance);
        Assert.Equal("unreachable", map.FindPath(2, 0).Reason);
        Assert.Equal("unreachable", map.FindPath(0, 2, new HashSet<NavigationEdge> { new(1, 2) }).Reason);
        Assert.True(map.FindPath(0, 2, new HashSet<NavigationEdge> { new(2, 1) }).Found);
    }

    [Fact]
    public void ChoosesShortestRouteAndCanRerouteAroundBlockedEdge()
    {
        var map = NavigationMap.Parse(Encode([(0, 0, 0, 0), (1, 0, 0, 1), (0, 10, 0, 2), (2, 0, 0, 3)],
            new NavigationEdge(0, 2), new(2, 3), new NavigationEdge(0, 1), new(1, 3)));
        Assert.Equal(new[] { 0, 1, 3 }, map.FindPath(0, 3).Vertices);
        Assert.Equal(new[] { 0, 2, 3 }, map.FindPath(0, 3, new HashSet<NavigationEdge> { new(1, 3) }).Vertices);
    }

    [Fact]
    public void BoundedSearchDoesNotReturnPartialPathAsSuccess()
    {
        var map = NavigationMap.Parse(Line());
        var path = map.FindPath(0, 2, maximumVisited: 2);
        Assert.False(path.Found);
        Assert.Equal("visit-budget-exhausted", path.Reason);
        Assert.Empty(path.Vertices);
        Assert.Equal(2, path.Visited);
        Assert.True(map.FindPath(0, 2, maximumVisited: 3).Found);
        Assert.Equal(new[] { 1 }, map.FindPath(1, 1, maximumVisited: 1).Vertices);
        Assert.Throws<ArgumentOutOfRangeException>(() => map.FindPath(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.FindPath(0, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.FindPath(0, 1, maximumVisited: 0));
    }

    [Fact]
    public void ZeroLengthEdgesAndCyclesTerminate()
    {
        var map = NavigationMap.Parse(Encode([(0, 0, 0, 0), (0, 0, 0, 1), (1, 0, 0, 2)],
            new(0, 0), new NavigationEdge(0, 1), new(1, 0), new(1, 2)));
        Assert.Equal(new[] { 0, 1, 2 }, map.FindPath(0, 2).Vertices);
        Assert.Equal(1, map.FindPath(0, 2).Distance);
    }
}

