namespace ProjectMaelstrom.Utilities;

/// <summary>Trusted adapter evidence, never deserialized as permission from a pack.
/// UI-tree booleans do not establish these semantic identities or click geometry.</summary>
internal sealed record W101ModuleContext(bool OutsideReady = false, bool InteractionVerified = false,
    bool GameMenuVerified = false, bool IdleControlVerified = false, bool InteractionMenuVerified = false);

/// <summary>Pure W101 vocabulary/observation transform. No capture, hook or input.</summary>
internal static class W101ModuleBindings
{
    internal static W101ModuleContext FromVisual(ModuleVisualRecognition visual, RoutineBinding binding,
        Size geometry, double nowMs, bool outsideVerified = false, bool interactionVerified = false)
    {
        ModuleRecognition.Validate(visual);
        if (visual.Frame.TextOrigin != "local-windows-ocr" || visual.Frame.Binding != binding || visual.Frame.Geometry != geometry || !double.IsFinite(nowMs) ||
            nowMs < visual.Frame.CapturedMs || nowMs - visual.Frame.CapturedMs > ModuleRecognition.MaximumAgeMs)
            return new();
        return new(outsideVerified, interactionVerified, visual.Scene == "MenuObserved",
            visual.Scene == "MenuObserved" && visual.Controls.Any(c => c.ControlId == "game-menu.idle"),
            visual.Scene == "SigilMenu.dance");
    }

    internal static OperationModuleBundle Bundle(string mode)
    {
        if (mode == "native-idle") return W101ModulePrograms.NativeIdle();
        var spec = mode switch
        {
            "sigil" => OperationModules.Spec("w101-interaction", "sigil", "wizard101",
                new SigilConfiguration("InteractionReady", "InteractionMenu",
                    new("game.interact", "keyboard-pulse", "X", 40, "w101.interaction"), 10000), "wizard101.module-context"),
            "menu" => OperationModules.Spec("w101-open-menu", "menu", "wizard101",
                new MenuConfiguration([new("open", "OutsideReady", "MenuObserved",
                    new("game.open-menu", "keyboard-pulse", "ESCAPE", 40, "outside-menu-command"))],
                    TimeoutMs: 10000, TransitionTimeoutMs: 5000), "wizard101.module-context"),
            _ => throw new InvalidDataException("Choose sigil, menu or native-idle.")
        };
        var bundle = new OperationModuleBundle(1, "w101-" + mode + "-binding", "wizard101",
            "wizard101.module." + mode, 10000, 1, [spec]);
        ModuleCatalog.Compile(bundle);
        return bundle;
    }

    internal static ModuleObservation Map(WizwalkerTarget target, W101UiTreeResult reading,
        RoutineObservation authority, W101ModuleContext context, long nowUtcTicks)
    {
        bool State(string name, bool expected) => reading.States?.GetValueOrDefault(name) == expected &&
            reading.StableStates?.GetValueOrDefault(name) == expected;
        var ready = reading.SchemaVersion == 1 && reading.Target == target &&
            authority.Binding.TargetId == "pid-" + target.ProcessId + "-start-" + target.StartedUtcTicks &&
            reading.Operation == W101UiTreeOperation.Version && reading.SourceSha256 == WizwalkerOperation.SourceSha256 &&
            reading.Status == "observed" && reading.CleanupConfirmed && reading.HookRestored && reading.TreeSamples >= 2 &&
            reading.ObservedUtcTicks > 0 && reading.StatesObservedUtcTicks > 0 &&
            reading.StatesObservedUtcTicks <= reading.ObservedUtcTicks && reading.ObservedUtcTicks <= nowUtcTicks &&
            nowUtcTicks - reading.StatesObservedUtcTicks <= TimeSpan.FromMilliseconds(2000).Ticks;
        var scene = "Unknown";
        var controls = new List<string>();
        if (ready && reading.Loading == true) scene = "Loading";
        else if (ready && reading.Loading == false && State("dialogue", false))
        {
            if (State("nativeIdle", true) && State("menu", false)) scene = "NativeIdle";
            else if (State("nativeIdle", false) && State("menu", true))
            {
                if (context.InteractionMenuVerified && !context.GameMenuVerified) scene = "InteractionMenu";
                else if (context.GameMenuVerified && !context.InteractionMenuVerified)
                {
                    scene = "MenuObserved";
                    if (context.IdleControlVerified) controls.Add("game-menu.idle");
                }
            }
            else if (State("nativeIdle", false) && State("menu", false) && context.OutsideReady)
            {
                scene = "OutsideReady"; controls.Add("outside-menu-command");
                if (State("interactionPrompt", true) && context.InteractionVerified)
                { scene = "InteractionReady"; controls.Clear(); controls.Add("w101.interaction"); }
            }
        }
        var known = scene is not ("Unknown" or "Loading");
        return new(authority with
        {
            Scene = scene,
            TimeMs = ready ? authority.TimeMs - TimeSpan.FromTicks(nowUtcTicks - reading.StatesObservedUtcTicks).TotalMilliseconds : authority.TimeMs,
            Fresh = authority.Fresh && ready,
            ContextVerified = authority.ContextVerified && known,
            ControlVerified = authority.ControlVerified && controls.Count > 0
        }, known ? ["wizard101.module-context", "wizard101.idle-context"] : [], controls.ToArray());
    }
}
