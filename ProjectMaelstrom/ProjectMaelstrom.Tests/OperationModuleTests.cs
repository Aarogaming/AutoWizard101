using System.Text.Json;
using System.Text.Json.Nodes;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class OperationModuleTests
{
    [Fact]
    public void BundleRoundTripUsesVersionedSpecificationsNotEmbeddedExecutableGraphs()
    {
        var bundle = ModuleRunnerTests.LabBundle(); var json = JsonSerializer.Serialize(bundle, OperationModules.Json);
        var restored = ModuleCatalog.ReadBundle(json);
        Assert.Equal(json, JsonSerializer.Serialize(restored, OperationModules.Json));
        Assert.Equal(new[] { "navigation", "sigil", "menu", "auto-idle" }, restored.Modules.Select(module => module.Kind));
        Assert.Equal(4, ModuleCatalog.Compile(restored).Length); Assert.False(restored.LiveQualified);
        Assert.DoesNotContain("live_qualified", json); Assert.DoesNotContain("nodes", json);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("kind")]
    [InlineData("schema")]
    [InlineData("unknown-config")]
    [InlineData("unknown-root")]
    [InlineData("live-permission")]
    [InlineData("script")]
    public void UnsupportedOrExecutableConfigurationIsRejected(string change)
    {
        var json = JsonNode.Parse(JsonSerializer.Serialize(ModuleRunnerTests.LabBundle(), OperationModules.Json))!;
        var module = json["modules"]![0]!;
        switch (change)
        {
            case "version": module["version"] = "9.0.0"; break;
            case "kind": module["kind"] = "shell"; break;
            case "schema": json["schema_version"] = 2; break;
            case "unknown-config": module["configuration"]!["mystery"] = 1; break;
            case "unknown-root": json["plugin"] = "external.dll"; break;
            case "live-permission": json["live_qualified"] = true; break;
            case "script": module["configuration"]!["actions"]![0]!["kind"] = "script"; break;
        }
        Assert.ThrowsAny<Exception>(() => ModuleCatalog.ReadBundle(json.ToJsonString()));
    }

    [Fact]
    public void DuplicateJsonPropertiesAndOversizedFilesAreRejected()
    {
        var json = JsonSerializer.Serialize(ModuleRunnerTests.LabBundle(), OperationModules.Json);
        Assert.Throws<InvalidDataException>(() => ModuleCatalog.ReadBundle(json.Replace("\"schema_version\": 1", "\"schema_version\": 1, \"schema_version\": 1")));
        Assert.Throws<InvalidDataException>(() => ModuleCatalog.ReadBundle(new string(' ', ModuleCatalog.MaximumBundleCharacters + 1)));
    }

    [Fact]
    public void CrossGameModulesRepeatedIdsAndUnboundedBundlesAreRejected()
    {
        var bundle = ModuleRunnerTests.LabBundle();
        Assert.Throws<InvalidDataException>(() => ModuleCatalog.Compile(bundle with { Modules = [bundle.Modules[0] with { GameId = "wrong" }] }));
        Assert.Throws<InvalidDataException>(() => ModuleCatalog.Compile(bundle with { Modules = [bundle.Modules[0], bundle.Modules[0]] }));
        Assert.Throws<InvalidDataException>(() => ModuleCatalog.Compile(bundle with { TimeoutMs = double.NaN }));
        Assert.Throws<InvalidDataException>(() => ModuleCatalog.Compile(bundle with { MaxActions = 0 }));
        Assert.Throws<InvalidDataException>(() => ModuleCatalog.Compile(bundle with { Modules = [] }));
    }

    [Fact]
    public void SameActionIdCannotChangeBindingBetweenModules()
    {
        var bundle = ModuleRunnerTests.LabBundle();
        var conflict = OperationModules.Spec("conflict", "sigil", bundle.GameId,
            new SigilConfiguration("A", "B", new("lab.walk", "keyboard-pulse", "DOWN", 50, "walk-area")));
        Assert.Throws<InvalidDataException>(() => ModuleCatalog.Compile(bundle with { Modules = [bundle.Modules[0], conflict] }));
    }

    [Fact]
    public void SaveValidatesBeforeWritingAndNeverOverwritesAnExistingPack()
    {
        var path = Path.Combine(Path.GetTempPath(), "module-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var bundle = ModuleRunnerTests.LabBundle();
            Assert.Throws<InvalidDataException>(() => ModuleCatalog.Save(bundle with { MaxActions = 0 }, path));
            Assert.False(File.Exists(path)); ModuleCatalog.Save(bundle, path);
            var bytes = File.ReadAllBytes(path);
            Assert.Throws<IOException>(() => ModuleCatalog.Save(bundle, path));
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
