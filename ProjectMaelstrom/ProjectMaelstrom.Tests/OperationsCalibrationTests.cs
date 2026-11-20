using System;
using System.Collections.Generic;
using System.Drawing;
using ProjectMaelstrom.Utilities;
using Xunit;

namespace ProjectMaelstrom.Tests;

public class OperationsCalibrationTests
{
    [Fact]
    public void MissingTelemetryIsReportedOnceAndUnknownCombatStaysUnknown()
    {
        var missing = ClientOperationsCalibrator.CalibrateWindowGeometry(new Size(1280, 720));
        Assert.Equal(CalibrationStatus.NotApplicable,
            Assert.Single(missing.Checks, c => c.Name == "Telemetry Health").Status);
        var unknown = ClientOperationsCalibrator.CalibrateWindowGeometry(new Size(1280, 720), new());
        Assert.Contains("InCombat=Unknown", Assert.Single(unknown.Checks, c => c.Name == "Telemetry Health").Details);
    }

    [Fact]
    public void CalibrateWindowGeometry_Valid4x3_PassesCalibration()
    {
        var size = new Size(800, 600);
        var report = ClientOperationsCalibrator.CalibrateWindowGeometry(size);

        Assert.True(report.IsReadyForOperations);
        Assert.Equal("4:3 Standard", report.AspectRatioName);
        Assert.Equal(1.0, report.ScaleFactor, 2);
        Assert.DoesNotContain(report.Checks, c => c.Status == CalibrationStatus.Failed);
    }

    [Fact]
    public void CalibrateWindowGeometry_Valid16x9_PassesCalibration()
    {
        var size = new Size(1280, 720);
        var report = ClientOperationsCalibrator.CalibrateWindowGeometry(size);

        Assert.True(report.IsReadyForOperations);
        Assert.Equal("16:9 Widescreen", report.AspectRatioName);
        Assert.Equal(1.0, report.ScaleFactor, 2);
        Assert.DoesNotContain(report.Checks, c => c.Status == CalibrationStatus.Failed);
    }

    [Fact]
    public void CalibrateWindowGeometry_TinyWindow_FailsCalibration()
    {
        var size = new Size(300, 200);
        var report = ClientOperationsCalibrator.CalibrateWindowGeometry(size);

        Assert.False(report.IsReadyForOperations);
        Assert.Contains(report.Checks, c => c.Status == CalibrationStatus.Failed && c.Name == "Window Dimensions");
    }

    [Fact]
    public void CalibrateWindowGeometry_WithValidTelemetry_PassesWithTelemetryDetails()
    {
        var size = new Size(1024, 768);
        var telemetry = new W101TelemetryData(
            X: 1250.5,
            Y: -432.1,
            Z: 25.0,
            Yaw: 1.57,
            ZoneId: "WC_Commons",
            CurrentHealth: 2500,
            MaxHealth: 3000,
            Pips: 5,
            PowerPips: 2,
            InCombat: false,
            TimestampUtc: DateTime.UtcNow);

        var report = ClientOperationsCalibrator.CalibrateWindowGeometry(size, telemetry);

        Assert.True(report.IsReadyForOperations);
        var telemCheck = Assert.Single(report.Checks, c => c.Name == "Telemetry Health");
        Assert.Equal(CalibrationStatus.Passed, telemCheck.Status);
        Assert.Contains("WC_Commons", telemCheck.Details);
    }

    [Fact]
    public void TranslateBaselinePoint_ScalesCoordinatesAccurately()
    {
        var baselinePoint = new Point(400, 300); // Center in 800x600
        var actualSize = new Size(1600, 1200); // 2x scaling

        var translated = ClientOperationsCalibrator.TranslateBaselinePoint(baselinePoint, actualSize);

        Assert.Equal(800, translated.X);
        Assert.Equal(600, translated.Y);
    }

    [Fact]
    public void TranslateNormalizedRect_ConvertsFractionsToPixels()
    {
        var normRect = new RectangleF(0.25f, 0.5f, 0.5f, 0.25f);
        var actualSize = new Size(1000, 800);

        var pixelRect = ClientOperationsCalibrator.TranslateNormalizedRect(normRect, actualSize);

        Assert.Equal(250, pixelRect.X);
        Assert.Equal(400, pixelRect.Y);
        Assert.Equal(500, pixelRect.Width);
        Assert.Equal(200, pixelRect.Height);
    }

    [Fact]
    public void TelemetryBridge_BuildsCombatantAndPlansTurn()
    {
        var telemetry = new W101TelemetryData(
            CurrentHealth: 1500,
            MaxHealth: 2000,
            Pips: 4,
            PowerPips: 2,
            ShadowPips: 0,
            InCombat: true,
            TimestampUtc: DateTime.UtcNow);

        var player = W101TelemetryBridge.BuildPlayerCombatant(
            telemetry,
            name: "Player",
            school: "Storm",
            damageBonus: 0.85,
            resistPercent: 0.30,
            piercePercent: 20.0,
            criticalRating: 400.0);

        Assert.Equal(1500, player.CurrentHp);
        Assert.Equal(4, player.NormalPips);
        Assert.Equal(2, player.PowerPips);
        int totalPips = player.NormalPips + (player.PowerPips * 2);
        Assert.Equal(8, totalPips);

        var enemy = W101TelemetryBridge.BuildEnemyCombatant("Halfang Bristlecrown", 0, 1200, 2000, "Myth");
        var stormLord = new CombatCardState(
            Name: "Storm Lord",
            School: "Storm",
            SpellType: "Attack",
            PipCost: 7,
            BaseDamage: 690,
            IsAoe: true,
            HandIndex: 0);

        var hand = new List<CombatCardState> { stormLord };
        var decision = W101TelemetryBridge.PlanTurnFromState(player, [enemy], hand);

        Assert.NotNull(decision);
        Assert.Equal("Storm Lord", decision.PrimaryCard?.Name);
        Assert.True(decision.WinScore > 0);
    }
}
