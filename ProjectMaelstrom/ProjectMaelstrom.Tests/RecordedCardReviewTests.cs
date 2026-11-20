using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class RecordedCardReviewTests
{
    private static readonly TeachingTarget Target = new(10, 20, 30, "game");
    private static TeachingSession Session => new(1, "session", "wizard101", 0, "complete",
        [new("chunk-001", 0, 10000, "complete", 0)], [], Target);
    private static JobCardSpec Card => new(Guid.NewGuid().ToString("N"), "w101.questing", "Quest", Conditions:
        [new("energy", "gte", JsonSerializer.SerializeToElement(10))]);
    private static TeachingStateSample Sample(double time, int energy, string status = "observed") =>
        new(1, "session", Target, time, "fixture", "zone", status, "world", new() { ["energy"] = JsonSerializer.SerializeToElement(energy) });
    [Theory]
    [InlineData(10, "Eligible")]
    [InlineData(9, "Blocked")]
    public void UsesNativeComparisonAtRecordedTime(int energy, string expected) =>
        Assert.Equal(expected, RecordedCardReview.Evaluate(Card, Session, [Sample(100, energy)], 200, JobFactCatalog.All).Status);
    [Fact]
    public void DoesNotBorrowFutureCarryThroughLoadingOrTrustStaleFacts()
    {
        var card = Card;
        Assert.Equal("Unknown", RecordedCardReview.Evaluate(card, Session, [Sample(100, 20)], 99, JobFactCatalog.All).Status);
        Assert.Equal("Unknown", RecordedCardReview.Evaluate(card, Session, [Sample(100, 20)], 2101, JobFactCatalog.All).Status);
        Assert.Equal("Unknown", RecordedCardReview.Evaluate(card, Session, [Sample(100, 20), Sample(200, 20, "loading")], 201, JobFactCatalog.All).Status);
        Assert.Equal("Unknown", RecordedCardReview.Evaluate(card, Session, [Sample(0, 20) with { Facts = [] }], 100, JobFactCatalog.All).Status);
    }
    [Fact]
    public void RejectsWrongBindingAndUnknownCondition()
    {
        Assert.Throws<InvalidDataException>(() => RecordedCardReview.Evaluate(Card, Session, [Sample(0, 20) with { Target = Target with { ProcessId = 99 } }], 100, JobFactCatalog.All));
        Assert.Throws<InvalidDataException>(() => RecordedCardReview.Evaluate(Card with { Conditions = [new("imaginary", "eq", JsonSerializer.SerializeToElement(true))] }, Session, [Sample(0, 20)], 100, JobFactCatalog.All));
    }
    [Fact]
    public void EditsChangeEvidenceIdentityAndNeverPromoteCard()
    {
        var card = Card; Assert.NotEqual(RecordedCardReview.Hash(card), RecordedCardReview.Hash(card with { CooldownSeconds = 20 }));
        var report = new RecordedCardReport(RecordedCardReview.Hash(card), "session", "hash", []);
        Assert.False(report.LiveQualified); Assert.Equal(0, report.DesktopDispatchCount); Assert.Contains("unverified", report.Qualification);
    }
    [Fact]
    public void ExclusionStillWinsInReplay()
    {
        var card = Card with { Filters = [new("quest.area", ["Commons"], ["Commons"])] };
        var sample = Sample(0, 20); sample.Facts.Add("quest.area", JsonSerializer.SerializeToElement("Commons"));
        Assert.Equal("Blocked", RecordedCardReview.Evaluate(card, Session, [sample], 100, JobFactCatalog.All).Status);
    }
    [Fact]
    public void OptionalRealRecordingReviewAndNativeCardRender()
    {
        var source = Environment.GetEnvironmentVariable("MAELSTROM_CARD_RECORDED_SESSION");
        if (!string.IsNullOrEmpty(source))
        {
            var report = RecordedCardReview.Run(Card with { Conditions = [new("nativeIdle", "eq", JsonSerializer.SerializeToElement(true))] }, source, JobFactCatalog.All, "*");
            Assert.NotEmpty(report.Decisions); Assert.False(report.LiveQualified);
            var output = Environment.GetEnvironmentVariable("MAELSTROM_CARD_REVIEW_OUTPUT");
            if (!string.IsNullOrEmpty(output)) File.WriteAllText(output, JsonSerializer.Serialize(report, JobDeckRules.Json));
        }
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var routine = JobDeckRoutines.Create().Single(r => r.Info.Id == "review.routine");
                using var view = new JobCardView(Card with { Title = "My custom replay" }, routine.Info, _ => { }, () => { });
                using var bitmap = new Bitmap(view.Width, view.Height); view.DrawToBitmap(bitmap, view.ClientRectangle);
                var image = Environment.GetEnvironmentVariable("MAELSTROM_NATIVE_CARD_IMAGE");
                if (!string.IsNullOrEmpty(image)) bitmap.Save(image);
                Assert.Equal(330, view.Width);
            }
            catch (Exception e) { failure = e; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(10))); Assert.Null(failure);
    }
}
