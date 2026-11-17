# RESOLUTION-CAPTURE-ANALYSIS-01 — passive 2544×1372 capture during owner play

Status: **offline analysis of owner-authorized read-only capture. No input, no hooks, no focus change, no implementation.**
Date: 2026-11-16 · owner: root · claim: `RESOLUTION-CAPTURE-ANALYSIS-01` · workstreams: `W101-DANCE`, `CORE-DELIVERY`

## 1. Capture provenance

Owner explicitly authorized read-only interaction with the client during their fire-wizard play session. Used Maelstrom's own passive capture path:

- Verb: `ProjectMaelstrom.exe --sample-window-stills 642779728 <fresh-data-root> F:\Modlists\AutoWizard101 6 3`
- Implementation: `PassiveTeachingSampler.RunAsync` — *"bounded, selected-window still sampling without a form, input hooks, or focus changes"* — using `WindowObserver` and target-identity checks.
- Two earlier attempts at 1 fps and 2 fps produced 0 frames (freshness gate 500 ms; observed ages 1033 ms / 529 ms). At 3 fps: **12 frames, 6 s, max frame age 482 ms, 0 rejected after startup, `inputs_sent: 0`, `input_recorded: false`**, exit 0.
- Frames: **2544 × 1372** PNG, ~42 MB total, under `.local/agent-runs/capture-20261116-223707/data/recordings/20261117-033708-0b902662/`. Receipt: `passive-sample.json` in that directory. No images are copied into tracked files.
- Window at capture time (read-only Win32 query): outer 2048×1104, client 2033×1066, DPI 120 (125% scale). The 2544×1372 frame is the DPI-aware physical capture of that client area.

## 2. What the frames show (visual inspection, frames 0000 / 0005 / 0011)

- Overworld dialogue with NPC **Duran Dunefather** (dialogue panel bottom-center; `BACK`, `MORE`/`DONE` buttons along its lower edge).
- **Quest completion sequence**: "QUEST COMPLETE: A VIEW TO A CURSE", reward overlay "7750 XP", "75 GOLD", level-up marker "110", "HARBINGER OF LIGHT", and a follow-up "NEW QUEST: STOPPING THE SAND OF TIME".
- HUD at this resolution: health `4920` and mana `408 / 543` globes bottom-left; a row of five purple pips beside them; quest arrow and objective text ("Talk To Logan Sprigolder in Krokha Desert") with a segmented green progress bar at bottom-center; compass/minimap and portrait top-right; spellbook bottom-right; green NPC marker above the quest giver.

## 3. Approximate layout landmarks (VISUAL ESTIMATES — not calibrated measurements)

| Element | Approx. region in the 2544×1372 frame |
| --- | --- |
| Dialogue panel | x ≈ 760–1790, y ≈ 1130–1335 |
| Dialogue buttons (BACK / MORE / DONE) | y ≈ 1330–1365, x ≈ 990–1140 and 1550–1690 |
| Health/mana globes + pips | x ≈ 0–340, y ≈ 1180–1372 |
| Quest arrow + objective bar | x ≈ 950–1600, y ≈ 1190–1360 |
| Reward/level-up overlay | centered, x ≈ 980–1570, y ≈ 170–950 |
| Compass/portrait | x ≈ 2380–2530, y ≈ 40–210 |
| Spellbook | x ≈ 2420–2520, y ≈ 1220–1360 |

These are eyeballed from rendered frames for planning only. **Do not use them as click targets or ROI constants** — the resolution plan requires measured transforms, DPI/mode evidence and profile hashes; a one-pixel error previously broke recognition.

## 4. How this information is useful

1. **1440p profile work:** confirms the real capture geometry near 2560×1440 (2544×1372 here), the DPI interaction (120 → physical frame ~1.25× the logical client), and which UI elements need new ROIs/templates. It supplies *candidate* frame evidence toward the profile required by `RESOLUTION-1440P-01.md`, not a qualified profile.
2. **Wrapped quest teacher (wrap-first principle):** the quest-completion and dialogue sequence is already handled by Maelstrom's existing conversation adapter; these frames show the *real* HUD anchors it would need (dialogue buttons, objective bar, reward overlay). A future wrapping task should bind the existing function to this layout first, then reverse-engineer a model from the working wrapper — not write a new "inspired by" dialogue bot.
3. **Vision fixtures:** these frames can become provenance-marked fixtures for an offline parser/teacher under a separate claim, with exact hashes recorded.

## 5. Limits and boundaries

- No keyboard or mouse input was sent; no hook was installed; no focus or window state was changed. The capture ran while the owner played and left the client as found.
- No combat/spell-card frame and no Dance screen were captured in this 6-second window; those ROIs remain unknown at this resolution.
- Zone identity is **not** inferred from these pixels. The earlier read-only UI-tree observation gave a candidate dorm zone; the objective text mentions Krokha Desert, but neither is a qualified location claim here.
- No calibrated transform, ROI, template, profile or code change was produced. Implementation requires a new claim with source/build authority; further live work requires fresh owner scope.

## 6. Next gates

1. If the owner wants 1440p work: a scoped implementation claim that measures transforms from saved frames (not eyeballed estimates), adds a default-off profile with provenance, and keeps 1282×760 qualified as the default.
2. If the owner wants a quest teacher: a wrapping-first brief around the existing conversation adapter, with these frames as candidate fixtures and exact negative bounds (no crowns, no snacks, no dialogue dismissal outside an explicit verified flow).
3. Both remain proposals until claimed.
