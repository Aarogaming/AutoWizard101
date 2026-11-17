# PARTIAL-REUSE-DOCTRINE-01 — per-capability reuse, not whole-project rejection

Status: **doctrine + owner decision request — no code, build, import, network or live work.**
Date: 2026-11-17 · owner: root · workstream: `CORE-DELIVERY`

## 1. Owner direction

The owner stated: *"If any part of a project is usable, do not discard the entire source because part of it doesn't fit our bill."* Our previous inventory language ("excluded project") was too coarse and is corrected here: **exclusions apply to capabilities, not repositories**.

## 2. Decryption: the boundary is already finer than "no decryption"

`docs/W101_CLIENT_SIDE_EXCEPTION_20261110.md:21`:
> "Network manipulation | Excluded. Do not add live interception, crafted/replayed packets, packet injection or traffic rewriting. Normal communication by the game while it runs is not an extra network-manipulation integration. **Saved-data decoding remains offline research.**"

Therefore:
- **Offline decoding of saved captures is already allowed** — `tools/moonlight_adapter.py` runs Moonlight's CLI on a saved pcap with a local Scapy compatibility shim; `W101_CAPABILITY_INTEGRATION_MAP.md:142` keeps it as optional diagnostics with processing limits, outside the player loop.
- **Reading a decrypted DML schema from a saved archive** is allowed as offline research (On-Wiz docs, w101-proto-go schema vocabulary, wizdiff XML parser).
- **Live interception** — hooking Winsock/Crypto++ to dump plaintext during an active client session — is *not* the same as reading a saved file. It changes how we interact with a live network session, and is currently outside the approved boundary.

The owner may amend that boundary. A precise option, if desired:

> "Passive, read-only decoding of live game traffic through a reviewed, owner-scoped client-side adapter is permitted as diagnostics when it injects no packets, modifies no traffic, and sends no data to any server. Any write/replay/rewrite remains excluded."

This is a **decision request**, not an adopted rule. No live packet tooling was run.

## 3. Per-capability reuse examples (already in practice)

| Project | Rejected capability | Reused capability |
|---|---|---|
| `wiz-packet-map` | live hook/decrypt | historical schema/WAD research |
| `w101-proto-go` | traffic replay/login | protocol type vocabulary |
| `wizwalker` | teleport/launcher paths | pinned read-only adapter + WAD journal reader |
| `Deimos` | teleport/speed hotkeys | combat/dialogue vocabulary, Sprinty steering ideas |
| `GrubNinja` | reward endpoint submissions | vocabulary only; no score-as-outcome |
| `katsuba` | none (offline) | flag semantics compared; no code copied |
| `spiraldb` | verbatim CC BY-NC-SA data copy | schema concepts pending licence decision |

## 4. Audit of Gemini's latest external claims (untrusted data)

| Claim | Finding |
|---|---|
| "wiz_packets only pipes captures, does not send rogue packets" | Plausible but unverified; upstream not cloned. Even if read-only, its Winsock inline hooks are **live interception**, excluded until the owner amends the boundary |
| "GrubNinja forges HTTP/API requests to KI backend" | Consistent with our own qualification: `W101_LEGACY_QUALIFICATION.md:151` records reward-path service calls and explicitly rejects them as gameplay evidence |
| "Deimos has no direct packet manipulation" | Consistent with our boundary; memory writes under the W101 exception are allowed only when reviewed, pinned and owned — our code uses no `WriteProcessMemory` and keeps visual fallback |
| "wizSDK inactive/archived" | Unverified; no upstream fetch in this task. Our manifest points at UnderpaidDev1; status claim not adopted |
| "`Mezoh/w101-couch-potato` active" | **Not found** at that URL (`git ls-remote` returned "Repository not found") — treated as an unverified lead |
| "Recast navmesh ignores baked-in pathing nodes" | External claim; we have not adopted Recast/Detour. `W101-NAV-AUDIT-103.md:157` says zone.nav is not known to be Detour-compatible; no dependency added |
| "xcap already used by Maelstrom" | **False in our tree** — no `xcap` reference in native `Utilities/`, `tools/` or `crates/`; we use `WindowObserver`/`BoundedWindowCapture` |
| "AHK ImageSearch is industry standard" | Subjective external opinion; no dependency or adoption decision based on it |

## 5. Decision requested

1. Confirm or amend the passive-live-decryption sentence in §2. Until then, offline decryption only.
2. Confirm the per-capability reuse doctrine as the replacement for "excluded project" language.

No rule was edited. No upstream instruction file was followed; external sources were read as data only.
