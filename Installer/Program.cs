using System.Reflection;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Installer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length > 0 && args[0] == "--install-cli")
        {
            // Explicit installation only; reserve a new report before mutation.
            // Same transaction/rollback and app-closed checks as SetupForm.
            try
            {
                if (args.Length != 2) throw new ArgumentException("Expected --install-cli <new-report.json>.");
                using var report = new FileStream(Path.GetFullPath(args[1]), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                try
                {
                    var engine = new InstallEngine(InstallEngine.DefaultRoot, InstallEngine.DefaultData,
                        new WindowsRegistration(InstallEngine.DefaultRoot));
                    var prior = engine.ReadInstalled();
                    using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("Maelstrom.Payload.zip")
                        ?? throw new IOException("Installer payload is missing.");
                    var installed = engine.Install(payload, Environment.ProcessPath!, "0.2.0-preview", prior?.DesktopShortcut ?? false);
                    JsonSerializer.Serialize(report, new { installed = true, directory = Path.Combine(engine.Root, installed.CurrentDirectory), dataRoot = engine.DataRoot });
                }
                catch (Exception ex)
                {
                    Environment.ExitCode = 1;
                    JsonSerializer.Serialize(report, new { installed = false, message = ex.Message });
                }
            }
            catch (Exception) { Environment.ExitCode = 1; }
            return;
        }
        try
        {
            if (args.Length == 2 && args[0] == "--verify-lifecycle")
            {
                try { InstallerVerification.Run(Path.GetFullPath(args[1])); }
                catch (Exception ex) { File.WriteAllText(Path.GetFullPath(args[1]) + ".error.txt", ex.ToString()); Environment.ExitCode = 1; }
                return;
            }
            if (args.Length == 2 && args[0] is "--preview" or "--preview-uninstall")
            {
                using var preview = new SetupForm(args[0] == "--preview-uninstall");
                preview.StartPosition = FormStartPosition.Manual;
                preview.Location = new Point(-30000, -30000);
                preview.Show();
                Application.DoEvents();
                using var bitmap = new Bitmap(preview.Width, preview.Height);
                preview.DrawToBitmap(bitmap, new Rectangle(Point.Empty, preview.Size));
                bitmap.Save(Path.GetFullPath(args[1]), System.Drawing.Imaging.ImageFormat.Png);
                preview.Close();
                return;
            }
            var uninstall = args.Contains("--uninstall");
            if (uninstall && InstallEngine.IsWithin(Path.GetFullPath(Environment.ProcessPath!), Path.GetFullPath(InstallEngine.DefaultRoot)))
            {
                // Run outside the app folder so Windows can remove this installation.
                var folder = Path.Combine(Path.GetTempPath(), "Maelstrom-uninstall-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(folder);
                var copy = Path.Combine(folder, "MaelstromSetup.exe");
                File.Copy(Environment.ProcessPath!, copy);
                var info = new ProcessStartInfo(copy) { UseShellExecute = false, WorkingDirectory = folder };
                info.ArgumentList.Add("--uninstall");
                Process.Start(info);
                return;
            }
            Application.Run(new SetupForm(uninstall));
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Maelstrom Setup", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
