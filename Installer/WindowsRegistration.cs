using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace Installer;

public sealed class WindowsRegistration : IInstallRegistration
{
    private readonly string _root, _key, _startLink, _desktopLink;
    public WindowsRegistration(string root, string? registryKey = null, string? startLink = null, string? desktopLink = null)
    {
        _root = Path.GetFullPath(root);
        _key = registryKey ?? @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Maelstrom.Desktop";
        _startLink = startLink ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Maelstrom.lnk");
        _desktopLink = desktopLink ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Maelstrom.lnk");
    }

    private void CheckOwnership()
    {
        using var existing = Registry.CurrentUser.OpenSubKey(_key);
        if (existing != null && (!string.Equals(existing.GetValue("InstallLocation") as string, _root, StringComparison.OrdinalIgnoreCase)
            || existing.GetValue("ProductId") as string != InstallEngine.Product))
            throw new IOException("An unrelated Windows installation entry already uses this name.");
    }

    public void Apply(InstallRecord record)
    {
        CheckOwnership();
        var app = Path.Combine(_root, record.CurrentDirectory.Replace('/', Path.DirectorySeparatorChar));
        Shortcut(_startLink, Path.Combine(app, "Maelstrom.exe"));
        if (record.DesktopShortcut) Shortcut(_desktopLink, Path.Combine(app, "Maelstrom.exe"));
        else RemoveShortcut(_desktopLink);
        using var key = Registry.CurrentUser.CreateSubKey(_key);
        key.SetValue("ProductId", InstallEngine.Product);
        key.SetValue("DisplayName", "Maelstrom");
        key.SetValue("Publisher", "Aarogaming");
        key.SetValue("DisplayVersion", record.Version);
        key.SetValue("InstallLocation", _root);
        key.SetValue("DisplayIcon", Path.Combine(app, "Maelstrom.exe"));
        key.SetValue("UninstallString", $"\"{Path.Combine(app, "MaelstromSetup.exe")}\" --uninstall");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    public void Remove()
    {
        CheckOwnership();
        RemoveShortcut(_startLink);
        RemoveShortcut(_desktopLink);
        Registry.CurrentUser.DeleteSubKeyTree(_key, throwOnMissingSubKey: false);
    }

    private bool OwnsShortcut(string path)
    {
        if (!File.Exists(path)) return true;
        InstallEngine.RejectLinks(path);
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic link = shell.CreateShortcut(path);
        try { return InstallEngine.IsWithin(Path.GetFullPath((string)link.TargetPath), _root); }
        finally { Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell); }
    }

    private void Shortcut(string path, string target)
    {
        if (!OwnsShortcut(path)) throw new IOException("A different app already uses shortcut " + path);
        InstallEngine.RejectLinks(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic link = shell.CreateShortcut(path);
        try
        {
            link.TargetPath = target;
            link.WorkingDirectory = Path.GetDirectoryName(target);
            link.IconLocation = target;
            link.Description = "Maelstrom — Game Pack Workshop";
            link.Save();
        }
        finally { Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell); }
    }

    private void RemoveShortcut(string path)
    {
        if (File.Exists(path) && OwnsShortcut(path)) File.Delete(path);
    }
}
