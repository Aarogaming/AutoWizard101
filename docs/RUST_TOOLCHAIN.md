# Rust toolchain pin

`rust-toolchain.toml` at the repository root pins the compiler for the Rust workspace (`crates/`): channel `1.100.0`, the
`minimal` profile, plus the `rustfmt` and `clippy` components. `cargo` and `rustc` run anywhere under the repository pick it up
through rustup, including `scripts/build_desktop.ps1`.

## Why

Before the pin the workspace had no declared compiler. A machine used its rustup default (on the development machine, a nightly
from 2026-09-09) and the `rust-tests` CI job used whatever Rust the `windows-latest` image carried. The same commit could be
compiled by different compilers in different places. 1.100.0 is a stable release (2026-11-09), so the pin also drops the nightly
dependency.

## What was checked (2026-11-19, offline, on 1.100.0)

- `cargo test --workspace --offline --locked`: 281 tests across 40 test binaries, 0 failed.
- `cargo clippy --workspace --all-targets --offline --locked -- -D warnings` and `cargo fmt --all -- --check`: clean.
- The release build command from `scripts/build_desktop.ps1` (`maelstrom-bridge` and `maelstrom-rust-preview`, with `--locked`
  added): built. The script itself was not run.

Passing on stable shows the workspace does not need nightly features today. The new `Install pinned Rust toolchain` step in
`.github/workflows/ci.yml` (`rustup show active-toolchain || rustup toolchain install`) has not run on a GitHub runner yet; it is
checked only by its YAML parsing.

## What the pin changes and does not change

- Local builds in this repository now use 1.100.0 instead of the nightly default. The nightly's extra targets
  (`thumbv7em-none-eabihf`, `wasm32-unknown-unknown`, `x86_64-unknown-linux-gnu`) are not installed for 1.100.0. Nothing in the
  repository builds for them: `maelstrom-core` does not build without `std`.
- `rust-version` is deliberately not set in any `Cargo.toml`. The lowest supported compiler has not been measured; the only
  hint is `Option::is_none_or` (used in `maelstrom_bridge.rs` and `native_poll.rs`), which needs at least 1.82.
- CI still runs only `cargo test`; clippy and rustfmt are not CI gates. The .NET SDK is pinned in CI (`9.0.x`); Python is the
  runner's.

## Installing and moving the pin

- First use on a machine: run `rustup toolchain install` in the repository root. Without network, install 1.100.0 with the
  `rustfmt` and `clippy` components beforehand.
- To move to a newer release, change `channel` in `rust-toolchain.toml`, run the three commands above, and update the
  numbers and date in this file.
