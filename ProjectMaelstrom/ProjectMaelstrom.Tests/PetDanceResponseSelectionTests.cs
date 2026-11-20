using ProjectMaelstrom.Utilities;
using Xunit;

namespace ProjectMaelstrom.Tests;

public class PetDanceResponseSelectionTests
{
    [Fact]
    public void VisualSequenceDoesNotRequireSound()
    {
        var cues = new[] { "left", "left", "up" };
        var result = PetDanceResponseSelection.Select(cues, true, [], false);
        Assert.True(result.Accepted);
        Assert.Equal("visual", result.Source);
        Assert.Equal(cues, result.Cues);
        cues[0] = "down";
        Assert.Equal("left", result.Cues[0]);
    }

    [Fact]
    public void IndependentAgreementIsRecorded()
    {
        var result = PetDanceResponseSelection.Select(["up", "down", "up"], true, ["up", "down", "up"], true);
        Assert.True(result.Accepted);
        Assert.False(result.ModalitiesDisagree);
        Assert.Contains("agrees", result.Reason);
    }

    [Fact]
    public void CompleteVisualSequenceWinsConflictWithoutCombiningArrows()
    {
        var result = PetDanceResponseSelection.Select(["up", "down", "up"], true, ["left", "right", "down"], true);
        Assert.Equal("visual", result.Source);
        Assert.True(result.ModalitiesDisagree);
        Assert.Equal(new[] { "up", "down", "up" }, result.Cues);
    }

    [Fact]
    public void QualifiedSoundCanRecoverIncompleteVisualEvidence()
    {
        var result = PetDanceResponseSelection.Select(["left"], false, ["up", "up", "left"], true);
        Assert.True(result.Accepted);
        Assert.Equal("sound-fallback", result.Source);
        Assert.Equal(new[] { "up", "up", "left" }, result.Cues);
    }

    [Fact]
    public void NeitherUnqualifiedSequenceIsSilentlyPromoted()
    {
        var result = PetDanceResponseSelection.Select(["up", "up", "left"], false, ["up", "up", "left"], false);
        Assert.False(result.Accepted);
        Assert.Empty(result.Cues);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(8)]
    public void InvalidSequenceLengthCannotAuthorizeResponse(int length)
    {
        var invalid = Enumerable.Repeat("up", length).ToArray();
        Assert.False(PetDanceResponseSelection.Select(invalid, true, invalid, true).Accepted);
    }

    [Fact]
    public void UnknownDirectionCannotBeInventedOrDispatched()
        => Assert.False(PetDanceResponseSelection.Select(["up", "unknown", "left"], true, [], false).Accepted);
}
