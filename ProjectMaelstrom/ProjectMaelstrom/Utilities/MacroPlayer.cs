using System.Text.Json;
using ProjectMaelstrom.Models;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Simple macro playback: loads InputCommand JSON and dispatches through executor with basic guards.
/// </summary>
internal static class MacroPlayer
{
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static bool Play(string macroPath, InputBridge? bridge, Func<GameStateSnapshot?> snapshotProvider, string? expectedResolution = null)
    {
        if (!File.Exists(macroPath))
        {
            MessageBox.Show("Macro file not found.", "Macro Runner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // Focus guard
        var sync = GameSyncService.Evaluate(expectedResolution);
        if (sync.Health == GameSyncHealth.FocusLost || sync.Health == GameSyncHealth.WindowMissing)
        {
            MessageBox.Show("Wizard101 is not focused/visible. Focus the game before running the macro.", "Macro Runner", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        // Health/energy guard
        var snap = snapshotProvider();
        if (snap != null)
        {
            var hp = snap.Health?.Current ?? int.MaxValue;
            if (hp < 50)
            {
                MessageBox.Show("Health too low to run macro (below 50).", "Macro Runner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        try
        {
            var json = File.ReadAllText(macroPath);
            var commands = ParseMacroCommands(json);
            if (commands == null || commands.Count == 0)
            {
                MessageBox.Show("Macro is empty or invalid.", "Macro Runner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            var policy = ExecutionPolicyManager.Current;
            var executor = ExecutorFactory.FromPolicy(policy);
            if (bridge == null && policy.AllowLiveAutomation)
            {
                MessageBox.Show("Live automation allowed but input bridge not ready.", "Macro Runner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            var ctx = new ExecutionContext { Source = "Macro", TaskName = Path.GetFileNameWithoutExtension(macroPath), Bridge = bridge };
            executor.Execute(commands, ctx);
            DevTelemetry.Log("Macros", $"Queued macro {Path.GetFileName(macroPath)} with {commands.Count} commands (mode={policy.Mode})");
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to play macro: {ex.Message}", "Macro Runner", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    internal static List<InputCommand>? ParseMacroCommands(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        var trimmed = json.TrimStart();
        if (trimmed.StartsWith('['))
        {
            return JsonSerializer.Deserialize<List<InputCommand>>(json, _jsonOpts);
        }

        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("nodes", out var nodesProp) && nodesProp.ValueKind == JsonValueKind.Array)
                {
                    var commands = new List<InputCommand>();
                    foreach (var node in nodesProp.EnumerateArray())
                    {
                        var kind = node.TryGetProperty("kind", out var k) ? k.GetString() : null;
                        var payload = node.TryGetProperty("payload", out var p) ? p : default;

                        if (kind == "interact_key" && payload.ValueKind == JsonValueKind.Object)
                        {
                            var key = payload.TryGetProperty("key", out var keyProp) ? keyProp.GetString() : "X";
                            commands.Add(new InputCommand { Type = "key_down", Key = key });
                            commands.Add(new InputCommand { Type = "delay", DelayMs = 100 });
                            commands.Add(new InputCommand { Type = "key_up", Key = key });
                        }
                        else if (kind == "click_ui" && payload.ValueKind == JsonValueKind.Object)
                        {
                            int cx = payload.TryGetProperty("client_x", out var xProp) ? xProp.GetInt32() : 0;
                            int cy = payload.TryGetProperty("client_y", out var yProp) ? yProp.GetInt32() : 0;
                            commands.Add(new InputCommand { Type = "click", X = cx, Y = cy });
                        }
                        else if (kind == "navigate_path")
                        {
                            var nodeId = node.TryGetProperty("node_id", out var idProp) ? idProp.GetString() : "nav";
                            commands.Add(new InputCommand { Type = "navigate_waypoints", Key = nodeId });
                        }
                        else if (kind == "combat_gate")
                        {
                            commands.Add(new InputCommand { Type = "combat_gate", Key = "combat-turn-decision" });
                        }
                    }
                    return commands;
                }
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}
