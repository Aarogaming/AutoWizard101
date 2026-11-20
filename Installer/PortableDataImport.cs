namespace Installer;

public sealed record ImportSummary(int PackVersions, int Reports, int Skipped);

public static class PortableDataImport
{
    public static ImportSummary Copy(string source, string destination)
    {
        source = Path.GetFullPath(source);
        destination = Path.GetFullPath(destination);
        if (InstallEngine.IsWithin(source, destination) || InstallEngine.IsWithin(destination, source))
            throw new IOException("Choose a separate portable Data folder.");
        InstallEngine.RejectLinks(source);
        InstallEngine.RejectLinks(destination);
        if (!Directory.Exists(Path.Combine(source, "game-packs")) && !Directory.Exists(Path.Combine(source, "reports")))
            throw new IOException("Choose the Data folder containing game-packs or reports.");
        Directory.CreateDirectory(destination);
        int versions = 0, reports = 0, skipped = 0;
        long bytes = 0;
        int count = 0;
        void CheckFile(string file)
        {
            InstallEngine.RejectLinks(file);
            bytes += new FileInfo(file).Length;
            if (++count > 40000 || bytes > 2L * 1024 * 1024 * 1024) throw new IOException("Import exceeds 40000 files or 2 GiB; copy a smaller selection.");
        }
        IEnumerable<string> Files(string folder)
        {
            InstallEngine.RejectLinks(folder);
            foreach (var file in Directory.GetFiles(folder)) { InstallEngine.RejectLinks(file); yield return file; }
            foreach (var child in Directory.GetDirectories(folder))
                foreach (var file in Files(child)) yield return file;
        }
        var packs = Path.Combine(source, "game-packs");
        if (Directory.Exists(packs))
        {
            InstallEngine.RejectLinks(packs);
            foreach (var pack in Directory.GetDirectories(packs))
            {
                InstallEngine.RejectLinks(pack);
                foreach (var version in Directory.GetDirectories(pack))
                {
                    InstallEngine.RejectLinks(version);
                    var relative = Path.GetRelativePath(packs, version);
                    InstallEngine.ValidateRelative(relative);
                    var target = Path.Combine(destination, "game-packs", relative);
                    InstallEngine.RejectLinks(target);
                    if (Directory.Exists(target) || File.Exists(target)) { skipped++; continue; }
                    var parent = Path.GetDirectoryName(target)!;
                    Directory.CreateDirectory(parent);
                    var stage = Path.Combine(parent, ".import-" + Guid.NewGuid().ToString("N"));
                    var created = new List<string>();
                    try
                    {
                        foreach (var file in Files(version))
                        {
                            CheckFile(file);
                            var name = Path.GetRelativePath(version, file);
                            InstallEngine.ValidateRelative(name);
                            var output = Path.Combine(stage, name);
                            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                            created.Add(output);
                            File.Copy(file, output, overwrite: false);
                        }
                        if (!File.Exists(Path.Combine(stage, "pack.json"))) { skipped++; continue; }
                        Directory.Move(stage, target); // Existing versions are never merged or replaced.
                        versions++;
                    }
                    finally
                    {
                        if (Directory.Exists(stage))
                        {
                            foreach (var path in created)
                            {
                                if (!InstallEngine.IsWithin(Path.GetFullPath(path), Path.GetFullPath(stage))) throw new IOException("Invalid import cleanup path.");
                                InstallEngine.RejectLinks(path);
                                if (File.Exists(path)) File.Delete(path);
                            }
                            foreach (var folder in Directory.GetDirectories(stage, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length))
                            { InstallEngine.RejectLinks(folder); if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder); }
                            if (!Directory.EnumerateFileSystemEntries(stage).Any()) Directory.Delete(stage);
                        }
                    }
                }
            }
        }
        var reportFolder = Path.Combine(source, "reports");
        if (Directory.Exists(reportFolder))
        {
            InstallEngine.RejectLinks(reportFolder);
            foreach (var file in Directory.GetFiles(reportFolder, "*.json"))
            {
                CheckFile(file);
                var target = Path.Combine(destination, "reports", Path.GetFileName(file));
                InstallEngine.RejectLinks(target);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (File.Exists(target)) { skipped++; continue; }
                var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try { File.Copy(file, temporary, false); File.Move(temporary, target); reports++; }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }
        return new ImportSummary(versions, reports, skipped);
    }
}
