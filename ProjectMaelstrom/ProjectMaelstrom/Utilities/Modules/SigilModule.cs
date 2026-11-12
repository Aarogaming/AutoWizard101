namespace ProjectMaelstrom.Utilities;

internal sealed record SigilConfiguration(string EntryScene, string MenuScene,
    ModuleAction Interaction, double TimeoutMs = 15000);

/// <summary>Compiles an observed interaction point into one bounded menu-opening
/// transition. Game packs supply the scene names, named control and action binding.
/// A dispatch receipt and a newer proposal-bound menu observation are both required.</summary>
internal static class SigilModule
{
    public static OperationModuleDefinition Compile(OperationModuleSpec spec)
    {
        OperationModules.ValidateSpec(spec);
        if (spec.Kind != "sigil") throw new InvalidDataException("Choose a sigil module specification.");
        var configuration = OperationModules.ReadConfiguration<SigilConfiguration>(spec);
        OperationModules.CheckId(configuration.EntryScene);
        OperationModules.CheckId(configuration.MenuScene);
        if (configuration.EntryScene == configuration.MenuScene ||
            configuration.EntryScene is "Unknown" or "Loading" || configuration.MenuScene is "Unknown" or "Loading" ||
            !double.IsFinite(configuration.TimeoutMs) || configuration.TimeoutMs <= 0 || configuration.TimeoutMs > 300000)
            throw new InvalidDataException("A sigil needs distinct entry/menu scenes and a finite bounded timeout.");
        OperationModules.ValidateActions([configuration.Interaction]);
        var graph = new RoutineGraph(1, spec.Id, spec.GameId, "entry", configuration.TimeoutMs, 1,
            ["Unknown", "Loading"],
            [
                new("entry", [new("interact", configuration.EntryScene, configuration.Interaction.ActionId,
                    [configuration.MenuScene], "done", configuration.TimeoutMs,
                    RequiresControl: true, RequiresOutcomeBinding: true)]),
                new("done", [], configuration.MenuScene)
            ]);
        return OperationModules.Define(spec, graph, configuration.Interaction);
    }
}
