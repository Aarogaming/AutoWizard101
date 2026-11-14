# Rust bridge host qualification and installed release

Status: complete for read-only host protocol and conditional installation. W101 was not attached or controlled.

## Verification

No native host was running in the real user session. Started the installed host hidden with an isolated data root, disconnected from games. Rust capabilities/catalog/state/snapshot requests all returned schema-1 success; the host exited 0 through its stop-file protocol. The previous installed host lacked typed aliases, so repeated the checks against current source and again after installation.

The current-source and newly installed hosts both returned 26 typed aliases, all unknown while disconnected. This is actual named-pipe/native-host evidence, not gameplay or connected-wizard telemetry qualification. Installed Rust executable hashes match the package manifest. W101's existing process remained running; no inputs, hooks, attachments, focus changes, capture, volume changes or desktop control occurred.

Local/private evidence:
- Prior installed host: `.local/rust-bridge-60-456c19a185c64a108c146d36dab23e9a/`
- Current source host: `.local/rust-bridge-current-60-eb22850e0a4d487a9d34499ee00bb2fd/`
- Installed host: `.local/rust-bridge-installed-60-d0b66631722b48c68a33d23473456c73/`
- Installer lifecycle: `.local/rust-bridge-60-lifecycle.json`
- Installation receipt: `.local/rust-bridge-60-install.json`

## Release

- Portable folder: `.local/releases/Maelstrom-20261113-230917-f647cf`
- Installer: `.local/releases/Setup-20261113-231023-68abe8/MaelstromSetup.exe`
- Installed directory: `C:/Users/aarog/AppData/Local/Programs/Maelstrom/versions/507efdea5deb419481628cdb02a44745`
- User data stays at `C:/Users/aarog/AppData/Local/Maelstrom`.

`-IncludeRustPreview` includes optional `maelstrom-bridge.exe` and `maelstrom-rust-preview.exe`, their guide and SHA-256 manifest (about 6 MB combined). Native Maelstrom remains the main application and input owner. Neither Rust tool auto-launches or starts gameplay. The CLI accepts only four read-only commands and refuses existing report files.

## Checks and limits

- Native tests: 2,616 passed. Initial restricted run aborted on a testhost connection timeout; normal-user-context rerun passed without raising resource limits.
- Rust tests: 71 passed; Clippy all targets with warnings denied passed; scoped rustfmt passed.
- Installer tests: 14 passed.
- Embedded installer lifecycle: install/update/uninstall and all pack/report/recording/experiment/routine/settings preservation checks passed.
- Packaging script syntax, project board and scoped whitespace checks passed.
- Builds were serialized and capped at two workers. No new production dependency or model was added.

Ordinary ASCII Windows paths are qualified; non-ASCII/device paths remain rejected. Busy pipes fail closed and require manual retry; automatic reconnect is not claimed. The new GUI preview was not opened, so its driver/startup behavior remains unqualified. No claim of live W101 stats or gameplay qualification follows from disconnected host tests.

Gemini adopted the local channel, returned a read-only audit and acknowledged decisions (GEM-0006/0007). The next bounded review is assigned through the same channel. The owner chose to retain Gemini's existing recurring wake-up because there is no external trigger. That separately reported timer is not a Maelstrom service and is not controlled or verified by this session; it grants no source/build/game authority.
