using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Closed, versioned catalog. A bundle configures known modules; loading
/// it cannot load a plugin, execute a script or acquire desktop input ownership.</summary>
internal static class ModuleCatalog
{
    public const int MaximumBundleCharacters = 1048576;

    public static OperationModuleDefinition Compile(OperationModuleSpec spec)
    {
        OperationModules.ValidateSpec(spec);
        return spec.Kind switch
        {
            "navigation" => NavigationModule.Compile(spec),
            "sigil" => SigilModule.Compile(spec),
            "menu" => MenuModule.Compile(spec),
            "auto-idle" => AutoIdleModule.Compile(spec),
            _ => throw new InvalidDataException("Unknown module kind.")
        };
    }

    public static OperationModuleDefinition[] Compile(OperationModuleBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        OperationModules.CheckId(bundle.Id); OperationModules.CheckId(bundle.GameId);
        OperationModules.CheckToken(bundle.ContextId, 512);
        if (bundle.SchemaVersion != 1 || !double.IsFinite(bundle.TimeoutMs) || bundle.TimeoutMs <= 0 || bundle.TimeoutMs > 300000 ||
            bundle.MaxActions is < 1 or > 10000 || bundle.Modules is null || bundle.Modules.Length is < 1 or > 16 ||
            bundle.Modules.Any(module => module is null || module.GameId != bundle.GameId) ||
            bundle.Modules.Select(module => module.Id).Distinct(StringComparer.Ordinal).Count() != bundle.Modules.Length)
            throw new InvalidDataException("Invalid module bundle identity, limits or membership.");
        var definitions = bundle.Modules.Select(Compile).ToArray();
        if (definitions.SelectMany(module => module.Actions).GroupBy(action => action.ActionId, StringComparer.Ordinal)
            .Any(group => group.Distinct().Count() != 1))
            throw new InvalidDataException("An action ID must keep the same binding throughout a bundle.");
        return definitions;
    }

    public static OperationModuleBundle ReadBundle(string json)
    {
        if (json.Length > MaximumBundleCharacters) throw new InvalidDataException("Module bundle exceeds size limit.");
        using var document = JsonDocument.Parse(json, new() { MaxDepth = 16 });
        RoutineGraphs.CheckProperties(document.RootElement);
        if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.TryGetProperty("live_qualified", out _))
            throw new InvalidDataException("A module file cannot provide live qualification.");
        var bundle = document.RootElement.Deserialize<OperationModuleBundle>(OperationModules.Json)
            ?? throw new InvalidDataException("Missing module bundle.");
        Compile(bundle);
        return bundle;
    }

    public static void Save(OperationModuleBundle bundle, string newPath)
    {
        // Validate the exact representation that will be saved, including nested configuration.
        Compile(bundle);
        var json = JsonSerializer.Serialize(bundle, OperationModules.Json);
        ReadBundle(json);
        using var output = new FileStream(newPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(output);
        writer.Write(json);
    }
}
