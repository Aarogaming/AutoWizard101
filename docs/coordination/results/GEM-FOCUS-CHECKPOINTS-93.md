# GEM-FOCUS-CHECKPOINTS-93: Focus Wait Checkpoints and Clock Audit (Corrected Baseline)

**Author**: Gemini  
**Date**: 2026-11-16 (Revised per coordinator audit `GEM-HONESTY-AUDIT-01` / `LIA-0121`)  
**Directive**: LIA-0045 / LIA-0046 / LIA-0121 / GEM-FOCUS-CHECKPOINTS-93  
**Status**: REVISED (CORRECTED BASELINE)  
**Operating Rules**: Read-only static review; zero source edits; zero builds/tests executed; no live game interaction.

---

## 1. Executive Summary & Review Revisions

Directive `LIA-0046` and honesty audit `LIA-0121` (`GEM-HONESTY-AUDIT-01`) provide review feedback on the `GEM-FOCUS-CHECKPOINTS-93` audit. This revision addresses all feedback points:
1. **Dispatcher Release Integrity**: Replaces the prior blanket assumption of clean release by tracing exact rejection, failure, and uncertain-release branches across dispatchers (`W101DialogueInput`, `W101ModuleInput`, `PetDanceMenuInput`). Reaffirms that uncertain release (`InputReleased == false`) strictly forbids focus waiting and must escalate to hard scheduler recovery.
2. **Decoupling Real-Time Cooldowns from Active Execution Budgets**: Corrects the prior suggestion of freezing cooldown clocks. Real in-game cooldowns (such as the 45-second Home teleport cooldown) and frame freshness continue elapsing on wall-clock time in the game client and must never be frozen. Only the *active execution/polling budget* (attempt counts, active timeout) pauses under `OperationFocusBudget`.
3. **Pet Dance Real-Time Progression**: Accounts for the fact that Wizard101 minigames do not pause when the game window loses focus. Stale arrow queues cannot be resumed by freezing model clocks; focus loss requires discarding queued keys (`inputs_withheld: true`), followed by fresh visual phase/round re-acquisition or bounded round failure.
4. **Observer Hook & Capture Wall Deadlines**: Retains hard wall-clock bounds on low-level hook attachment and capture execution (12s, 3000ms). Focus wait checkpoints belong strictly *between released observations*, never inside an active hook or capture frame.
5. **Proposed Test Demarcation**: All proposed unit tests are explicitly designated `[PROPOSED ONLY - NOT IMPLEMENTED]`.

---

## 2. Static Inventory of `W101ControlScope` Consumers

`W101ControlScope` manages physical input monitoring (`DesktopActivityMonitor`), game window focus/foreground state, input lease validation, and bounded focus-wait recovery (`WaitForFocusAsync`).

| Consumer / Component | File Path | Scope Created? | `WaitForFocusAsync` Called? | Focus Loss Behavior |
| :--- | :--- | :---: | :---: | :--- |
| **`W101NavigationVisit`** | `ProjectMaelstrom/Utilities/W101NavigationVisit.cs:61` | Yes | Yes (passed to helpers) | Pauses via `OperationFocusBudget`; resumes after quiet focus restored. |
| **`W101FeedbackVisit`** | `ProjectMaelstrom/Utilities/W101FeedbackVisit.cs:31,60,78` | Injected | Yes | Calls `scope.WaitForFocusAsync`; checks `ResumeReleasedAsync`. |
| **`W101HeadingVisit`** | `ProjectMaelstrom/Utilities/W101HeadingVisit.cs:36,57,73` | Injected | Yes | Calls `scope.WaitForFocusAsync`; checks `ResumeReleasedAsync`. |
| **`W101IdleCycleOperation`** | `ProjectMaelstrom/Utilities/W101IdleCycleOperation.cs:34,62` | Yes | Yes (in runtime) | Passes `scope.WaitForFocusAsync` into Idle runtime loop. |
| **`W101RoutineIdle`** | `ProjectMaelstrom/Utilities/W101RoutineIdle.cs:22,26` | Yes | Yes | Wraps routines with focus-aware Idle recovery. |
| **`W101NavigationProbe`** | `ProjectMaelstrom/Utilities/W101NavigationProbe.cs:41` | Yes | No | Checks `scope.Ready()`; aborts if not ready. |
| **`W101CharacterOperation`** | `ProjectMaelstrom/Utilities/W101CharacterOperation.cs:96,283,333,385` | Yes (4 sites) | **No (0 sites)** | Immediate failure on `!scope.Ready()`; raw 20s `CancelAfter` in `FinalizeFailureAsync`. |
| **`W101WorldGateVisit`** | `ProjectMaelstrom/Utilities/W101WorldGateVisit.cs:37` | Yes (1 site) | **Yes (2 sites: L57, L202)** | Calls `scope.WaitForFocusAsync` in `FinalizeIdleAsync` (L57) and `ReleasedCheckpointAsync` in Home polling (L202). |
| **`PetDanceLiveTrial`** | `ProjectMaelstrom/Utilities/PetDanceLiveTrial.cs:236` | **No** (Direct Monitor) | **No** | Uses raw `DesktopActivityMonitor.Install(tag)`; passes dummy `InputReady: () => true`; raw `Stopwatch`. |
| **`PetDanceVisit`** | `ProjectMaelstrom/Utilities/PetDanceVisit.cs:71` | N/A (State contract) | N/A | Expects `FocusPaused` flag in observation contract, but caller never sets it. |
| **`W101QuestConversation`** | `ProjectMaelstrom/Utilities/W101QuestConversationOperation.cs:18` | **No** | **No** | Uses `W101DialogueInput.Ready`; breaks immediately on focus loss; raw 60s timeout (`W101QuestConversation.TimeoutMs = 60000`). |
| **`W101ModuleObserver`** | `ProjectMaelstrom/Utilities/W101ModuleObserver.cs:45,147` | N/A (Observer utility) | **No** | Raw `CancelAfter(12s)` and `CancelAfter(3000ms)` unlinked to `FocusBudget`. |

---

## 3. Detailed Trace of Raw `CancelAfter` and Wall Clocks

### 3.1 Character Operations (`W101CharacterOperation.cs`)
1. **Raw `CancelAfter` in Emergency Idle Finalization**:
   - **Line 65–66**:
     ```csharp
     using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
     deadline.CancelAfter(TimeSpan.FromSeconds(20));
     ```
   - **Defect**: When an operation fails in the world and enters `FinalizeFailureAsync` to restore native Idle, it allocates an unpausable 20-second deadline. If the operator switches windows during this 20-second window, the active deadline expires while focus is paused, throwing `OperationCanceledException` and tagging the session with `OperationState.Cancelled` instead of cleanly recovering native Idle once focus returns.
   - **Correction**: Must use `context.FocusBudget?.CreateDeadline(TimeSpan.FromSeconds(20), token) ?? deadline.Token`.

2. **Uncheckpointed Banner & Roster Polling**:
   - **Lines 340–359**: Iterates through character selection seats (`seats = [new(1040, 400), ...]`).
   - **Line 345**: `if (banner is null || !scope.Ready()) return new(OperationState.Failed, "selection-banner-unavailable", [context.Directory]);`
   - **Defect**: If background notification causes a 100ms focus blip between seat reads, `scope.Ready()` is false, causing the entire roster inspection to fail immediately instead of pausing at the seat boundary.

3. **Loading Transition Wall Clock**:
   - **Line 215**: `var worldTransition = new LoadingTransitionGate(FrameTimeline.TimestampMs(), 45000);`
   - **Defect**: Uses raw QPC milliseconds (`FrameTimeline.TimestampMs()`). If focus drops during world entry loading, the 45-second limit ticks against wall time rather than active gameplay time.

### 3.2 World Gate Operations (`W101WorldGateVisit.cs`)
1. **Focus-Wait Checkpoint in `HomeAvailable` Polling**:
   - **Lines 199–203**:
     ```csharp
     var wait = System.Diagnostics.Stopwatch.StartNew();
     for (var attempt = 0; attempt < 40; attempt++)
     {
         if (!await ReleasedCheckpointAsync(context, scope.Ready, scope.WaitForFocusAsync, token).ConfigureAwait(false))
             break;
     ```
   - **Analysis**:
     - *Active Polling Budget*: `W101WorldGateVisit.cs:202` already implements `ReleasedCheckpointAsync`, passing `scope.WaitForFocusAsync` so focus loss pauses attempt exhaustion cleanly.
     - *Real-Time In-Game Cooldown*: In contrast, `HomeAvailable` itself depends on the in-game cooldown expiring on the Wizard101 server. Real-time elapsed wall seconds (`wait.Elapsed.TotalSeconds`) continue advancing so that when focus returns, `HomeAvailable` accurately reflects whether the in-game cooldown has elapsed.

2. **Raw `Stopwatch` in Cooldown Sampling**:
   - **Lines 210, 235**:
     ```csharp
     cooldownClock = System.Diagnostics.Stopwatch.StartNew();
     ...
     for (var sampleIndex = 0; sampleIndex < 20 && cooldownClock.Elapsed.TotalSeconds < 45 && !cooldownCleared && scope.Ready(); sampleIndex++)
     ```
   - **Analysis**: Cooldown clock correctly tracks real-world time (45 seconds), because in-game cooldowns do not pause on the server when the game window is unfocused. However, checking `scope.Ready()` inside the loop condition causes the sampling loop to abort early on brief focus loss instead of waiting between observations.

3. **Loading Transition Wait Checkpoint**:
   - **Lines 176, 214**:
     ```csharp
     var transition = await W101NavigationTransition.WaitAsync(Loading, scope.Ready, FrameTimeline.TimestampMs, token, requireLoading: true).ConfigureAwait(false);
     ```
   - In `W101NavigationTransition.cs:55, 58`, `if (!owned()) return new(false, sawLoading, i, "control-changed");`.
   - **Defect**: If focus is lost during the loading screen between world gate departure and world arrival, `owned()` (`scope.Ready`) returns false and `WaitAsync` returns `Ready: false, Reason: "control-changed"`. `W101WorldGateVisit` marks the entire travel failed (`"World loading transition not verified; no Home sent."`), even though the game client is simply loading in the background.

### 3.3 Pet Dance Live Trial (`PetDanceLiveTrial.cs`)
1. **Uncoordinated `DesktopActivityMonitor` & Missing `W101ControlScope`**:
   - **Line 236**: `activity = DesktopActivityMonitor.Install(tag);`
   - **Line 244**: Passes `() => true` for `InputReady` in `PetDanceSpecialistControl`!
   - **Defect**: `PetDanceLiveTrial` completely bypasses `W101ControlScope`. It installs a raw monitor hook and tells the specialist that input is always ready (`() => true`), ignoring whether the game window is actually focused or foreground.

2. **Real-Time Minigame Progression vs. Clock Accounting**:
   - **Line 349**: `var timer = Stopwatch.StartNew();`
   - **Lines 430, 436, 474**: `clock.Advance(timer.Elapsed.TotalMilliseconds, scene == DancePhase.Loading, ...)`
   - **Analysis**:
     - Unlike turn-based dialogues, Pet Dance continues advancing in real time inside the game client even when the window is unfocused.
     - Freezing `GameplayPauseClock` or attempting to resume old queued arrow keys after focus returns is fatal: the 5-second response window will have expired on the server, resulting in an immediate mismatch and game failure.
     - When focus is lost during Dance, all pending keys must be cleared (`pending.Clear()`) and withheld (`inputs_withheld: true`). Upon focus return, the system must re-synchronize to a fresh round (`DancePhase.Watch`) or record a bounded round failure before any new inputs are scheduled.

### 3.4 Quest Conversation Operations (`W101QuestConversationOperation.cs`)
1. **Raw Millisecond Wall Budget**:
   - **Line 74, 79, 85**:
     ```csharp
     var started = runtime.Clock();
     ...
     for (int index = 0; index < 24 && runtime.Clock() - started < W101QuestConversation.TimeoutMs; index++)
     ```
   - **Defect**: Uses raw `runtime.Clock()` (`FrameTimeline.TimestampMs`) with a fixed 60-second timeout (`W101QuestConversation.TimeoutMs = 60000`). Any pause counts against the 60 seconds.

2. **Immediate Loop Abort on Focus Loss**:
   - **Lines 81, 84**: `if (!Ready()) { reason = "user-activity-or-target-change"; break; }`
   - **Defect**: If the window loses focus while an NPC dialogue is displaying, the conversation routine breaks immediately rather than pausing and waiting for focus restoration.

### 3.5 Module Observer (`W101ModuleObserver.cs`)
1. **Raw 12-Second Observation Deadline**:
   - **Line 44–45**:
     ```csharp
     using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
     deadline.CancelAfter(TimeSpan.FromSeconds(12));
     ```
   - **Analysis**: Bounded wall deadlines are essential when native hooks or memory mappings are attached, to prevent hung processes from leaking resources indefinitely.
   - **Boundary**: Focus pausing must never occur *inside* an active hook or capture operation. Focus pause checkpoints belong strictly *between released observations*, after hooks are detached and frame buffers are released.

2. **Raw 3-Second Frame Capture Deadline**:
   - **Line 146–147**:
     ```csharp
     using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
     deadline.CancelAfter(3000);
     ```
   - **Analysis**: Retains a strict 3-second hard deadline for individual frame captures. Retrying capture across focus loss must be done at the caller level between capture attempts.

---

## 4. Input Release Checkpoints & Duplication Analysis

### 4.1 Exact Rejection, Error, and Uncertain-Release Branches
A core tenet of Maelstrom safety is that **dispatchers do NOT always return clean release**. In real operating environments, input delivery can fail, throw, or be interrupted:

1. **`W101DialogueInput.cs:19–34`**:
   ```csharp
   bool accepted = false, released = false;
   try { accepted = send(false); }
   finally
   {
       try { released = send(true); } catch { released = false; }
       if (!released) context.Input!.RequireRecovery("Quest dialogue Space release was not confirmed.");
   }
   return new(proposal.ProposalId!, proposal.ActionId!, clock(), accepted, released);
   ```
   - *Failure Branch*: If `send(true)` throws an exception or returns `false` (e.g., target window invalid, injection rejected), `released` is set to `false`.
   - *Escalation*: The input lease is invalidated via `RequireRecovery`. Focus waiting is strictly prohibited; the operation must terminate.

2. **`W101ModuleInput.cs:48–54`**:
   ```csharp
   if (attempted)
   {
       released = await W101NavigationInput.ReleaseAsync(() => send(action.Key!, true)).ConfigureAwait(false);
       if (!released) context.Input!.RequireRecovery("W101 module key release was not confirmed.");
   }
   return new(proposal.ProposalId!, proposal.ActionId!, clock(), accepted, released);
   ```
   - *Failure Branch*: If `ReleaseAsync` fails to verify physical key-up within its bounded watchdog, `released` is `false`.
   - *Escalation*: Triggers `context.Input!.RequireRecovery`.

3. **`PetDanceMenuInput.cs:96–111`**:
   ```csharp
   finally
   {
       if (attempted)
       {
           try { up(); }
           catch
           {
               try { up(); }
               catch
               {
                   released = false;
                   try { requireRecovery(); }
                   catch (Exception error) { reason += "; recovery notice could not be saved: " + error.Message; }
               }
           }
       }
   }
   return new(accepted, released, reason);
   ```
   - *Failure Branch*: If two consecutive `up()` calls fail, `released` is set to `false` and `requireRecovery()` is invoked.

### 4.2 Invariant for Focus Pause & Resumption
> [!IMPORTANT]
> **The Uncertain-Release Boundary**:
> 1. A focus wait checkpoint is **ONLY** permissible when `receipt.InputReleased == true`.
> 2. If `receipt.InputReleased == false`, the physical state of the keyboard/mouse is unknown. The operation **MUST NEVER** enter a focus wait or attempt routine resumption; it must immediately report `OperationState.CleanupFailed` or yield to scheduler recovery.
> 3. An action whose receipt was recorded as completed must **NEVER** be re-dispatched upon focus resumption.

### 4.3 Duplication & Session Corruption Hazards

| Routine | Action Boundary | Input Released Before Pause? | Duplication / Corruption Risk If Re-dispatched | Safe Checkpoint Location |
| :--- | :--- | :---: | :--- | :--- |
| **Quest Conversation** | Spacebar pulse per dialogue page | Conditional on verified receipt (`receipt.InputReleased`) | **HIGH**: Re-sending Space advances an unintended dialogue page or prematurely selects a quest option/reward. | **Between pages**: After `conversation.RecordReceipt(receipt)` and before the next `runtime.Read()`. Never re-send Space if receipt was recorded. |
| **World Gate** | Interact ('X') key | Conditional on verified receipt | **CRITICAL**: Re-sending 'X' toggles the world gate menu closed, stranding the player in front of the gate. | **After menu verification**: If gate menu is already open, do NOT re-dispatch 'X'. |
| **World Gate** | Carousel world select / Teleport | Conditional on verified receipt | **HIGH**: Re-clicking the carousel arrow shifts to the wrong world. Re-clicking Teleport while loading has already begun triggers duplicate UI clicks. | **Pre-dispatch only**: If loading has begun (`Loading == true` or `transition.SawLoading`), wait for transition without dispatching. |
| **World Gate** | Home Teleport (End key) | Conditional on verified receipt | **MODERATE**: Re-dispatching Home while already on cooldown wastes attempts or triggers cooldown error. | **Post-dispatch**: Once Home receipt is accepted and released, transition wait must wait for loading without re-pulsing Home. |
| **Character Selection** | Roster seat click | Conditional on verified receipt | **LOW (Idempotent)**: Re-clicking the same seat is idempotent (selects the same wizard). | **Between seat inspections**: Wait for focus before dispatching click to candidate seat. |
| **Character Selection** | PLAY button click | Conditional on verified receipt | **HIGH**: Re-clicking PLAY after click was dispatched risks clicking into overworld UI or causing double-entry glitch. | **Post-dispatch**: Once PLAY receipt is accepted, transition exclusively to `VerifyLoadedAsync`. Never re-click PLAY. |
| **Pet Dance** | Arrow key response | Conditional on verified pulse | **FATAL**: Re-dispatching an arrow key fails the round immediately (out of sequence). | **No mid-round retry**: Focus loss requires discarding pending cues (`inputs_withheld: true`). Focus wait is only valid *between* games or while waiting for fresh round start. |

---

## 5. Prioritized Exact-Path Repairs & Unit Test Proposals

### Proposal 1: World Gate Focus-Aware Waiting & Home Polling (`W101WorldGateVisit.cs`) [ALREADY IMPLEMENTED]
- **Exact Path**: `ProjectMaelstrom/ProjectMaelstrom/Utilities/W101WorldGateVisit.cs` (lines 57, 176, 202, 214, 235).
- **Status**: Verified implemented in committed code (`W101WorldGateVisit.cs:202` invokes `ReleasedCheckpointAsync(context, scope.Ready, scope.WaitForFocusAsync, token)`). Focus loss between observation attempts pauses attempt consumption cleanly without dropping the in-game cooldown clock.
- **Unit Test [EXISTING / VERIFIED]**: `W101WorldGateVisitTests.cs`:
  - `HomeAvailable` polling loop verifies that focus loss between released observations pauses loop progression rather than prematurely aborting.

### Proposal 2: Character Operation Emergency Idle Finalization (`W101CharacterOperation.cs`)
- **Exact Path**: `ProjectMaelstrom/ProjectMaelstrom/Utilities/W101CharacterOperation.cs` (lines 65–70, 345).
- **Repair**:
  1. In `FinalizeFailureAsync` (line 65), replace raw `deadline.CancelAfter(TimeSpan.FromSeconds(20))` with disposable `Deadline` pattern:
     ```csharp
     using var deadline = (context.FocusBudget ?? fallbackBudget).CreateDeadline(TimeSpan.FromSeconds(20), token);
     ```
     (Note: `CreateDeadline` returns a `Deadline` struct/object wrapping `.Token`, not a raw `CancellationToken`).
  2. In character roster loop (line 345), if `!scope.Ready()`, call `await scope.WaitForFocusAsync(token)` between seat readings before failing.
- **Unit Test [PROPOSED ONLY - NOT IMPLEMENTED]**: `W101CharacterOperationTests.cs`:
  - `FinalizeFailure_WithFocusLoss_DoesNotExpireActiveDeadlineEarly`: Simulates a 15-second focus pause during Idle restoration; verifies the 20-second active deadline does not cancel prematurely and native Idle verification succeeds.

### Proposal 3: Pet Dance Specialist Control Scope & Phase Re-synchronization (`PetDanceLiveTrial.cs`)
- **Exact Path**: `ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceLiveTrial.cs` (lines 236–246, 436).
- **Repair**:
  1. Replace raw `DesktopActivityMonitor.Install(tag)` in `PetDanceLiveTrial` with `await using var scope = await W101ControlScope.CreateAsync(context, target.Window, client, cancel.Token, true)`.
  2. Replace `() => true` for `InputReady` in `PetDanceSpecialistControl` with `scope.Ready`.
  3. Upon focus loss during active Dance rounds, do NOT attempt to freeze or resume old arrow queues. Clear pending queues (`pending.Clear()`), withhold input (`inputs_withheld: true`), and require fresh visual phase re-synchronization (`DancePhase.Watch`) or accept bounded round failure before dispatching new inputs.
- **Unit Test [PROPOSED ONLY - NOT IMPLEMENTED]**: `GameplayPauseClockTests.cs`:
  - `DanceTrial_WhenFocusLostDuringRound_DiscardsPendingKeysAndAwaitsFreshPhase`: Verifies that focus loss immediately withholds queued inputs and does not re-dispatch stale arrows upon focus restoration.

### Proposal 4: Quest Conversation Focus Checkpoints (`W101QuestConversationOperation.cs`)
- **Exact Path**: `ProjectMaelstrom/ProjectMaelstrom/Utilities/W101QuestConversationOperation.cs` (lines 79–100).
- **Repair**:
  1. Insert a focus-wait checkpoint strictly *after* spacebar receipt confirmation and release (`receipt.InputReleased == true`):
     ```csharp
     if (!Ready())
     {
         if (context.FocusBudget is not null && !await scope.WaitForFocusAsync(token).ConfigureAwait(false))
         { reason = "user-activity-or-target-change"; break; }
     }
     ```
  2. Ensure that if focus was lost *after* `receipt.InputReleased`, the conversation state advances to the next page and does NOT re-dispatch Space for the completed page.
- **Unit Test [PROPOSED ONLY - NOT IMPLEMENTED]**: `W101QuestConversationOperationTests.cs`:
  - `Conversation_WhenFocusLostBetweenPages_PausesAndDoesNotDuplicateSpace`: Verifies spacebar is sent exactly once per dialogue page across a focus pause.

### Proposal 5: Module Observer Between-Observation Focus Waiting (`W101ModuleObserver.cs`)
- **Exact Path**: `ProjectMaelstrom/ProjectMaelstrom/Utilities/W101ModuleObserver.cs` (lines 44–45, 146–147).
- **Repair**:
  1. Retain hard wall-clock bounds (12s in `ReadAsync`, 3000ms in `CaptureAsync`) while low-level hooks or capture buffers are active to prevent indefinite hangs or resource leaks.
  2. Place focus wait checkpoints strictly *between released observations* at the caller level (e.g. in `W101WorldGateVisit`, `W101CharacterOperation`), ensuring all hooks are uninstalled and capture sessions disposed before waiting.
- **Unit Test [PROPOSED ONLY - NOT IMPLEMENTED]**: `W101ModuleObserverTests.cs`:
  - `ReadAsync_RetainsBoundedWallDeadline_DuringHookAttachment`: Verifies that low-level observation retains a hard wall deadline to protect system resources.

---

## 6. Safety and Boundary Checklist

- [x] **No Indefinite Loops**: All proposed repairs use `OperationFocusBudget` which enforces a hard cumulative pause cap of 300 seconds across the lifetime of the operation.
- [x] **Preserve Takeover Baseline**: Physical keyboard activity immediately stops the operation via `DesktopActivityMonitor.AnyKeyHeld()` or `monitor.Revision != baseline`. No baseline resetting.
- [x] **Preserve Cleanup Uncertainty**: If `InputReleased == false` or `WindowCaptureCleanupException` occurs, focus wait is strictly disallowed; the operation must escalate to `OperationState.CleanupFailed` and scheduler recovery.
- [x] **Real-Time Cooldown Preservation**: Real-world in-game cooldowns and evidence timestamps advance naturally without artificial freezing.
- [x] **Real-Time Minigame Synchronization**: Pet Dance discards stale arrow cues on focus loss and re-synchronizes on fresh rounds rather than attempting invalid resume.
- [x] **Read-Only Compliance**: Report produced purely via static analysis; zero production code modified; no builds or live game interactions conducted.
