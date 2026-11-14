# Dance scheduler and card qualification — 2026-11-14

## Delivered source

Tasks75/76 extract the existing Pet Dance specialist into an owner-bound in-process call and add a full-visit coordinator over the existing receipt/outcome state machine. The coordinator prepares one scheduler job, requires countdown admission, records dispatch/specialist receipts, verifies the result and no-feed exit, and requires native Idle plus released input for completion. The specialist handoff preserves target, geometry, visit and arena; round feedback never substitutes for result-screen evidence. No second controller is created.

The deck has a trusted Dance adapter seam. Its default remains unavailable because the Windows entry observation/dispatch runtime is not connected. The fake-runtime integration tests exercise the whole sequence but are explicitly offline and never confer live qualification.

Task79 fixes focus handling: an explicit FocusPaused observation pauses without proposing inputs; generic unavailable targets, changed identity/geometry/context, lost owner, cancellation and physical takeover remain terminal. Pauses cannot extend the overall visit budget.

Task77 adds exact-card replay qualification. Frozen card, graph, fact schema, scope and adapter versions are checked and replayed before ReplayPassed. Changed content invalidates current qualification while retaining history. Forged success reports cannot replace replay outcomes. LivePassed and Repeatable remain reserved; no automatic promotion or execution permission exists. Native editor and deck qualification integration remain pending; see ../../CARD_QUALIFICATION.md.

## Live evidence and limits

Observation-only installed CLI probe86b97ffc52094ff6b7ef063029e411f4 completed with inputCount0, cleanupConfirmed=true and restored client hook. The saved frame shows the Death wizard in the dorm with YOU ARE IDLE. Artifact root: .local/dance-live-76-93a7f07ab9f94eb5b91e963d41342b95/data/operations/86b97ffc52094ff6b7ef063029e411f4. No movement, focus change, snack use, volume change or full Dance game occurred.

The current backend UI contract does not export Dance arena identity/selection controls. The saved Wizard City level detector uses one1282x760 recording and requires independent control/pointer evidence; it is not an independently qualified live entry detector. Entry dispatch must explicitly support InteractX, ChooseLevel and Play under the existing owner. The specialist must observe the complete first Watch after Play. A qualified route from the current dorm to the sigil is also pending. These are implementation/qualification gaps, not missing user authorization.

## Gemini coordination

Reports71–73 delivered. Root-reviewed tests exposed the focus issue and an incorrect failed-specialist expectation, repaired in79. Audit71's claim that the specialist is subprocess-only is superseded by75; its missing live runtime finding still applies. Legacy73's blanket dismissal of anti-idle wandering must not override the owner's approved configurable movement routines. Task78 now inventories exact entry producers/fixtures under LIA-0035, without source edits/builds/live access.

## Verification

Scoped whitespace formatting completed. Full native suite and analyzer checks are recorded below when finished. Installed release62 is unchanged; new source is not installed or live-qualified.

## Next integration gate

Build and replay-test a real entry evidence/dispatch adapter using independently verified controls; connect it to the prepared visit and host; qualify approach separately; then attempt one bounded countdown-controlled no-feed visit and verify Idle. Do not turn availability on simply because the offline runtime passes.

Final validation:2696/2696 native tests passed with warnings-as-errors build; scoped whitespace verification and analyzers passed; project-board validator passed30workstreams. Owned-path diff whitespace check passed. Shared Gemini FROM_GEMINI.md has a trailing blank-line warning and was not edited by root.

Additional reuse finding: imported Deimos src/paths.py names PetGameTracks/wndBkgTracks/wndTracks/btnTrack0, PetGameTracks/btnNext, wndBkgEnergy/txtEnergyCost and txtYourEnergy. These are historical discovery hints, not current verified controls. A bounded optional entry context reader can expose them for independent screenshot/selection validation before enabling actions. Existing W101ModuleInput supports owned game.interact; PetTrainingEnergyGate requires fresh current energy and verified next-attempt cost. Do not invent the cost from available energy.
