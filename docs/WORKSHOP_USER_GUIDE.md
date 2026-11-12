# First offline Game Pack Workshop

Open **Maelstrom.exe**, then choose **Open Game Pack Workshop**. The portable
edition carries its own Python/Tk and .NET runtimes; keep its whole folder together.
Packs and reports live in its `Data` folder. The native home window supervises the
Workshop helper and asks you to close it before exiting so you can save changes.

For source development, `Launch Game Pack Workshop.cmd` remains a convenience
shortcut using the existing local Python environment. No new package is needed.

## Try the complete offline workflow

1. Choose **Keyboard sample** or **Pointer sample**. This loads a draft, a synthetic
   session and its expected proposals.
2. Choose **Evaluate saved version**. The result shows whether proposals match,
   how many observations had no matching rule, and the saved report's location.
3. Select a rule to populate the editor. Change its state label or proposed action,
   then click **Add / update rule** to apply the edit to the draft. Reusing a rule ID
   replaces it; a different ID adds a rule. Remove selected deletes that rule.
4. Change **Version** (for example to `0.1.1`) and choose **Save version**. Existing
   versions are never intentionally overwritten. Portable drafts go in `Data/game-packs/`;
   development drafts go in `.local/game-packs/`.
5. Choose **Open pack…** to reload that version's `pack.json`. Select a session and,
   optionally, an expected-proposals file; replay the saved version again.

Use **New draft** to create your own pack. IDs use lowercase letters, digits,
periods, underscores or hyphens. All four detail fields are required. The editor
supports key proposals, normalized pointer clicks, and waits of 0–60000 ms.
Click X/Y range from 0 to 1 within the game area; they are not desktop pixels.
Only committed rules (after Add / update) are part of the draft.

Replay is asynchronous and writes a unique report beside the pack library in `reports/`.
Unsaved draft changes must be saved first. A report names the exact pack/version
and hashes the bytes evaluated. Expected-result mismatches are visible, but do
not modify the pack. Reports explicitly record zero dispatches and zero verified
game outcomes. Two rules matching the same state produce two proposals; this
early evaluator does not arbitrate actions or simulate elapsed gameplay.

## Current limits

This window edits rules against pre-labeled recording states. It does not yet
capture video, label screenshots, recognize objects, drag potion rows, play the
dance sequence, package assets or send inputs. The sample names identify intended
future activities; the samples only demonstrate keyboard versus pointer proposals.
The application remains in draft mode. A matched fixture is evidence about rules,
not live qualification.

For the intended broader workflow see [Workshop specification](GAME_PACK_WORKSHOP.md).
# Teaching examples from Observe

Open a game workspace, select its window and start Observe. Choose **Start
recording**, then **Stop recording → Review and label**. Name states on individual
frames and **Save teaching pack**. **Edit rules in Workshop** opens that new draft
with its included replay session. Add rules for the state labels and save a new
version before replaying. Image evidence travels with the edited version.

Recording is sampled (at most one frame per second, 120 frames/128 MiB), with no
audio or game input. These are human-labeled examples, not a trained visual model.
Existing packs are not merged by the teaching export. See
[workspace guide](GAME_WORKSPACES.md) for recovery and limitations.

