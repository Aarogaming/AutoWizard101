using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class AppPreferencesTests
{
    [Fact]
    public void SettingsRoundTripAndRejectUnknownVersionsWithoutOverwriting()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Equal(new AppPreferences(), AppPreferences.Load(root));
            var settings = new AppPreferences(false, true);
            settings.Save(root);
            Assert.Equal(settings, AppPreferences.Load(root));
            var original = File.ReadAllText(Path.Combine(root, "settings.json"));
            Assert.Throws<InvalidDataException>(() => (settings with { Version = 2 }).Save(root));
            Assert.Equal(original, File.ReadAllText(Path.Combine(root, "settings.json")));
            File.WriteAllText(Path.Combine(root, "settings.json"), "{\"Version\":2}");
            Assert.Throws<InvalidDataException>(() => AppPreferences.Load(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
