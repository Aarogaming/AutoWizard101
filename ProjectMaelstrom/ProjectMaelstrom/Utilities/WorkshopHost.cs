using System.Diagnostics;

namespace ProjectMaelstrom.Utilities;

/// <summary>Resolves the private Workshop runtime without requiring a system Python installation.</summary>
internal static class WorkshopHost
{
    internal static string DataRoot(string appRoot) => File.Exists(Path.Combine(appRoot, "portable.mode"))
        ? Path.Combine(appRoot, "Data")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Maelstrom");

    internal static ProcessStartInfo BuildStartInfo(string appRoot, string? workspace = null)
    {
        var runtime = Path.Combine(appRoot, "runtime", "python", "python.exe");
        var script = Path.Combine(appRoot, "tools", "game_pack_workshop.py");
        var dataRoot = DataRoot(appRoot);
        if (!File.Exists(runtime) || !File.Exists(script))
        {
            // A packaged runtime must be complete; only development builds use the checkout.
            if (File.Exists(Path.Combine(appRoot, "portable.mode")) || Directory.Exists(Path.Combine(appRoot, "runtime")))
                throw new InvalidOperationException("The Workshop runtime is incomplete. Extract the complete Maelstrom package again.");
            workspace ??= ToolIntegrationService.FindWorkspace(appRoot);
            if (workspace == null) throw new InvalidOperationException("The Workshop runtime is missing from this build.");
            runtime = Path.Combine(workspace, ".local", "envs", "dance", "Scripts", "python.exe");
            script = Path.Combine(workspace, "tools", "game_pack_workshop.py");
            dataRoot = Path.Combine(workspace, ".local");
        }
        if (!File.Exists(runtime) || !File.Exists(script))
            throw new InvalidOperationException("The Workshop runtime is missing. Build the portable edition or restore the development environment.");
        Directory.CreateDirectory(dataRoot);
        var info = ToolIntegrationService.BuildStartInfo(runtime, dataRoot, script,
            new[] { "--library", Path.Combine(dataRoot, "game-packs") });
        info.Environment.Remove("PYTHONHOME");
        info.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
        return info;
    }
}
