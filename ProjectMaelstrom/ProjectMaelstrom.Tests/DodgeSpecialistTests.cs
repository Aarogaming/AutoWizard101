using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class DodgeSpecialistTests
{
    private static DodgeGameContext Context() => DodgeLivePack.Load(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeContext", "pack.json")).Context!;

    [Fact]
    public void RecognizesPlayerWhenScoreAndEnemyOverlapAtFiftyNine()
    {
        using var frame = new Bitmap(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeTracking", "score-59-player-occluded.png"));
        var result = DodgeSpecialist.Analyze(frame, Context());
        Assert.True(result.State == "playing", result.Detail);
        Assert.NotNull(result.Player);
        Assert.InRange(result.Player.Value.X * frame.Width, 195, 230);
        Assert.InRange(result.Player.Value.Y * frame.Height, 30, 75);
    }

    [Fact]
    public void RecognizesPlayerUnderSevenWithNearbyEnemies()
    {
        using var frame = new Bitmap(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeTracking", "score-7-overlap-ambiguous.png"));
        var result = DodgeSpecialist.Analyze(frame, Context());
        Assert.True(result.State == "playing", result.Detail);
        Assert.NotNull(result.Player);
        Assert.InRange(result.Player.Value.X * frame.Width, 220, 255);
        Assert.InRange(result.Player.Value.Y * frame.Height, 40, 75);
    }

    [Fact]
    public void TracksMovementWhenTheTrailMergesWithThePlayer()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeTracking");
        using var before = new Bitmap(Path.Combine(folder, "trail-before.png"));
        using var after = new Bitmap(Path.Combine(folder, "trail-after.png"));
        var first = DodgeSpecialist.Analyze(before, Context());
        var second = DodgeSpecialist.Analyze(after, Context());
        Assert.Equal("movement-observed", DodgeLiveRun.ClassifyMovement(first.Player, second.Player, "move-down", before.Size));
    }

    [Fact]
    public void RecognizesPlayerWhenScoreOverlaySplitsTheSprite()
    {
        using var frame = new Bitmap(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeTracking", "score-overlap.png"));
        var result = DodgeSpecialist.Analyze(frame, Context());
        Assert.True(result.State == "playing", result.Detail);
        Assert.NotNull(result.Player);
        Assert.InRange(result.Player!.Value.X * frame.Width, 185, 225);
        Assert.InRange(result.Player!.Value.Y * frame.Height, 15, 70);
    }

    [Fact]
    public void RecognizesPlayerWhenScoreOverlaySplitsSpriteVertically()
    {
        using var frame = new Bitmap(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeTracking", "score-overlap-29.png"));
        var result = DodgeSpecialist.Analyze(frame, Context());
        Assert.Equal("playing", result.State);
        Assert.NotNull(result.Player);
        Assert.InRange(result.Player!.Value.X * frame.Width, 225, 270);
        Assert.InRange(result.Player!.Value.Y * frame.Height, 15, 75);
    }

    [Fact]
    public void RecognizesPlayerWhenScoreOverlaySeparatesItsUpperEdge()
    {
        using var frame = new Bitmap(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeTracking", "score-overlap-low.png"));
        var result = DodgeSpecialist.Analyze(frame, Context());
        Assert.Equal("playing", result.State);
        Assert.NotNull(result.Player);
        Assert.InRange(result.Player!.Value.X * frame.Width, 210, 255);
        Assert.InRange(result.Player!.Value.Y * frame.Height, 45, 90);
    }

    [Fact]
    public void RecognizesLargerScoreOverlayFragmentUsingRelativeSize()
    {
        using var frame = new Bitmap(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeTracking", "score-overlap-29-low.png"));
        var result = DodgeSpecialist.Analyze(frame, Context());
        Assert.Equal("playing", result.State);
        Assert.NotNull(result.Player);
        Assert.InRange(result.Player!.Value.X * frame.Width, 225, 270);
        Assert.InRange(result.Player!.Value.Y * frame.Height, 45, 90);
    }

    [Fact]
    public void RecognizesPlayerWhenScoreSplitsItIntoSimilarSizedPieces()
    {
        using var frame = new Bitmap(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeTracking", "score-overlap-balanced.png"));
        var result = DodgeSpecialist.Analyze(frame, Context());
        Assert.Equal("playing", result.State);
        Assert.NotNull(result.Player);
        Assert.InRange(result.Player!.Value.X * frame.Width, 225, 265);
        Assert.InRange(result.Player!.Value.Y * frame.Height, 25, 75);
    }

    [Fact]
    public void RecognizesPlayerWhenScoreSplitsItIntoThreePieces()
    {
        using var frame = new Bitmap(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeTracking", "score-overlap-three.png"));
        var result = DodgeSpecialist.Analyze(frame, Context());
        Assert.True(result.State == "playing", result.Detail);
        Assert.NotNull(result.Player);
        Assert.InRange(result.Player!.Value.X * frame.Width, 220, 260);
        Assert.InRange(result.Player!.Value.Y * frame.Height, 25, 70);
    }

    [Fact]
    public void RecognizesPlayerAtUpperScoreOverlap()
    {
        using var frame = new Bitmap(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeTracking", "score-overlap-29-upper.png"));
        var landmark = DodgeSpecialist.ScoreAreaPupil(frame, new Rectangle(62, 0, 35, 25));
        Assert.True(landmark.Position != null && !landmark.Ambiguous, $"Landmark {landmark.Position}, ambiguous {landmark.Ambiguous}");
        var result = DodgeSpecialist.Analyze(frame, Context());
        Assert.True(result.State == "playing", result.Detail);
        Assert.NotNull(result.Player);
        Assert.InRange(result.Player.Value.X * frame.Width, 220, 260);
        Assert.InRange(result.Player.Value.Y * frame.Height, 25, 70);
    }

    [Fact]
    public void ScoreLandmarkAloneDoesNotCreateAPlayer()
    {
        using var frame = new Bitmap(480, 720);
        using (var graphics = Graphics.FromImage(frame))
        using (var background = new SolidBrush(Color.FromArgb(52, 89, 90)))
        {
            graphics.Clear(background.Color);
            graphics.FillEllipse(Brushes.White, 224, 32, 25, 25);
            graphics.FillEllipse(Brushes.Black, 234, 42, 5, 5);
        }
        var landmark = DodgeSpecialist.ScoreAreaPupil(frame, new Rectangle(62, 0, 35, 25));
        Assert.NotNull(landmark.Position);
        var result = DodgeSpecialist.Analyze(frame, Context());
        Assert.Equal("unknown", result.State);
        Assert.Null(result.Player);
    }

    [Fact]
    public void ObservesDiagonalMovementUnderScoreOverlay()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeTracking");
        using var before = new Bitmap(Path.Combine(folder, "score-move-before.png"));
        using var after = new Bitmap(Path.Combine(folder, "score-move-after.png"));
        var first = DodgeSpecialist.Analyze(before, Context());
        var second = DodgeSpecialist.Analyze(after, Context());
        Assert.Equal("movement-observed", DodgeLiveRun.ClassifyMovement(first.Player, second.Player, "move-down-right", before.Size));
        Assert.InRange(second.Player!.Value.X * after.Width, 206, 236);
        Assert.InRange(second.Player!.Value.Y * after.Height, 43, 74);
    }

    [Fact]
    public void ObservesUpLeftMovementUnderScoreOverlay()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeTracking");
        using var before = new Bitmap(Path.Combine(folder, "score-move-up-before.png"));
        using var after = new Bitmap(Path.Combine(folder, "score-move-up-after.png"));
        var first = DodgeSpecialist.Analyze(before, Context());
        var second = DodgeSpecialist.Analyze(after, Context());
        Assert.True(DodgeLiveRun.ClassifyMovement(first.Player, second.Player, "move-up-left", before.Size) == "movement-observed", $"Before {first.Player}; after {second.Player}");
        Assert.InRange(second.Player!.Value.X * after.Width, 219, 250);
        Assert.InRange(second.Player!.Value.Y * after.Height, 40, 70);
    }

    [Fact]
    public void ObservesUpLeftMovementBehindTheSevenScore()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeTracking");
        using var before = new Bitmap(Path.Combine(folder, "score-7-move-before.png"));
        using var after = new Bitmap(Path.Combine(folder, "score-7-move-after.png"));
        var first = DodgeSpecialist.Analyze(before, Context());
        var second = DodgeSpecialist.Analyze(after, Context());
        Assert.True(DodgeLiveRun.ClassifyMovement(first.Player, second.Player, "move-up-left", before.Size) == "movement-observed", $"Before {first.Player}; after {second.Player}");
        Assert.InRange(second.Player!.Value.X * after.Width, 249, 263);
    }

    [Fact]
    public void ObservesUpwardMovementBehindTheEightySixScore()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeTracking");
        using var before = new Bitmap(Path.Combine(folder, "score-86-move-before.png"));
        using var after = new Bitmap(Path.Combine(folder, "score-86-move-after.png"));
        var first = DodgeSpecialist.Analyze(before, Context());
        var second = DodgeSpecialist.Analyze(after, Context());
        Assert.True(DodgeLiveRun.ClassifyMovement(first.Player, second.Player, "move-up", before.Size) == "movement-observed", $"Before {first.Player}; after {second.Player}");
        Assert.InRange(first.Player!.Value.X * before.Width, 233, 264);
        Assert.InRange(first.Player!.Value.Y * before.Height, 70, 100);
        Assert.InRange(second.Player!.Value.X * after.Width, 233, 264);
        Assert.InRange(second.Player!.Value.Y * after.Height, 47, 77);
    }

    [Fact]
    public void PupilSearchDoesNotChooseBetweenTwoPlausibleLandmarks()
    {
        using var frame = new Bitmap(480, 720);
        using var dark = new SolidBrush(Color.FromArgb(76, 56, 52));
        using (var graphics = Graphics.FromImage(frame))
        {
            graphics.Clear(Color.FromArgb(56, 95, 97));
            graphics.FillEllipse(Brushes.White, 200, 35, 24, 24);
            graphics.FillEllipse(dark, 210, 45, 4, 4);
        }
        var bounds = new Rectangle(62, 8, 30, 20);
        var single = DodgeSpecialist.ScoreAreaPupil(frame, bounds);
        Assert.NotNull(single.Position);
        Assert.False(single.Ambiguous);
        using (var graphics = Graphics.FromImage(frame))
        {
            // A dark object cut by the right edge of the search area must not
            // masquerade as an extra pupil.
            graphics.FillEllipse(Brushes.White, 278, 35, 50, 48);
            graphics.FillRectangle(dark, 286, 40, 30, 30);
        }
        var clipped = DodgeSpecialist.ScoreAreaPupil(frame, bounds);
        Assert.NotNull(clipped.Position);
        Assert.False(clipped.Ambiguous);
        using (var graphics = Graphics.FromImage(frame))
        {
            graphics.Clear(Color.FromArgb(56, 95, 97));
            graphics.FillEllipse(Brushes.White, 200, 35, 24, 24);
            graphics.FillEllipse(dark, 210, 45, 4, 4);
            graphics.FillEllipse(Brushes.White, 278, 35, 24, 24);
            graphics.FillEllipse(dark, 286, 45, 6, 6);
        }
        var clippedSecondEye = DodgeSpecialist.ScoreAreaPupil(frame, bounds);
        Assert.Null(clippedSecondEye.Position);
        Assert.True(clippedSecondEye.Ambiguous);
        using (var graphics = Graphics.FromImage(frame))
        {
            graphics.Clear(Color.FromArgb(56, 95, 97));
            graphics.FillEllipse(Brushes.White, 200, 35, 24, 24);
            graphics.FillEllipse(dark, 210, 45, 4, 4);
            graphics.FillEllipse(Brushes.White, 298, 35, 24, 24);
            graphics.FillEllipse(dark, 306, 45, 6, 6);
        }
        var unresolvedEdge = DodgeSpecialist.ScoreAreaPupil(frame, bounds);
        Assert.Null(unresolvedEdge.Position);
        Assert.True(unresolvedEdge.Ambiguous);
        using (var graphics = Graphics.FromImage(frame))
        {
            graphics.Clear(Color.FromArgb(56, 95, 97));
            graphics.FillEllipse(Brushes.White, 200, 35, 24, 24);
            graphics.FillEllipse(dark, 210, 45, 4, 4);
            graphics.FillEllipse(Brushes.White, 245, 35, 24, 24);
            graphics.FillEllipse(dark, 255, 45, 4, 4);
        }
        var multiple = DodgeSpecialist.ScoreAreaPupil(frame, bounds);
        Assert.Null(multiple.Position);
        Assert.True(multiple.Ambiguous);
    }

    [Fact]
    public void AmbiguousScoreAreaLandmarksWithholdMovementAdvice()
    {
        using var frame = new Bitmap(480, 720);
        using var body = new SolidBrush(Color.FromArgb(150, 130, 130));
        using var dark = new SolidBrush(Color.FromArgb(76, 56, 52));
        using (var graphics = Graphics.FromImage(frame))
        {
            graphics.Clear(Color.FromArgb(56, 95, 97));
            graphics.FillEllipse(body, 192, 15, 80, 60);
            graphics.FillEllipse(Brushes.White, 205, 30, 24, 24);
            graphics.FillEllipse(dark, 215, 40, 4, 4);
            graphics.FillEllipse(Brushes.White, 240, 30, 24, 24);
            graphics.FillEllipse(dark, 250, 40, 4, 4);
        }
        var result = DodgeSpecialist.Analyze(frame, Context());
        Assert.Equal("unknown", result.State);
        Assert.Null(result.Player);
        Assert.Equal("Unknown", result.Advice);
        Assert.Contains("Multiple possible player landmarks", result.Detail);
    }

    [Fact]
    public void SeparatePlayerCandidatesRemainAmbiguous()
    {
        using var frame = new Bitmap(480, 720);
        using var graphics = Graphics.FromImage(frame);
        graphics.Clear(Color.FromArgb(55, 95, 94));
        using var body = new SolidBrush(Color.FromArgb(150, 130, 130));
        foreach (var x in new[] { 100, 300 })
        {
            graphics.FillEllipse(body, x, 350, 54, 54);
            graphics.FillEllipse(Brushes.White, x + 14, 364, 26, 26);
        }
        Assert.Equal("unknown", DodgeSpecialist.Analyze(frame, Context()).State);
    }

    [Fact]
    public void CollisionAwarePenaltyRisesAsClearanceShrinks()
    {
        Assert.Equal(0, DodgeMotionAvoidance.ProximityPenalty(55));
        Assert.True(DodgeMotionAvoidance.ProximityPenalty(0) > DodgeMotionAvoidance.ProximityPenalty(30));
        Assert.True(DodgeMotionAvoidance.ProximityPenalty(-10) > DodgeMotionAvoidance.ProximityPenalty(0));
    }

    [Fact]
    public void CollisionAwareCandidateChangesMoveForConvergingThreats()
    {
        var context = Context();
        var player = new PointF(.5f, .5f);
        DodgeThreatMotion[] threats =
        [
            new(new PointF(266f / 480, 424f / 720), new PointF(114, -70), true),
            new(new PointF(280f / 480, 466f / 720), new PointF(95, -180), true)
        ];
        Assert.Equal("Up", DodgeMotionAvoidance.Recommend(player, threats, context, 120));
        Assert.Equal("Left", DodgeMotionAvoidance.Recommend(player, threats, context with { PlayerRadius = 34, ThreatRadius = 50 }, 120, collisionAware: true));
    }

    [Fact]
    public void EdgeAwareCandidateRecentersWhenNoThreatsAreVisible()
    {
        var context = Context();
        Assert.Equal("Hold", DodgeMotionAvoidance.Recommend(new PointF(.1f, .5f), Array.Empty<DodgeThreatMotion>(), context, 120));
        Assert.Equal("Right", DodgeMotionAvoidance.Recommend(new PointF(.1f, .5f), Array.Empty<DodgeThreatMotion>(), context, 120, edgeAware: true));
        Assert.Equal("Down", DodgeMotionAvoidance.Recommend(new PointF(.5f, .1f), Array.Empty<DodgeThreatMotion>(), context, 120, edgeAware: true));
        Assert.Equal("Hold", DodgeMotionAvoidance.Recommend(new PointF(.5f, .5f), Array.Empty<DodgeThreatMotion>(), context, 120, edgeAware: true));
    }

    [Fact]
    public void SafeEdgeCandidateRejectsMovingTowardApproachingThreat()
    {
        var context = Context() with { PlayerRadius = 34, ThreatRadius = 50 };
        var player = new PointF(.790989f, .297223f);
        DodgeThreatMotion[] threats =
        [
            new(new PointF(.749141f, .1277f), new PointF(-76.5f, 111.78f), true),
            new(new PointF(.57037f, .365531f), new PointF(223.17f, 20.85f), true),
            new(new PointF(.687963f, .605284f), new PointF(44.4f, -153.53f), true),
            new(new PointF(.892607f, .715512f), new PointF(172.92f, 9.69f), true),
            new(new PointF(.628765f, .968048f), new PointF(-25.12f, -91.28f), true)
        ];
        Assert.Equal("Left", DodgeMotionAvoidance.Recommend(player, threats, context, 120, collisionAware: true, edgeAware: true));
        Assert.NotEqual("Left", DodgeSafeEdgeAvoidance.Recommend(player, threats, context, 120));
    }

    [Fact]
    public void LookaheadRespectsBoundsAndApproachingThreats()
    {
        var context = Context() with { PlayerRadius = 34, ThreatRadius = 50 };
        Assert.Equal("Hold", DodgeLookaheadAvoidance.Recommend(new PointF(.5f, .5f), Array.Empty<DodgeThreatMotion>(), context, 60));
        var threats = new[] { new DodgeThreatMotion(new PointF(.25f, .5f), new PointF(-160, 0), true) };
        Assert.DoesNotContain("Left", DodgeLookaheadAvoidance.Recommend(new PointF(.08f, .5f), threats, context, 60));
    }

    [Fact]
    public void CenterGuardRecentersOnlyWhenThereIsNoImmediateDanger()
    {
        var context = Context() with { PlayerRadius = 34, ThreatRadius = 50 };
        Assert.Equal("Right", DodgeCenterGuardAvoidance.Recommend(new PointF(.1f, .5f), Array.Empty<DodgeThreatMotion>(), context, 60));
        var player = new PointF(.1f, .5f);
        var threats = new[] { new DodgeThreatMotion(new PointF(.25f, .5f), new PointF(-160, 0), true) };
        Assert.NotEqual("Right", DodgeCenterGuardAvoidance.Recommend(player, threats, context, 60));
    }

    [Fact]
    public void CenterFirstHoldsAtCenterAndReturnsWhenSafe()
    {
        var context = Context() with { PlayerRadius = 34, ThreatRadius = 50 };
        Assert.Equal("Hold", DodgeCenterFirstAvoidance.Recommend(new PointF(.5f, .5f), Array.Empty<DodgeThreatMotion>(), context, 60));
        Assert.Equal("Right", DodgeCenterFirstAvoidance.Recommend(new PointF(.1f, .5f), Array.Empty<DodgeThreatMotion>(), context, 60));
        var threats = new[] { new DodgeThreatMotion(new PointF(.25f, .5f), new PointF(-160, 0), true) };
        Assert.NotEqual("Right", DodgeCenterFirstAvoidance.Recommend(new PointF(.1f, .5f), threats, context, 60));
    }

    [Fact]
    public void LongerHorizonReactivelyMovesBeforeThreatArrives()
    {
        var context = Context() with { PlayerRadius = 34, ThreatRadius = 50 };
        var player = new PointF(.5f, .5f);
        var threats = new[] { new DodgeThreatMotion(new PointF(.5f, 160f / 720), new PointF(0, 200), true) };
        Assert.Equal("Hold", DodgeCenterFirstAvoidance.Recommend(player, threats, context, 60, horizonSeconds: .35, safeClearance: 30));
        Assert.NotEqual("Hold", DodgeCenterFirstAvoidance.Recommend(player, threats, context, 60, horizonSeconds: .7, safeClearance: 30));
    }

    [Fact]
    public void PackHorizonCanChangeAnticipationWithoutChangingThePolicy()
    {
        var context = Context() with { PlayerRadius = 34, ThreatRadius = 50 };
        var player = new PointF(.5f, .5f);
        var threats = new[] { new DodgeThreatMotion(new PointF(.5f, 140f / 720), new PointF(0, 200), true) };
        Assert.Equal("Hold", DodgeCenterFirstAvoidance.Recommend(player, threats, context, 60, horizonSeconds: .35));
        Assert.NotEqual("Hold", DodgeCenterFirstAvoidance.Recommend(player, threats, context, 60, horizonSeconds: .7));
    }

    [Fact]
    public void AdaptiveHorizonAvoidsPanicWhenOnlyTheLongForecastIsOverconstrained()
    {
        var context = Context() with { PlayerRadius = 34, ThreatRadius = 50, HorizonMs = 700 };
        var player = new PointF(.49811745f, .48843375f);
        var first = new[] { new DodgeThreatMotion(new PointF(.93242645f, .6314353f), new PointF(-179.86f, -126.63f), true) };
        Assert.Null(DodgeCenterFirstAvoidance.RecommendSafe(player, first, context, 60, .7));
        Assert.Equal("Hold", DodgeAdaptiveHorizonAvoidance.Recommend(player, first, context, 60));
        var second = new[] { new DodgeThreatMotion(new PointF(.48871952f, .89560974f), new PointF(8.78f, -239.54f), true) };
        Assert.Equal("Up-Right", DodgeAdaptiveHorizonAvoidance.Recommend(player, second, context, 60));
    }

    [Fact]
    public void AdaptiveCenterRelaxesTheMarginOnlyWhenAPathRemainsClear()
    {
        Assert.Equal(40, DodgeAdaptiveCenterAvoidance.RequiredClearance(100));
        Assert.Equal(5, DodgeAdaptiveCenterAvoidance.RequiredClearance(15));
        Assert.Equal(0, DodgeAdaptiveCenterAvoidance.RequiredClearance(3));
        var context = Context() with { PlayerRadius = 34, ThreatRadius = 50 };
        Assert.Equal("Hold", DodgeAdaptiveCenterAvoidance.Recommend(new PointF(.5f, .5f), Array.Empty<DodgeThreatMotion>(), context, 60));
        Assert.Equal("Right", DodgeAdaptiveCenterAvoidance.Recommend(new PointF(.1f, .5f), Array.Empty<DodgeThreatMotion>(), context, 60));
    }

    [Fact]
    public void TrajectoryPlannerKeepsAStationaryCenterAndAvoidsClosedEdges()
    {
        var context = Context() with { PlayerRadius = 34, ThreatRadius = 50 };
        Assert.Equal("Hold", DodgeTrajectoryAvoidance.Recommend(new PointF(.5f, .5f), Array.Empty<DodgeThreatMotion>(), context, 60));
        Assert.DoesNotContain("Left", DodgeTrajectoryAvoidance.Recommend(new PointF(.08f, .5f), Array.Empty<DodgeThreatMotion>(), context, 60));
    }

    [Fact]
    public void RolloutDoesNotDriveIntoABoundaryOrAnApproachingThreat()
    {
        var context = Context() with { PlayerRadius = 34, ThreatRadius = 50 };
        var player = new PointF(.08f, .5f);
        var threats = new[] { new DodgeThreatMotion(new PointF(.25f, .5f), new PointF(-160, 0), true) };
        Assert.DoesNotContain("Left", DodgeRolloutAvoidance.Recommend(player, threats, context, 60));
        Assert.Equal("Hold", DodgeRolloutAvoidance.Recommend(new PointF(.5f, .5f), Array.Empty<DodgeThreatMotion>(), context, 60));
    }

    [Fact]
    public void RecognizesRecordedGameStatesAndDoesNotGuessBlankFrames()
    {
        var context = Context();
        foreach (var (file, expected) in new[] { ("menu.png", "menu"), ("ready.png", "ready"), ("game-over.png", "game_over"), ("trail-left.png", "playing") })
        {
            using var frame = new Bitmap(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeTracking", file));
            Assert.Equal(expected, DodgeSpecialist.Analyze(frame, context).State);
        }
        using var blank = new Bitmap(480, 720);
        Assert.Equal("unknown", DodgeSpecialist.Analyze(blank, context).State);
    }

    [Fact]
    public void IdentificationRubricRequiresAllThreeEvidenceSignals()
    {
        var context = Context();
        Assert.True(context.IdentityScore(true, true, true) >= context.IdentityThreshold);
        Assert.True(context.IdentityScore(false, true, true) < context.IdentityThreshold);
        Assert.True(context.IdentityScore(true, false, true) < context.IdentityThreshold);
        Assert.True(context.IdentityScore(true, true, false) < context.IdentityThreshold);
    }

    [Fact]
    public void TracksMotionOnlyWithRecentUnambiguousObservations()
    {
        var context = Context();
        var tracker = new DodgeMotionTracker();
        Assert.False(tracker.Update(new[] { new PointF(.75f, .5f) }, 1000, context)[0].VelocityKnown);
        var motion = tracker.Update(new[] { new PointF(.70f, .5f) }, 1200, context)[0];
        Assert.True(motion.VelocityKnown);
        Assert.InRange(motion.VelocityPxPerSecond.X, -121, -119);
        Assert.False(tracker.Update(new[] { new PointF(.65f, .5f) }, 3000, context)[0].VelocityKnown);
        var ambiguous = new DodgeMotionTracker();
        ambiguous.Update(new[] { new PointF(.45f, .5f), new PointF(.55f, .5f) }, 1000, context);
        Assert.False(ambiguous.Update(new[] { new PointF(.5f, .5f) }, 1200, context)[0].VelocityKnown);
    }

    [Fact]
    public void VelocityGuardMarksAbruptInconsistentVisualMotionUnknown()
    {
        var context = Context();
        var tracker = new DodgeMotionTracker(guardVelocity: true);
        tracker.Update(new[] { new PointF(200f / 480, .5f) }, 1000, context);
        Assert.True(tracker.Update(new[] { new PointF(160f / 480, .5f) }, 1200, context)[0].VelocityKnown);
        Assert.False(tracker.Update(new[] { new PointF(160f / 480, .5f) }, 1400, context)[0].VelocityKnown);
    }

    [Fact]
    public void MotionCarryRetainsOneUncertainEstimateWithoutAssertingCertainty()
    {
        var context = Context();
        var tracker = new DodgeMotionTracker(carryUncertainVelocity: true);
        tracker.Update(new[] { new PointF(200f / 480, .5f) }, 1000, context);
        Assert.True(tracker.Update(new[] { new PointF(160f / 480, .5f) }, 1200, context)[0].VelocityKnown);
        var uncertain = tracker.Update(new[] { new PointF(160f / 480, .5f) }, 1400, context)[0];
        Assert.False(uncertain.VelocityKnown);
        Assert.InRange(uncertain.VelocityPxPerSecond.X, -201, -199);
        var next = tracker.Update(new[] { new PointF(160f / 480, .5f) }, 1600, context)[0];
        Assert.True(next.VelocityKnown);
        Assert.Equal(0, next.VelocityPxPerSecond.X);
    }

    [Fact]
    public void SmoothedVelocityUsesRecentHistoryAndExpiresIt()
    {
        var context = Context();
        var tracker = new DodgeMotionTracker(smoothVelocity: true);
        tracker.Update(new[] { new PointF(200f / 480, .5f) }, 1000, context);
        Assert.True(tracker.Update(new[] { new PointF(160f / 480, .5f) }, 1200, context)[0].VelocityKnown);
        var smoothed = tracker.Update(new[] { new PointF(160f / 480, .5f) }, 1400, context)[0];
        Assert.True(smoothed.VelocityKnown);
        Assert.InRange(smoothed.VelocityPxPerSecond.X, -131, -129);
        Assert.False(tracker.Update(new[] { new PointF(160f / 480, .5f) }, 3000, context)[0].VelocityKnown);
    }

    [Theory]
    [InlineData("directional-guard-v27")]
    [InlineData("directional-carry-v28")]
    [InlineData("directional-smooth-v29")]
    [InlineData("directional-configurable-v30")]
    [InlineData("directional-two-phase-v31")]
    [InlineData("directional-pulse-v32")]
    public void DirectionalTrackerVariantsAreAcceptedInGameContext(string policy)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeContext", "pack.json");
        var pack = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        pack["context"]!["avoidance_policy"] = policy;
        if (policy == "directional-configurable-v30")
        {
            pack["context"]!["constraints"]!["risk_weight_percent"] = 200;
            pack["context"]!["constraints"]!["edge_weight_percent"] = 50;
        }
        using var document = System.Text.Json.JsonDocument.Parse(pack.ToJsonString());
        var context = DodgeGameContext.Parse(document.RootElement.GetProperty("context"));
        Assert.Equal(policy, context.AvoidancePolicy);
    }

    [Fact]
    public void PlanningWeightsChangeTheRiskAndRoomTradeoff()
    {
        var riskFirst = Context() with { RiskWeightPercent = 400, EdgeWeightPercent = 0 };
        var roomFirst = Context() with { RiskWeightPercent = 25, EdgeWeightPercent = 300 };
        Assert.True(DodgeDirectionalAvoidance.Score(10, 0, 10, riskFirst, true) > DodgeDirectionalAvoidance.Score(30, 0, 200, riskFirst, true));
        Assert.True(DodgeDirectionalAvoidance.Score(10, 0, 10, roomFirst, true) < DodgeDirectionalAvoidance.Score(30, 0, 200, roomFirst, true));
    }

    [Fact]
    public void DirectionalPlanningWeightsAreBounded()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeContext", "pack.json");
        var pack = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        pack["context"]!["avoidance_policy"] = "directional-configurable-v30";
        pack["context"]!["constraints"]!["risk_weight_percent"] = 401;
        pack["context"]!["constraints"]!["edge_weight_percent"] = 100;
        using var document = System.Text.Json.JsonDocument.Parse(pack.ToJsonString());
        Assert.Throws<InvalidDataException>(() => DodgeGameContext.Parse(document.RootElement.GetProperty("context")));
    }

    [Fact]
    public void EdgeAwareFallbackAvoidsMovingFartherIntoBoundary()
    {
        var context = Context();
        var player = new PointF(44f / 480, .5f);
        DodgeThreatMotion[] threats = [new(new PointF(.1f, .43f), new PointF(0, 150), true)];
        var advice = DodgeCenterFirstAvoidance.Recommend(player, threats, context, 60, edgeAwareFallback: true);
        Assert.DoesNotContain("Left", advice);
    }

    [Fact]
    public void GuardedEdgeFallbackRejectsLosingImmediateClearance()
    {
        var context = Context();
        var player = new PointF(44f / 480, .5f);
        DodgeThreatMotion[] threats = [new(new PointF(125f / 480, .5f), PointF.Empty, true)];
        Assert.False(DodgeGuardedEdgeFallback.HasComparableClearance(player, threats, context, 60, "Down", "Right"));
        Assert.True(DodgeGuardedEdgeFallback.HasComparableClearance(player, threats, context, 60, "Down", "Down"));
    }

    [Fact]
    public void DirectionalPlannerCanRecoverWhenAlreadyInsideEdgeMargin()
    {
        var context = Context();
        Assert.True(DodgeDirectionalAvoidance.AllowedAxis(5, 20, 25, 455));
        Assert.False(DodgeDirectionalAvoidance.AllowedAxis(5, 0, 25, 455));
        Assert.Equal("Right", DodgeDirectionalAvoidance.Recommend(new PointF(5f / 480, .5f), [], context, 60));
    }

    [Fact]
    public void DirectionalPlannerDoesNotDriveTowardAnApproachingEnemy()
    {
        var context = Context();
        DodgeThreatMotion[] threats = [new(new PointF(.67f, .5f), new PointF(-150, 0), true)];
        var advice = DodgeDirectionalAvoidance.Recommend(new PointF(.5f, .5f), threats, context, 60);
        Assert.DoesNotContain("Right", advice);
    }

    [Fact]
    public void DefaultConfigurableWeightsMatchThePreviousDirectionalPlanner()
    {
        var context = Context() with { RiskWeightPercent = 100, EdgeWeightPercent = 100 };
        DodgeThreatMotion[] threats = [new(new PointF(.67f, .5f), new PointF(-150, 0), true)];
        foreach (var player in new[] { new PointF(.5f, .5f), new PointF(.1f, .4f), new PointF(.85f, .8f) })
            Assert.Equal(DodgeDirectionalAvoidance.Recommend(player, threats, context, 60), DodgeDirectionalAvoidance.Recommend(player, threats, context, 60, configurable: true));
    }

    [Fact]
    public void TwoPhasePlannerKeepsMovesBoundedAndAvoidsAnApproachingThreat()
    {
        var context = Context();
        DodgeThreatMotion[] threats = [new(new PointF(.67f, .5f), new PointF(-150, 0), true)];
        Assert.DoesNotContain("Right", DodgeTwoPhaseAvoidance.Recommend(new PointF(.5f, .5f), threats, context, 60));
        Assert.DoesNotContain("Left", DodgeTwoPhaseAvoidance.Recommend(new PointF(5f / 480, .5f), [], context, 60));
    }

    [Fact]
    public void PulseForecastDoesNotInventMovementDuringTheReleaseGap()
    {
        const double duration = .06;
        var cycle = duration + 1.0 / 15;
        Assert.Equal(.03, DodgeTwoPhaseAvoidance.PulseSeconds(.03, duration, cycle), 6);
        Assert.Equal(.06, DodgeTwoPhaseAvoidance.PulseSeconds(.10, duration, cycle), 6);
        Assert.Equal(.08, DodgeTwoPhaseAvoidance.PulseSeconds(cycle + .02, duration, cycle), 6);
        Assert.Equal(.12, DodgeTwoPhaseAvoidance.PulseSeconds(2 * cycle, duration, cycle), 6);
    }

    [Fact]
    public void PredictiveTrackerKeepsMotionDirectionWhenEnemiesCross()
    {
        var context = Context();
        var tracker = new DodgeMotionTracker(predictive: true);
        tracker.Update(new[] { new PointF(150f / 480, .5f), new PointF(290f / 480, .5f) }, 1000, context);
        tracker.Update(new[] { new PointF(190f / 480, .5f), new PointF(250f / 480, .5f) }, 1200, context);
        var crossed = tracker.Update(new[] { new PointF(230f / 480, .5f), new PointF(210f / 480, .5f) }, 1400, context);
        Assert.True(crossed[0].VelocityKnown);
        Assert.True(crossed[1].VelocityKnown);
        Assert.InRange(crossed[0].VelocityPxPerSecond.X, 199, 201);
        Assert.InRange(crossed[1].VelocityPxPerSecond.X, -201, -199);
    }

    [Fact]
    public void TrackerConsensusRejectsLargeOrUnknownVelocityDisagreement()
    {
        var p = new PointF(.5f, .5f);
        var a = new[] { new DodgeThreatMotion(p, new PointF(100, 20), true) };
        var close = new[] { new DodgeThreatMotion(p, new PointF(120, 40), true) };
        var far = new[] { new DodgeThreatMotion(p, new PointF(-100, 20), true) };
        var unknown = new[] { new DodgeThreatMotion(p, PointF.Empty, false) };
        Assert.True(DodgeMotionConsensus.Merge(a, close)[0].VelocityKnown);
        Assert.False(DodgeMotionConsensus.Merge(a, far)[0].VelocityKnown);
        Assert.False(DodgeMotionConsensus.Merge(a, unknown)[0].VelocityKnown);
    }

    [Fact]
    public void MovingThreatDiscouragesSteppingIntoItsPath()
    {
        var context = Context();
        var threat = new DodgeThreatMotion(new PointF(.67f, .5f), new PointF(-180, 0), true);
        Assert.NotEqual("Right", DodgeMotionAvoidance.Recommend(new PointF(.5f, .5f), new[] { threat }, context, 120));
        Assert.Equal("Hold", DodgeMotionAvoidance.Recommend(new PointF(.5f, .5f), Array.Empty<DodgeThreatMotion>(), context, 120));
    }

    [Fact]
    public void CandidateAvoidsApproachingThreatAndRespectsEdge()
    {
        var candidate = DodgeLivePack.Load(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeContextV2", "pack.json")).Context!;
        Assert.Equal("resilient-v2", candidate.AvoidancePolicy);
        Assert.Equal("motion-v1", Context().AvoidancePolicy);
        var threat = new DodgeThreatMotion(new PointF(.67f, .5f), new PointF(-180, 0), true);
        Assert.NotEqual("Right", DodgeResilientAvoidance.Recommend(new PointF(.5f, .5f), new[] { threat }, candidate, 120, null));
        Assert.NotEqual("Left", DodgeResilientAvoidance.Recommend(new PointF(.09f, .5f), new[] { threat }, candidate, 120, null));
        Assert.Equal("Hold", DodgeResilientAvoidance.Recommend(new PointF(.5f, .5f), Array.Empty<DodgeThreatMotion>(), candidate, 120, null));
    }

    [Fact]
    public void GuardedCandidateKeepsBaselineForEqualClearanceAndNoThreats()
    {
        var guarded = DodgeLivePack.Load(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeContextV3", "pack.json")).Context!;
        Assert.Equal("guarded-v3", guarded.AvoidancePolicy);
        Assert.Equal(Context().ThreatRadius, guarded.ThreatRadius);
        Assert.Equal(Context().HorizonMs, guarded.HorizonMs);
        Assert.Equal("Hold", DodgeGuardedAvoidance.Recommend(new PointF(.5f, .5f), Array.Empty<DodgeThreatMotion>(), guarded, 120, null));
        var threat = new DodgeThreatMotion(new PointF(.67f, .5f), new PointF(-180, 0), true);
        Assert.NotEqual("Right", DodgeGuardedAvoidance.Recommend(new PointF(.5f, .5f), new[] { threat }, guarded, 120, null));
        var recovery = DodgeLivePack.Load(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeContextV4", "pack.json")).Context!;
        Assert.Equal("guarded-v4", recovery.AvoidancePolicy);
        Assert.Equal(Context().ThreatRadius, recovery.ThreatRadius);
    }
    [Fact]
    public void RecommendsAwayFromThreatAndAvoidsEdge()
    {
        Assert.Equal("Left", DodgeSpecialist.Recommend(new(.5f, .5f), new[] { new PointF(.7f, .5f) }));
        Assert.NotEqual("Left", DodgeSpecialist.Recommend(new(.1f, .5f), new[] { new PointF(.3f, .5f) }));
        Assert.Equal("Hold", DodgeSpecialist.Recommend(new(.5f, .5f), Array.Empty<PointF>()));
    }

    [Fact]
    public void FindsPlayerAndThreatWithoutSendingInput()
    {
        using var frame = new Bitmap(480, 750);
        using (var graphics = Graphics.FromImage(frame))
        {
            graphics.Clear(Color.FromArgb(55, 95, 94));
            using var player = new SolidBrush(Color.FromArgb(150, 130, 130));
            using var enemy = new SolidBrush(Color.FromArgb(210, 50, 70));
            graphics.FillEllipse(player, 210, 380, 54, 54);
            graphics.FillEllipse(Brushes.White, 224, 394, 26, 26);
            graphics.FillEllipse(enemy, 340, 385, 30, 30);
        }
        var result = DodgeSpecialist.Analyze(frame);
        Assert.NotNull(result.Player);
        Assert.Single(result.Threats);
        Assert.Equal("Left", result.Advice);
    }

    [Fact]
    public void RecognizesPlayerAmongRecordedMotionTrails()
    {
        foreach (var (file, x, y) in new[] { ("trail-left.png", .112f, .620f), ("trail-down.png", .276f, .73f) })
        {
            using var frame = new Bitmap(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeTracking", file));
            var result = DodgeSpecialist.Analyze(frame);
            Assert.NotNull(result.Player);
            Assert.InRange(result.Player.Value.X, x - .03f, x + .03f);
            Assert.InRange(result.Player.Value.Y, y - .04f, y + .04f);
        }
    }

    [Fact]
    public void RecognizesPlayerNearTopOfPrivateViewport()
    {
        using var frame = new Bitmap(Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeTracking", "top-edge.png"));
        var result = DodgeSpecialist.Analyze(frame, Context());
        Assert.Equal("playing", result.State);
        Assert.NotNull(result.Player);
        Assert.InRange(result.Player.Value.X, .08f, .15f);
        Assert.InRange(result.Player.Value.Y, .04f, .12f);
    }

    [Fact]
    public void TwoBrightEyedPlayersRemainAmbiguous()
    {
        using var frame = new Bitmap(480, 750);
        using (var graphics = Graphics.FromImage(frame))
        using (var player = new SolidBrush(Color.FromArgb(150, 130, 130)))
        {
            graphics.Clear(Color.FromArgb(55, 95, 94));
            foreach (var x in new[] { 100, 300 })
            {
                graphics.FillEllipse(player, x, 300, 54, 54);
                graphics.FillEllipse(Brushes.White, x + 14, 314, 26, 26);
            }
        }
        var result = DodgeSpecialist.Analyze(frame);
        Assert.Null(result.Player);
        Assert.Equal("Unknown", result.Advice);
    }

    [Fact]
    public void TooManyThreatCandidatesDoNotProduceAdvice()
    {
        using var frame = new Bitmap(480, 750);
        using (var graphics = Graphics.FromImage(frame))
        using (var enemy = new SolidBrush(Color.FromArgb(210, 50, 70)))
        {
            graphics.Clear(Color.FromArgb(55, 95, 94));
            for (var row = 0; row < 7; row++)
                for (var column = 0; column < 5; column++)
                    graphics.FillEllipse(enemy, 15 + column * 90, 120 + row * 70, 18, 18);
        }
        var result = DodgeSpecialist.Analyze(frame);
        Assert.Null(result.Player);
        Assert.Contains("Too many", result.Detail);
    }

    [Fact]
    public void UnknownOrAmbiguousSceneDoesNotAdviseMovement()
    {
        using var frame = new Bitmap(480, 750);
        using (var graphics = Graphics.FromImage(frame)) graphics.Clear(Color.FromArgb(55, 95, 94));
        Assert.Equal("Unknown", DodgeSpecialist.Analyze(frame).Advice);
        using (var graphics = Graphics.FromImage(frame))
        using (var player = new SolidBrush(Color.FromArgb(150, 130, 130)))
        {
            graphics.FillEllipse(player, 100, 300, 45, 45);
            graphics.FillEllipse(player, 300, 300, 45, 45);
        }
        Assert.Equal("Unknown", DodgeSpecialist.Analyze(frame).Advice);
    }
}
