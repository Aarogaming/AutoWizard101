using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using ProjectMaelstrom.Models;
using Xunit;

namespace ProjectMaelstrom.Tests;

public class RunnerLogicTests
{
    private static readonly Type ScriptLibrary = typeof(ProjectMaelstrom.Utilities.ScriptLibraryService);
    private static readonly MethodInfo BuildTargetFolderName = ScriptLibrary.GetMethod("BuildTargetFolderName", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ResolveGitHubDownloadUrl = ScriptLibrary.GetMethod("ResolveGitHubDownloadUrl", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ValidateManifest = ScriptLibrary.GetMethod("ValidateManifest", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Theory]
    [InlineData("https://github.com/owner/repo", null, "repo_main_")]
    [InlineData("https://github.com/owner/repo.git", "dev", "repo_dev_")]
    public void BuildTargetFolderName_UsesRepoAndRef(string url, string? branch, string expectedPrefix)
    {
        var result = (string)BuildTargetFolderName.Invoke(null, new object?[] { url, branch })!;
        Assert.StartsWith(expectedPrefix, result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://github.com/owner/repo", null, "owner/repo", "refs/heads/main")]
    [InlineData("https://github.com/owner/repo", "dev", "owner/repo", "refs/heads/dev")]
    [InlineData("https://github.com/owner/repo/tree/feature", null, "owner/repo", "refs/heads/feature")]
    public void ResolveGitHubDownloadUrl_MapsGitHubInputsToCodeload(string url, string? branch, string expectedPathPrefix, string expectedRef)
    {
        var result = (string)ResolveGitHubDownloadUrl.Invoke(null, new object?[] { url, branch })!;
        var uri = new Uri(result);

        Assert.Equal("https", uri.Scheme);
        Assert.Equal("codeload.github.com", uri.Host, StringComparer.OrdinalIgnoreCase);
        Assert.Equal($"/{expectedPathPrefix}/zip/{expectedRef}", uri.AbsolutePath, StringComparer.Ordinal);
    }

    [Fact]
    public void ResolveGitHubDownloadUrl_ReturnsNonGitHubUrlUnchanged()
    {
        const string url = "https://example.com/custom.zip";
        var result = (string)ResolveGitHubDownloadUrl.Invoke(null, new object?[] { url, null })!;
        Assert.Equal(url, result);
    }

    [Theory]
    [InlineData("@echo %cd%>wd.txt\r\nexit /b 0", "")]
    [InlineData("@echo %1 %2>args.txt\r\nexit /b 0", "\"argument with spaces\" arg2")]
    [InlineData("@for /l %%i in (1,1,400) do @echo LINE%%i>>out.txt\r\nexit /b 0", "")]
    [InlineData("@echo ERR_MSG 1>&2\r\n@echo ERR_MSG>err.txt\r\nexit /b 0", "")]
    [InlineData("@exit /b 7", "")]
    [InlineData("@echo OUTLINE>out.txt\r\n@echo ERRLINE 1>&2\r\n@echo ERRLINE>err.txt\r\nexit /b 0", "")]
    public void LaunchProcess_RejectsLegacyCodeBeforeAnyBatchOrOutputSideEffect(string content, string arguments)
    {
        using var temp = new TempFolder();
        var script = temp.WriteScript(content);
        var invocation = Assert.Throws<TargetInvocationException>(() => InvokeLaunchProcess(script, arguments, temp.Path));
        var error = Assert.IsType<InvalidOperationException>(invocation.InnerException);
        Assert.Contains("UI-only mode", error.Message);
        Assert.Equal(content, File.ReadAllText(script));
        Assert.Equal(new[] { script }, Directory.GetFiles(temp.Path));
    }

    private static Process InvokeLaunchProcess(string entryFullPath, string arguments, string workingDirectory)
    {
        var method = ScriptLibrary.GetMethod("LaunchProcess", BindingFlags.NonPublic | BindingFlags.Static)!;
        var process = (Process)method.Invoke(null, new object?[] { entryFullPath, arguments, workingDirectory })!;
        return process;
    }

    [Fact]
    public void ValidateManifest_ReturnsErrors_ForMissingFields()
    {
        using var temp = new TempFolder();
        var manifest = new ScriptManifest { Name = "", EntryPoint = "" };
        var errors = (System.Collections.IEnumerable)ValidateManifest.Invoke(null, new object[] { temp.Path, manifest })!;
        var errorList = errors.Cast<string>().ToList();
        Assert.Contains("Name is required", errorList);
        Assert.Contains("Entry point is required", errorList);
    }

    [Fact]
    public void ValidateManifest_FlagsMissingEntryPointFile()
    {
        using var temp = new TempFolder();
        var manifest = new ScriptManifest { Name = "sample", EntryPoint = "missing.exe" };
        var errors = (System.Collections.IEnumerable)ValidateManifest.Invoke(null, new object[] { temp.Path, manifest })!;
        Assert.Contains("Entry point missing", errors.Cast<string>());
    }

    private sealed class TempFolder : IDisposable
    {
        public string Path { get; }
        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"maelstrom_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string WriteScript(string content)
        {
            var scriptPath = System.IO.Path.Combine(Path, "script.bat");
            File.WriteAllText(scriptPath, content);
            return scriptPath;
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* ignore */ }
        }
    }
}
