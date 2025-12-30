using System;
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
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* ignore */ }
        }
    }
}
