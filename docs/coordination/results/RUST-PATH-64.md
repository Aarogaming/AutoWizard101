# Blank native host root validation

Status: offline source repair complete; not installed.

Rust's Windows absolute-path conversion accepted a tab-only data root, producing an unusable pipe name rather than rejecting the missing host folder. A regression test reproduced this. The native .NET check rejects spaces-only roots but accepts tab/newline-only paths. This is deliberate Rust input validation, not a claim of exact native parity for control-character paths.

The shared Rust normalization entry now rejects trim-empty roots before deriving a pipe name. Nonempty paths are not trimmed; ordinary folder names containing spaces remain supported. No host autodiscovery or path scope expansion was added.

Validation: 45 maelstrom-fallback tests passed, including 19 native bridge cases. Clippy all-targets with warnings denied, scoped rustfmt and project-board checks passed. Two workers, offline only; no host/game/desktop/installation work.

Next: retain this with task63 for the next authorized release. Larger identity/GUI/path support work remains separately scoped.
