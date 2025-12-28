using Microsoft.Win32;

namespace ProjectMaelstrom.Utilities;

internal static class WizardLauncher
{
    private static readonly string[] CandidatePaths =
    {
        @"C:\ProgramData\KingsIsle Entertainment\Wizard101\PlayWizard101.exe",
        @"C:\Program Files (x86)\KingsIsle Entertainment\Wizard101\PlayWizard101.exe",
        @"C:\Program Files\KingsIsle Entertainment\Wizard101\PlayWizard101.exe",
        @"C:\KingsIsle Entertainment\Wizard101\PlayWizard101.exe"
    };

    public static string? FindClient()
    {
        try
        {
            var regPath = FindFromRegistry();
            if (!string.IsNullOrEmpty(regPath))
            {
                return regPath;
            }

            foreach (var path in CandidatePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("[WizardLauncher] Failed to search for client", ex);
        }

        return null;
    }

    private static string? FindFromRegistry()
    {
        string? found = TryRegistryKey(@"SOFTWARE\KingsIsle Entertainment\Wizard101");
        if (!string.IsNullOrEmpty(found))
        {
            return found;
        }

        found = TryRegistryKey(@"SOFTWARE\WOW6432Node\KingsIsle Entertainment\Wizard101");
        return found;
    }

    private static string? TryRegistryKey(string keyPath)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            if (key == null)
            {
                return null;
            }

            var installDir = key.GetValue("InstallLocation") as string ?? key.GetValue("InstallDir") as string;
            if (string.IsNullOrEmpty(installDir))
            {
                return null;
            }

            string candidate = Path.Combine(installDir, "PlayWizard101.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("[WizardLauncher] Registry search failed", ex);
        }

        return null;
    }

    public static bool Launch(out string message)
    {
        message = string.Empty;
        string? client = FindClient();
        if (string.IsNullOrEmpty(client))
        {
            var dialog = new OpenFileDialog
            {
                Title = "Locate PlayWizard101.exe",
                Filter = "PlayWizard101|PlayWizard101.exe|Exe Files|*.exe",
                Multiselect = false
            };
            if (dialog.ShowDialog() == DialogResult.OK && File.Exists(dialog.FileName))
            {
                client = dialog.FileName;
            }
            else
            {
                message = "Wizard101 client not found. Please install or set a custom path.";
                return false;
            }
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = client,
                UseShellExecute = true
            });
            message = $"Launched Wizard101 from {client}";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Failed to launch Wizard101: {ex.Message}";
            Logger.LogError("[WizardLauncher] Launch failed", ex);
            return false;
        }
    }
}
