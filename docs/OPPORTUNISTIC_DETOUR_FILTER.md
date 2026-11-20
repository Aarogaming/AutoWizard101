# Opportunistic Detour Budget Filter

> **Status: design note, partly aspirational (audit 2026-11-19, Claude).** In code today: the budget equation and the
> two-leg corridor check (`evaluate_detour_candidate`, `is_corridor_safe_3d`, `find_best_detour` in
> `crates/maelstrom-core/src/costmap.rs`) and a detour branch in the behavior tree that emits proposal strings
> (`behavior_tree.rs`); offline unit tests pass; nothing calls them and nothing was run against the game. **Not
> implemented:** zone surface penalties in `C` (the code uses plain 3D distance), the Section 3 peak threat clamp, dwell-time
> prediction, the 30-second blacklist, Pure Pursuit steering, the `X` interact pulse and collection verification, and the
> Section 4 priority order. **Corridor check:** it examines every mob at its predicted position (an earlier version only saw mobs
> that passed an index broad phase on current position and `repulsion_radius`, and could miss a mob whose aggro reaches the
> corridor; fixed 2026-11-19), but it uses one arrival time for the whole corridor, so a mob is not tracked along the walk.
> **Unsourced:** the 25 and
> 5.0 unit values and the health wisps; three of the reagent names come from a comment in a separate C# card, not from
> the Rust code. Details:
> [W101-DETOUR-FILTER-CORE-02, Audit note](coordination/results/W101-DETOUR-FILTER-CORE-02.md).

## 1. Architectural Purpose

While navigating along mainline quest routes, the player frequently passes within harvesting proximity of reagents (Black Lotus, Deep Mushroom, Frost Flower, Ore, Red Mandrake), silver chests, and health wisps. Blindly pathing to every entity across the zone derails quest progression and risks pulling street patrols. Conversely, ignoring nearby resources wastes free gold and crafting components.

The **Opportunistic Detour Budget Filter** introduces a mathematically bounded decision gate:
1. Queries the 3D spatial index (`SpatialTree3D`) for candidate entities within an acquisition radius ($R_{\text{detect}} \le 25\text{ units}$).
2. Evaluates the differential path cost against a strict **Detour Budget**.
3. Verifies that both the detour and resumption corridors remain completely clear of enemy mob aggro cylinders.

```
Mainline Route: P_curr ─────────────────────────────────────────> P_resume
                   \                                             /
                    \ C(P_curr -> P_detour)     C(P_detour -> P_resume)
                     \                                         /
                      ▼                                       /
                              [ Candidate Node ]
                             (Reagent / SilverChest)
```

---

## 2. The Detour Budget Equation

A candidate detour at $\mathbf{p}_{\text{detour}}$ is approved if and only if:

$$\Delta C = \left( C(\mathbf{p}_{\text{curr}} \to \mathbf{p}_{\text{detour}}) + C(\mathbf{p}_{\text{detour}} \to \mathbf{p}_{\text{resume}}) \right) - C(\mathbf{p}_{\text{curr}} \to \mathbf{p}_{\text{resume}}) < \text{detour\_budget}$$

Where:
- $\mathbf{p}_{\text{curr}}$ is the player's current position.
- $\mathbf{p}_{\text{resume}}$ is the next scheduled waypoint along the mainline route.
- $C(\mathbf{a} \to \mathbf{b})$ evaluates combined Euclidean distance plus zone surface penalties ($P_{\text{street}}$ vs $P_{\text{sidewalk}}$).
- $\text{detour\_budget}$ is a configurable threshold (default: $35.0\text{ cost units}$).

---

## 3. Corridor Threat & Aggro Thresholding

Even if $\Delta C < \text{budget}$, a detour is strictly **vetoed** if approaching or returning from the candidate node risks combat:

1. **Approach Corridor**: The transit cylinder from $\mathbf{p}_{\text{curr}}$ to $\mathbf{p}_{\text{detour}}$ with radius $R_{\text{player}} = 5.0\text{ units}$ must satisfy `is_corridor_safe_3d == true`.
2. **Resumption Corridor**: The transit cylinder from $\mathbf{p}_{\text{detour}}$ to $\mathbf{p}_{\text{resume}}$ must also satisfy `is_corridor_safe_3d == true`.
3. **Peak Threat Clamp**: The peak dynamic mob repulsive cost along both corridors must stay below `COST_PREFERENTIAL_MAX` ($< 128$). Any proximity to an aggro radius ($Cost \ge 253$) instantly rejects the candidate.

---

## 4. Behavior Tree Execution Cycle

The detour filter integrates into Tier 2 tactical execution as a dedicated behavior tree leaf (`Branch: OpportunisticDetour`):

```
       [ Behavior Tree Root ]
                 │
       ┌─────────┴─────────┐
       ▼ (Priority 1)      ▼ (Priority 2)
  [ In Combat? ]     [ Vitals Low? ]
       │                   │
       ▼ (Priority 3)      ▼ (Priority 4)
  [ Opportunistic Detour? ] ──> [ Mainline Waypoint Navigation ]
       │
       ├──> 1. Check: Detour candidate approved? (Budget & Threat)
       ├──> 2. Action: Steer to candidate node (Pure Pursuit)
       ├──> 3. Condition: Within interact radius (d <= 3.5 units)
       ├──> 4. Action: Dispatch 'X' interact pulse
       └──> 5. Action: Clear candidate from blackboard -> Resume mainline
```

### Safety & Preemption Invariants
- If `in_combat` flips to `true` while traveling toward a detour node, the Behavior Tree immediately preempts the detour leaf in the same tick, halting `W` and transitioning to the Combat Subtree.
- Harvested or depleted nodes are placed on a 30-second local blacklist to prevent oscillation around empty anchors.
