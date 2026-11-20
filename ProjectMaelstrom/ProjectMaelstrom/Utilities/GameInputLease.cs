using System.Collections.Concurrent;

namespace ProjectMaelstrom.Utilities;

/// <summary>One input owner per Windows user, shared by native and Python workers.
/// Lock byte zero before reading it: empty legacy or ASCII '0' is clean; ASCII '1' is
/// unconfirmed ownership. Arm and flush '1' before granting input. Never replace this file.</summary>
internal sealed class GameInputLease : IDisposable
{
    private FileStream? _stream;
    private readonly object _gate = new();
    private readonly string _directory;
    private readonly Action<FileStream, byte> _persist;
    private readonly Action<string, string> _writeNotice;
    private int _recoveryRequired;
    private int _disposing;
    private bool _quarantined;
    private string? _noticeFailure;
    private static readonly AsyncLocal<GameInputLease?> Scope = new();
    internal sealed record RecoveryHold(Guid Epoch, string Reason);
    private static readonly ConcurrentDictionary<string, RecoveryHold> RecoveryHolds = new(StringComparer.OrdinalIgnoreCase);
    // If even restoring the dirty state fails, keep the OS lock alive. This is not a
    // power-loss guarantee when the filesystem cannot durably acknowledge any write.
    private static readonly ConcurrentDictionary<string, GameInputLease> Quarantine = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, RecoveryReservation> RecoveryQuarantine = new(StringComparer.OrdinalIgnoreCase);

    internal static GameInputLease? Current => Scope.Value is { CanDispatch: true } lease ? lease : null;
    public string Owner { get; }
    public bool IsHeld => Volatile.Read(ref _stream) != null;
    public bool RecoveryRequired => Volatile.Read(ref _recoveryRequired) != 0 || RecoveryHolds.ContainsKey(_directory);
    public bool CanDispatch => IsHeld && Volatile.Read(ref _disposing) == 0 && !RecoveryRequired;
    public string? RecoveryNoticeFailure => Volatile.Read(ref _noticeFailure);
    internal static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Maelstrom", "session-locks");

    private GameInputLease(FileStream stream, string owner, string directory,
        Action<FileStream, byte> persist, Action<string, string> writeNotice)
    {
        _stream = stream; Owner = owner; _directory = directory;
        _persist = persist; _writeNotice = writeNotice;
    }

    public static GameInputLease Acquire(string owner, string? directory = null) =>
        AcquireCore(owner, directory ?? DirectoryPath, PersistState, File.WriteAllText);

    internal static GameInputLease AcquireForTesting(string owner, string directory,
        Action<FileStream, byte>? persistState = null, Action<string, string>? writeWarning = null) =>
        AcquireCore(owner, directory, persistState ?? PersistState, writeWarning ?? File.WriteAllText);

    private static GameInputLease AcquireCore(string owner, string directory,
        Action<FileStream, byte> persist, Action<string, string> writeNotice)
    {
        if (string.IsNullOrWhiteSpace(owner) || owner.Length > 256) throw new ArgumentException("Name the input owner (up to 256 characters).", nameof(owner));
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Shared desktop input ownership requires Windows.");
        directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (RecoveryHolds.ContainsKey(directory)) throw Blocked();
        Directory.CreateDirectory(directory);
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Input ownership cannot use a linked directory.");
        var path = Path.Combine(directory, "game-input.lock");
        var attributes = AttributesOrMissing(path);
        if (attributes is { } value && (value & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new InvalidDataException("Input ownership requires a regular lock file.");
        var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        try
        {
            try { stream.Lock(0, 1); }
            catch (IOException error) { throw new InvalidOperationException("Another Maelstrom routine owns game input. Stop it before starting this routine.", error); }
            if (RecoveryHolds.ContainsKey(directory) || AttributesOrMissing(Path.Combine(directory, "input-recovery-required.txt")) != null || !IsClean(stream))
            {
                RecoveryHolds.TryAdd(directory, NewHold("Unconfirmed prior input ownership or recovery notice."));
                throw Blocked();
            }
            try { persist(stream, (byte)'1'); }
            catch
            {
                RecoveryHolds.TryAdd(directory, NewHold("Could not durably arm input ownership."));
                try { PersistState(stream, (byte)'1'); } catch { /* No input was granted. */ }
                throw;
            }
            return new(stream, owner, directory, persist, writeNotice);
        }
        catch { stream.Dispose(); throw; }
    }

    private static FileAttributes? AttributesOrMissing(string path)
    {
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static bool IsClean(FileStream stream)
    {
        if (stream.Length == 0) return true;
        if (stream.Length != 1) return false;
        stream.Position = 0;
        return stream.ReadByte() == (byte)'0';
    }

    internal static void PersistState(FileStream stream, byte state)
    {
        if (state != (byte)'0' && state != (byte)'1') throw new ArgumentOutOfRangeException(nameof(state));
        stream.Position = 0;
        stream.WriteByte(state);
        stream.SetLength(1);
        stream.Flush(flushToDisk: true);
    }

    private static InvalidOperationException Blocked() => new("A previous input release or recovery write needs review. Live controls remain blocked. Open Settings or Routine runs > Restore game controls.");
    private static string Bounded(string? value) => value is null ? "Unspecified recovery failure." : value.Length <= 2048 ? value : value[..2024] + " [diagnostic truncated]";
    private static RecoveryHold NewHold(string reason) => new(Guid.NewGuid(), Bounded(reason));
    private void Poison(string reason)
    {
        Volatile.Write(ref _recoveryRequired, 1);
        RecoveryHolds[_directory] = NewHold(reason);
    }

    internal void RequireRecovery(string reason)
    {
        // A report arriving while Dispose persists clean state must revoke dispatch
        // immediately, even before it can acquire the lifecycle gate.
        Poison(reason);
        lock (_gate)
        {
            // Latch before attempting diagnostics; an unwritable warning must never grant input.
            try
            {
                var noticePath = Path.Combine(_directory, "input-recovery-required.txt");
                if (AttributesOrMissing(noticePath) is { } attributes && (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                    throw new IOException("The recovery notice path is not a regular file.");
                _writeNotice(noticePath, DateTime.UtcNow.ToString("O") + "\n" + Owner + "\n" + Bounded(reason));
            }
            catch (Exception error)
            {
                Volatile.Write(ref _noticeFailure, Bounded(error.Message));
                throw;
            }
            // Confirmed close ends the reporting contract. Preserve a best-effort
            // warning for a late caller, but never reacquire or steal another owner.
            if (_stream == null) throw new ObjectDisposedException(nameof(GameInputLease));
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            var stream = _stream;
            if (stream == null || _quarantined) return;
            Volatile.Write(ref _disposing, 1);
            Exception? failure = null;
            bool committedClean = false;
            if (!RecoveryRequired)
            {
                try
                {
                    if (AttributesOrMissing(Path.Combine(_directory, "input-recovery-required.txt")) != null)
                        Poison("A recovery notice appeared during input ownership.");
                    else { _persist(stream, (byte)'0'); committedClean = true; }
                }
                catch (Exception error)
                {
                    Poison("Could not durably close input ownership.");
                    failure = error;
                    if (!RestoreDirty(stream))
                    {
                        QuarantineOwner();
                        throw new IOException("Input recovery state could not be persisted; ownership remains held and controls are blocked.", error);
                    }
                }
            }
            if (committedClean && RecoveryRequired && !RestoreDirty(stream))
            {
                QuarantineOwner();
                throw new IOException("Recovery was reported during close, but its state could not be persisted; ownership remains held.");
            }
            try { if (OperatingSystem.IsWindows()) stream.Unlock(0, 1); }
            catch (Exception error)
            {
                Poison("Could not release input ownership.");
                failure ??= error;
                if (!RestoreDirty(stream)) QuarantineOwner();
            }
            finally
            {
                if (!_quarantined)
                {
                    Volatile.Write(ref _stream, null);
                    try { stream.Dispose(); }
                    catch (Exception error) { Poison("Could not close input ownership."); failure ??= error; }
                }
            }
            if (failure != null) throw new IOException("Input ownership cleanup failed; live controls remain blocked pending review.", failure);
        }
    }

    private bool RestoreDirty(FileStream stream)
    {
        try { _persist(stream, (byte)'1'); return true; }
        catch (Exception error) { Volatile.Write(ref _noticeFailure, Bounded("Dirty-state restoration failed: " + error.Message)); return false; }
    }
    private void QuarantineOwner() { _quarantined = true; Quarantine[_directory] = this; }

    /// <summary>Reserves ownership solely for verified cleanup. This is never an
    /// input lease, never becomes Current, and leaves dirty state unless committed.</summary>
    internal static RecoveryReservation ReserveRecovery(string directory, Action<FileStream, byte> persist, Action<FileStream>? close = null)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Shared input recovery requires Windows.");
        directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (Quarantine.ContainsKey(directory) || RecoveryQuarantine.ContainsKey(directory))
            throw new InvalidOperationException("An owner still retains the input lock after a storage failure. Close its worker before recovery.");
        Directory.CreateDirectory(directory);
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Recovery cannot use a linked directory.");
        var path = Path.Combine(directory, "game-input.lock");
        if (AttributesOrMissing(path) is { } attributes && (attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new InvalidDataException("Recovery requires a regular input lock file.");
        var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        RecoveryReservation? reservation = null;
        try
        {
            try { stream.Lock(0, 1); }
            catch (IOException error) { throw new InvalidOperationException("An input owner is still active. Stop it and wait for cleanup before recovery.", error); }
            if (stream.Length > 1) throw new InvalidDataException("The input guard is malformed; preserve it for review.");
            stream.Position = 0;
            var original = stream.Length == 0 ? Array.Empty<byte>() : new[] { checked((byte)stream.ReadByte()) };
            if (original.Length == 1 && original[0] != (byte)'0' && original[0] != (byte)'1')
                throw new InvalidDataException("The input guard is malformed; preserve it for review.");
            var hold = RecoveryHolds.GetOrAdd(directory, _ => NewHold("Verified input recovery is pending."));
            reservation = new RecoveryReservation(stream, directory, hold, original, persist, close ?? (ownedStream => ownedStream.Dispose()));
            persist(stream, (byte)'1');
            return reservation;
        }
        catch
        {
            // A failed arm may be followed by another failed write. Preserve the
            // reservation's physical lock in that case; never dispose it here.
            if (reservation is null) stream.Dispose();
            else reservation.Dispose();
            throw;
        }
    }

    internal sealed class RecoveryReservation : IDisposable
    {
        private readonly FileStream _stream;
        private readonly string _directory;
        private readonly RecoveryHold _hold;
        private readonly Action<FileStream, byte> _persist;
        private readonly Action<FileStream> _close;
        private bool _closed, _quarantined;
        internal byte[] OriginalState { get; }
        internal string Epoch => _hold.Epoch.ToString("N");
        internal string? CloseWarning { get; private set; }
        internal RecoveryReservation(FileStream stream, string directory, RecoveryHold hold, byte[] original,
            Action<FileStream, byte> persist, Action<FileStream> close)
        { _stream = stream; _directory = directory; _hold = hold; OriginalState = original; _persist = persist; _close = close; }
        internal void CheckCurrent()
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            if (!RecoveryHolds.TryGetValue(_directory, out var hold) || !ReferenceEquals(hold, _hold))
                throw new InvalidOperationException("A new recovery report arrived; verification must start again.");
            _stream.Position = 0;
            if (_stream.Length != 1 || _stream.ReadByte() != (byte)'1')
                throw new InvalidDataException("The input guard changed during verification.");
        }
        internal void CommitAndRelease()
        {
            CheckCurrent();
            try
            {
                _persist(_stream, (byte)'0');
                if (!((ICollection<KeyValuePair<string, RecoveryHold>>)RecoveryHolds).Remove(new(_directory, _hold)) || RecoveryHolds.ContainsKey(_directory))
                    throw new InvalidOperationException("Recovery changed while committing; controls remain blocked.");
                if (OperatingSystem.IsWindows()) _stream.Unlock(0, 1);
                // Successful unlock is the commit boundary. Another owner can now
                // acquire the file; no later diagnostic may rewrite its state.
                _closed = true;
            }
            catch
            {
                RecoveryHolds.TryAdd(_directory, NewHold("Recovery commit failed."));
                RestoreOrQuarantine();
                throw;
            }
            try { _close(_stream); }
            catch (Exception error)
            {
                CloseWarning = Bounded(error.Message);
                try { _stream.Dispose(); } catch { /* Already committed; never write after unlock. */ }
            }
        }
        private void RestoreOrQuarantine()
        {
            try { _persist(_stream, (byte)'1'); }
            catch { _quarantined = true; RecoveryQuarantine[_directory] = this; }
        }
        public void Dispose()
        {
            if (_closed || _quarantined) return;
            // Abandoning/cancelling a verification never grants a clean lease.
            RestoreOrQuarantine();
            if (_quarantined) return;
            try { if (OperatingSystem.IsWindows()) _stream.Unlock(0, 1); }
            finally { _stream.Dispose(); _closed = true; }
        }
    }
    internal static IDisposable EnterScope(GameInputLease? lease)
    {
        var previous = Scope.Value; Scope.Value = lease;
        return new ScopeExit(() => Scope.Value = previous);
    }
    private sealed class ScopeExit(Action exit) : IDisposable { public void Dispose() => exit(); }
}
