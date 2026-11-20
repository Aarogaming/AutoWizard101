# Disconnected Rust snapshot context

Status: offline source repair complete; not installed.

The fresh timestamp of a retained snapshot could make observation_is_fresh return true even with connected=false. Disconnected snapshots also exposed historical game/target/source as current context. A synthetic regression reproduced the freshness failure before repair.

Freshness now requires connected=true. Disconnected projection clears game, target, source and observation timestamp; original raw_snapshot evidence stays intact for inspection. Numeric/identity alias guards are unchanged.

Validation: 56 tests across maelstrom-fallback and maelstrom-ide passed (18 native bridge cases); Clippy all targets with warnings denied passed; scoped rustfmt and board checks passed. Two workers, offline only, no host/game/desktop/install work. Incremental cache disabled for final checks after restricted-filesystem hardlink warnings.

Next: include in a future authorized release; retain separate qualification for identity metadata and non-ASCII paths.
