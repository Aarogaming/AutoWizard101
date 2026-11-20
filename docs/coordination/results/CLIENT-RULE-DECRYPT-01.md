# CLIENT-RULE-DECRYPT-01 — passive live decoding approved (owner decision)

Status: **RULE AMENDMENT APPLIED — owner-approved 2026-11-17.**
Date: 2026-11-17 · owner: root · workstream: `CORE-DELIVERY`

## Owner decision (verbatim intent)

"I approve of everything that isn't teleporting, and network manipulation. Don't mess with the server, but the client is fair game. The game scans and patches all of the files every time the launcher runs, so we're not going to be modifying the local files either. We just have to run with the live client and use that to the best it can give us."

## Applied changes

- `AGENTS.md` runtime boundary: passive, read-only decoding of live client traffic through a reviewed, owner-scoped client-side adapter is permitted as diagnostics when it injects nothing, modifies no traffic and sends no data to any server. Saved-capture decoding remains offline research. Coordinate teleporting, crafted/replayed/injected packets, traffic rewriting, auth/session manipulation and other network manipulation remain excluded. Installed game files remain read-only and are not modified because the launcher patches them.
- `docs/W101_CLIENT_SIDE_EXCEPTION_20261110.md`: network-manipulation row reconciled with the same permission and exclusions.

## What remains excluded

Server-side manipulation of any kind · crafted/replayed/injected packets · traffic rewriting · auth/session manipulation · coordinate teleporting · installed-file writes · Crown Shop · volume changes. Client-side reads/hooks/input dispatch remain optional, reviewed, owner-scoped and under the single scheduler/input owner. Takeover, cancellation, recovery holds and cleanup verification are unchanged.

## Scope note

This is a permission, not an implementation. No live packet adapter was created or run, no upstream hook tool was wired, and no build/test/live session occurred for this task. Any future passive-live-decoding adapter needs its own claim, pin, bounded diagnostics and evidence.
