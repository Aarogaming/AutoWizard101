# Architectural Doctrine: Ownership, Borrowing & Typestates in Reality Runtimes

> **Status (audit 2026-11-20, Claude): draft design, not an enforced architecture.** `crates/maelstrom-core/src/capability_token.rs` is real library code with offline tests, and exactly one property is real:
> `InputLeaseToken` and `SingleUseTurnIntent` cannot be cloned and are consumed by value, so reuse is a compile error (checked with `rustc`: E0382 for reuse, E0599 for `.clone()`; pinned by doctests).
> Nothing else below is true of the running system: no code outside the module and its tests uses a token, no actuator takes a lease, no planner takes a `Verified` fact, and the types can be bypassed
> (`Verified::new_verified`, `pub` fields, `Deserialize`). Corrections are inline under **Today**. The text comes from a pasted note by a Gemini browser session. Input ownership in the running
> system is the C# `GameInputLease` (one owner per Windows user, a lock file with recovery holds) and, for key release, the `InputActuator::release_all_keys` method in `maelstrom-platform`; neither uses these
> types. Evidence: [CORE-BORROW-SEMANTICS-01](../coordination/results/CORE-BORROW-SEMANTICS-01.md).

## 1. Executive Summary & Philosophy

In conventional game bots and desktop automation scripts, execution state is often scattered across global mutable flags (`is_in_combat`, `current_zone`, `can_act`). This design introduces critical race conditions, phantom actions, and desynchronization failure modes when game reality shifts asynchronously.

In Maelstrom, **real-world capabilities—hardware input actuation, combat turn commitments, zone topology anchors, and environmental observations—are modeled as scarce, non-duplicable linear resources governed by Rust's ownership, borrowing, and borrow-checker semantics**.

By encoding operational authority into Rust's type system, invalid runtime actions are eliminated at compile time:
- A planner cannot dispatch actions from unverified or stale sensor data.
- Two distinct subsystems cannot actuate physical keystrokes concurrently.
- An action cannot execute within a zone after the zone has unloaded.
- A combat turn action cannot be accidentally executed twice in the same planning round.

**Today:** none of the four bullets holds for the running system, because nothing uses the types, and "eliminated at compile time" is withdrawn. Inside the module, checked with a throwaway probe:
(1) nothing stops a planner using unverified data: no function takes a `Verified`, and a `Verified` can be forged (one deserialized from JSON with `ttl_ms` set to `u64::MAX`, never passed through `verify`,
was fresh at 10^12 ms and yielded an `OutOfCombatToken`); (2) two leases, even with the same `lease_id`, were both valid and both returned receipts; (3) a `ZoneLifetimeToken` takes any `&str`, so it cannot
know a zone unloaded; (4) true only for the same intent *value*: two different intents committed for round 3 gave two round-3 receipts. Only (4), narrowed that way, is a compile-time property. The race-condition
story in the first paragraph is motivation; nothing in this repository measures it.

---

## 2. The Four Pillars of Borrowing in Reality Runtimes

| Rust Concept | Reality Runtime Equivalent | Operational Invariant Guaranteed |
| :--- | :--- | :--- |
| **Shared Borrows (`&T`)** | **Read-Only Concurrent Observation** | Multiple observers (HUD overlays, replay loggers, costmap evaluators, combat planners) can inspect `WorldSnapshot` or `PoseFrame` simultaneously without mutating anything or invalidating concurrent readers. |
| **Mutable Borrows (`&mut T`)** | **Exclusive Control / Mutation** | Only one controller or state updater can mutate a stateful lease, active turn plan, or world-model store at any instant—preventing race conditions and split-brain states. |
| **Move Semantics (`val`)** | **Consumed Authority / Single-Use Transitions** | Once an `InputLeaseToken` or `SingleUseTurnIntent` is moved into an executor, it is consumed by value. It cannot be re-executed, replayed twice, or used after its round has elapsed. |
| **RAII / `Drop`** | **Guaranteed Invariant Cleanup** | Just as dropping a `MutexGuard` releases a lock, dropping an `InputLeaseToken` on interrupt, exception, or early return guarantees all held keys are released and the system safely restores `Idle`. |

**Today:** the table states Rust's general rules, not a property of this code. No type named `WorldSnapshot` or `PoseFrame` exists anywhere in the repository (both names come from the pasted note), and no API
in the module takes `&mut self` on a lease or a plan. The move row holds only in the narrow compile-time sense of the status banner (a lease also expires by clock, a runtime check). The `Drop` row is false for
`InputLeaseToken`: its `Drop` clears a private flag and releases no key. The `Drop` impls in this repository that clean up something real are elsewhere (for example `JobObjectGuard` in `maelstrom-platform`,
which closes a Windows job handle).

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

**Today:** the comment "Compiler rejects passing raw_obs directly to an action planner!" describes a planner that does not exist: no function takes an `Unverified` or a `Verified`. `verify` is also not the only
way to a `Verified`: `Verified::new_verified` is `pub` (the module's own tests use it), `confidence`, `verified_at_ms` and `ttl_ms` are `pub` fields (a TTL can be edited afterwards) and `Deserialize` is derived.
The TTL runs from the observation's own timestamp, not from the `verify` call (the API has no clock). Confidence is clamped to 0..1, except that a `NaN` passes through unchanged. Unit tests added on 2026-11-20 pin
the inclusive freshness boundary, the capture-time TTL and the clamp.

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

**Today:** `swap_deck_profile` is an illustration; no such function exists and nothing in the repository requires an `OutOfCombatToken`. `Ctrl+K` and `Ctrl+M` are unsourced (see
[EPISTEMIC_RUNTIME_HARDENING.md](../EPISTEMIC_RUNTIME_HARDENING.md): they trace to Gemini notes, not to the game). The token proves only that the fact was fresh and out of combat *when it was acquired*: it has no
methods, never re-checks, and a newer fact saying "in combat" does not invalidate it (probe: an old token stayed held while a newer fact said in combat), so "cannot be used if combat starts" is withdrawn.
`ZoneLifetimeToken` has no manifold: it holds a `&str` and an epoch, so "impossible by construction" is withdrawn too.

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

**Today:** verified: no `Clone` or `Copy`, and `consume_action` moves the lease. Not true: "exclusive ownership of the physical input dispatch pipeline" (`acquire` is `pub` and keeps no registry; two leases
with the same id were both valid and both returned receipts); "linear" (the lease is affine: dropping an unused one is allowed); and "Drop guarantees all held keys are released and `Idle` is restored" (the
`Drop` is a no-op for the outside world and the module touches no input device; the source comment now says so). `consume_action` dispatches nothing, and `success: true` in its receipt means only that the lease
had not expired. Exclusive input ownership is the C# `GameInputLease`, a runtime lock, not a compile-time type.

### 4. Single-Use Combat Intent: `SingleUseTurnIntent`
In Wizard101 dueling circles, only one card or pass action may be committed per round. To eliminate the risk of duplicate keystroke emissions or stale turn actions:
- `SingleUseTurnIntent` wraps the chosen card decision.
- Calling `.commit(self, round_number, now)` consumes the intent by value, emitting a `TurnCommitReceipt`.
- Attempting to reuse or re-dispatch the intent produces a compile-time use-after-move error.

**Today:** verified (E0382, pinned by a doctest). The guarantee is per intent *value*: `round_number` is a caller-supplied number that nothing records, so "only one card or pass per round" is not enforced here
(two different intents committed for the same round both succeeded). Nothing builds a `SingleUseTurnIntent`; combat turns run through the C# `W101CombatTurnOperation`, which does not use this type.

---

## 4. Practical Guardrail: Compile-Time vs. Live Reality

A borrow-style model provides **structural clarity and compile-time enforcement of invariants**, not external reality guarantees. Wizard101's server, network packets, and UI rendering can desync or behave unpredictably.

To maintain real-world honesty, compile-time borrow semantics are paired with:
1. **Dynamic Epistemic TTLs**: Facts expire if not refreshed by live sensor streams within their configured time-to-live.
2. **Spatial Residual Monitoring**: Kinematic prediction errors exceeding tolerance trigger automated degradation tiers (Tier 0 $\rightarrow$ Tier 1 $\rightarrow$ Tier 2 Safe Halt).
3. **Deterministic Replay Benchmarking**: Trace corpuses in `maelstrom-replay` validate that type-level constraints correspond to genuine in-game success.

**Today:** (1) TTLs exist in the module, but nothing refreshes or reads them outside its tests. (2) The degradation ladder lives in `ontology.rs`, and that file's own doc comments say nothing releases an
input lease or parks the character at Tier 2. (3) False as written: the replay harness does not use these types and cannot show that a type-level constraint matches in-game success; its committed matrix is
also mostly vacuous (see the banner on [REPLAY-BENCHMARK-MATRIX.md](../coordination/results/REPLAY-BENCHMARK-MATRIX.md)).
