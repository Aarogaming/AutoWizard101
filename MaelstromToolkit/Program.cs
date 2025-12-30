using System.Reflection;
using System.Text;

namespace MaelstromToolkit;

internal static class Program
{
    private record CommandOptions(
        string Command,
        string Subcommand,
        Dictionary<string, string> Args,
        bool Force,
        bool DryRun,
        bool Verbose,
        bool ShowHelp,
        bool ShowVersion);

    private static int Main(string[] args)
    {
        var options = Parse(args);
        if (options == null || options.ShowHelp)
        {
            PrintUsage();
            return options == null ? 1 : 0;
        }

        if (options.ShowVersion)
        {
            Console.WriteLine(GetVersion());
            return 0;
        }

        var root = options.Args.TryGetValue("out", out var outDir)
            ? Path.GetFullPath(outDir)
            : Directory.GetCurrentDirectory();

        var summary = new List<string>();
        var warnings = new List<string>();

        try
        {
            switch (options.Command)
            {
                case "init":
                    RunInit(root, options, summary, warnings);
                    break;
                case "policy" when options.Subcommand == "init":
                    CopyTemplate(root, "POLICY_BOUNDARY.md", options, summary, warnings);
                    CopyTemplate(root, "policy.config.sample", options, summary, warnings);
                    break;
                case "tags" when options.Subcommand == "init":
                    CopyTemplate(root, "TAG_POLICY.md", options, summary, warnings);
                    break;
                case "stewardship" when options.Subcommand == "init":
                    CopyTemplate(root, "STEWARDSHIP_CHECKLIST.md", options, summary, warnings);
                    CopyTemplate(root, "FEEDBACK_LOG.md", options, summary, warnings);
                    break;
                case "ux" when options.Subcommand == "init":
                    var framework = options.Args.TryGetValue("framework", out var fw) ? fw : "winforms";
                    CopyTemplate(root, "UX_MAINTENANCE.md", options, summary, warnings);
                    CopyTemplate(root, "UX_STYLE_GUIDE.md", options, summary, warnings, framework);
                    CopyTemplate(root, "UX_CHANGELOG.md", options, summary, warnings, framework);
                    CopyTemplate(root, "UX_TOKENS.md", options, summary, warnings, framework);
                    break;
                case "ci" when options.Subcommand == "add":
                    var provider = options.Args.TryGetValue("provider", out var p) ? p : "github";
                    var profile = options.Args.TryGetValue("profile", out var pr) ? pr : "tools-only";
                    CopyTemplate(root, $"{provider}_{profile}_workflow.yml", options, summary, warnings);
                    break;
                case "handoff":
                    CopyTemplate(root, "README.md", options, summary, warnings);
                    break;
                case "selftest":
                    return RunSelftest(options, summary, warnings);
                default:
                    PrintUsage();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }

        if (summary.Count > 0)
        {
            Console.WriteLine("Generated:");
            foreach (var line in summary) Console.WriteLine($"- {line}");
        }
        if (warnings.Count > 0)
        {
            Console.WriteLine("Warnings:");
            foreach (var line in warnings) Console.WriteLine($"- {line}");
        }

        return 0;
    }

    private static CommandOptions? Parse(string[] args)
    {
        if (args.Length == 0) return null;
        var showHelp = args.Contains("--help", StringComparer.OrdinalIgnoreCase);
        var showVersion = args.Contains("--version", StringComparer.OrdinalIgnoreCase);
        string? command = null;
        string sub = string.Empty;
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var force = args.Contains("--force", StringComparer.OrdinalIgnoreCase);
        var dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
        var verbose = args.Contains("--verbose", StringComparer.OrdinalIgnoreCase);

        foreach (var arg in args)
        {
            if (arg.StartsWith("--", StringComparison.Ordinal)) continue;
            if (command == null)
            {
                command = arg;
            }
            else if (string.IsNullOrEmpty(sub))
            {
                sub = arg;
            }
        }

        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
            var key = args[i][2..];
            if (key is "force" or "dry-run" or "verbose" or "help" or "version") continue;
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                dict[key] = args[i + 1];
            }
        }

        if (string.IsNullOrWhiteSpace(command)) return null;
        return new CommandOptions(command, sub, dict, force, dryRun, verbose, showHelp, showVersion);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("MaelstromToolkit (net8.0) - safe, deterministic scaffolds");
        Console.WriteLine("Usage (examples):");
        Console.WriteLine("  maelstromtoolkit init --out ./out");
        Console.WriteLine("  maelstromtoolkit policy init --out ./out");
        Console.WriteLine("  maelstromtoolkit tags init --out ./out");
        Console.WriteLine("  maelstromtoolkit stewardship init --out ./out");
        Console.WriteLine("  maelstromtoolkit ux init --framework winforms --out ./out");
        Console.WriteLine("  maelstromtoolkit ci add --provider github --profile tools-only --out ./out");
        Console.WriteLine("  maelstromtoolkit handoff --out ./out");
        Console.WriteLine("  maelstromtoolkit selftest");
        Console.WriteLine("Flags: --force (overwrite), --dry-run, --verbose, --help, --version, --out <dir>");
    }

    private static void RunInit(string root, CommandOptions options, List<string> summary, List<string> warnings)
    {
        CopyTemplate(root, "POLICY_BOUNDARY.md", options, summary, warnings);
        CopyTemplate(root, "policy.config.sample", options, summary, warnings);
        CopyTemplate(root, "TAG_POLICY.md", options, summary, warnings);
        CopyTemplate(root, "STEWARDSHIP_CHECKLIST.md", options, summary, warnings);
        CopyTemplate(root, "FEEDBACK_LOG.md", options, summary, warnings);
        CopyTemplate(root, "UX_MAINTENANCE.md", options, summary, warnings);
    }

    private static void CopyTemplate(string targetRoot, string templateName, CommandOptions options, List<string> summary, List<string> warnings, string? framework = null)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Templates", templateFolderFor(templateName), templateName);
        if (!File.Exists(source))
        {
            warnings.Add($"Template missing: {templateName}");
            return;
        }
        var content = File.ReadAllText(source);
        if (!string.IsNullOrWhiteSpace(framework))
        {
            content = content.Replace("{{FRAMEWORK}}", framework, StringComparison.OrdinalIgnoreCase);
        }
        var dest = Path.Combine(targetRoot, templateName);
        if (File.Exists(dest) && !options.Force)
        {
            warnings.Add($"Skipped existing file: {dest}");
            return;
        }
        WriteFile(dest, content, options, summary);
    }

    private static int RunSelftest(CommandOptions options, List<string> summary, List<string> warnings)
    {
        var templates = new[]
        {
            ("Policy","POLICY_BOUNDARY.md"),
            ("Policy","policy.config.sample"),
            ("Tags","TAG_POLICY.md"),
            ("Stewardship","STEWARDSHIP_CHECKLIST.md"),
            ("Stewardship","FEEDBACK_LOG.md"),
            ("UX","UX_MAINTENANCE.md"),
            ("UX","UX_STYLE_GUIDE.md"),
            ("UX","UX_CHANGELOG.md"),
            ("UX","UX_TOKENS.md"),
            ("CI","github_tools-only_workflow.yml"),
            ("Handoff","README.md"),
        };

        foreach (var (folder, name) in templates)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Templates", folder, name);
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"SELFTEST FAIL: missing template {folder}/{name}");
                return 1;
            }
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"maelstromtoolkit_selftest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var testOptions = options with { Force = false };
        CopyTemplate(tempDir, "POLICY_BOUNDARY.md", testOptions, summary, warnings);
        CopyTemplate(tempDir, "policy.config.sample", testOptions, summary, warnings);
        CopyTemplate(tempDir, "TAG_POLICY.md", testOptions, summary, warnings);
        CopyTemplate(tempDir, "STEWARDSHIP_CHECKLIST.md", testOptions, summary, warnings);
        CopyTemplate(tempDir, "FEEDBACK_LOG.md", testOptions, summary, warnings);
        CopyTemplate(tempDir, "UX_MAINTENANCE.md", testOptions, summary, warnings, "winforms");
        CopyTemplate(tempDir, "UX_STYLE_GUIDE.md", testOptions, summary, warnings, "winforms");
        CopyTemplate(tempDir, "UX_CHANGELOG.md", testOptions, summary, warnings, "winforms");
        CopyTemplate(tempDir, "UX_TOKENS.md", testOptions, summary, warnings, "winforms");
        CopyTemplate(tempDir, "github_tools-only_workflow.yml", testOptions, summary, warnings);
        CopyTemplate(tempDir, "README.md", testOptions, summary, warnings);

        try { Directory.Delete(tempDir, true); } catch { /* ignore */ }

        Console.WriteLine("SELFTEST PASS");
        return 0;
    }

    private static void WriteFile(string path, string content, CommandOptions options, List<string> summary)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, NormalizeLineEndings(content), utf8);
        if (options.DryRun)
        {
            File.Delete(tmp);
            summary.Add($"(dry-run) {path}");
            return;
        }
        File.Move(tmp, path, overwrite: true);
        summary.Add(path);
        if (options.Verbose)
        {
            Console.WriteLine($"Wrote {path}");
        }
    }

    private static string NormalizeLineEndings(string input) =>
        input.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);

    private static string GetVersion() =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    private static string templateFolderFor(string templateName)
    {
        if (templateName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)) return "CI";
        if (templateName.Contains("POLICY", StringComparison.OrdinalIgnoreCase) || templateName.Contains("policy", StringComparison.OrdinalIgnoreCase)) return "Policy";
        if (templateName.Contains("TAG", StringComparison.OrdinalIgnoreCase)) return "Tags";
        if (templateName.Contains("FEEDBACK", StringComparison.OrdinalIgnoreCase) || templateName.Contains("STEWARDSHIP", StringComparison.OrdinalIgnoreCase)) return "Stewardship";
        if (templateName.StartsWith("UX_", StringComparison.OrdinalIgnoreCase) || templateName.StartsWith("UX ", StringComparison.OrdinalIgnoreCase) || templateName.Contains("UX", StringComparison.OrdinalIgnoreCase)) return "UX";
        if (templateName.Equals("README.md", StringComparison.OrdinalIgnoreCase)) return "Handoff";
        return string.Empty;
    }
}
