using System.Security;
using System.Text;

namespace ProjectMaelstrom.Utilities;

/// <summary>Read-only check of the installed application's explicit live-control
/// setting. Reads at most 16 KiB and caches for less than 250 ms; it neither creates a
/// policy file nor reloads plugins. Missing, linked, malformed, or unreadable policy
/// and invalid/nonmonotonic clocks deny control. This does not grant focus or input ownership.</summary>
internal sealed class PetDanceControlPermission
{
    internal const int MaximumPolicyBytes = 16 * 1024;
    internal const double CacheMs = 250;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly string? _policyPath;
    private readonly Func<double> _clock;
    private readonly object _gate = new();
    private double? _lastClock, _lastRead;
    private bool _allowed;

    public PetDanceControlPermission(string policyPath, Func<double>? clock = null)
    {
        _clock = clock ?? FrameTimeline.TimestampMs;
        try
        {
            if (!string.IsNullOrWhiteSpace(policyPath) && Path.IsPathFullyQualified(policyPath))
                _policyPath = Path.GetFullPath(policyPath);
        }
        catch (Exception error) when (IsReadFailure(error)) { }
    }

    public bool IsAllowed()
    {
        lock (_gate)
        {
            double now;
            try { now = _clock(); }
            catch { return DenyAndInvalidate(); }
            if (!double.IsFinite(now) || now < 0 || _lastClock is { } previous && now < previous)
                return DenyAndInvalidate();
            _lastClock = now;
            if (_lastRead is { } read && now - read < CacheMs) return _allowed;
            _lastRead = now;
            _allowed = ReadPolicy();
            return _allowed;
        }
    }

    private bool DenyAndInvalidate() { _allowed = false; _lastRead = null; return false; }

    private bool ReadPolicy()
    {
        if (_policyPath == null) return false;
        try
        {
            // Reject both a linked leaf and linked ancestor directories. Do not
            // follow a junction into a different policy tree during normal reads.
            if (!UnlinkedPath(_policyPath)) return false;
            using var stream = new FileStream(_policyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 or > MaximumPolicyBytes) return false;
            var bytes = new byte[(int)stream.Length];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1 || !UnlinkedPath(_policyPath)) return false;
            var text = StrictUtf8.GetString(bytes);
            if (text.StartsWith('\uFEFF')) text = text[1..];
            using var lines = new StringReader(text);
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool? allow = null;
            while (lines.ReadLine() is { } line)
            {
                line = line.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var separator = line.IndexOf('=');
                if (separator <= 0 || line.IndexOf('=', separator + 1) >= 0) return false;
                var key = line[..separator].Trim(); var value = line[(separator + 1)..].Trim();
                if (key.Length == 0 || value.Length == 0 || key.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_') ||
                    value.Any(char.IsControl) || !keys.Add(key)) return false;
                if (key.Equals("ALLOW_LIVE_AUTOMATION", StringComparison.OrdinalIgnoreCase))
                {
                    if (!bool.TryParse(value, out var parsed)) return false;
                    allow = parsed;
                }
                else if (key.Equals("EXECUTION_PROFILE", StringComparison.OrdinalIgnoreCase) &&
                    (!Enum.TryParse<ExecutionProfile>(value, true, out var profile) || !Enum.IsDefined(profile))) return false;
            }
            return allow == true;
        }
        catch (Exception error) when (IsReadFailure(error)) { return false; }
    }

    private static bool UnlinkedPath(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) return false;
        var directory = Path.GetDirectoryName(path);
        for (var depth = 0; directory != null; depth++)
        {
            if (depth >= 128 || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return false;
            directory = Path.GetDirectoryName(directory);
        }
        return true;
    }

    private static bool IsReadFailure(Exception error) => error is IOException or UnauthorizedAccessException or
        ArgumentException or NotSupportedException or SecurityException;
}
