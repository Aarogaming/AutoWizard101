# Game Pack Workshop — proposed specification

Status: the first offline Workshop window is implemented. Launch
`Launch Game Pack Workshop.cmd`; follow the [quick guide](WORKSHOP_USER_GUIDE.md).
It supports draft details, key/click/wait rule editing, versioned save/load and
replay evaluation. Screenshot labeling, capture, asset management, import/export
and qualification remain planned. No package loader treats a pack as executable.

`tools/evaluate_game_pack.py` writes replay evidence tied to hashes of the exact
pack manifest, session manifest and event stream. It reports unmatched observations
and proposal counts. Reports explicitly state zero dispatches and zero verified
outcomes; replay evidence does not qualify a pack for live play. Example:

Add `--expected tools/fixtures/game_packs/pet-dance/expected.json` to compare
the complete ordered proposals against a fixture. A mismatch is recorded in the
report and returns exit code 1. Both sample packs have expected-proposal files.
These checks evaluate labeled-state rules only, not perception or gameplay.

```powershell
.local/envs/dance/Scripts/python.exe tools/evaluate_game_pack.py tools/fixtures/game_packs/pet-dance tools/fixtures/game_packs/session.json --report .local/reports/pet-dance-replay.json
```

Create a starter draft with:

```powershell
.local/envs/dance/Scripts/python.exe tools/create_game_pack.py pet-dance "Pet Dance" wizard101 --library .local/game-packs
```

This writes `.local/game-packs/<pack-id>/<version>/pack.json`. Revisions are
immutable: increment the pack version to save edits. The current schema validates
identity, draft status and the supported rule shapes; assets and gameplay semantics
are not yet qualified. Only draft status is accepted.

## Named actions and Dodge replay

Schema 2 adds a data-only action registry to each pack. A named action contains
an ID, title, uppercase key name and a bounded hold duration of 1–1000 ms. A rule
can propose an action ID; replay resolves it to the saved binding and includes
sample resolutions in the report. The same action may be referenced by multiple
rules. Schema 1 packs and their key, click and wait proposals still replay as
before. Schema 2 may also contain those older proposal shapes. The registry is a
shared *format and evaluator*, not a system-wide global action database or a live
input dispatcher. All packs remain draft and replay sends no inputs.

In the workspace, **Prepare Dodge replay** reads a completed recording, verifies
its saved frames, and creates a timestamped session under `sessions`. It runs one
frame at a time with a 100 ms pause between frames; Stop cancels analysis. It
does not start a model or game. **Open Dodge replay in Workshop** opens the
bundled experimental Dodge sample with that session when Workshop is not already
running. If Workshop is already open, select **Dodge sample** and browse to the
session path shown in the home window. Select a rule to see its named binding;
edit the key/duration, select **Add / update action**, and save a new pack version
before replaying. Do not assume suggested moves are correct: this first detector
is a single-frame, color-specific prototype, not temporal tracking.

Sessions retain the source recording path/hash, each frame hash and the detector
version. `unknown` is unmatched; `dodge-advice-hold` intentionally has no key
action. The report separates unmatched state counts and shows up to 50 proposal
examples; these are derived predictions, not independently labeled truth or
successful outcomes. An exported session does not automatically include the
source recording itself. See `GAME_WORKSPACES.md` for capture limitations.

## Bounded Dodge tests and comparisons

With a game window selected in Observe, enable game controls in Tools and select
**Run Dodge test**. Choose a saved schema 2 Dodge pack. The test attempts to
focus the selected target and stops if focus, identity, client geometry, policy,
or frame freshness fails. It permits only arrow/WASD keys, with a maximum hold of
200 ms, for at most eight seconds and ten key presses. Escape, Stop, closing the
workspace or losing focus interrupts the test and releases a held key. Windows
focus can change between validation and delivery, so this is not an absolute
target-isolation guarantee. Do not use it on an unintended window.

Reports are stored under `dodge-runs/<run-id>/report.json`. Each accepted key-down
is recorded separately from the frame-based movement observation. Before/after
frame hashes and capture/input times are included when available. An observed
movement is not proof of survival, cause, successful play, or detection accuracy;
unknown and interrupted results remain explicit. If a key release fails the
dispatcher retries and reports the failure or retry.

Workshop's **Compare another version** evaluates two versions of the same pack
on the same recording. **Compare Dodge test runs** compares outcome counts from
two saved live reports and verifies hashes and timestamp order for movement
evidence. Different runs may have different enemies and initial states, so neither
comparison by itself establishes that one player is better. The first live test
attempt reached the decision loop but rejected its first action; actual key
delivery and game response have not yet been verified.

The Local test games window also offers **Run headless Dodge test**. This makes a
bounded, instrumented copy of the locally prepared Godot sample, caps it at
30 fps and runs at most eight seconds or ten action requests. The game supplies
positions and accepts its own logical input actions; the ordinary game source is
not modified, and no desktop focus or keyboard input is needed. The selected pack
supplies the direction rules, but its desktop key binding is not used by this
native adapter. Reports explicitly identify the game-state observation and native
input path. They do not verify screen recognition or Windows input.

Each acknowledged action has a hashed engine response containing before/after
positions. Workshop can compare two headless reports and checks each response
against its hash and claimed movement. A changed score or observed movement is
not a general measure of play quality; separate runs can diverge even with the
same random seed. The app keeps the test copies and evidence under its user data
folder. **Choose Dodge pack** selects an alternative saved version; the built-in
draft sample is used by default.

Sample data-only packs exercise distinct key and pointer proposals:
`tools/fixtures/game_packs/pet-dance/pack.json` and
`tools/fixtures/game_packs/potion-motion/pack.json`. Against the included synthetic
session, run `python tools/replay_session.py tools/fixtures/game_packs/session.json
--pack tools/fixtures/game_packs/pet-dance/pack.json --proposals`. Output is labeled
SIMULATED and no executor exists in the replay path. These examples illustrate
contract shape; they are not game detectors or playable packs.

The Workshop is a core product feature. Its smallest complete workflow is create
pack → record/import examples → label → define one skill → replay-test → save →
reload. Export/import and live qualification follow the same versioned identity.

## Authoring workflow

1. Create a draft: game/pack ID, title, supported platform/display conditions,
   target identification hints, capabilities and provenance.
2. Record a user-selected window or import saved media. Preserve timestamps,
   geometry and source references. Label demonstrations separately from outcomes.
3. Label screens, regions, text, objects and state transitions. Keep unknown and
   ambiguous examples; split training examples from held-out evaluation sessions.
4. Define bindings and one bounded skill with preconditions, observations, actions,
   deadlines, expected outcomes and recovery. Start declarative; code extensions
   are an advanced capability with explicit dependencies and execution controls.
5. Test in replay using a fake executor. Inspect observations, action proposals,
   reasons and outcome evidence. Replay cannot send real inputs.
6. Save a new revision and reload it without losing assets or definitions. Preserve
   old revisions used by runs; edit a new draft rather than mutate evidence.
7. Qualify in Assist, then controlled Play. Publish local test evidence and supported
   conditions. Export a version and import it into a fresh local library for review.

## Logical pack contents

| Component | Contents |
| --- | --- |
| Manifest | Schema version, pack ID/version, title, game identity, capabilities, supported conditions, dependencies and source/license references. |
| Perception | Screen states, regions, OCR settings, templates/detectors and their versions. |
| Controls | Named coordinate spaces, bindings, duration limits and release behavior. |
| Skills | Preconditions, decision rules or model references, bounded actions and recovery. |
| Outcomes | Observable success/failure checks; unknown is a first-class result. |
| Fixtures | Referenced inputs, expected observations/decisions and held-out evaluation cases. |
| Evidence | Test results tied to exact pack, runtime and dataset revisions. |

Large recordings belong in the shared Library with stable content references.
An export must declare whether it is self-contained and list missing external
assets/dependencies. It must never silently substitute a different model or file.

## Save/import requirements

Save validates the manifest and references, writes a complete temporary version,
then publishes it atomically where supported. A failed save preserves the previous
version. Record incomplete captures explicitly. Keep settings and credentials out
of exported packs; runtime configuration is separate.

Import rejects unsupported schemas, missing required assets, hash mismatches,
duplicate IDs/paths and paths escaping the destination, including archive traversal
and linked-path cases. Bound archive/file sizes and expansion. Never install
dependencies or run bundled scripts as an automatic consequence of import.
Conflicting versions require an explicit choice; existing runs remain reproducible.

Draft, Replay-tested and Live-tested are evidence-backed labels for an exact version
under stated conditions. A changed pack requires new qualification. Live-tested
is not a guarantee for every resolution, game update or task. Rules, model outputs
and game-specific structured-state backends identify their provenance separately.

## Acceptance tests for the first Workshop milestone

- Two different fixture packs use the same authoring/session contract.
- Create/save/reload preserves every definition and resolves every asset.
- Invalid save leaves the last valid revision intact.
- Replay produces deterministic decisions under a fake clock without desktop input.
- Import/export round trip retains version identity and evidence references.
- A modified pack cannot inherit an old version's qualification silently.
- Missing assets, unsupported versions and malformed packages produce useful errors.

Pet Dance, Home Navigation, Potion Motion and Diego should eventually be authored
through this workflow. None is supplied as a working generic pack by this document.
