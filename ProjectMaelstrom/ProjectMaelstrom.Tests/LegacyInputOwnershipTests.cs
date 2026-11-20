using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class LegacyInputOwnershipTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-input-" + Guid.NewGuid().ToString("N"));
    private bool _ready = true;
    private long _time = 100;
    private readonly List<string> _events = [];
    private PlayerController Create() => new(new LegacyInputHooks(() => _ready, k => _events.Add("down:" + k),
        k => _events.Add("up:" + k), _ => _events.Add("click")),
        () => GameInputLease.Acquire("test-controller", _root), () => _time, false);

    [Fact]
    public void HeldKeyRetainsOwnershipAndFocusLossReleasesWithoutRefocusing()
    {
        var controller = Create(); controller.KeyDown("UP");
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("competitor", _root));
        _ready = false; controller.CheckHeld();
        Assert.Equal(new[] { "down:UP", "up:UP" }, _events);
        using var next = GameInputLease.Acquire("next", _root); Assert.True(next.IsHeld);
        Assert.Throws<InvalidOperationException>(() => controller.KeyPress("UP"));
        Assert.Equal(2, _events.Count);
    }

    [Fact]
    public void RepeatedDownCannotExtendTheHoldBudget()
    {
        var controller = Create(); controller.KeyDown("UP"); _time = 1800; controller.KeyDown("UP");
        _time = 2100; controller.CheckHeld();
        Assert.Equal("up:UP", _events.Last());
        using var next = GameInputLease.Acquire("next", _root); Assert.True(next.IsHeld);
    }

    [Fact]
    public void ScopeBorrowingDoesNotReleaseSchedulersLease()
    {
        using var owner = GameInputLease.Acquire("scheduler", _root);
        using (GameInputLease.EnterScope(owner)) Create().KeyPress("UP");
        Assert.True(owner.IsHeld);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("competitor", _root));
        Assert.Equal(new[] { "down:UP", "up:UP" }, _events);
    }

    [Fact]
    public void BusyOwnerPreventsDispatch()
    {
        using var owner = GameInputLease.Acquire("teacher", _root);
        Assert.Throws<InvalidOperationException>(() => Create().KeyPress("UP"));
        Assert.Empty(_events);
    }

    [Theory]
    [InlineData("key-down")]
    [InlineData("click")]
    public void PoisonedCachedOwnerRejectsNewInputAndOnlyReleasesHeldKeys(string action)
    {
        using var owner = GameInputLease.Acquire("scheduler", _root);
        using var scope = GameInputLease.EnterScope(owner);
        var controller = Create();
        try
        {
            controller.KeyDown("UP");
            owner.RequireRecovery("simulated uncertain release");
            Assert.True(owner.IsHeld); Assert.True(owner.RecoveryRequired); Assert.False(owner.CanDispatch);
            Assert.Null(GameInputLease.Current);
            Assert.Throws<InvalidOperationException>(() =>
            {
                if (action == "key-down") controller.KeyDown("LEFT");
                else controller.Click(new Point(20, 20));
            });
            Assert.Equal(new[] { "down:UP", "up:UP" }, _events);
            Assert.True(owner.IsHeld);
        }
        finally { controller.ReleaseAll(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PoisonedOwnerStillAllowsReleaseOnlyCleanup(bool throughTimerCheck)
    {
        using var owner = GameInputLease.Acquire("scheduler", _root);
        using var scope = GameInputLease.EnterScope(owner);
        var controller = Create();
        try
        {
            controller.KeyDown("UP");
            owner.RequireRecovery("simulated uncertain release");
            if (throughTimerCheck) controller.CheckHeld();
            else controller.KeyUp("UP");
            Assert.Equal(new[] { "down:UP", "up:UP" }, _events);
            Assert.True(owner.IsHeld);
            Assert.False(owner.CanDispatch);
        }
        finally { controller.ReleaseAll(); }
    }

    [Fact]
    public void ReleaseFailureAndUnavailableWarningDoNotAbortLaterReleaseCleanup()
    {
        using var owner = GameInputLease.Acquire("scheduler", _root);
        using var scope = GameInputLease.EnterScope(owner);
        var releaseAttempts = 0;
        var controller = new PlayerController(new LegacyInputHooks(() => true, key => _events.Add("down:" + key), key =>
        {
            _events.Add("release-attempt:" + key);
            if (++releaseAttempts == 1) throw new IOException("test key release failed");
        }, _ => _events.Add("click")), () => GameInputLease.Acquire("must-not-acquire", _root), () => _time, false);
        var notice = Path.Combine(_root, "input-recovery-required.txt");
        try
        {
            controller.KeyDown("UP");
            Directory.CreateDirectory(notice); _time = 2100;
            Assert.Null(Record.Exception(controller.CheckHeld));
            Assert.True(owner.RecoveryRequired); Assert.False(owner.CanDispatch); Assert.True(owner.IsHeld);
            Directory.Delete(notice);
            controller.KeyUp("UP");
            Assert.Equal(2, releaseAttempts);
            Assert.Equal(new[] { "down:UP", "release-attempt:UP", "release-attempt:UP" }, _events);
            Assert.True(owner.IsHeld); Assert.False(Path.Exists(notice));
        }
        finally
        {
            if (Directory.Exists(notice)) Directory.Delete(notice);
            controller.ReleaseAll();
        }
    }

    [Fact]
    public void CreateBackgroundHooks_ZeroHandleOrPolicyDisabled_IsNotReady()
    {
        var zeroHooks = PlayerController.CreateBackgroundHooks(IntPtr.Zero);
        Assert.False(zeroHooks.Ready());

        // Even with non-zero dummy handle, if live automation policy is false, ready is false
        var fakeHooks = PlayerController.CreateBackgroundHooks(new IntPtr(12345));
        if (!ExecutionPolicyManager.Current.AllowLiveAutomation)
        {
            Assert.False(fakeHooks.Ready());
        }
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
