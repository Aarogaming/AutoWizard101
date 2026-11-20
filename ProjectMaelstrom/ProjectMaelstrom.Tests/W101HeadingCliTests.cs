using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101HeadingCliTests
{
    private static string[] Args(string heading) => ["--cli", "navigation-visit", "--output", "heading-result.json",
        "--workspace", ".", "--window", "42", "--process-id", "123", "--started-utc-ticks", "456",
        "--enable-client-observation", "--enable-ui-tree", "--enable-module-actions", "--include-pose-context",
        "--key", "A", "--pulse-ms", "300", "--heading-radians", heading];

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1.0", 1)]
    public void ExplicitHeadingParses(string input, double expected)
        => Assert.Equal(expected, MaelstromCli.Parse(Args(input), ".").HeadingRadians);

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-0.1")]
    [InlineData("6.284")]
    public void InvalidHeadingRejected(string input)
        => Assert.Throws<ArgumentException>(() => MaelstromCli.Parse(Args(input), "."));

    [Fact]
    public void HeadingCannotUseUnmeasuredOrForwardMovement()
    {
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse(Args("1").Where(a => a != "--include-pose-context").ToArray(), "."));
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse(Args("1").Select(a => a == "A" ? "W" : a).ToArray(), "."));
    }
}
