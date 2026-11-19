# Architectural Doctrine: Ownership, Borrowing & Typestates in Reality Runtimes

## 1. Executive Summary & Philosophy

In conventional game bots and desktop automation scripts, execution state is often scattered across global mutable flags (`is_in_combat`, `current_zone`, `can_act`). This design introduces critical race conditions, phantom actions, and desynchronization failure modes when game reality shifts asynchronously.

In Maelstrom, **real-world capabilities—hardware input actuation, combat turn commitments, zone topology anchors, and environmental observations—are modeled as scarce, non-duplicable linear resources governed by Rust's ownership, borrowing, and borrow-checker semantics**.

By encoding operational authority into Rust's type system, invalid runtime actions are eliminated at compile time:
- A planner cannot dispatch actions from unverified or stale sensor data.
- Two distinct subsystems cannot actuate physical keystrokes concurrently.
- An action cannot execute within a zone after the zone has unloaded.
- A combat turn action cannot be accidentally executed twice in the same planning round.

---

## 2. The Four Pillars of Borrowing in Reality Runtimes

| Rust Concept | Reality Runtime Equivalent | Operational Invariant Guaranteed |
| :--- | :--- | :--- |
| **Shared Borrows (`&T`)** | **Read-Only Concurrent Observation** | Multiple observers (HUD overlays, replay loggers, costmap evaluators, combat planners) can inspect `WorldSnapshot` or `PoseFrame` simultaneously without mutating anything or invalidating concurrent readers. |
| **Mutable Borrows (`&mut T`)** | **Exclusive Control / Mutation** | Only one controller or state updater can mutate a stateful lease, active turn plan, or world-model store at any instant—preventing race conditions and split-brain states. |
| **Move Semantics (`val`)** | **Consumed Authority / Single-Use Transitions** | Once an `InputLeaseToken` or `SingleUseTurnIntent` is moved into an executor, it is consumed by value. It cannot be re-executed, replayed twice, or used after its round has elapsed. |
| **RAII / `Drop`** | **Guaranteed Invariant Cleanup** | Just as dropping a `MutexGuard` releases a lock, dropping an `InputLeaseToken` on interrupt, exception, or early return guarantees all held keys are released and the system safely restores `Idle`. |

---

## 3. Concrete Architectural Realizations (`capability_token.rs`)

### 1. Typestate Lifecycle: `Unverified<T>` $\rightarrow$ `Verified<T>`
Sensory observations from OCR, pixel samplers, or memory probes start as unverified candidate observations. Planners cannot consume them until they undergo explicit verification:

```rust
let raw_obs: Unverified<CombatPresenceFact> = Unverified::new(
    CombatPresenceFact { in_combat: false, sigil_index: None },
    "OCR_Combat_HUD",
    now_ms,
);

// Compiler rejects passing raw_obs directly to an action planner!
// Must explicitly verify with an epistemic validator, confidence, and TTL:
let verified_fact: Verified<CombatPresenceFact> = raw_obs
    .verify(|f| !f.in_combat, 0.95, 2000)
    .expect("Combat verification failed");
```

### 2. Scoped Capability Tokens: `OutOfCombatToken<'a>` & `ZoneLifetimeToken<'zone>`
Actions that strictly require out-of-combat conditions (e.g. potion consumption, deck swapping via `Ctrl+K`, fishing cast initialization, realm hopping via `Ctrl+M`) require holding an `OutOfCombatToken<'a>`. Because this token borrows from the `Verified<CombatPresenceFact>`, it cannot outlive the verification or be used if combat starts:

```rust
pub fn swap_deck_profile<'a>(
    token: OutOfCombatToken<'a>,
    deck_name: &str,
) -> Result<(), String> {
    // Statically guaranteed: character is proven outside combat!
}
```

Similarly, `ZoneLifetimeToken<'zone>` borrows an active manifold presence with phantom lifetime `'zone`. Navigation and POI interaction actions require this token, making actions in unmapped or despawned zones impossible by construction.

### 3. Linear Non-Cloneable Actuation Lease: `InputLeaseToken`
The `InputLeaseToken` represents exclusive ownership of the physical input dispatch pipeline:
- Does not implement `Clone` or `Copy`.
- Consumes itself linearly (`consume_action(self, key, duration, now)`) for single discrete pulses, returning an `ActionExecutionReceipt`.
- Implements `Drop` to guarantee all held keys are released and `Idle` is restored even upon early return or unexpected panic:

```rust
impl Drop for InputLeaseToken {
    fn drop(&mut self) {
        // Enforce cleanup on drop (releasing keys and returning to verified Idle state)
        self.active = false;
    }
}
```

### 4. Single-Use Combat Intent: `SingleUseTurnIntent`
In Wizard101 dueling circles, only one card or pass action may be committed per round. To eliminate the risk of duplicate keystroke emissions or stale turn actions:
- `SingleUseTurnIntent` wraps the chosen card decision.
- Calling `.commit(self, round_number, now)` consumes the intent by value, emitting a `TurnCommitReceipt`.
- Attempting to reuse or re-dispatch the intent produces a compile-time use-after-move error.

---

## 4. Practical Guardrail: Compile-Time vs. Live Reality

A borrow-style model provides **structural clarity and compile-time enforcement of invariants**, not external reality guarantees. Wizard101's server, network packets, and UI rendering can desync or behave unpredictably.

To maintain real-world honesty, compile-time borrow semantics are paired with:
1. **Dynamic Epistemic TTLs**: Facts expire if not refreshed by live sensor streams within their configured time-to-live.
2. **Spatial Residual Monitoring**: Kinematic prediction errors exceeding tolerance trigger automated degradation tiers (Tier 0 $\rightarrow$ Tier 1 $\rightarrow$ Tier 2 Safe Halt).
3. **Deterministic Replay Benchmarking**: Trace corpuses in `maelstrom-replay` validate that type-level constraints correspond to genuine in-game success.
