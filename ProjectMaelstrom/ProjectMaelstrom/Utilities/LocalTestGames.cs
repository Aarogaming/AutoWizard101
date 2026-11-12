using System.Diagnostics;

namespace ProjectMaelstrom.Utilities;

/// <summary>Locates optional local test games; neither game is an application dependency.</summary>
internal static class LocalTestGames
{
    internal static string ReflexPath(string workspace) => Path.Combine(workspace, ".local", "test-games", "reflex-offline-679450da", "index.html");
    internal static string DodgePath(string workspace) => Path.Combine(workspace, ".local", "test-games", "godot-dodge-the-creeps-4.7-6ad6167", "source", "dodge_the_creeps", "project.godot");
    internal static string GodotPath(string workspace) => Path.Combine(workspace, ".local", "test-games", "godot-4.7.2", "runtime", "Godot_v4.7.2-stable_win64.exe");

    internal static ProcessStartInfo ReflexStartInfo(string workspace, string edge)
    {
        var game = ReflexPath(workspace);
        if (!File.Exists(game)) throw new FileNotFoundException("The offline Reflex copy is missing. See the test-games guide.", game);
        if (!File.Exists(edge)) throw new FileNotFoundException("Microsoft Edge is needed to display this local HTML game.", edge);
        var info = new ProcessStartInfo(edge) { UseShellExecute = false };
        info.ArgumentList.Add("--app=" + new Uri(Path.GetFullPath(game)).AbsoluteUri);
        info.ArgumentList.Add("--user-data-dir=" + Path.Combine(workspace, ".local", "test-games", "reflex-browser-profile"));
        info.ArgumentList.Add("--no-first-run");
        return info;
    }

    internal static ProcessStartInfo DodgeStartInfo(string workspace)
    {
        var project = DodgePath(workspace);
        var godot = GodotPath(workspace);
        if (!File.Exists(project) || !File.Exists(godot)) throw new FileNotFoundException("Dodge the Creeps or its portable Godot runtime is missing. See the test-games guide.");
        var info = new ProcessStartInfo(godot) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(project)! };
        info.ArgumentList.Add("--path");
        info.ArgumentList.Add(Path.GetDirectoryName(project)!);
        return info;
    }
}
