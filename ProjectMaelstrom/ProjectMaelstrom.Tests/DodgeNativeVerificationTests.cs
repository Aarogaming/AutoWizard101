using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class DodgeNativeVerificationTests
{
    [Fact]
    public void PrepareUsesASeparateBoundedCopyAndPreservesSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-native-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "project.godot"), "config");
            File.WriteAllText(Path.Combine(source, "main.gd"), "extends Node\n");
            var probe = Path.Combine(root, "probe.gd");
            File.WriteAllText(probe, "# MAELSTROM_DODGE_NATIVE_TEST_V1\n");
            var copy = DodgeNativeVerification.PrepareProject(source, probe, Path.Combine(root, "data"), CancellationToken.None);
            Assert.Equal("extends Node\n", File.ReadAllText(Path.Combine(source, "main.gd")));
            Assert.Contains("MAELSTROM_DODGE_NATIVE_TEST_V1", File.ReadAllText(Path.Combine(copy, "main.gd")));
            Assert.Throws<InvalidOperationException>(() => DodgeNativeVerification.PrepareProject(source, probe, source, CancellationToken.None));
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(() => DodgeNativeVerification.PrepareProject(source, probe, Path.Combine(root, "cancelled"), cancel.Token));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
