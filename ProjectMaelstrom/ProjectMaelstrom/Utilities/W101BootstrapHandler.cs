using System;
using System.Collections.Generic;
using System.Drawing;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Discrete stages of the client bootstrap lifecycle.
/// </summary>
public enum BootstrapStage
{
    NotStarted,
    ClientLaunch,
    Authentication,
    CharacterSelect,
    PlayRequested,
    WorldTunnelLoading,
    InWorldReady
}

/// <summary>
/// Planned execution sequence for launching and entering the world.
/// </summary>
public record BootstrapPlan(
    int TargetCharacterSlot,
    BootstrapStage InitialStage,
    IReadOnlyList<CombatClickAction> RequiredClicks,
    string Summary);

/// <summary>
/// Versioned Modular Client Bootstrap & Character Selection Engine.
/// Handles the transition from desktop launch and login carousel to in-world telemetry readiness.
/// </summary>
public static class W101BootstrapHandler
{
    // Window-relative coordinates for standard Wizard101 1024x768 / 1280x720 character select screen
    public static readonly Point PlayButtonCoord = new(512, 680);
    public static readonly Point QuitButtonCoord = new(960, 710);

    // Horizontal carousel slot coordinates for the 6 character pedestals
    private static readonly Point[] CharacterSlotPositions =
    [
        new(230, 420), // Slot 1
        new(340, 420), // Slot 2
        new(450, 420), // Slot 3
        new(560, 420), // Slot 4
        new(670, 420), // Slot 5
        new(780, 420)  // Slot 6
    ];

    /// <summary>
    /// Returns the window-relative click coordinate for a specific character pedestal (1 to 6).
    /// </summary>
    public static Point GetCharacterSlotCoordinate(int slotNumber)
    {
        if (slotNumber is < 1 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(slotNumber), "Character slot must be between 1 and 6.");
        }

        return CharacterSlotPositions[slotNumber - 1];
    }

    /// <summary>
    /// Generates the deterministic click plan to select the target character slot and enter the world.
    /// </summary>
    public static BootstrapPlan PlanCharacterSelection(int targetSlot = 1)
    {
        if (targetSlot is < 1 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(targetSlot), "Target slot must be between 1 and 6.");
        }

        var slotCoord = GetCharacterSlotCoordinate(targetSlot);
        var clicks = new List<CombatClickAction>
        {
            new($"Click Character Slot {targetSlot} Pedestal", slotCoord, 300),
            new("Click 'Play' Button to Enter Spiral", PlayButtonCoord, 500)
        };

        return new BootstrapPlan(
            TargetCharacterSlot: targetSlot,
            InitialStage: BootstrapStage.CharacterSelect,
            RequiredClicks: clicks,
            Summary: $"Selecting Character Slot {targetSlot} at ({slotCoord.X}, {slotCoord.Y}) and clicking Play at ({PlayButtonCoord.X}, {PlayButtonCoord.Y}).");
    }
}
