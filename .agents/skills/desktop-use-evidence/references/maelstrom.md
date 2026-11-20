# Maelstrom application notes

These notes apply only when the current workspace contains Maelstrom. Inspect the current source and reports before relying on them; project status changes over time.

- `docs/PLAYER_INTEGRATION_PLAN.md` and `docs/GAME_PACK_WORKSHOP.md` describe the intended loop and current pack/replay boundaries.
- `tools/game_packs.py`, `tools/evaluate_game_pack.py`, and `tools/compare_pack_versions.py` validate draft packs and compare offline evidence. Replay does not send inputs.
- `ProjectMaelstrom/ProjectMaelstrom/Utilities/DodgeInputPulse.cs` and `DodgeLiveRun.cs` implement an experimental target-bound desktop test. An OS input receipt is not the game's response; inspect its report and saved frames. The desktop path has not been generally qualified.
- `DodgeNativeVerification.cs` and `tools/fixtures/godot_dodge_native_probe.gd` provide a bounded, headless test for an explicitly instrumented, isolated Godot Dodge copy. Engine-state observations and native input do not validate screenshots, Windows focus, desktop key delivery, or arbitrary games.
- One recorded desktop attempt accepted a key but showed no meaningful motion; the root cause was not established. Do not promote a suspected fix as verified. Use the source report under `.local/dodge-desktop-diagnostic-output/` if it still exists.
- Do not launch models or benchmark/game processes just to read a report. When running an authorized test, select a short duration and bounded concurrency; record actual results and whether any process-level metrics exclude the rest of the system.
