namespace ProjectMaelstrom.Utilities;

/// <summary>Trusted W101 UI bindings shared by image review and the prepared owned
/// input adapter. Saved packs cannot extend this narrow action/outcome allowlist.</summary>
internal static class W101ModulePrograms
{
    public static ModuleScreenPolicy Policy() => new("wizard101",
    [
        new("pet-dance.next", "verified-control-click", null, 40, "pet-dance.next"),
        new("pet-dance.finish-without-snack", "verified-control-click", null, 40, "pet-dance.finish-without-snack"),
        new("game.open-menu", "keyboard-pulse", "ESCAPE", 40, "outside-menu-command"),
        new("wizard101.native-idle", "verified-control-click", null, 40, "game-menu.idle")
    ],
    [
        new("pet-dance.next", "GameResult", "SnackOffer"),
        new("pet-dance.finish-without-snack", "SnackOffer", "OutsideReady"),
        new("game.open-menu", "OutsideReady", "MenuObserved"),
        new("wizard101.native-idle", "MenuObserved", "NativeIdle")
    ], ["wizard101.idle-context", "wizard101.postgame-context"],
    [new("OutsideCandidate", "OutsideReady", "outside-menu-command", ["pet-dance.finish-without-snack"])]);

    public static OperationModuleBundle NativeIdle() => W101SharedOperations.IdleCycle("screen-review", new(Mode: HomeIdleMode.NativeIdle));

    public static OperationModuleBundle Postgame()
    {
        var policy = Policy();
        var menus = OperationModules.Spec("w101-postgame-menus", "menu", "wizard101", new MenuConfiguration(
        [
            new("next", "GameResult", "SnackOffer", policy.Actions[0]),
            new("finish", "SnackOffer", "OutsideReady", policy.Actions[1])
        ], TimeoutMs: 30000, TransitionTimeoutMs: 10000), "wizard101.postgame-context");
        var idle = NativeIdle().Modules[0];
        return new(1, "w101-postgame-screen", "wizard101", "wizard101.postgame-screen", 45000, 4, [menus, idle]);
    }

    public static PetDanceLifecycleAction Resolve(ModuleAction action) => action switch
    {
        var item when item == Policy().Actions[0] => PetDanceLifecycleAction.Next,
        var item when item == Policy().Actions[1] => PetDanceLifecycleAction.FinishWithoutSnack,
        var item when item == Policy().Actions[2] => PetDanceLifecycleAction.OpenMenu,
        var item when item == Policy().Actions[3] => PetDanceLifecycleAction.SelectIdle,
        _ => throw new InvalidDataException("No existing W101 UI dispatcher is registered for this exact action binding.")
    };
}
