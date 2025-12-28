namespace ProjectMaelstrom.Models;

internal class ScriptDefinition
{
    public ScriptDefinition(string manifestPath, ScriptManifest manifest, string rootPath, string[]? validationErrors = null)
    {
        ManifestPath = manifestPath;
        Manifest = manifest;
        RootPath = rootPath;
        ValidationErrors = validationErrors ?? Array.Empty<string>();
    }

    public string ManifestPath { get; }
    public ScriptManifest Manifest { get; }
    public string RootPath { get; }
    public string DisplayName => string.IsNullOrWhiteSpace(Manifest.Description)
        ? Manifest.Name
        : $"{Manifest.Name} - {Manifest.Description}";
    public string[] ValidationErrors { get; }
}
