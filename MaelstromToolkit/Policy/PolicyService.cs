using System.Security.Cryptography;
using System.Text;

namespace MaelstromToolkit.Policy;

internal sealed class PolicyService
{
    private readonly PolicyParser _parser = new();

    public PolicyLoadResult Load(string policyPath)
    {
        var text = File.Exists(policyPath) ? File.ReadAllText(policyPath) : string.Empty;
        var result = _parser.Parse(text);
        return result;
    }

    public PolicyLoadResult LoadFromText(string text) => _parser.Parse(text);

    public string WriteLkg(string outRoot, string content)
    {
        var systemDir = Path.Combine(outRoot, "system");
        Directory.CreateDirectory(systemDir);
        var lkgPath = Path.Combine(systemDir, "policy.lkg.txt");
        WriteAtomic(lkgPath, content);
        var hash = ComputeSha256(content);
        WriteAtomic(Path.Combine(systemDir, "policy.lkg.sha256"), hash);
        return hash;
    }

    public void WriteRejected(string outRoot, PolicyLoadResult result)
    {
        var systemDir = Path.Combine(outRoot, "system");
        Directory.CreateDirectory(systemDir);
        var rejectedPath = Path.Combine(systemDir, "policy.rejected.txt");
        var diagnostics = FormatDiagnostics(result);
        WriteAtomic(rejectedPath, diagnostics);
    }

    public void WriteDiagnostics(string outRoot, PolicyLoadResult result)
    {
        var systemDir = Path.Combine(outRoot, "system");
        Directory.CreateDirectory(systemDir);
        var diagPath = Path.Combine(systemDir, "policy.diagnostics.txt");
        WriteAtomic(diagPath, FormatDiagnostics(result));
    }

    public PolicyLoadResult TryLoadLkg(string outRoot)
    {
        var lkgPath = Path.Combine(outRoot, "system", "policy.lkg.txt");
        if (!File.Exists(lkgPath))
        {
            return new PolicyLoadResult
            {
                Document = null,
                Diagnostics = { new PolicyDiagnostic("POL990", DiagnosticSeverity.Error, "lkg", "missing", null, "No LKG found.") }
            };
        }

        var text = File.ReadAllText(lkgPath);
        var result = _parser.Parse(text);
        if (result.Document == null)
        {
            result.Diagnostics.Add(new PolicyDiagnostic("POL991", DiagnosticSeverity.Error, "lkg", "invalid", null, "LKG exists but is invalid."));
        }
        return result;
    }

    public static string FormatDiagnostics(PolicyLoadResult result)
    {
        var lines = result.SortedDiagnostics()
            .Select(d =>
            {
                var line = d.Line.HasValue ? d.Line.Value.ToString() : "-";
                return $"{d.Code}|{d.Severity}|{d.Section}|{d.Key}|{line}|{d.Message}";
            });
        return string.Join(Environment.NewLine, lines);
    }

    private static void WriteAtomic(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
        var tmp = path + ".tmp_" + Guid.NewGuid().ToString("N");
        File.WriteAllText(tmp, content, new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }

    private static string ComputeSha256(string content)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = sha.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}
