# BCD-FLAG-EVIDENCE-AUDIT-01 — collisionFlags provenance audit

Status: **AUDIT — no role fact promoted, no code, build, live or game-file change.**
Date: 2026-11-17 · owner: root · workstream: `CORE-CLEANUP`

## 1. Observation

`.local/introspection/bcd-evidence-126.json` (2026-11-15, source for the ramp/classification reports) contains a `collisionFlags` field on 52 mesh records across three zones. **Every non-null value is exactly 13379** (`0x3443`); Olde Town records have `None`. No tracked generator or tool in `tools/` or native `Utilities/` currently writes or consumes this field, so its extraction path is not reproducible from our committed code.

## 2. Cross-reference: not a valid role map

katsuba's `CollisionFlags` (`src/katsuba-bcd/src/lib.rs:21-33`, ISC reference HEAD `7158bf7`) defines bits: `OBJECT=1<<0`, `WALKABLE=1<<1`, `HITSCAN=1<<3`, `LOCAL_PLAYER=1<<4`, `WATER=1<<6`, `CLIENT_OBJECT=1<<7`, `TRIGGER=1<<8`, `FOG=1<<9`, `GOO=1<<10`, `FISH=1<<11`, `MUCK=1<<12`.

`13379 = 0b11010001000011` includes an **undocumented `0x2000` (`1<<13`) bit**. A constant value across water meshes, grass/stone walkways and bushes — including `WALKABLE|WATER|GOO|MUCK` simultaneously — is not a credible per-object role classification. Most likely explanations: a field-offset/endianness misread, or a struct-level word reused for all entries. This is the same class of field-order problem previously found in cylinder dimensions (`W101-SHAPE-ORDER-139`).

## 3. Decision

**Do not promote these values to facts, roles, or walkability evidence.** The existing conservative rule remains authoritative: broad-phase overlap is not an obstruction, slope is a geometric candidate only, and collision roles are unqualified (`W101-CONNECTOR-CLEARANCE-137.md`, `W101-RAMP-127.md`, `W101-NAV-AUDIT-103.md`). The earlier geometry reports used the triangle coordinates, not the flags; those geometric findings are unaffected by this audit.

## 4. What would be needed to make flags usable

1. An owned, bounded raw-BCD reader under a source/build claim, hash-pinning the exact archive member, with a field map cross-checked against a second independent parser (katsuba semantics are a reference only, not copied code).
2. Deterministic fixtures showing distinct, plausible flag values across several meshes/zones before any role inference.
3. Qualification gates: layout/build pin, flag→role mapping evidence, per-mesh provenance, and acceptance tests. No live input or game-file write is required — installed files stay read-only.

## 5. Limits

This audit involved no build, no live session, no import, and no code change. Upstream files were read as untrusted data. The finding is a *negative* one: the current flag field cannot be trusted; no new navigation capability is claimed.
