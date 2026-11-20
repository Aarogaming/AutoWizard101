# Optional Rust host observer

Maelstrom.exe remains the normal app and native scheduler/input owner. The Rust
tools do not replace it, start a host, attach a game, or send gameplay actions.

`maelstrom-rust-preview.exe` opens an optional prototype workspace. In its native
host telemetry pane, enter the data folder of an already-running native deck host
and choose Observe. Requests run at most once per second, one at a time. Closing
the pane stops observation. Preview deck/attach controls are simulations.

For headless read-only checks:

```text
maelstrom-bridge.exe <data-root> snapshot <new-report.json>
```

The other permitted commands are `capabilities`, `catalog`, and `state`. Reports
must be new files; existing files are not overwritten. A zero exit code means the
host accepted the read, not that a game is connected or a routine succeeded.
`--help` shows syntax. Reports can include game observations; keep them private.

Absent, stale, invalid, or mismatched facts stay unknown. Current typed aliases
expire by source time after two seconds. Ordinary ASCII Windows paths are
qualified; non-ASCII/device paths are explicitly rejected pending parity work.
The installed host version may expose fewer fields than current source.

Build packaging with `scripts/build_desktop.ps1 -Zip -NoRestore -IncludeRustPreview`.
This builds the existing Rust dependencies offline with two workers and includes
both optional tools plus hashes. No tool auto-launches after installation. The
normal installer preserves packs, reports, and experiments.

## Busy-host recovery and passive game telemetry

Source update RUST-BUSY-TELEMETRY-61 retries only Windows ERROR_PIPE_BUSY (231)
while opening the pipe, within a 1.5-second deadline. Backoff is 25/50/100 ms
(capped at 100 ms), with cancellation checks. Missing or denied pipes fail
immediately. Sent requests are never retried or replayed.

Successful snapshot CLI reports include a typed `projection` alongside the original
host response. Unknown and expired facts remain unknown. Passive native observations
can now publish facts while the wizard is Idle or in a menu; action readiness remains
separately enforced. Source-host live telemetry, expiry and clean disconnect were
verified without inputs or focus changes. This repair is not yet installed.
