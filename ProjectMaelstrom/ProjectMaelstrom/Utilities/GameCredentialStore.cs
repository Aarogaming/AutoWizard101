using System.Runtime.InteropServices;

namespace ProjectMaelstrom.Utilities;

/// <summary>Nonsecret profile metadata; credentials are never part of a game pack.</summary>
internal sealed record GameCredentialProfile
{
    internal static GameCredentialProfile Wizard101 { get; } = new("wizard101", "Wizard101");
    internal string Id { get; }
    internal string DisplayName { get; }
    internal string VaultTarget => "Maelstrom/GameSignIn/v1/" + Id;
    internal GameCredentialProfile(string id, string displayName)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || id.Any(c => !char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c) && c != '-'))
            throw new ArgumentException("Use a lowercase game profile identifier.", nameof(id));
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 80 || displayName.Any(char.IsControl))
            throw new ArgumentException("Use a short game display name.", nameof(displayName));
        Id = id; DisplayName = displayName;
    }
}

internal enum GameCredentialStatus { Missing, Stored, Unavailable }
internal sealed record GameCredentialEnumeration(nint Items, uint Count, int Error);

/// <summary>Native seams use synthetic buffers in tests; never exercise the real
/// Windows vault as part of unit tests. None of these APIs returns a password.</summary>
internal sealed record GameCredentialNativeRuntime(Func<string, GameCredentialEnumeration> Enumerate,
    Func<GameNativeCredential, int> Write, Func<string, int> Delete,
    Action<nint> FreeCredentials, Action<nint> FreeMemory);

[StructLayout(LayoutKind.Sequential)]
internal struct GameNativeCredential
{
    public uint Flags, Type;
    public nint TargetName, Comment;
    public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
    public uint CredentialBlobSize;
    public nint CredentialBlob;
    public uint Persist, AttributeCount;
    public nint Attributes, TargetAlias, UserName;
}

/// <summary>Explicit per-Windows-user credential storage. No retrieval/export or
/// automatic login API exists yet. Secrets are never stored in application JSON,
/// environment variables, logs, packs or reports.</summary>
internal sealed class GameCredentialStore
{
    private const int NotFound = 1168, MaximumPasswordChars = 1280, MaximumUserNameChars = 256;
    private readonly GameCredentialNativeRuntime _native;
    internal GameCredentialStore(GameCredentialNativeRuntime native) => _native = native ?? throw new ArgumentNullException(nameof(native));

    internal static GameCredentialStore Windows() => new(new(
        target => CredEnumerateW(target, 0, out var count, out var items)
            ? new(items, count, 0) : new(0, 0, Marshal.GetLastWin32Error()),
        credential => CredWriteW(ref credential, 0) ? 0 : Marshal.GetLastWin32Error(),
        target => CredDeleteW(target, 1, 0) ? 0 : Marshal.GetLastWin32Error(),
        CredFree, Marshal.FreeCoTaskMem));

    /// <summary>Read existence only. Existing usernames and password bytes are
    /// never copied into managed objects or returned to settings.</summary>
    internal GameCredentialStatus GetStatus(GameCredentialProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        GameCredentialEnumeration? found = null;
        var status = GameCredentialStatus.Unavailable;
        try
        {
            found = _native.Enumerate(profile.VaultTarget);
            status = Inspect(profile, found);
        }
        catch (Exception) { status = GameCredentialStatus.Unavailable; }
        finally
        {
            if (found is { Items: not 0 })
            {
                // CredEnumerate owns its returned native buffers. Clear bounded blob
                // copies before releasing them; no password/username is displayed.
                try
                {
                    try
                    {
                        if (found.Count <= 16)
                            for (var i = 0; i < found.Count; i++)
                            {
                                var pointer = Marshal.ReadIntPtr(found.Items, i * IntPtr.Size);
                                if (pointer == 0) continue;
                                var value = Marshal.PtrToStructure<GameNativeCredential>(pointer);
                                if (value.CredentialBlobSize <= MaximumPasswordChars * 2)
                                    Zero(value.CredentialBlob, (int)value.CredentialBlobSize);
                            }
                    }
                    finally { _native.FreeCredentials(found.Items); }
                }
                catch (Exception) { status = GameCredentialStatus.Unavailable; }
            }
        }
        return status;
    }

    private static GameCredentialStatus Inspect(GameCredentialProfile profile, GameCredentialEnumeration found)
    {
        if (found.Error == NotFound) return GameCredentialStatus.Missing;
        if (found.Error != 0 || found.Count > 16 || (found.Count > 0 && found.Items == 0))
            return GameCredentialStatus.Unavailable;
        for (var i = 0; i < found.Count; i++)
        {
            var pointer = Marshal.ReadIntPtr(found.Items, i * IntPtr.Size);
            if (pointer == 0) return GameCredentialStatus.Unavailable;
            var value = Marshal.PtrToStructure<GameNativeCredential>(pointer);
            if (value.Type == 1 && Marshal.PtrToStringUni(value.TargetName) == profile.VaultTarget)
                return GameCredentialStatus.Stored;
        }
        return GameCredentialStatus.Missing;
    }

    internal void Save(GameCredentialProfile profile, string userName, ReadOnlySpan<char> password)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(userName) || userName.Length > MaximumUserNameChars || userName.Any(char.IsControl))
            throw new ArgumentException("Enter a username of up to 256 characters.", nameof(userName));
        if (password.Length is < 1 or > MaximumPasswordChars || password.Contains('\0'))
            throw new ArgumentException("Enter a password of up to 1280 characters without a null character.", nameof(password));
        nint target = 0, user = 0, secret = 0;
        var secretBytes = checked(password.Length * 2);
        bool saved = false, cleanupFailed = false;
        try
        {
            target = Marshal.StringToCoTaskMemUni(profile.VaultTarget);
            user = Marshal.StringToCoTaskMemUni(userName);
            secret = Marshal.AllocCoTaskMem(secretBytes);
            // Avoid an additional immutable string or managed byte array for the secret.
            for (var i = 0; i < password.Length; i++) Marshal.WriteInt16(secret, i * 2, unchecked((short)password[i]));
            var credential = new GameNativeCredential
            {
                Type = 1, TargetName = target, UserName = user,
                CredentialBlobSize = (uint)secretBytes, CredentialBlob = secret,
                Persist = 2 // CRED_PERSIST_LOCAL_MACHINE: this Windows user, on this PC.
            };
            saved = _native.Write(credential) == 0;
        }
        catch (Exception) { saved = false; }
        finally
        {
            try { FreeOwned(secret, secretBytes); } catch (Exception) { cleanupFailed = true; }
            try { FreeOwned(user, (userName.Length + 1) * 2); } catch (Exception) { cleanupFailed = true; }
            try { FreeOwned(target, (profile.VaultTarget.Length + 1) * 2); } catch (Exception) { cleanupFailed = true; }
        }
        // Never propagate provider exception text: it may contain submitted values.
        if (!saved || cleanupFailed)
            throw new InvalidOperationException("Windows could not confirm saving this game sign-in. Check the saved status before trying again.");
    }

    internal void Remove(GameCredentialProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        try
        {
            var error = _native.Delete(profile.VaultTarget);
            if (error is not (0 or NotFound)) throw new InvalidOperationException();
        }
        catch (Exception) { throw new InvalidOperationException("Windows could not remove this game sign-in. Try again in Windows Credential Manager."); }
    }

    private void FreeOwned(nint pointer, int bytes)
    {
        if (pointer == 0) return;
        Zero(pointer, bytes); _native.FreeMemory(pointer);
    }
    private static void Zero(nint pointer, int bytes)
    {
        if (pointer == 0) return;
        for (var i = 0; i < bytes; i++) Marshal.WriteByte(pointer, i, 0);
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredEnumerateW(string filter, uint flags, out uint count, out nint credentials);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWriteW(ref GameNativeCredential credential, uint flags);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDeleteW(string target, uint type, uint flags);
    [DllImport("advapi32.dll")] private static extern void CredFree(nint buffer);
}
