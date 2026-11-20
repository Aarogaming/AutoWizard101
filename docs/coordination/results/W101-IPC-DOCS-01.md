# Evidence: W101-IPC-DOCS-01

**Task**: Relative Documentation Synchronization: Host IPC Bridge Integration  
**Owner**: `root`  
**Workstream**: `W101-NAVIGATION`  
**Status**: Documentation delivered; describes a cycle the code does not perform (see Audit note)  
**Date**: 2026-11-18  
**Owned Paths**:
- `docs/BACKEND_CONTROL_PLANE.md`
- `docs/ROUTINE_EXECUTION_MODEL.md`
- `docs/INDEX.md`
- `docs/coordination/results/W101-IPC-DOCS-01.md`

---

## 1. Scope & Synchronization Summary

Prior to implementing the Rust core to C# host IPC integration (`MaelstromAutonomyBridge` in `crates/maelstrom-fallback/src/maelstrom_bridge.rs`), this task documented the named pipe framing, synchronization cycle, blackboard fact projections, and single-lease action dispatch contract across project documentation.

### Synchronized Documents:
1. **`docs/BACKEND_CONTROL_PLANE.md`**:
   - Documented Section 5: Rust Core / C# Host IPC Integration (`MaelstromAutonomyBridge`).
   - Detailed the named pipe transport (`\\.\pipe\maelstrom-deck-<HASH_HEX_24>`), wire framing (4-byte LE length prefix + JSON payload up to 262,144 bytes).
   - Documented the perception-decision synchronization cycle: `snapshot` retrieval $\to$ qualified fact projection (`NativeHostState::update_blackboard`) $\to$ Behavior Tree tick $\to$ local steering/kinematic parameter calculation $\to$ returning structured `AutonomyCycleResult`.
   - Reaffirmed the `Facts ≠ Authority ≠ Actions` boundary: Rust autonomy logic emits intent and parameter plans; only the C# host's single `OperationScheduler` executes physical actuation under verified input leases and focus constraints.
2. **`docs/ROUTINE_EXECUTION_MODEL.md`**:
   - Documented Section 5: Host IPC Bridge (`MaelstromAutonomyBridge`).
   - Defined how the perception-decision loop coordinates between C# observations and Rust autonomy trees.
3. **`docs/INDEX.md`**:
   - Added reference entry for `docs/BACKEND_CONTROL_PLANE.md` under Direction and Implementation.

---

## 2. Quality Gates Execution

- `python tools/check_project_board.py`: Project board valid (30 workstreams, all evidence links resolved).
- `python tools/check_text_encoding.py`: 0 violations across repository.
- Zero code modifications or unstaged regressions.

---

## Audit note (2026-11-19, Claude)

Checked against `docs/BACKEND_CONTROL_PLANE.md` Section 5, `docs/ROUTINE_EXECUTION_MODEL.md` Section 5, the audit note in
`docs/coordination/results/W101-IPC-CORE-02.md` (which holds the evidence and the probe results), `JobDeckHost.cs`,
`JobDeckWire.cs` and `JobFactCatalog.cs`. Nothing was run against a live host or game. The status above was changed from
"Released": the sections exist and the framing is right, but the cycle they describe is not what the code does.

**Verified.** Both sections and the `docs/INDEX.md` entry exist. The pipe name, the 4-byte little-endian length prefix and
the 2 to 262,144 byte budget match `JobDeckWire.cs`. The statement that the Rust side holds no input API matches
`maelstrom-fallback`. The host has an `actuate` command routed to `DesktopInputActuator`.

**Corrections.**
1. **The host does not return `spatialPosition` or `combatActive`.** `BACKEND_CONTROL_PLANE.md` Section 5 lists them among
   "qualified, fresh facts" returned by `snapshot`; the host's fact catalog has neither, and the Rust code reads them as
   unqualified raw keys. See the CORE-02 audit note.
2. **The behavior-tree order is incomplete.** "Combat > Potion Recovery > Kinematic Stuck > Quest Nav > Idle" omits the
   detour branch that sits fourth in `build_w101_canonical_tree`.
3. **Only the Bezier generator is used.** "`WindMouseGenerator` or `BezierTrajectoryGenerator`" overstates the bridge; it
   builds Bezier plans only.
4. **The "End-to-End Actuation Event Loop" is not an integrated loop.** The host exposes `actuate`, but the Rust bridge
   rejects every command outside five read-only ones, so no Rust code sends an intent to it.
5. **"The resulting `AutonomyCycleResult` is returned to the host" is not implemented.** The result is returned to the Rust
   caller. Nothing in the repository sends it to the host, and the bridge's only users are the replay harness and tests.
6. **The index entry overstated the integration** ("Rust-to-C# named pipe IPC integration"); it now says the bridge is a
   read-only snapshot client with offline decision logic.

**What this evidence does not prove.** This is a read-through against the code and the CORE-02 tests; it does not show a
live pipe session or any actuation driven by the Rust bridge.

**Follow-up (2026-11-19, Claude).** Correction 1 is addressed in code and in `BACKEND_CONTROL_PLANE.md` Section 5: the bridge reads
combat, zone and position only as qualified aliases (`combat.active`, `zone.name`, `position.x|y|z`), not as raw keys. The host
still publishes none of them, so against it those facts are unknown and each cycle ends `Withheld`. Corrections 2 to 6 are
unchanged. See the follow-up in [W101-IPC-CORE-02](W101-IPC-CORE-02.md).
