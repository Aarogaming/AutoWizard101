using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ProjectMaelstrom.Tests;

public class OcrParsingTests
{
    private static readonly Type GameStateServiceType = typeof(ProjectMaelstrom.Utilities.GameStateService);
    private static readonly MethodInfo ParsePair = GameStateServiceType.GetMethod("ParsePair", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ParseSingle = GameStateServiceType.GetMethod("ParseSingle", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ClampToRange = GameStateServiceType.GetMethod("ClampToRange", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Theory]
    [InlineData("123/456", 123, 456)]
    [InlineData("  12 / 34 ", 12, 34)]
    public void ParsePair_ReturnsPair_WhenValid(string text, int expectedCurrent, int expectedMax)
    {
        var result = ParsePair.Invoke(null, new object[] { text });
        Assert.NotNull(result);
        var tuple = ((int current, int max))result!;
        Assert.Equal(expectedCurrent, tuple.current);
        Assert.Equal(expectedMax, tuple.max);
    }

    [Fact]
    public void ParsePair_ReturnsNull_OnInvalid()
    {
        var result = ParsePair.Invoke(null, new object[] { "abc/xyz" });
        Assert.Null(result);
    }

    [Theory]
    [InlineData("Energy 250", 250)]
    [InlineData(" 42 ", 42)]
    [InlineData("Value=77", 77)]
    public void ParseSingle_ReturnsValue_WhenValid(string text, int expected)
    {
        var value = (int?)ParseSingle.Invoke(null, new object[] { text });
        Assert.Equal(expected, value);
    }

    [Fact]
    public void ParseSingle_ReturnsNull_OnInvalid()
    {
        var value = (int?)ParseSingle.Invoke(null, new object[] { "none here" });
        Assert.Null(value);
    }

    [Theory]
    [InlineData(-5, 0, 10, 0)]
    [InlineData(15, 0, 10, 10)]
    [InlineData(5, 0, 10, 5)]
    public void ClampToRange_ClampsAsExpected(int value, int min, int max, int expected)
    {
        var result = (int)ClampToRange.Invoke(null, new object[] { value, min, max })!;
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParsePair_MatchesSampleFiles()
    {
        var baseDir = Path.Combine(AppContext.BaseDirectory, "TestData", "ocr");
        var files = Directory.GetFiles(baseDir, "*.txt").OrderBy(f => f, StringComparer.Ordinal);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var result = ParsePair.Invoke(null, new object[] { text });
            if (Path.GetFileName(file).StartsWith("invalid", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Null(result);
            }
            else
            {
                Assert.NotNull(result);
            }
        }
    }
}
