using System.Security.Cryptography;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101ModuleBindingsTests
{
    private readonly WizwalkerTarget _target = new(1, 42, 100, "WizardGraphicalClient", "inert", new('a', 64));
    private readonly long _now = DateTime.UtcNow.Ticks;
    private W101UiTreeResult Reading(bool menu = false, bool idle = false, bool dialogue = false, bool prompt = false)
    {
        Dictionary<string, bool?> States() => new() { ["menu"] = menu, ["nativeIdle"] = idle, ["dialogue"] = dialogue, ["interactionPrompt"] = prompt };
        return new(1, "synthetic", W101UiTreeOperation.Version, WizwalkerOperation.SourceSha256,
            _target, "observed", "test", false, States(), _now, _now, 1, true, true, 2, 2, 0, States());
    }
    private RoutineObservation Authority(OperationModuleBundle bundle) => new(1000, "Unknown",
        bundle.Bind("pid-42-start-100", "geometry", "test"), true, true, true, true, true);

    [Theory]
    [InlineData("sigil")]
    [InlineData("menu")]
    [InlineData("native-idle")]
    public void BundlesUseExistingCompilers(string mode) => Assert.Single(ModuleCatalog.Compile(W101ModuleBindings.Bundle(mode)));

    [Fact]
    public void PromptWithoutSpecificIdentityCannotInteract()
    {
        var bundle = W101ModuleBindings.Bundle("sigil"); var authority = Authority(bundle);
        var map = W101ModuleBindings.Map(_target, Reading(prompt: true), authority, new(OutsideReady: true), _now);
        Assert.DoesNotContain("w101.interaction", map.VerifiedControls!);
        Assert.False(new ModuleRunner(bundle, authority.Binding).Observe(map).IsProposal);
        var verified = W101ModuleBindings.Map(_target, Reading(prompt: true), authority, new(true, true), _now);
        Assert.Equal("game.interact", new ModuleRunner(bundle, authority.Binding).Observe(verified).ActionId);
    }
    [Fact]
    public void DialogueLoadingStaleWrongTargetAndUnknownStatesNeverPropose()
    {
        var bundle = W101ModuleBindings.Bundle("menu"); var authority = Authority(bundle);
        foreach (var reading in new[] { Reading(dialogue: true), Reading() with { Loading = true },
            Reading() with { StatesObservedUtcTicks = _now - TimeSpan.FromSeconds(3).Ticks },
            Reading() with { Target = _target with { ProcessId = 43 } },
            Reading() with { StableStates = [] } })
        {
            var mapped = W101ModuleBindings.Map(_target, reading, authority, new(OutsideReady: true), _now);
            Assert.False(new ModuleRunner(bundle, authority.Binding).Observe(mapped).IsProposal);
        }
    }
    [Fact]
    public void SavedMenuRecognitionSuppliesIdleControlButNotOutcomeOrPermission()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "TestData", "Modules", "W101Menus");
        using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, "recognition-cases.json")));
        var item = doc.RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("id").GetString() == "graphics-menu");
        var bytes = File.ReadAllBytes(Path.Combine(folder, item.GetProperty("image").GetString()!));
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.Equal(item.GetProperty("source_image_sha256").GetString(), hash);
        var text = item.GetProperty("text").Deserialize<PetDanceMenuText>(OperationModules.Json)!;
        var bundle = W101ModuleBindings.Bundle("native-idle"); var authority = Authority(bundle);
        // Saved fixture has no capture clock: this clock is explicitly synthetic.
        var visual = W101MenuRecognition.Classify(new(hash, 1000, text.ImageSize, authority.Binding, "saved-ocr"), text);
        foreach (var origin in new[] { "saved-ocr", "manual-annotation", "injected-test-ocr" })
        {
            var untrusted = visual with { Frame = visual.Frame with { TextOrigin = origin } };
            Assert.False(W101ModuleBindings.FromVisual(untrusted, authority.Binding, text.ImageSize, 1000).IdleControlVerified);
        }
        // Synthetic producer-boundary test only: this is not an actual local OCR run
        // or live qualification of these historical pixels.
        visual = visual with { Frame = visual.Frame with { TextOrigin = "local-windows-ocr" } };
        var context = W101ModuleBindings.FromVisual(visual, authority.Binding, text.ImageSize, 1000);
        var frame = W101ModuleBindings.Map(_target, Reading(menu: true), authority, context, _now);
        var runner = new ModuleRunner(bundle, authority.Binding);
        var decision = runner.Observe(frame);
        Assert.Equal("wizard101.native-idle", decision.ActionId);
        Assert.False(decision.Completed);
        Assert.Null(frame.Observation.OutcomeForProposalId);
        var unavailable = W101ModuleBindings.FromVisual(visual, authority.Binding, text.ImageSize, 1400);
        Assert.False(unavailable.IdleControlVerified);
    }
}
