using System;
using System.IO;

namespace ProjectMaelstrom.Utilities;

internal static class Logger
{
    private static readonly string LogFilePath = "bot_log.txt";
    private static readonly string LogDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
    private static readonly object LockObject = new object();
    private const long ScriptLogMaxBytes = 1_000_000; // ~1 MB per script log

    public static void Log(string message)
    {
        EnsureLogDirectory();

        string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
        Console.WriteLine(logMessage);

        try
        {
            lock (LockObject)
            {
                File.AppendAllText(LogFilePath, logMessage + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to write to log file: {ex.Message}");
        }
    }

    public static void LogError(string message, Exception? ex = null)
    {
        string errorMessage = $"ERROR: {message}";
        if (ex != null)
        {
            errorMessage += $" - Exception: {ex.Message}";
        }
        Log(errorMessage);
    }

    public static void LogBotAction(string botName, string action)
    {
        Log($"[{botName}] {action}");
    }

    public static void LogImageSearch(string imagePath, bool found)
    {
        Log($"Image search: {Path.GetFileName(imagePath)} - {(found ? "FOUND" : "NOT FOUND")}");
    }

    public static void LogScriptEvent(string scriptName, string message, string level = "INFO")
    {
        EnsureLogDirectory();
        string sanitized = SanitizeName(scriptName);
        string scriptLogPath = Path.Combine(LogDirectory, $"{sanitized}.log");
        string logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | level={level} | script={scriptName} | {message}";

        try
        {
            lock (LockObject)
            {
                EnsureScriptLogCapacity(scriptLogPath);
                File.AppendAllText(scriptLogPath, logLine + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to write script log for {scriptName}: {ex.Message}");
        }
    }

    private static void EnsureLogDirectory()
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create log directory: {ex.Message}");
        }
    }

    private static void EnsureScriptLogCapacity(string scriptLogPath)
    {
        try
        {
            if (!File.Exists(scriptLogPath))
            {
                return;
            }

            var info = new FileInfo(scriptLogPath);
            if (info.Length > ScriptLogMaxBytes)
            {
                string backupPath = scriptLogPath + ".bak";
                File.Copy(scriptLogPath, backupPath, true);
                File.WriteAllText(scriptLogPath, string.Empty);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to rotate script log: {ex.Message}");
        }
    }

    private static string SanitizeName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "script" : name;
    }
}
