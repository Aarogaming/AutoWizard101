using System.Runtime.InteropServices;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

/// <summary>All credentials and buffers are synthetic. No Windows vault calls.</summary>
public sealed class GameCredentialStoreTests
{
    private static GameCredentialProfile Profile => GameCredentialProfile.Wizard101;

    [Theory]
    [InlineData("ok")]
    [InlineData("native-error")]
    [InlineData("native-throws")]
    [InlineData("free-throws")]
    public void SaveUsesLocalUserVaultAndErasesOwnedBuffersOnSuccessAndFailure(string mode)
    {
        const string user = "synthetic-user", secret = "synthetic-\u03bb-password";
        GameNativeCredential written = default;
        var frees = 0; var clearedUser = false; var clearedSecret = false;
        var runtime = new GameCredentialNativeRuntime(_ => throw new Exception("must not enumerate on save"), credential =>
        {
            written = credential;
            Assert.Equal(1u, credential.Type); Assert.Equal(2u, credential.Persist);
            Assert.Equal(0u, credential.Flags); Assert.Equal(0u, credential.AttributeCount);
            Assert.Equal(Profile.VaultTarget, Marshal.PtrToStringUni(credential.TargetName));
            Assert.Equal(user, Marshal.PtrToStringUni(credential.UserName));
            Assert.Equal(secret.Length * 2, (int)credential.CredentialBlobSize);
            Assert.Equal(secret, Marshal.PtrToStringUni(credential.CredentialBlob, secret.Length));
            if (mode == "native-throws") throw new IOException(user + secret);
            return mode == "native-error" ? 5 : 0;
        }, _ => throw new Exception("must not delete on save"), _ => throw new Exception("must not free enumeration"), pointer =>
        {
            frees++;
            if (pointer == written.CredentialBlob)
            {
                for (var i = 0; i < secret.Length * 2; i++) Assert.Equal(0, Marshal.ReadByte(pointer, i));
                clearedSecret = true;
            }
            if (pointer == written.UserName)
            {
                for (var i = 0; i < (user.Length + 1) * 2; i++) Assert.Equal(0, Marshal.ReadByte(pointer, i));
                clearedUser = true;
            }
            Marshal.FreeCoTaskMem(pointer);
            if (mode == "free-throws") throw new IOException(user + secret);
        });
        var store = new GameCredentialStore(runtime);
        if (mode == "ok") store.Save(Profile, user, secret.AsSpan());
        else
        {
            var error = Assert.Throws<InvalidOperationException>(() => store.Save(Profile, user, secret.AsSpan()));
            Assert.DoesNotContain(user, error.ToString()); Assert.DoesNotContain(secret, error.ToString());
            Assert.Null(error.InnerException);
        }
        Assert.Equal(3, frees); Assert.True(clearedUser); Assert.True(clearedSecret);
    }

    [Theory]
    [InlineData("", "test")]
    [InlineData(" ", "test")]
    [InlineData("user\0name", "test")]
    [InlineData("user", "")]
    [InlineData("user", "test\0password")]
    public void InvalidCredentialFieldsDoNotReachTheVault(string user, string secret)
    {
        var calls = 0;
        var store = Store(write: _ => { calls++; return 0; });
        Assert.Throws<ArgumentException>(() => store.Save(Profile, user, secret.AsSpan()));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void LengthLimitsRejectOversizedInputsBeforeNativeWrite()
    {
        var store = Store(write: _ => throw new Exception("not expected"));
        Assert.Throws<ArgumentException>(() => store.Save(Profile, new string('u', 257), "test"));
        Assert.Throws<ArgumentException>(() => store.Save(Profile, "user", new string('p', 1281)));
    }

    [Theory]
    [InlineData("../other")]
    [InlineData("*")]
    [InlineData("Wizard101")]
    [InlineData("")]
    [InlineData("game\0")]
    public void GameProfileCannotAddressUnrelatedCredentialNamespaces(string id) =>
        Assert.Throws<ArgumentException>(() => new GameCredentialProfile(id, "Game"));

    [Fact]
    public void ProfileContainsOnlyNonsecretMetadata()
    {
        Assert.Equal("Maelstrom/GameSignIn/v1/wizard101", Profile.VaultTarget);
        var another = new GameCredentialProfile("offline-game", "Offline game");
        Assert.Equal("Maelstrom/GameSignIn/v1/offline-game", another.VaultTarget);
        Assert.NotEqual(Profile.VaultTarget, another.VaultTarget);
    }

    [Theory]
    [InlineData(1168, 0)]
    [InlineData(5, 2)]
    public void MissingAndUnavailableStatusAreDistinct(int nativeError, int expected)
    {
        var store = Store(enumerate: target =>
        {
            Assert.Equal(Profile.VaultTarget, target);
            return new(0, 0, nativeError);
        });
        Assert.Equal((GameCredentialStatus)expected, store.GetStatus(Profile));
    }

    [Fact]
    public void ProviderFailureDoesNotEscapeThroughStatus()
    {
        var store = Store(enumerate: _ => throw new IOException("synthetic sensitive provider diagnostic"));
        Assert.Equal(GameCredentialStatus.Unavailable, store.GetStatus(Profile));
    }

    [Theory]
    [InlineData("matching")]
    [InlineData("other-target")]
    [InlineData("other-type")]
    [InlineData("free-fails")]
    public void StatusDoesNotReadUsernameOrReturnSecretAndClearsNativeCopies(string mode)
    {
        var target = Marshal.StringToCoTaskMemUni(mode == "other-target" ? "other-entry" : Profile.VaultTarget);
        var blob = Marshal.StringToCoTaskMemUni("synthetic-secret");
        var credential = Marshal.AllocCoTaskMem(Marshal.SizeOf<GameNativeCredential>());
        var list = Marshal.AllocCoTaskMem(IntPtr.Size);
        Marshal.StructureToPtr(new GameNativeCredential
        {
            TargetName = target,
            Type = mode == "other-type" ? 2u : 1u,
            CredentialBlob = blob,
            CredentialBlobSize = 32,
            UserName = 1 // Intentionally invalid: readiness must never dereference it.
        }, credential, false);
        Marshal.WriteIntPtr(list, credential);
        var freed = false;
        var store = Store(enumerate: _ => new(list, 1, 0), freeCredentials: pointer =>
        {
            Assert.Equal(list, pointer);
            for (var i = 0; i < 32; i++) Assert.Equal(0, Marshal.ReadByte(blob, i));
            Marshal.FreeCoTaskMem(target); Marshal.FreeCoTaskMem(blob);
            Marshal.FreeCoTaskMem(credential); Marshal.FreeCoTaskMem(list); freed = true;
            if (mode == "free-fails") throw new IOException("synthetic sensitive diagnostic");
        });
        var expected = mode == "matching" ? GameCredentialStatus.Stored : mode == "free-fails" ? GameCredentialStatus.Unavailable : GameCredentialStatus.Missing;
        Assert.Equal(expected, store.GetStatus(Profile)); Assert.True(freed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1168)]
    public void ExplicitRemovalIsExactAndIdempotent(int error)
    {
        var calls = 0;
        Store(delete: target => { Assert.Equal(Profile.VaultTarget, target); calls++; return error; }).Remove(Profile);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void RemovalErrorsExcludeProviderDiagnostics()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Store(delete: _ => throw new IOException("synthetic-secret")).Remove(Profile));
        Assert.DoesNotContain("synthetic-secret", error.ToString()); Assert.Null(error.InnerException);
    }

    private static GameCredentialStore Store(Func<string, GameCredentialEnumeration>? enumerate = null,
        Func<GameNativeCredential, int>? write = null, Func<string, int>? delete = null, Action<nint>? freeCredentials = null) =>
        new(new(enumerate ?? (_ => new(0, 0, 1168)), write ?? (_ => throw new Exception("Unexpected native write")),
            delete ?? (_ => throw new Exception("Unexpected native deletion")),
            freeCredentials ?? (_ => throw new Exception("Unexpected enumeration release")), Marshal.FreeCoTaskMem));
}
