# Pet Dance: one complete visit before repetition

The new [lifecycle planner](../ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceLifecycle.cs)
describes one visit from a known approach location through the game, result,
optional snack review and return to native Idle. It is a **pure proposal planner**:
it does not recognize screenshots, dispatch input, navigate a route, feed a pet
or establish that the installed app can complete this workflow. Continuous scene
recording/detectors and execution integration are separate work.

For reusable execution coordination, use the newer [receipt-aware routine graphs](ROUTINE_GRAPHS.md).
They distinguish proposals, accepted/released dispatch and newer observed outcomes;
the older lifecycle outline below records some milestones at proposal time. The
postgame adapter uses the stronger shared runner, while the full visit remains an
offline candidate until entry, navigation, level selection and Play are qualified.
The newer [full-visit adapter](PET_DANCE_ENTRY_TRANSITIONS.md) now rehearses sigil
and instructions entry, specialist completion and cleanup through the shared
graph. Its Play-button matcher uses saved pixels, while selected-arena detection
still lacks independent qualification. The live launcher remains unchanged.

## Observed scenes and proposed next steps

| Observed scene | Proposal or prerequisite |
| --- | --- |
| OutsideNoSigil | ApproachRoute only with a configured, qualified route from the observed start. Otherwise wait; no coordinates or walk duration are invented. |
| DanceSigil | InteractX, then require an observed instructions screen. |
| InstructionsUnselected | ChooseLevel using the configured profile ID, then observe the selected level. |
| InstructionsReady | Play only when the observed selection matches the configured level. |
| Loading | Wait with loading-pause context. Pause inputs/gameplay time; discard incomplete cue sequences and reobserve on resume. |
| Watch | Hand off once to the visual teacher when associated with this visit's Play proposal and a complete Watch beginning. |
| Go / RoundFeedback | Teacher continues observing variable arrows; no lobby/result click is proposed. |
| GameResult | Next only for a verified whole-game result associated with this visit. Round feedback is insufficient. |
| SnackOffer | FinishWithoutSnack by default. Explicitly enabled snacks permit only a separate snack-planner handoff; no feeding is implemented. |
| OutsideAfterGame | OpenMenu only after the visit's game and whole-game result were observed. |
| MenuObserved | SelectIdle only after verified return outside. This is a distinct scene, not inferred from requesting OpenMenu. |
| NativeIdle | Done only after this visit's game, result and outside return. Initial Idle is not completion; the requested visit still needs an explicit UI start/wake. |
| Unknown | Reobserve without retrying the previous click or inventing a state transition. |

This is an event-driven visit, not a linear sequence of arrow presses. Pet Dance
cue order and round feedback remain the cue teacher's responsibility. Whole-game
completion may be a successful or failed game; it does not claim the teacher won.
Returning outside and entering native Idle satisfy this planner's end goal. A
different physical walk-away destination still needs its own qualified route;
the planner does not invent one or claim the Commons route is already qualified.

## Evidence contract and anti-duplication

Construct the planner with pinned target identity, capture geometry and game
context, plus the selected level and optional approach route. Each observation
supplies a finite increasing timestamp, stable logical transition ID, fresh
pixels, target readiness, verified context and observed scene. A transition ID
identifies a real UI transition, not each frame number. Detector confidence,
scene matching and visible button geometry must be established by the caller.

The planner records each admitted transition identity and its observed scene
before it can advance any visit milestone. A repeated identity with the same
scene waits; assigning a different scene to that identity is rejected. Neither
case can fabricate a game start, result, outside return or completion. Repeated
loading observations retain their pause context. The planner also permits each
action only once in this one-game visit. Repainted frames, new IDs
after Unknown and repeated GameResult/Menu screens therefore cannot trigger
another Play, Next or Idle click. A rejected/unexecuted proposal is not retried
automatically: reconcile the observation and explicitly start a new visit if
needed. A proposal is not a dispatch receipt or a verified outcome.

When an unresolved prerequisite becomes verified, publish a distinct logical
evidence event: for example, whole-game result verification or completion of
snack review. Reusing the earlier unresolved transition ID only waits. This
event identity represents new verified evidence, not a new frame number or an
attempt to retry a rejected click.

Takeover or a known changed bound target/geometry/context latches interruption,
even when pixels are stale or scene readiness is not established. Missing binding
metadata only withholds progress; it never authorizes action. Fresh frames from
the original target do not silently resume an interrupted visit. Stale/unready
frames and invalid timestamps produce no proposal. Dispatcher admission still requires
the shared input owner, fresh pre-action capture and current target/focus checks;
this planner does not replace them.

The returned plan contains action, optional profile/route argument, reason,
evidence still needed, loading-pause context and completion status. `Wait` and
`Done` are not dispatch proposals. No pixel coordinates, arbitrary code, memory
operations or game-process access exist in this class.

## Snacks stay separate

Snacks are disabled by default. The optional handoff asks the
[snack planner](../ProjectMaelstrom/ProjectMaelstrom/Utilities/PetSnackPlanner.cs)
for its own verified pet/item/opportunity, reserve and budget checks. It does not
mean consume a snack. The lifecycle waits until the separate review is complete,
then can propose leaving without any additional feeding. An unresolved snack
operation must not be marked reviewed simply to exit the screen. The caller must
associate that completion with the current opportunity; imported research is not
live inventory evidence. Completed review needs its own new logical transition
identity before FinishWithoutSnack can be proposed; it must not reuse the handoff
transition or manufacture one from another frame of the unchanged screen.

## Qualification and bounded repetition

The [tests](../ProjectMaelstrom/ProjectMaelstrom.Tests/PetDanceLifecycleTests.cs)
exercise the complete synthetic scene trace, unknown interruptions, stale/wrong
context, takeover, level/route prerequisites, no default feeding, distinct menu
observation and duplicate-action prevention. They do not establish real detector
accuracy or a working walk-up/walk-away dispatcher. The parent session owns
build/tests and integration; this change performs no desktop work.

First qualify one full visit with continuous evidence and native Idle cleanup.
Then add repetition as a separate bounded coordinator, default count **1**:

- Count a game only after a unique verified whole-game result and observed
  OutsideAfterGame; neither five round results nor a process exit count as a game.
- Require a finite requested count, fresh observed Energy and a reserve before
  another Play. Unknown resources prevent another game.
- Preserve unique result/snack-opportunity IDs and reconcile consumption before
  another run. Never reset a pending Play/Next/feed merely to retry it.
- Start a fresh one-game planner only after the previous visit is reconciled;
  require the actual starting scene and configured level again.
- Respect takeover, cancellation and cumulative time/resource budgets, and
  finish through observed native Idle. No unattended repetition is enabled here.
