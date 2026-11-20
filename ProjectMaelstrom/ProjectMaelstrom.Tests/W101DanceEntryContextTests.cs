using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101DanceEntryContextTests
{
    private const long Now = 638000000000000000;
    private static W101DanceEntryControl Control() => new(3, 2,
        ["Root", "WorldView", "PetGameTracks", "btnNext"], "btnNext", null,
        true, true, [10, 20, 50, 40], null, "selection-field-unqualified");
    private static W101DanceEntryContext Candidate() => new(1, Now, "structural-unqualified",
        "raw-parent-layout-unqualified", [Control()]);
    private static W101UiTreeResult Result() => new(1, "fixture", W101UiTreeOperation.Version,
        WizwalkerOperation.SourceSha256, new(42, 123, 456, "WizardGraphicalClient", "unused", new string('a', 64)),
        "observed", "fixture", false, new(), Now, Now, 100, true, true, 2, 2, 0, new());

    [Theory]
    [InlineData("<center>8</center>", true)]
    [InlineData(" 8 ", true)]
    [InlineData("8/10", false)]
    [InlineData("-8", false)]
    [InlineData("8 energy", false)]
    [InlineData("<center>8</center><center>9</center>", false)]
    public void EnergyCandidatePreservesUnqualifiedStatusAndRejectsAmbiguousLabels(string text, bool expected)
    {
        W101DanceEntryControl Label(int index, string name, string content) => new(index, 2,
            ["Root", "WorldView", "PetGameTracks", "wndBkgEnergy", name], name, content,
            true, false, null, null, "selection-field-unqualified");
        var cost = Label(3, "txtEnergyCost", text);
        var energy = Label(4, "txtYourEnergy", "100");
        var context = Candidate() with { Controls = [cost, energy] };
        var candidate = W101DanceEntryContext.ReadEnergyCandidate(context, true, Result(), Now);
        Assert.Equal(expected, candidate is not null);
        if (candidate is not null)
        {
            Assert.Equal(8, candidate.Cost); Assert.Equal(100, candidate.Current);
            Assert.Equal("structural-unqualified", candidate.Qualification);
            Assert.Equal(Now, candidate.ObservedUtcTicks);
        }
        Assert.Null(W101DanceEntryContext.ReadEnergyCandidate(context, false, Result(), Now));
        Assert.Null(W101DanceEntryContext.ReadEnergyCandidate(context, true, Result(), Now + TimeSpan.FromSeconds(6).Ticks));
        Assert.Null(W101DanceEntryContext.ReadEnergyCandidate(context with { Controls = [cost, energy, cost with { Index = 5 }] }, true, Result(), Now));
        Assert.Null(W101DanceEntryContext.ReadEnergyCandidate(context with { Controls = [cost with { Visible = false }, energy] }, true, Result(), Now));
    }

    [Fact]
    public void CandidateRetainsUnknownSelectionAndRawGeometryThroughJson()
    {
        var value = Candidate();
        var json = JsonSerializer.Serialize(value, MaelstromCli.Json);
        var roundtrip = JsonSerializer.Deserialize<W101DanceEntryContext>(json, MaelstromCli.Json)!;
        Assert.True(W101DanceEntryContext.Valid(roundtrip, true, Result(), Now));
        Assert.Null(roundtrip.Controls[0].Selected);
        Assert.Equal("raw-parent-layout-unqualified", roundtrip.GeometrySpace);
        Assert.Equal(Control().RawRectangle, roundtrip.Controls[0].RawRectangle);
        Assert.Equal(Control().Path, roundtrip.Controls[0].Path);
        Assert.True(W101DanceEntryContext.Valid(null, false, Result(), Now));
    }

    [Theory]
    [InlineData("opt-out")]
    [InlineData("loading")]
    [InlineData("unknown-loading")]
    [InlineData("cleanup")]
    [InlineData("hook")]
    [InlineData("unavailable")]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("zero-time")]
    [InlineData("mismatched-time")]
    [InlineData("schema")]
    [InlineData("qualification")]
    [InlineData("geometry-space")]
    public void CandidateNeedsExplicitOptInAndFreshCleanObservation(string fault)
    {
        var value = Candidate(); var result = Result();
        value = fault switch
        {
            "stale" => value with { ObservedUtcTicks = Now - TimeSpan.FromSeconds(6).Ticks },
            "future" => value with { ObservedUtcTicks = Now + 1 },
            "zero-time" => value with { ObservedUtcTicks = 0 },
            "mismatched-time" => value with { ObservedUtcTicks = Now - 1 },
            "schema" => value with { SchemaVersion = 2 },
            "qualification" => value with { Qualification = "verified" },
            "geometry-space" => value with { GeometrySpace = "client-pixels" },
            _ => value
        };
        result = fault switch
        {
            "loading" => result with { Loading = true },
            "unknown-loading" => result with { Loading = null },
            "cleanup" => result with { CleanupConfirmed = false },
            "hook" => result with { HookRestored = false },
            "unavailable" => result with { Status = "unavailable" },
            _ => result
        };
        Assert.False(W101DanceEntryContext.Valid(value, fault != "opt-out", result, Now));
    }

    [Theory]
    [InlineData("selected")]
    [InlineData("selection-reason")]
    [InlineData("missing-visibility")]
    [InlineData("missing-enabled")]
    [InlineData("negative-index")]
    [InlineData("out-of-range-parent")]
    [InlineData("wrong-root")]
    [InlineData("wrong-name")]
    [InlineData("arbitrary-text")]
    [InlineData("rectangle-size")]
    [InlineData("rectangle-inverted")]
    [InlineData("rectangle-range")]
    [InlineData("private-path")]
    [InlineData("null-control")]
    [InlineData("duplicate-index")]
    [InlineData("too-many")]
    public void MalformedOrPromotedControlEvidenceIsRejected(string fault)
    {
        var control = Control();
        control = fault switch
        {
            "selected" => control with { Selected = true },
            "selection-reason" => control with { SelectionReason = "verified" },
            "missing-visibility" => control with { Visible = null },
            "missing-enabled" => control with { Enabled = null },
            "negative-index" => control with { Index = -1 },
            "out-of-range-parent" => control with { Parent = 384 },
            "wrong-root" => control with { Path = ["Root", "OtherWorld", "PetGameTracks", "btnNext"] },
            "wrong-name" => control with { Name = "OtherControl" },
            "arbitrary-text" => control with { Text = "Not allowlisted" },
            "rectangle-size" => control with { RawRectangle = [1, 2, 3] },
            "rectangle-inverted" => control with { RawRectangle = [50, 20, 10, 40] },
            "rectangle-range" => control with { RawRectangle = [0, 0, 65537, 40] },
            "private-path" => control with { Path = ["Root", "WorldView", "PetGameTracks", "AccountPanel", "btnNext"] },
            _ => control
        };
        var value = Candidate() with
        {
            Controls = fault switch
            {
                "null-control" => [null!],
                "duplicate-index" => [control, control],
                "too-many" => Enumerable.Range(0, 97).Select(i => control with { Index = i }).ToArray(),
                _ => [control]
            }
        };
        Assert.False(W101DanceEntryContext.Valid(value, true, Result(), Now));
    }

    [Fact]
    public void EnergyLabelsRemainTextNotApprovedNumbers()
    {
        var control = Control() with
        {
            Path = ["Root", "WorldView", "PetGameTracks", "wndBkgEnergy", "txtEnergyCost"],
            Name = "txtEnergyCost",
            Text = "<center>8</center>",
            RawRectangle = null,
            Enabled = false
        };
        Assert.True(W101DanceEntryContext.Valid(Candidate() with { Controls = [control] }, true, Result(), Now));
        Assert.False(W101DanceEntryContext.Valid(Candidate() with { Controls = [control with { Text = new string('x', 161) }] }, true, Result(), Now));
    }

    private static string[] CliArgs(string root) => ["--cli", "ui-tree-watch", "--output", Path.Combine(root, "report.json"),
        "--workspace", root, "--window", "42", "--process-id", "123", "--started-utc-ticks", "456",
        "--enable-client-observation", "--enable-ui-tree"];

    [Fact]
    public void CliDanceDiscoveryIsSeparateDefaultOffOptIn()
    {
        var root = Path.Combine(Path.GetTempPath(), "dance-cli-" + Guid.NewGuid().ToString("N"));
        var args = CliArgs(root);
        Assert.False(MaelstromCli.Parse(args, root).IncludeDanceEntryContext);
        var accepted = MaelstromCli.Parse([.. args, "--include-dance-entry-context"], root);
        Assert.True(accepted.IncludeDanceEntryContext);
        Assert.False(accepted.ModuleActions);
        Assert.False(accepted.QuestActions);
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--include-dance-entry-context", "--include-dance-entry-context"], root));
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args.Where(x => x != "--enable-ui-tree"), "--include-dance-entry-context"], root));
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args.Where(x => x != "--enable-client-observation"), "--include-dance-entry-context"], root));
        args[1] = "wizwalker-watch";
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse([.. args, "--include-dance-entry-context"], root));
    }
}
