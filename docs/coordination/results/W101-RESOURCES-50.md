# W101 resource registry foundation

2026-11-12 Â· CORE-DELIVERY Â· offline integration verified; live producer pending.

Added a bounded sequential read-only collector reusing imported wizwalker current_hitpoints/max_hitpoints, current_mana/max_mana, current_energy/energy_max and potion_charge/potion_max. It owns no process/hook and refuses unqualified invocation. Reads each pair twice, omits changing or invalid pairs, and discards a timed-out batch. This helper is not yet invoked by the live UI worker: qualification/ownership of the exact-build stats hook remains required.

Native optional resource evidence maps into health/mana/energy/potions, .max and .percent. Health.Current/Health.Max/Health.Percent and equivalent aliases resolve canonical facts. Current/max must share the same acquisition stamp as their containing observation; missing fields stay unknown. Potions preserve fractional charge. Result-to-deck and recorded-evidence transforms share the resource validator. Registry producers remain unavailable until live qualification; no current resource value was claimed.

2,587 native tests passed after fixing a capability payload overflow: common definitions are emitted once, with routineFacts containing only extensions. Three mock Python resource tests pass. Scoped formatting/analyzers and Python formatting/syntax checks passed. No installation.

Actual current stats hook, final energy maximum semantics and visible 4/4 potion comparison remain unqualified. The separate bounded existing UI observer audit is recorded in W101_DATA_SUPPLY.md; it did not run this new collector.
