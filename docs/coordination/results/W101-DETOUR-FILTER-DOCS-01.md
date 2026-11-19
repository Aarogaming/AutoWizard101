# Result Evidence: W101-DETOUR-FILTER-DOCS-01

## Task Identity
- **Claim ID**: `W101-DETOUR-FILTER-DOCS-01`
- **Workstream**: `CORE-TRANSITIONS`
- **Task**: Opportunistic Detour Budget Filter Specification
- **Status**: Completed

## 1. Executive Summary
This document registers the specification and design for the Opportunistic Detour Budget Filter in `crates/maelstrom-core/src/costmap.rs` and its integration into `behavior_tree.rs`, allowing the autonomy stack to opportunistically harvest reagents and silver chests without compromising mainline quest progression or pulling mob aggro.

### Key Deliverables
- Authored `docs/OPPORTUNISTIC_DETOUR_FILTER.md` detailing:
  - The Detour Budget Equation: $\Delta C = (C_{\text{detour}} + C_{\text{resume}}) - C_{\text{direct}} < \text{detour\_budget}$.
  - Dual-corridor safety invariants (approach corridor and resumption corridor both verified via `is_corridor_safe_3d`).
  - Threat clamp: peak dynamic repulsive cost along corridors must remain $< 128$ (`COST_PREFERENTIAL_MAX`).
  - Behavior Tree leaf execution cycle: steering, interact pulse, collection verification, and resumption.
- Updated `docs/DESTINATION_NAVIGATION.md` with roadmap point 7.
- Updated `docs/INDEX.md` with links to the new specification.

## 2. Adversarial Red Team / Blue Team Self-Audit
- **Red Team Vector**: A candidate reagent node might sit just outside a mob's static aggro cylinder, but the mob's moving patrol could collide with the player during the interact dwell time.
- **Blue Team Mitigation**: The detour filter computes predicted mob positions across both transit and dwell time horizons ($t_{\text{arrival}} + t_{\text{interact}}$), ensuring the node remains safe throughout the entire harvesting sequence.

## 3. Claim Release
- Claim `W101-DETOUR-FILTER-DOCS-01` released.
- Owned paths:
  - `docs/OPPORTUNISTIC_DETOUR_FILTER.md`
  - `docs/DESTINATION_NAVIGATION.md`
  - `docs/INDEX.md`
  - `docs/coordination/results/W101-DETOUR-FILTER-DOCS-01.md`
