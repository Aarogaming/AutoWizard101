# W101 transition and route-teaching integration — 2026-11-11

Status: transition slice live-qualified at the recorded configuration; navigation
teaching is source/offline verified, not live-qualified. Source changes are not an installed release.
The coordinator retains the build and live-control slot; agents worked on pure
cycle logic, control-scope diagnostics, recognition, navigation transport and
observer latency in separate claimed files.

## Implemented scope

- Receipt-bound Idle/options/Idle graph on the existing RoutineRunner and scheduler.
  Separate wake and ensure-Idle modes support composition without assistant menus.
- Hidden bounded activity-monitor scope with exact target, foreground, geometry,
  cancellation and takeover checks. Admission diagnostics distinguish actual focus
  loss from heartbeat or ownership failure.
- CLI DPI awareness, bounded game-frame evidence, and separate initial semantic
  freshness versus continuing ownership during an already admitted input pulse.
- Observer-owned cleanup classification: recognition or evidence-write failure
  after confirmed cleanup no longer creates a false recovery lock. Unconfirmed
  hook/capture/input cleanup still blocks gameplay.
- Optional owned snapshot ends after two successful serial tree samples; normal
  watch duration, acquisition timestamps and hook restoration remain unchanged.
- Regular-UI menu OCR regions/contrast and a calibrated, provenance-marked Idle
  glyph matcher. A template match requires the Graphics heading and aligned
  neighboring controls; gap position alone never permits a click.
- One explicitly selected W/A/D teaching pulse, capped at 300 ms, with independent
  before/after evidence under the same input owner. This is not autonomous route
  navigation or proof of arrival.

## Live evidence so far

Local immutable artifacts: `.local/w101-cycle-06/`.

| Trial | Observed result |
| --- | --- |
| 01 / 03 | Admission failed before input; older diagnostics could not distinguish reasons. |
| 02 | Escape opened Options. A stale pre-press reading incorrectly rejected the delivered pulse afterward; repaired. Assistant restored Idle. |
| 04 | Exact diagnostics: foreground was Codex, not W101; zero inputs and scope released. |
| 05 | Wake receipt accepted and Options observed; OCR missed yellow labels. Deadline stopped the run. Assistant restored Idle. |
| 06 | Repaired recognizer found Idle; click receipt accepted, but Options remained with hovered button. No automatic Idle outcome. Assistant restored native Idle and released desktop control immediately. |
| 07 | W101 no longer foreground; zero inputs, immediate scope release. |
| 08 | Automatic NativeIdle → MenuObserved → NativeIdle, two actions, 9,369 ms. No assistant cleanup. Operation `4e3b4f16452142b09ab5712469ac1ee6`. |
| Wake 01 / 02 | Both wake-only runs independently observed awake state after closing Options. Operations `58fa4a369dd846c6873217b7c3e33386` and `94011bc249684c6fb4ffdb44746ded35`. |
| Ensure Idle 01 | Automatic awake → Options → native Idle completed; operation `1568e7517f694d3ca497fd33c0760cad`. |
| Turn 01 / 02 | No input: OCR made pre-action pixels too old (approximately 412 / 324 ms). |
| Turn 03 | No client opened and no input: selected game process was gone. Owner confirmed restarting a stale/acting-up client. Latest capture-only route probe could not be live-qualified. |

Mouse repair separates cursor move, an 80 ms settle and 80 ms hold, retaining
takeover/target checks throughout and mandatory release. This is a measured fix
for this UI, not proof of universal click reliability. Actual saved yellow and
white-hover Idle labels both classify correctly; glyph provenance is distinct
from Windows OCR. CLI now uses physical pixel coordinates at the observed DPI.

The final route-teaching probe skips unnecessary OCR: reviewed UI-tree states gate
one explicitly selected pulse, while capture-only pixels retain their original
timestamps and provide no visual facts or controls. The 300 ms frame freshness
bound is unchanged. This version has not sent a live movement pulse.

Trial 06 operation `252c825da5804211ab93a5467de149e5` reduced measured tree
age to roughly 1.16–1.51 seconds, versus roughly 1.93–2.11 seconds in trial 05.
This is an observed freshness improvement, not a general throughput benchmark.
No quest progression, purchases, snack feeding, volume changes or client restart
were performed. The user's wizard is now at Commons; previous Firecat location
must not be reused as current state.

## Client-side Idle review

The UI reader deliberately treats SettingPage/SettingsPage as structural leaves.
Saved menu trees therefore do not inventory the Idle button. Legacy Deimos identifies
an adjacent `WorldView/DeckConfiguration/SettingPage/QuitButton`, but this does not
establish Idle's control name or handler. wizwalker's named-window click relies on
a separate mouseless cursor hook, not a direct Idle-state function. Its legacy
unbounded sends and incomplete cleanup cannot bypass qualification.

Next candidate: narrowly inventory settings child names/visibility/enabled state,
correlate exact controls against observed pixels, then qualify a bounded adapter.
No direct Idle flag was written and no additional cursor hook was activated.

## Navigation and architecture limits

The human Commons-to-Pavilion demonstration remains useful route evidence, not
an autonomous success. Five fabricated-coordinate examples were quarantined as
non-executable previews; see W101-ROUTE-PROVENANCE-06. Navigation transport and
teaching probes cannot claim arrival from a changed image or accepted key.

No Rust port is needed to correct the observed timing, recognition or DPI issues.
Keep these modules and contracts replaceable; consider selective native components
only when profiling or a concrete correctness requirement justifies them.

## Verification checkpoint

Final complete native suite: **2,302 passed, zero failed/skipped**, warnings as
errors and bounded build workers. Python: **54 observer tests and 5 route tests
passed**. Changed Python files pass Black and syntax checks. Scoped C# whitespace
and analyzer checks pass; no repository-wide formatting was used.

No installer was built or run. No game navigation route or interaction/sigil
outcome is newly qualified. The next live gate is the new client identity, a short
movement observation, then arrival-verified Commons landmarks and reusable
interaction. No unrelated processes were stopped and desktop control was released
immediately after each assistant UI check.

## Composed visit qualification — November 11

W101-VISIT-08 connects existing wake, motion and Idle executors under one scheduler
lease and control scope. The CLI command is `navigation-visit`; no new controller,
model, dependency, virtual input driver or assistant desktop session is involved.
Motion remains a semantic `ModuleAction` mapped by the W101 adapter to bounded
W/A/D input. Future human-controller sources should submit those same intents
through the owner rather than create an independent sender.

Full native suite: **2,315 passed**, zero failures/skips, warnings-as-errors and
processor count two. Focused navigation/Idle/CLI suite: **120 passed**. Scoped
whitespace and analyzer verification passed. The initial nine fixture failures
were an incorrect reference-equality assertion against caller pins: scheduler
submission intentionally freezes them. Tests now require equal pin contents and
one shared frozen snapshot across stages. No production guard was weakened.

Live evidence under `.local/w101-cycle-06/live-data/operations/`:

| Run | Outcome |
| --- | --- |
| `85839297ecda43a4b82a29e1e458b196` | Wake → D 150 ms → native Idle: all stages Completed; user independently confirmed all three visible actions. |
| `d616e7aaaf664aa7990d29886aab5f16` | Rejected before input because chat held foreground. No forced activation or automatic retry. |
| `031847362d6046a794f512e710bedac8` | After a new readiness notification and focus-switch delay: wake → A 150 ms → native Idle; all stages Completed and user independently confirmed all three. |

Target: W101 PID33168/start639300254366438220/HWND2073165994. The prior separate
A150/W300 probes and ensure-Idle run also succeeded after this user-initiated
client restart. Neither changed pixels nor delivered keys establish route arrival.
Both composed visits explicitly report arrivalVerified=false and routeQualified=false.
Final observed state is native Idle. No assistant desktop-control tool was used
for these tests. No installer was built or run. Current next gate is a reusable
landmark/progress/arrival contract, not longer blind pulse sequences.

User verification protocol: question notification before **each** live test;
wait for Ready, allow a stated focus-switch delay, execute one bounded CLI run,
then ask what visibly happened. Offline work requires no user attention.
