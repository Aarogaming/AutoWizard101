using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDancePresetTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Maelstrom-Dance-Preset-" + Guid.NewGuid().ToString("N"));
    private string Folder => Path.Combine(_root, PetDancePreset.RelativeDirectory);
    private string Profile => Path.Combine(Folder, "profile.json");
    private string Audio => Path.Combine(Folder, "audio-comparison.json");

    public PetDancePresetTests()
    {
        Directory.CreateDirectory(Folder);
        var supplied = Path.Combine(AppContext.BaseDirectory, PetDancePreset.RelativeDirectory);
        Assert.True(Directory.Exists(supplied), "The checked-in preset must be copied into application and test output.");
        foreach (var source in Directory.EnumerateFiles(supplied))
            File.Copy(source, Path.Combine(Folder, Path.GetFileName(source)));
    }

    [Fact]
    public void PublishedPresetLoadsFromApplicationOutputWithoutCheckoutPaths()
    {
        var preset = PetDancePreset.Load(AppContext.BaseDirectory);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, PetDancePreset.RelativeDirectory, "profile.json"), preset.ProfilePath);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, PetDancePreset.RelativeDirectory, "audio-comparison.json"), preset.AudioComparisonPath);
        using var vision = new PetDancePanelVision(preset.ProfilePath);
        Assert.True(vision.UsesShapeSequence);
        Assert.Equal(12, vision.Pins.Count); // Profile and its eleven image references.
        var detector = PetDanceAudioDetector.LoadFrozenComparison(preset.AudioComparisonPath);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(preset.AudioComparisonPath))).ToLowerInvariant(), detector.TemplateSourceSha256);
        var profile = JsonSerializer.Deserialize<DancePanelProfile>(File.ReadAllText(preset.ProfilePath))!;
        Assert.All(profile.References, reference => Assert.True(string.IsNullOrEmpty(reference.Source) || !Path.IsPathFullyQualified(reference.Source)));
    }

    [Fact]
    public void LoadingPresetDoesNotChangeItsFilesOrCreateUserData()
    {
        var before = Directory.EnumerateFiles(Folder).ToDictionary(path => Path.GetFileName(path)!,
            path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        var preset = PetDancePreset.Load(_root);
        Assert.Equal(Profile, preset.ProfilePath);
        Assert.Equal(Audio, preset.AudioComparisonPath);
        Assert.False(Directory.Exists(Path.Combine(_root, "Data")));
        Assert.Equal(before.Count, Directory.EnumerateFiles(Folder).Count());
        foreach (var item in before)
            Assert.Equal(item.Value, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Folder, item.Key!)))));
    }

    [Theory]
    [InlineData("profile.json")]
    [InlineData("audio-comparison.json")]
    [InlineData("Neutral.png")]
    public void MissingRuntimeAssetsCannotYieldAUsablePlan(string name)
    {
        File.Delete(Path.Combine(Folder, name));
        if (name == "Neutral.png") Assert.Throws<FileNotFoundException>(() => PetDancePreset.Load(_root));
        else Assert.Throws<InvalidDataException>(() => PetDancePreset.Load(_root));
    }

    [Fact]
    public void ModifiedPinnedReferenceIsRejectedBeforeUse()
    {
        var path = Path.Combine(Folder, "Up.png");
        var bytes = File.ReadAllBytes(path); bytes[^1] ^= 1;
        File.WriteAllBytes(path, bytes);
        Assert.Throws<InvalidDataException>(() => PetDancePreset.Load(_root));
    }

    [Theory]
    [InlineData("profile.json", 65537)]
    [InlineData("audio-comparison.json", 262145)]
    [InlineData("audio-comparison.json", 0)]
    public void OversizedOrEmptyConfigurationsAreRejected(string name, int bytes)
    {
        File.WriteAllBytes(Path.Combine(Folder, name), new byte[bytes]);
        Assert.Throws<InvalidDataException>(() => PetDancePreset.Load(_root));
    }

    [Fact]
    public void OlderFixedPixelProfileIsNotSilentlyUsedAsThePackagedPlayer()
    {
        var node = JsonNode.Parse(File.ReadAllText(Profile))!.AsObject();
        node.Remove("UiLayout"); File.WriteAllText(Profile, node.ToJsonString());
        Assert.Throws<InvalidDataException>(() => PetDancePreset.Load(_root));
    }

    [Fact]
    public void UnsupportedAudioFeatureVersionIsRejected()
    {
        var node = JsonNode.Parse(File.ReadAllText(Audio))!.AsObject();
        node["feature_version"] = "unqualified-feature-version";
        File.WriteAllText(Audio, node.ToJsonString());
        Assert.Throws<InvalidDataException>(() => PetDancePreset.Load(_root));
    }

    [Fact]
    public void RelativeOrMissingApplicationRootCannotSelectLocalStudyData()
    {
        Assert.Throws<ArgumentException>(() => PetDancePreset.Load("."));
        Assert.Throws<ArgumentException>(() => PetDancePreset.Load(""));
        Assert.Throws<InvalidDataException>(() => PetDancePreset.Load(Path.Combine(_root, "missing")));
    }

    public void Dispose()
    {
        var full = Path.GetFullPath(_root);
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (Directory.Exists(full) && full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(full).StartsWith("Maelstrom-Dance-Preset-", StringComparison.Ordinal))
            Directory.Delete(full, true);
    }
}
