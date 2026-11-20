using System.Text.Json;
using ProjectMaelstrom.Models;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Writes the latest GameState snapshots to a shared cache file for external scripts.
/// </summary>
internal sealed class SnapshotBridge : IDisposable
{
    private readonly string _snapshotPath;
    private System.Threading.Timer? _timer;
    private bool _disposing;

    public SnapshotBridge()
    {
        string cacheDir = Path.Combine(StorageUtils.GetScriptsRoot(), ".cache");
        Directory.CreateDirectory(cacheDir);
        _snapshotPath = Path.Combine(cacheDir, "snapshot.json");
    }

    public void Start(TimeSpan interval)
    {
        Stop();
        _timer = new System.Threading.Timer(async _ => await WriteSnapshotSafeAsync(), null, TimeSpan.Zero, interval);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose()
    {
        _disposing = true;
        Stop();
    }

    private async Task WriteSnapshotSafeAsync()
    {
        if (_disposing) return;
        try
        {
            var snapshot = await GameStateService.CaptureSnapshotAsync(StateManager.Instance.SelectedResolution);
            WriteSnapshot(snapshot);
            ScriptLibraryService.Instance.SetLatestSnapshot("default", snapshot);
        }
        catch (Exception ex)
        {
            Logger.LogError("[SnapshotBridge] Failed to capture snapshot", ex);
        }
    }

    private void WriteSnapshot(GameStateSnapshot snapshot)
    {
        try
        {
            WriteSnapshotFile(_snapshotPath, snapshot);
        }
        catch (Exception ex)
        {
            Logger.LogError("[SnapshotBridge] Failed to write snapshot cache", ex);
        }
    }

    internal static void WriteSnapshotFile(string path, GameStateSnapshot snapshot)
    {
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(snapshot));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
