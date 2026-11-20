# GEM-MODAL-BOUNDARIES-115: Modal and Navigation Interruption Boundary Audit (Corrected Baseline)

**Task ID**: `GEM-MODAL-BOUNDARIES-115`  
**Claim Owner**: `gemini` (Active in `docs/coordination/claims.json`)  
**Status**: Read-only static review and architectural gap audit; offline analysis only (CORRECTED BASELINE).  
**Deliverable Path**: `docs/coordination/results/GEM-MODAL-BOUNDARIES-115.md`  
**Revision Directives**: Revised per `LIA-0073` and `LIA-0121` (`GEM-HONESTY-AUDIT-01`):
1. Correct line reference in `tools/w101_ui_tree.py`: `classify(tree)` is defined at line 1739.
2. Correct reason string in `PathFollower.cs:186`: `"forward-progress-stalled"`.
3. Clarify fact flow: `W101UiTreeOperation` extracts UI state dictionaries directly; `JobFactCatalog` defines trusted fact vocabulary independently.

---

## 1. Executive Summary & Boundaries

This static audit examines how modal windows, UI menus, dialogue states, and popups intersect with action admission, path following, and recovery across Maelstrom's Wizard101 integration.

### Strict Boundary Declarations
- **Zero Production or Test Edits**: No modifications to files in `ProjectMaelstrom/`, `tools/`, or test suites.
- **Zero Builds or Test Runs**: No execution of native build tools, cargo, or Python test suites.
- **Zero Live Interactions / Process Injection**: No client memory reads, hooks, key dispatches, or window activation.
- **No Invented Mechanics**: No invented UI names, timers, auto-dismiss policies, or duplicate schedulers. All analyses are rooted in existing source code.
- **Coordinator Clarifications Preserved**:
  - Source-derived zone names remain distinct from live-confirmed zone facts.
  - Portal loading is verified via explicit loading telemetry rather than assuming a visible black screen.
  - Native Idle is outer-routine cleanup, not mandatory internal portal arrival.
  - Heading relative to an NPC is not a proven interaction prompt prerequisite.

---

## 2. Inventory of Existing Modal & Mode Facts in the Repository

### 2.1 Trusted Vocabulary (`JobFactCatalog.cs:13–39`)
The centralized catalog defines trusted facts with strict types and producers:
- `interactionPrompt` (boolean, producer `w101.ui-tree`, freshness 2s): Stable interaction prompt (`NPCRangeWin`).
- `dialogue` (boolean, producer `w101.ui-tree`, freshness 2s): Stable spoken dialogue (`wndDialogMain`, `NPCServicesWin`).
- `menu` (boolean, producer `w101.ui-tree`, freshness 2s): Stable menu (`shopGUI`, `PayToPlay`, `DeckConfiguration`, `SettingsPage`, Spiral Scene).
- `nativeIdle` (boolean, producer `w101.ui-tree`, freshness 2s): Stable native Idle (`idleMessage` / `"YOU ARE IDLE"`).
- `area` (string, producer `unavailable`, false): Current area (marked unavailable pending live qualification).
- `quest.objective`, `quest.action`, `quest.area`, `quest.target`: Parsed intent facts from quest text.

### 2.2 UI Tree Classification (`tools/w101_ui_tree.py:1739–1785`)
The Python observation helper extracts node trees from the client's root window and classifies them in `classify(tree)`:
```python
def classify(tree):
    values = dict.fromkeys(STATES) # ("interactionPrompt", "dialogue", "menu", "nativeIdle")
    if not tree.get("complete"):
        return values
    visible = [node for node in tree["nodes"] if node["visible"]]
    worlds = {node["index"] for node in visible if node["name"] == "WorldView"}
    if not worlds:
        return values
    children = [node for node in visible if node["parent"] in worlds]
    names = {node["name"] for node in children}
    values["interactionPrompt"] = "NPCRangeWin" in names
    values["dialogue"] = bool(names & {"wndDialogMain", "NPCServicesWin"})
    settings = {
        node["index"] for node in children if node["name"] == "DeckConfiguration"
    }
    values["menu"] = bool(names & {"shopGUI", "PayToPlay", "NPCServicesWin"}) or any(
        node["name"] in {"SettingPage", "SettingsPage"} and node["parent"] in settings
        for node in visible
    )
    # Spiral Map can remain open underneath native Idle; Escape is not Exit.
    visible_names = {node["name"] for node in visible}
    values["menu"] |= {
        "spiralScene",
        "teleportButton",
        "cancelButton",
        "optionWindow",
    } <= visible_names
    # ... nativeIdle evaluation based on Root -> idleMessage -> idleText == "YOU ARE IDLE"
```
Structural leaves cataloged in `tools/w101_ui_tree.py:47–58` include:
`CompassWindow`, `windowHUD`, `BallonLayer`, `WizardPatchingWindow`, `SettingPage`, `SettingsPage`, `wndDialogMain`, `NPCServicesWin`, `shopGUI`, `PayToPlay`.
*(Note: Hypothetical names like `TutorialTipWin` or `FriendRequestWin` are not found in the codebase and must not be treated as source facts).*

---

## 3. Mapping Modal/Mode Facts to Action Admission & Recovery Paths

### 3.1 Module Action Admission & the Sigil Interaction Protocol
1. **Module Key Dispatch (`W101ModuleInput.cs:11–25`)**:
   - Dispatches single addressed pulses (`C`, `HOME`, `END`, `X`, `ESCAPE`) via `SendMessageTimeoutW`.
   - Admitted only if `Owned()` returns true (`WritesGame`, `!DelegatedInput`, `GameInputLease.Current`, `ready()`).
2. **Sigil Interaction Module Protocol (`W101ModuleBindings.cs:25–85`, `JobDeckRoutines.cs:92–95`)**:
   - `JobDeckRoutines.cs:92` binds `"w101.interact"` with `"prompt"` option via `BoundModule`.
   - `W101ModuleBindings.Bundle("sigil")` creates a `SigilConfiguration`:
     ```csharp
     new SigilConfiguration("InteractionReady", "InteractionMenu",
         new("game.interact", "keyboard-pulse", "X", 40, "w101.interaction"), 10000)
     ```
   - In `W101ModuleBindings.Map`:
     - When `interactionPrompt == true` and `InteractionVerified == true`, the scene evaluates to `"InteractionReady"`, advertising control `"w101.interaction"`.
     - After dispatching 'X', completion is **not** inferred from transport alone: the module runner requires transitioning from `"InteractionReady"` to `"InteractionMenu"`.
     - `"InteractionMenu"` requires `menu == true` and `context.InteractionMenuVerified` (e.g. `visual.Scene == "SigilMenu.dance"`).
     - If the interaction opens a dialogue window (`wndDialogMain`) rather than a sigil menu, `State("dialogue", false)` fails in `Map`, leaving `scene = "Unknown"`. The sigil module will not see `"InteractionMenu"` and will time out or fail.
     - **Crucial Invariant**: An interaction that opens dialogue may be entirely valid and intentional (e.g. talking to an NPC). It must **not** be automatically advanced, closed with Escape, or transferred to questing without configured user intent.

### 3.2 Recovery & Focus Scope Boundaries
1. **OS Control Scope (`W101ControlScope.cs:9–29, 118–144`)**:
   - Scoped strictly to OS-level window focus, client geometry, and physical keyboard activity (`DesktopActivityMonitor`).
   - `WaitForFocusAsync` pauses the focus budget (`_context.FocusBudget?.Pause()`) and waits for `state.Ready` (foreground handle match, geometry match, lack of held keys).
   - **Boundary**: `W101ControlScope` is an OS-level input guard, **not** a game-state classifier. Game fact revalidation belongs at routine stage boundaries, not inside the low-level OS scope.
2. **Routine Stage Boundaries (`W101NavigationVisit.cs:118–145`)**:
   - Chained stages execute under `StageAsync`.
   - If focus was lost, `runtime.WaitForFocus` waits for OS readiness.
   - However, the routine stage boundary currently lacks a re-verification checkpoint for stage-specific game preconditions (e.g. asserting `OutsideReady` before dispatching a locomotion pulse).

---

## 4. Three Proven Gaps in Modal Boundary Handling

### Gap 1: `PathFollower` Locomotion Blindness to In-Game Modal / Blocking Modes
- **Exact Call Chain & Evidence**:
  - `W101NavigationVisit.cs:120` (`RunOwnedAsync` / `MeasureAsync`) $\rightarrow$ `PathFollower.Observe(sample, nowMs)` (`PathFollower.cs:87–140`).
  - `PathObservation` (`PathFollower.cs:9–11`) defines:
    ```csharp
    internal sealed record PathObservation(double X, double Y, double Z, double Heading, double TimeMs,
        string Zone, string Context, bool Fresh = true, bool Loading = false, bool Focused = true, bool Stopped = false);
    ```
  - `PathObservation` contains no field for in-game modal states (`dialogue`, `menu`, or `combat`).
  - When an in-game event locks avatar locomotion during a navigation routine (e.g., character steps into an NPC proximity trigger that opens `wndDialogMain`, or is pulled into a combat ring `PlanningPhase`):
    1. The game client locks avatar movement.
    2. Because `sample.Focused == true` (OS window remains focused) and `sample.Loading == false`, `PathFollower` continues emitting active movement intents (`PathMotion.State == "active"`).
    3. The avatar's spatial coordinates $(X, Y, Z)$ remain stationary, causing `_stalledMs += delta` to accumulate.
    4. When `_stalledMs > _options.StuckMs` (1500ms), `PathFollower` invalidates the route with `"forward-progress-stalled"` (`PathFollower.cs:184–186`) or `"route-wall-budget-exhausted"`.
  - **Impact**: In-game modal pauses are misclassified as physical geometric collisions or stuck conditions, triggering erroneous route invalidation while input leases continue dispatching against blocked controls.
- **Smallest Reusable Repair**:
  Add an optional, action-specific blocking property to `PathObservation` (e.g. `bool LocomotionBlocked = false`). When asserted by the navigation observer (e.g., when `dialogue == true`, `menu == true`, or combat context is active), `PathFollower.Observe` transitions to `Hold("paused", "locomotion-blocked")` without accumulating `_stalledMs`, mirroring the existing `Focused == false` handling until the modal is resolved or the outer routine budget expires.
- **Proposed Pure Unit Test**:
  `PathFollowerTests.Observe_WhenLocomotionBlocked_HoldsWithoutAccumulatingStuckTime`

---

### Gap 2: Incomplete Modal Window Classification in `tools/w101_ui_tree.py`
- **Exact Call Chain & Evidence**:
  - `tools/w101_ui_tree.py:1739–1785` (`classify(tree)`) $\rightarrow$ `ProjectMaelstrom/ProjectMaelstrom/Utilities/W101UiTreeOperation.cs:133–145`.
  - In `tools/w101_ui_tree.py:1739–1785`, `classify` sets:
    - `values["interactionPrompt"] = "NPCRangeWin" in names`
    - `values["dialogue"] = bool(names & {"wndDialogMain", "NPCServicesWin"})`
    - `values["menu"] = bool(names & {"shopGUI", "PayToPlay", "NPCServicesWin"}) or any(...) or {...} <= visible_names`
  - Notice that `STRUCTURAL_LEAVES` (`tools/w101_ui_tree.py:47–58`) defines known top-level structural windows including `WizardPatchingWindow`.
  - However, `classify` completely ignores `WizardPatchingWindow` and other top-level structural leaves. If `WizardPatchingWindow` or an unclassified top-level dialog appears under `WorldView` or `Root`:
    1. `interactionPrompt`, `dialogue`, and `menu` all evaluate to `False`.
    2. Upstream mapping in `W101ModuleBindings.Map` evaluates `State("dialogue", false) && State("menu", false) && State("nativeIdle", false)`.
    3. The system falsely classifies the scene as `OutsideReady`, concluding the character is free to navigate, open menus, or interact, even though a blocking modal window is actively capturing input.
- **Smallest Reusable Repair**:
  In `tools/w101_ui_tree.py:1739–1785`, check for known structural modal leaves (such as `WizardPatchingWindow`) under `Root` or `WorldView`, setting `values["menu"] = True` (or setting an explicit `modal` fact) so that downstream consumers do not infer `OutsideReady` when the viewport is obstructed.
- **Proposed Pure Unit Test**:
  `test_classify_structural_leaf_patching_window_sets_menu` in `tools/tests/test_w101_ui_tree.py`

---

### Gap 3: Missing State-Invariant Revalidation at Routine Checkpoints Across Focus Loss
- **Exact Call Chain & Evidence**:
  - `W101ControlScope.WaitForFocusAsync` (`W101ControlScope.cs:118–144`) $\rightarrow$ `W101NavigationVisit.StageAsync` (`W101NavigationVisit.cs:122–126`).
  - `W101ControlScope` is strictly an OS-level focus/input monitor. When focus is lost, `WaitForFocusAsync` pauses the execution budget and waits for `state.Ready` (OS foreground handle, matching client geometry, lack of physical keyboard input).
  - When focus returns, `WaitForFocusAsync` returns `true`.
  - In `W101NavigationVisit.StageAsync`:
    ```csharp
    var admitted = Ready();
    if (!admitted && runtime.WaitForFocus is not null && parent.Input is { CanDispatch: true } &&
        ReferenceEquals(parent.Input, GameInputLease.Current))
        admitted = await runtime.WaitForFocus(deadline.Token).ConfigureAwait(false) && Ready();
    ```
  - Once `WaitForFocus` returns `true` and `Ready()` is true, the stage immediately proceeds to `execute(child, deadline.Token)`.
  - However, during the unfocused interval, in-game world events continue advancing on the server:
    - An approaching mob may have triggered combat (`PlanningPhase`).
    - An NPC may have initiated dialogue (`wndDialogMain`).
    - The character may have timed out into native Idle (`idleMessage`).
  - Neither `StageAsync` nor the dispatch runner performs an intervening observation read or revalidates stage-specific preconditions (e.g. verifying `OutsideReady` for a navigation stage, or `InteractionMenu` for a sigil stage) before resuming execution.
  - **Impact**: The stage resumes blind key dispatches into a mutated game state immediately upon regaining OS focus, without verifying that the client state still matches the stage's operational context.
- **Smallest Reusable Repair**:
  At the routine stage boundary (`W101NavigationVisit.StageAsync`), after `WaitForFocus` succeeds, require an explicit stage precondition check (via a caller-supplied predicate or fresh observation read) before executing the child stage. If the required in-game state is no longer satisfied, the stage cleanly aborts with `"stage-precondition-invalid-after-focus-loss"` rather than executing blind dispatches into an incompatible game state.
- **Proposed Pure Unit Test**:
  `W101NavigationVisitTests.StageAsync_WhenGameModeMutatesDuringFocusLoss_AbortsStageCleanly` in `ProjectMaelstrom.Tests/W101NavigationVisitTests.cs`

---

## 5. Smallest Reusable Repairs & Proposed Pure Test Matrix

To address these three proven gaps without expanding scope, creating new schedulers, or adding live controller dependencies:

| Gap # | Proposed Test Name | Target File | Scope & Invariant Verified |
| :--- | :--- | :--- | :--- |
| **Gap 1** | `Observe_WhenLocomotionBlocked_HoldsWithoutAccumulatingStuckTime` | `ProjectMaelstrom.Tests/PathFollowerTests.cs` | Asserts that `PathFollower.Observe` holds in `"paused"` without incrementing `_stalledMs` when `PathObservation.LocomotionBlocked == true`. |
| **Gap 2** | `test_classify_structural_leaf_patching_window_sets_menu` | `tools/tests/test_w101_ui_tree.py` | Asserts that `classify(tree)` sets `values["menu"] = True` when tree contains `WizardPatchingWindow` under `Root` or `WorldView`. |
| **Gap 3** | `StageAsync_WhenGameModeMutatesDuringFocusLoss_AbortsStageCleanly` | `ProjectMaelstrom.Tests/W101NavigationVisitTests.cs` | Asserts that `W101NavigationVisit.StageAsync` revalidates stage preconditions after `WaitForFocus` and cleanly aborts if the required state is absent. |

> [!NOTE]
> All proposed unit tests are strictly **[PROPOSED ONLY - NOT IMPLEMENTED]**. In accordance with the Codex Working Agreement and directive `LIA-0073`, no test files or production source files have been modified.

---

## 6. Handback & Next Steps

This revised report incorporates all corrections from `LIA-0073` and `LIA-0121` (`GEM-HONESTY-AUDIT-01`):
- Corrected line references to line 1739 in `tools/w101_ui_tree.py`.
- Corrected reason string to `"forward-progress-stalled"` (`PathFollower.cs:186`).
- Clarified that `W101UiTreeOperation` extracts dictionary facts directly without calling `JobFactCatalog`.
- Clarified `w101.interact` sigil protocol and post-condition requirements.
- Replaced global dialogue gating with action-specific mode contracts.
- Refined focus return revalidation to routine stage checkpoints rather than OS scope.
- Scoped audit to exactly three proven gaps with full call-chain evidence.

- **Delivered**: `docs/coordination/results/GEM-MODAL-BOUNDARIES-115.md`.
- **Next Step**: Append `GEM-0079` / `GEM-0108` (`Kind: READY_FOR_REVIEW`) to `docs/coordination/gemini/FROM_GEMINI.md` and stand down awaiting root coordinator review.
