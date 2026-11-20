namespace ProjectMaelstrom.Utilities;

/// <summary>Validated paths to the versioned application-supplied Dance preset.
/// Loading is read-only and grants no permission to capture, control or feed a pet.
/// Per-run readers still pin and recheck their inputs before use.</summary>
internal sealed record PetDancePreset(string ProfilePath, string AudioComparisonPath)
{
    internal static readonly string RelativeDirectory = Path.Combine("Resources", "PetDance", "default-v1");

    public static PetDancePreset Load(string appRoot)
    {
        if (string.IsNullOrWhiteSpace(appRoot) || !Path.IsPathFullyQualified(appRoot))
            throw new ArgumentException("Use the absolute application folder for the packaged Dance preset.", nameof(appRoot));
        var root = Path.GetFullPath(appRoot);
        CheckDirectory(root);
        var folder = root;
        foreach (var segment in new[] { "Resources", "PetDance", "default-v1" })
        {
            folder = Path.Combine(folder, segment);
            CheckDirectory(folder);
        }

        var profile = Path.Combine(folder, "profile.json");
        var audio = Path.Combine(folder, "audio-comparison.json");
        CheckFile(profile, 65536);
        CheckFile(audio, 262144);
        // The readers validate schema, geometry, image bounds and every reference
        // hash, plus four finite normalized audio templates. They open no device.
        using var vision = new PetDancePanelVision(profile);
        if (!vision.UsesShapeSequence)
            throw new InvalidDataException("The packaged Dance preset must include the qualified UI layout.");
        _ = PetDanceAudioDetector.LoadFrozenComparison(audio);
        vision.VerifyInputsUnchanged();
        return new(profile, audio);
    }

    private static void CheckDirectory(string path)
    {
        var info = new DirectoryInfo(path);
        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The packaged Dance preset folder is missing or linked. Keep the complete application folder together.");
    }

    private static void CheckFile(string path, long maximum)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length <= 0 || info.Length > maximum || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("A packaged Dance preset file is missing, linked or outside its size limit.");
    }
}
