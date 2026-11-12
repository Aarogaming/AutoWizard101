using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Installer;

public sealed record InstallRecord(int Schema, string Product, string Version, string CurrentDirectory,
    Dictionary<string, string> Files, bool DesktopShortcut, bool Installed = true);

public interface IInstallRegistration
{
    void Apply(InstallRecord record);
    void Remove();
}

/// <summary>Per-user install transaction. User data is never part of the removal ledger.</summary>
public sealed class InstallEngine
{
    public const string Product = "Maelstrom.Desktop";
    public const string ReceiptName = "installation.json";
    public string Root { get; }
    public string DataRoot { get; }
    private readonly IInstallRegistration _registration;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Maelstrom");
    public static string DefaultData => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Maelstrom");

    public InstallEngine(string root, string dataRoot, IInstallRegistration registration)
    {
        Root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        DataRoot = Path.GetFullPath(dataRoot).TrimEnd(Path.DirectorySeparatorChar);
        if (Root == Path.GetPathRoot(Root)?.TrimEnd(Path.DirectorySeparatorChar) || IsWithin(Root, DataRoot) || IsWithin(DataRoot, Root))
            throw new InvalidOperationException("Application and user-data folders must be separate.");
        _registration = registration;
    }

    public static bool IsWithin(string path, string root) => path.Equals(root, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    public static void RejectLinks(string path)
    {
        for (var current = new DirectoryInfo(Path.GetFullPath(path)); current != null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked application paths are not supported: " + current.FullName);
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Linked files are not supported: " + path);
    }

    private string Resolve(string relative)
    {
        ValidateRelative(relative);
        var path = Path.GetFullPath(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithin(path, Root) || path.Equals(Root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Path escapes application folder.");
        RejectLinks(path);
        return path;
    }

    public static void ValidateRelative(string name)
    {
        var parts = name.Replace('\\', '/').Split('/');
        foreach (var part in parts)
        {
            var stem = part.Split('.')[0].ToUpperInvariant();
            if (part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ')
                || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || new[] { "CON", "PRN", "AUX", "NUL" }.Contains(stem)
                || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && char.IsDigit(stem[3])))
                throw new IOException("Unsupported package path: " + name);
        }
    }

    public InstallRecord? ReadInstalled()
    {
        RejectLinks(Root);
        var path = Path.Combine(Root, ReceiptName);
        RejectLinks(path);
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new IOException("Installation record is too large.");
        var record = JsonSerializer.Deserialize<InstallRecord>(File.ReadAllText(path)) ?? throw new IOException("Invalid installation record.");
        if (record.Schema != 1 || record.Product != Product || record.Files == null || record.Files.Count > 40000)
            throw new IOException("This folder is not a supported Maelstrom installation.");
        ValidateRelative(record.CurrentDirectory);
        if (!record.CurrentDirectory.StartsWith("versions/", StringComparison.Ordinal) || record.CurrentDirectory.Split('/').Length != 2)
            throw new IOException("Invalid active version directory.");
        foreach (var (name, hash) in record.Files)
        {
            if (!name.StartsWith("versions/", StringComparison.Ordinal) || hash.Length != 64 || !hash.All(Uri.IsHexDigit))
                throw new IOException("Invalid installation file ledger.");
            Resolve(name);
        }
        return record;
    }

    private FileStream Lock()
    {
        RejectLinks(Root);
        Directory.CreateDirectory(Root);
        var path = Resolve("maintenance.lock");
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public void EnsureAppClosed()
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId) continue;
                string? path;
                try { path = process.MainModule?.FileName; }
                catch { continue; }
                if (path != null && IsWithin(Path.GetFullPath(path), Root))
                    throw new IOException("Close Maelstrom and its Workshop before continuing.");
            }
        }
    }

    public InstallRecord Install(Stream package, string maintenanceExe, string version, bool desktop, Action<string>? progress = null)
    {
        var previous = ReadInstalled();
        if (previous == null && Directory.Exists(Root) && Directory.EnumerateFileSystemEntries(Root).Any(p => Path.GetFileName(p) != "maintenance.lock"))
            throw new IOException("The application folder contains unrecognized files. Nothing was changed.");
        using var gate = Lock();
        previous = ReadInstalled();
        EnsureAppClosed();
        var relativeRoot = "versions/" + Guid.NewGuid().ToString("N");
        var created = new List<string>();
        var files = new Dictionary<string, string>(previous?.Files ?? new(), StringComparer.OrdinalIgnoreCase);
        var committed = false;
        try
        {
            progress?.Invoke("Checking and preparing application files…");
            using var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
            if (zip.Entries.Count > 40000 || zip.Entries.Sum(e => e.Length) > 2L * 1024 * 1024 * 1024)
                throw new IOException("Package exceeds installation limits.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in zip.Entries)
            {
                var name = entry.FullName.Replace('\\', '/').TrimEnd('/');
                ValidateRelative(name);
                if (!seen.Add(name)) throw new IOException("Duplicate package path: " + name);
                if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new IOException("Linked package entries are not supported.");
                if (name.Equals("Data", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Data/", StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Application packages must not contain user data.");
                if (name.Equals("portable.mode", StringComparison.OrdinalIgnoreCase)) continue;
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) continue;
                if (name.Equals("MaelstromSetup.exe", StringComparison.OrdinalIgnoreCase)) throw new IOException("Reserved maintenance filename.");
                var relative = relativeRoot + "/" + name;
                var destination = Resolve(relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                created.Add(relative);
                using (var source = entry.Open())
                using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    source.CopyTo(output);
                files[relative] = Hash(destination);
            }
            foreach (var required in new[] { "Maelstrom.exe", "ProjectMaelstrom.dll", "runtime/python/python.exe", "tools/game_pack_workshop.py" })
                if (!files.ContainsKey(relativeRoot + "/" + required)) throw new IOException("Incomplete application package: " + required);
            var maintenance = relativeRoot + "/MaelstromSetup.exe";
            created.Add(maintenance);
            File.Copy(maintenanceExe, Resolve(maintenance), overwrite: false);
            files[maintenance] = Hash(Resolve(maintenance));
            var record = new InstallRecord(1, Product, version, relativeRoot, files, desktop);
            progress?.Invoke("Adding Windows shortcuts and uninstall entry…");
            try
            {
                _registration.Apply(record);
                WriteReceipt(record);
            }
            catch
            {
                if (previous is { Installed: true }) _registration.Apply(previous); else _registration.Remove();
                throw;
            }
            committed = true;
            progress?.Invoke("Removing unchanged files from older versions…");
            if (previous != null)
            {
                try { foreach (var name in RemoveUnchanged(previous.Files)) files.Remove(name); }
                catch (IOException) { /* Receipt retains ownership; retry cleanup on the next update. */ }
                catch (UnauthorizedAccessException) { }
                // The earlier receipt already retains ownership if this cleanup write fails.
                try { WriteReceipt(record); } catch (IOException) { }
            }
            return record;
        }
        finally
        {
            if (!committed)
                foreach (var name in created.AsEnumerable().Reverse())
                {
                    var path = Resolve(name);
                    if (File.Exists(path)) File.Delete(path);
                    PruneEmpty(Path.GetDirectoryName(path)!);
                }
        }
    }

    public IReadOnlyList<string> Uninstall(Action<string>? progress = null)
    {
        var installed = ReadInstalled() ?? throw new IOException("No recorded installation was found.");
        using var gate = Lock();
        installed = ReadInstalled() ?? throw new IOException("The installation record changed.");
        if (!installed.Installed) throw new IOException("Maelstrom is already uninstalled. Your data is still available.");
        EnsureAppClosed();
        progress?.Invoke("Removing recorded application files. Your packs and reports stay in place…");
        var removed = RemoveUnchanged(installed.Files).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var remaining = installed.Files.Where(p => !removed.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);
        _registration.Remove();
        // Retain ownership metadata so reinstall can coexist with unknown/modified
        // files left in the app folder. This small receipt is not user data.
        WriteReceipt(installed with { Files = remaining, Installed = false });
        return remaining.Keys.ToList();
    }

    private IEnumerable<string> RemoveUnchanged(Dictionary<string, string> files)
    {
        foreach (var (name, expected) in files.OrderBy(pair => pair.Key.EndsWith("/MaelstromSetup.exe", StringComparison.OrdinalIgnoreCase)))
        {
            var path = Resolve(name);
            if (!File.Exists(path)) { yield return name; continue; }
            if (!Hash(path).Equals(expected, StringComparison.OrdinalIgnoreCase)) continue;
            File.Delete(path); // A lock/access error must remain a retryable failure.
            PruneEmpty(Path.GetDirectoryName(path)!);
            yield return name;
        }
    }

    private void PruneEmpty(string folder)
    {
        while (!folder.Equals(Root, StringComparison.OrdinalIgnoreCase) && IsWithin(folder, Root))
        {
            RejectLinks(folder);
            if (!Directory.Exists(folder) || Directory.EnumerateFileSystemEntries(folder).Any()) return;
            Directory.Delete(folder); // nonrecursive: unknown files are never removed
            folder = Path.GetDirectoryName(folder)!;
        }
    }

    private void WriteReceipt(InstallRecord record)
    {
        var temporary = Resolve("receipt-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(record, _json));
            File.Move(temporary, Resolve(ReceiptName), overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
