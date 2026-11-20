# GEM-HONESTY-AUDIT-01 — Independent claim-vs-source audit of Gemini result documents

Date: 2026-11-16 · owner: root (coordinator) · scope: 17 Gemini-authored result documents plus `docs/coordination/gemini/QUEUE.md`
Method: static inspection only against the committed tree (HEAD `6afa0f5`). No builds, no live runs, no edits to Gemini's reports.
Claim: `GEM-REPORT-HONESTY-AUDIT-01` (report-only). Correction routing: `GEM-HONESTY-AUDIT-LIA-01` (LIA-0121 in `docs/coordination/gemini/TO_GEMINI.md`).

## 1. Verdict summary

| Document | Verdict | Headline overstates body? |
| --- | --- | --- |
| GEM-MENU-INVENTORY-90 | **MATERIAL OVERCLAIM** | yes — "exhaustive" matrix with wrong provenance |
| GEM-PROTOTYPE-ACCEPTANCE-85 | **MATERIAL OVERCLAIM** | yes — "Priority 1 (Confirmed UX Defect)" on code that does not exist |
| GEM-MINIGAME-READINESS-98 | **MATERIAL OVERCLAIM** | partly — §3 body vs §4 headline |
| GEM-ENTRY-EVIDENCE-78 | **MATERIAL OVERCLAIM** | partly — "verified production producers" |
| GEM-CONJ-REPLAY-GAPS-102 | MINOR DISCREPANCY | no — but densest body errors (incl. a fabricated API) |
| GEM-IDLE-RECOGNITION-87 | MINOR DISCREPANCY | no |
| GEM-FOCUS-CHECKPOINTS-93 | MINOR DISCREPANCY | no |
| GEM-MODAL-BOUNDARIES-115 | MINOR DISCREPANCY | no |
| GEM-DANCE-RESYNC-95 | MINOR DISCREPANCY | no |
| GEM-DANCE-ENTRY-WIRING-97 | MINOR DISCREPANCY | no |
| GEM-DANCE-REPLAY-SCOPE-144 | MINOR DISCREPANCY | no |
| GEM-ROUTE-PROVENANCE-83 | MINOR DISCREPANCY | no |
| GEM-MAP-METADATA-107 | MINOR DISCREPANCY | no |
| GEM-CONNECTOR-125 | MINOR DISCREPANCY | no |
| GEM-CONJ-BOUNDARIES-101 | MINOR DISCREPANCY | no — headline substantiated |
| GEM-EXPLORATION-CONTRACT-86 | MINOR DISCREPANCY | no |
| GEM-REPLAY-DIAGNOSTICS-146 | **OK** | no — every citation verified exact |
| `gemini/QUEUE.md` | MINOR DISCREPANCY | no — stale task markers, no dangling paths |

Totals: 4 material overclaims, 13 minor discrepancies, 1 clean, 1 coordination-file drift.
**No report reproduced the UNIVERSAL-CONTROL-LOOP-01 / UNIVERSAL-BUS-01 headline failure**: every status line is appropriately hedged ("READ-ONLY AUDIT", "PROPOSED", "offline analysis only").

## 2. Systemic findings

1. **Phantom APIs in "existing producer" columns.** The dominant defect: matrix tables whose *Existing Producer Method / Core Symbols* column names plausible APIs that do not exist, often marked "Implemented". Coordinator re-verification of the highest-risk ones: `VisualStateRecognizer.Sha256Hex` → 0 hits in tracked `.cs`/`.py`; `GameWindow.IsReady/ClientSize/IsForegroundWindow` → no such members; `CapturedWindowFrame.Size` → absent; `PetDanceArenaCosts` → 0 files; `GameInputLease.DispatchClickAsync` → 0 files; `PotionBoard`, `find_matches`, `score_matches`, `apply_gravity`, `find_all_valid_moves` → 0 hits each. A grep-per-named-symbol check before handback would have caught all of these.
2. **Stale gap statements.** A gap already closed upstream is still presented as open: World Gate focus-wait "0 sites" (actual 2 at HEAD); the "GRAPHICS-only" options gate (now accepts four tabs); "no live pose telemetry" (a native pose stream now exists); "Priority 1" and "Priority 2" defects already implemented in committed code. A report whose purpose is a gap audit must re-check each gap against HEAD before handback.
3. **Uncommitted evidence presented as committed evidence.** `.local/` is git-ignored (0 tracked files) yet GEM-MAP-METADATA-107 §7.1's numeric block and GEM-CONNECTOR-125's evidence list derive entirely from it. The numbers are internally consistent, but they are not committed evidence and must be labelled as local artifacts.
4. **Numeric errors.** 3000 ms vs the actual 300 ms frame-age limit (`ModuleRecognition.cs:14`); 90 s vs the actual 60 s conversation timeout; 18 vs the actual 22 fixture events; "12 tests" vs 21 in `test_conjuration_memory.py`; heading error ≈1.57 rad vs π; 4 of 5 SHA-256 digests in GEM-MENU-INVENTORY-90.
5. **Line-citation drift.** Many documents cite line numbers from an older baseline or from a revision that does not contain the audited file at all (e.g. `595ef2c` for `CardPrototypeForm.cs`, added in `236eb00`).

## 3. Material overclaims — required corrections

### GEM-MENU-INVENTORY-90
- 4 of 5 SHA-256 digests are wrong. Coordinator recomputation: `graphics-menu.png` claimed `1d1a1b…`, actual `bc665c42…`; `native-idle.png` claimed `78e5f2…`, actual `21cc5af3…`; `quinn-offer.json` claimed `7c6605…`, actual `9654db1e…`; `snack.png` claimed `43286b…`, actual `ab687d77…`. Only `reward.png` (`47d3e9…`) is correct.
- Control IDs `game-menu.help` and `game-menu.ok` exist **only inside this report** (0 occurrences in any tracked `.cs`/`.py`/`.json`). The only real options-menu control is `game-menu.idle`.
- "Close: `ESCAPE` or `game-menu.ok`" describes no implemented mechanism.
- Layer A lists `PetDanceLiveTrial` as integrated with `W101ControlScope`/`GameInputLease`; its own doc comment states it "installs no hooks, owns no message loop and never acquires an input lease".
- "`DeckConfiguration` visible ⇒ `menu = true`" is false; `w101_ui_tree.py` requires a `SettingPage`/`SettingsPage` child.
- **Do not release this report to the board as an evidence atlas until the digests and control IDs are corrected** — it is exactly the artifact other documents copy from.

### GEM-PROTOTYPE-ACCEPTANCE-85
- "Priority 1 … (Confirmed UX Defect)": the committed `CardPrototypeForm.cs:147` sets `OverwritePrompt = false` and guards via a `FileOk` cancel + message box. There is **no `throw new IOException`** in the file. The report's own "Expected Behavior" is already implemented.
- "Priority 2 (Functional Gap)": the Test button is already disabled and labelled for non-`review.routine` cards (`CardPrototypeForm.cs:130-131`).
- Stated baseline `595ef2c` does not contain the audited files.
- The `CardPrototype.cs` / `CardQualification.cs` citations are byte-accurate and must be preserved.

### GEM-MINIGAME-READINESS-98
- "`tools/tests/test_conjuration_memory.py` (**12 tests passing**)" — the file has **21** test methods.
- Python Potion Motion "core symbols" `PotionBoard`, `find_matches`, `score_matches`, `apply_gravity`, `find_all_valid_moves` do not exist; `best_move` is only a substring of `find_best_moves`. "1-ply and 2-ply lookahead" and "gravity collapse simulation" are not implemented (single shifts; no collapse).
- C# `PotionMotionBoard`: no `FindAllValidMoves`, no `BestMove`, no default 7×7 grid, no column slides — "full C# port" is not accurate.
- Table row "ScheduledVisit (**Wired in Task 97**)" contradicts GEM-DANCE-ENTRY-WIRING-97, which establishes there is no production `IPetDanceVisitRuntime`.
- The Potion Motion refill threshold is stated as fact (two different numbers) while `docs/MINIGAME_RESEARCH.md:30` labels it unverified and inconsistent.

### GEM-ENTRY-EVIDENCE-78
- Field matrix marks non-existent producers as "Implemented" (`Sha256Hex`, `target.IsForegroundWindow()`, `target.IsReady()`, `target.ClientSize`, `CapturedWindowFrame.Size`, `PetDanceMenuOcr.ExtractAsync`, `PetDanceArenaCosts`, `W101ResourceFacts.GetLiveResource`).
- The arena energy figures ("Wizard City = 4 Energy, Krok = 6") have no source in the tree.
- "All static test assets … have been surveyed" — 18 of 79 fixture files are listed.
- Traceability line states claim `state: active`; the registry says **released**.
- **Preserve §4**: all 18 fixture byte counts and SHA-256 digests verified exact — genuinely strong work.

## 4. Minor discrepancies — required corrections

- **GEM-IDLE-RECOGNITION-87** — frame-age limit is **300 ms**, not 3000 ms (`ModuleRecognition.cs:14`); the "GRAPHICS-only" gate is stale (`PetDancePostGame.cs:46` accepts GRAPHICS/SOUND/GAMEPLAY/PRIVACY, regression at `PetDancePostGameTests.cs:227-238`); `classify` is at `tools/w101_ui_tree.py:1739`, not 1520-1555; `nativeIdle` stays unknown for unrecognised layouts.
- **GEM-FOCUS-CHECKPOINTS-93** — World Gate has **2** focus-wait sites (`W101WorldGateVisit.cs:57,202`); Proposal 1 is already implemented; Proposal 2 cannot compile (`OperationFocusBudget.CreateDeadline` returns `Deadline`, not `CancellationToken`); Proposal 4 cites non-existent `FocusWaitAllowedAsync`; the conversation timeout is **60 s** (`W101QuestConversation.cs:17`).
- **GEM-MODAL-BOUNDARIES-115** — `classify(tree)` is at `w101_ui_tree.py:1739`; `"route-stalled-progress-timeout"` does not exist (actual `"forward-progress-stalled"`, `PathFollower.cs:184-186`); `JobFactCatalog` is not referenced by `W101UiTreeOperation` as the chain claims.
- **GEM-DANCE-RESYNC-95** — no baseline declared; line references off 4-30; the `DodgeInputPulse` block is quoted non-verbatim (omits `reason = ex.Message`); the observation type is `DancePanelObservation`.
- **GEM-DANCE-ENTRY-WIRING-97** — cites `PetDanceVisitObservation.cs:8`, a file that never existed (the record is `PetDanceVisit.cs:5-9`); `PetDanceMenuOcr.ExtractText` does not exist (the entry point is `ReadAsync`). The report's central negative claim is correct and its proposal labelling is exemplary.
- **GEM-DANCE-REPLAY-SCOPE-144** — the stated baseline is wrong (it audits files added later); §3.2/§3.3/§4.1 are a pre-145 snapshot now superseded. All hashes and trace quotes verify exactly.
- **GEM-ROUTE-PROVENANCE-83** — the teleport waits for `WizardCity/WC_Hub`, not `WC_PET_Park`; W-12 (`auto_pet.py:344`) is commented out; `read_pose` is at `w101_pose_reader.py:71`; heading error is ≈π, not ≈1.57 rad; "no live pose telemetry" is stale at HEAD; claim state is `released`; "TO PLAY"/"YOU ARE IDLE" attributions are unsupported.
- **GEM-MAP-METADATA-107** — `client_zone.py` zone_name is at 27-31 (offset 88 is correct); the Automatus PET_Park entry is at `locations.py:53`; the §7.1 numeric block derives from uncommitted `.local/` evidence and must be labelled as such; 218,391 B is 213 KiB, not 218 KiB.
- **GEM-CONNECTOR-125** — `factor = 1.0` does not exist in `w101_bcd_evidence.py`; the file does not flag Proxy 88 as an obstruction — `W101-CONNECTOR-CLEARANCE-137.md:19` requires narrow-phase assessment first; `read_dimensions` is at `w101_pose_reader.py:16-48`; the "≈2.71 rad/s" figure is uncorroborated (derived ≈2.76); `cube_to_vertices` is commented-out dead code presented as live.
- **GEM-CONJ-BOUNDARIES-101** — headline is substantiated (file tracked, exactly 4 tests, root run corroborated); all four test line ranges are off by 3-6 lines; the test asserts `ValueError` type, not the quoted message.
- **GEM-CONJ-REPLAY-GAPS-102** — the fixture has **22** events, not 18; `complete-revealed-selection`, `match-known-pair`, `select-known-group` do not exist (actual `complete-known-group`); a rejected dispatch **latches a block**, it does not unlatch `_pending_slot`; `GameInputLease.DispatchClickAsync` does not exist; "17 identical instructions frames" is unsupported (one frame recorded); `phase_detection_implemented` lives in the replay report, not the vision observation.
- **GEM-EXPLORATION-CONTRACT-86** — cleanest of the six; `w101_pose_reader.py#L36-L79` wrong (read_pose at :71); `LoadingTransitionGate.cs` is 35 lines, not 6-45; the §4 pose-stream and zone rows are stale at HEAD (the end-to-end follower still has no production caller).
- **gemini/QUEUE.md** — "Active next task: GEM-ROUTE-PROVENANCE-83" and "GEM-CONNECTOR-125 — ready" are both **released**; completed tasks 144/146 are missing entirely; the task-78 section reads as live instruction although it is complete; **no dangling file paths** (21/21 named outputs exist).

## 5. Work worth preserving

- **GEM-REPLAY-DIAGNOSTICS-146** — every code citation, quoted string and behavioural claim verified exact; acceptable as-is.
- **GEM-DANCE-ENTRY-WIRING-97** — identifies the single most important structural gap in the Dance lane (no production `IPetDanceVisitRuntime`), proves it by negative grep, and labels every proposal `[PROPOSED ONLY - NOT AN EXISTING API]`. This is the model the other reports should follow.
- **GEM-ENTRY-EVIDENCE-78 §4** — 18/18 fixture byte counts and digests exact.
- **GEM-DANCE-REPLAY-SCOPE-144 §1-§3** — every SHA-256 and trace quote exact.
- **GEM-CONJ-BOUNDARIES-101** — the nonredundancy claim and all `conjuration_memory.py` citations are line-exact; the headline is true.
- **GEM-EXPLORATION-CONTRACT-86 §3.3** — every tolerance in the heading-feedback table matches source.

## 6. Recommendations

1. **Route corrections to the report owners** (LIA-0121, `docs/coordination/gemini/TO_GEMINI.md`). Gemini's owners revise their own reports; the coordinator does not edit them. Required items are the §3 and §4 lists above, each with a file:line correction or an explicit "unverified/local-evidence" label.
2. **Do not use GEM-MENU-INVENTORY-90 as an evidence atlas** until its digests and control IDs are corrected; it is the most likely source of downstream copy-paste.
3. **Keep the claims of the four material-overclaim reports active** until the corrected handback is verified. Claims for GEM-REPLAY-DIAGNOSTICS-146 and GEM-CLI-RUNBOOK-AUDIT-01 are already released and need no action.
4. **Adopt a pre-handback rule:** every symbol named in a report's "existing producer"/"core symbols" column must be grep-verified, every count must be counted, and every "gap" must be re-checked against HEAD, not against the author's working tree. A grep-per-named-symbol pass would have caught all 19 phantom symbols found here.
5. **Headline discipline stands:** title and executive summary are the claim surface; a report whose body is corrected must have its headline corrected in the same revision (the UNIVERSAL-BUS-01 lesson).

## 7. Verification method and limits

Coordinator spot-verification performed directly: SHA-256 recomputation of 5 menu fixtures; repo-wide `git grep` for `game-menu.help|game-menu.ok`, `Sha256Hex`, `IsForegroundWindow|IsReady()|ClientSize`, `DispatchClickAsync`, `PetDanceArenaCosts`, `factor`; direct reads of `PetDancePostGame.cs:46`, `ModuleRecognition.cs:14`, `CardPrototypeForm.cs:147`, `OperationFocusBudget.cs:55-67`, `W101QuestConversation.cs:17`, `PathFollower.cs:184-186`, `W101WorldGateVisit.cs:57,202`, `conjuration_memory.py:244-251`, `synthetic-pairs.json` (22 events), `claims.json` states.

Limits: static inspection only — no live qualification, no test execution for the audited reports (the only executed suites in this audit window were the coordinator's own 3,048 native and 890 Python tests), and no claim is made about the intent of any author, only about what the sources show.
