using System;
using System.Collections.Generic;
using System.Drawing;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Status of an individual calibration check.
/// </summary>
public enum CalibrationStatus
{
    Passed,
    Warning,
    Failed,
    NotApplicable
}

/// <summary>
/// Record representing the result of a single calibration check.
/// </summary>
public record CalibrationCheckResult(
    string Name,
    CalibrationStatus Status,
    string Details,
    double Confidence = 1.0);

/// <summary>
/// Complete report produced by client operations calibration.
/// </summary>
public record ClientCalibrationReport(
    bool IsReadyForOperations,
    Size ClientSize,
    double AspectRatio,
    string AspectRatioName,
    double ScaleFactor,
    IReadOnlyList<CalibrationCheckResult> Checks,
    DateTime TimestampUtc);

/// <summary>
/// Calibrator and verifier for client dimensions, scaling, coordinate translations, and telemetry readiness without invasive memory writes or unauthorized inputs.
/// </summary>
public static class ClientOperationsCalibrator
{
    // Standard Wizard101 viewport baselines
    public static readonly Size Baseline4x3 = new(800, 600);
    public static readonly Size Baseline4x3Alt = new(1024, 768);
    public static readonly Size Baseline16x9 = new(1280, 720);
    public static readonly Size Baseline16x9Alt = new(1920, 1080);

    /// <summary>
    /// Performs a full calibration of the game client window, checking dimensions, aspect ratio, combat UI bounds, button locations, and optional telemetry health.
    /// Returns a <see cref="ClientCalibrationReport"/> indicating overall readiness for automated operations.
    /// </summary>
    public static ClientCalibrationReport CalibrateWindowGeometry(Size clientSize, W101TelemetryData? telemetry = null)
    {
        var checks = new List<CalibrationCheckResult>();
        // Minimum dimensions check
        if (clientSize.Width < 640 || clientSize.Height < 480)
        {
            checks.Add(new("Window Dimensions", CalibrationStatus.Failed,
                $"Client size {clientSize.Width}x{clientSize.Height} is below minimum supported 640x480."));
        }
        else
        {
            checks.Add(new("Window Dimensions", CalibrationStatus.Passed,
                $"Client size {clientSize.Width}x{clientSize.Height} meets minimum requirements."));
        }

        // Aspect ratio detection
        double aspect = (double)clientSize.Width / Math.Max(1, clientSize.Height);
        string aspectName;
        double scaleFactor = 1.0;
        if (Math.Abs(aspect - (4.0 / 3.0)) <= 0.05)
        {
            aspectName = "4:3 Standard";
            scaleFactor = (double)clientSize.Width / 800.0;
            checks.Add(new("Aspect Ratio", CalibrationStatus.Passed,
                $"Recognized {aspectName} (Ratio: {aspect:F2}, Scale: {scaleFactor:F2}x relative to 800x600)."));
        }
        else if (Math.Abs(aspect - (16.0 / 9.0)) <= 0.05)
        {
            aspectName = "16:9 Widescreen";
            scaleFactor = (double)clientSize.Width / 1280.0;
            checks.Add(new("Aspect Ratio", CalibrationStatus.Passed,
                $"Recognized {aspectName} (Ratio: {aspect:F2}, Scale: {scaleFactor:F2}x relative to 1280x720)."));
        }
        else if (Math.Abs(aspect - (16.0 / 10.0)) <= 0.05)
        {
            aspectName = "16:10 Display";
            scaleFactor = (double)clientSize.Width / 1280.0;
            checks.Add(new("Aspect Ratio", CalibrationStatus.Warning,
                $"Detected {aspectName} (Ratio: {aspect:F2}). Some fixed HUD coordinates may require offset compensation."));
        }
        else
        {
            aspectName = "Non-standard";
            scaleFactor = (double)clientSize.Width / 800.0;
            checks.Add(new("Aspect Ratio", CalibrationStatus.Warning,
                $"Non-standard aspect ratio {aspect:F2}. Centered UI elements may experience coordinate drift."));
        }

        // Combat tray geometry verification
        bool trayInside = CombatGeometry.SpellArea.Right <= clientSize.Width &&
                          CombatGeometry.SpellArea.Bottom <= clientSize.Height;
        if (trayInside)
        {
            checks.Add(new("Combat Tray Anchoring", CalibrationStatus.Passed,
                $"Combat spell area (X:{CombatGeometry.SpellArea.X}, Y:{CombatGeometry.SpellArea.Y}, W:{CombatGeometry.SpellArea.Width}, H:{CombatGeometry.SpellArea.Height}) is fully bounded inside client bounds."));
        }
        else
        {
            checks.Add(new("Combat Tray Anchoring", CalibrationStatus.Failed,
                $"Combat spell area exceeds client dimensions {clientSize.Width}x{clientSize.Height}."));
        }

        // Pass & Flee button coordinates
        bool buttonsInside = CombatGeometry.PassButton.X < clientSize.Width &&
                             CombatGeometry.PassButton.Y < clientSize.Height &&
                             CombatGeometry.FleeButton.X < clientSize.Width &&
                             CombatGeometry.FleeButton.Y < clientSize.Height;
        if (buttonsInside)
        {
            checks.Add(new("Combat Navigation Anchors", CalibrationStatus.Passed,
                "Pass and Flee button coordinates resolve within active client frame."));
        }
        else
        {
            checks.Add(new("Combat Navigation Anchors", CalibrationStatus.Failed,
                "Pass or Flee buttons fall outside active client window."));
        }

        // Telemetry validation
        if (telemetry != null)
        {
            if (telemetry.IsValid)
            {
                string posInfo = telemetry.HasPosition
                    ? $"Position=({telemetry.X:F1}, {telemetry.Y:F1}, {telemetry.Z:F1})"
                    : "Position=None";
                string combatInfo = telemetry.InCombat switch
                {
                    true => "InCombat=True",
                    false => "InCombat=False",
                    null => "InCombat=Unknown"
                };
                checks.Add(new("Telemetry Health", CalibrationStatus.Passed,
                    $"Telemetry valid ({posInfo}, {combatInfo}, Zone={telemetry.ZoneId ?? "Unknown"})."));
            }
            else
            {
                checks.Add(new("Telemetry Health", CalibrationStatus.Warning,
                    "Telemetry data present but failed validation rules."));
            }
        }
        else
        {
            checks.Add(new("Telemetry Health", CalibrationStatus.NotApplicable,
                "No live telemetry supplied; operating in UI/Visual mode."));
        }
        bool isReady = checks.TrueForAll(c => c.Status != CalibrationStatus.Failed);
        return new ClientCalibrationReport(isReady, clientSize, aspect, aspectName, scaleFactor, checks, DateTime.UtcNow);
    }

    /// <summary>
    /// Translates a baseline point (800x600) into actual client coordinates.
    /// </summary>
    public static Point TranslateBaselinePoint(Point baselinePoint, Size actualSize, Size? referenceSize = null)
    {
        var refSize = referenceSize ?? Baseline4x3;
        double scaleX = (double)actualSize.Width / Math.Max(1, refSize.Width);
        double scaleY = (double)actualSize.Height / Math.Max(1, refSize.Height);
        return new Point(
            (int)Math.Round(baselinePoint.X * scaleX, MidpointRounding.AwayFromZero),
            (int)Math.Round(baselinePoint.Y * scaleY, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Translates a normalized rectangle (0..1) into client pixel coordinates.
    /// </summary>
    public static Rectangle TranslateNormalizedRect(RectangleF normalized, Size actualSize)
    {
        return new Rectangle(
            (int)Math.Round(normalized.X * actualSize.Width, MidpointRounding.AwayFromZero),
            (int)Math.Round(normalized.Y * actualSize.Height, MidpointRounding.AwayFromZero),
            (int)Math.Round(normalized.Width * actualSize.Width, MidpointRounding.AwayFromZero),
            (int)Math.Round(normalized.Height * actualSize.Height, MidpointRounding.AwayFromZero));
    }
}
