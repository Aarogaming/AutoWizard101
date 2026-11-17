# REF-STUDY-BCD-ZONE-01 — katsuba/spiraldb vs. our navigation stack

Status: **read-only reference study — no copied code, no build, no live use, no dependency.**
Date: 2026-11-17 · owner: root · workstream: `CORE-REFERENCES`

## 1. Method and boundaries

`.local/upstream/katsuba` (ISC, HEAD `7158bf7`) and `.local/upstream/spiraldb` (CC BY-NC-SA 4.0, HEAD `a0da1d1`) were read as untrusted data. No upstream instruction files were followed, no scripts run, no code copied into tracked paths, no licence granted for integration by this study.

## 2. BCD parsing — concrete gap and no-copy decision

katsuba provides a complete Rust BCD reader (`src/katsuba-bcd/src/lib.rs`): `Bcd`, `Collision`, `ProxyGeometry`, `ProxyMesh`, `GeomParams`, and explicit `CollisionFlags` bits — `OBJECT=1<<0`, `WALKABLE=1<<1`, `HITSCAN=1<<3`, `LOCAL_PLAYER=1<<4`, `WATER=1<<6`, `CLIENT_OBJECT=1<<7`, `TRIGGER=1<<8`, `FOG=1<<9`, `GOO=1<<10`, `FISH=1<<11`, `MUCK=1<<12`.

Our exporter `tools/w101_bcd_evidence.py` works from **already-exported world-space geometry** (translations/rotations/scales/primitives) and deliberately computes conservative enclosing bounds while marking roles unqualified (`roleVerified=False`, `role="conservative-blocker-candidate"` for unsupported primitives). It does **not** read raw BCD binary flags and therefore has no direct evidence of `WALKABLE`/`TRIGGER`/`WATER` roles.

**Finding:** the highest-value reuse is the *semantic fact* that BCD carries role flags — not the parser code. Our evidence model can later add a role fact only from an owned, pinned reader of those flags with provenance; until then, `W101-CONNECTOR-CLEARANCE-137.md`'s rule stands: a broad-phase overlap is not a confirmed obstruction.

**No code copied:** katsuba is ISC (compatible in principle), but any tracked copy still needs its own provenance/licence claim, tests, and bounds hardening. Our independent parser already resolved the cylinder field-order discrepancy (`W101-SHAPE-ORDER-139`), so wholesale replacement is unwarranted.

## 3. Zone topology — licence-blocked data, smaller local subset

spiraldb contains **1,336** `ZoneTransfer/*.json` files with a JSON schema (`schemas/zone-transfer.schema.json`) describing `ZoneName`, `Events`, `Teleports`, `m_destinationZone`, `m_destinationLoc` and transition tags. Our `ZoneTopologyService.cs` is a 55-zone WizardCity graph ported from Automatus (`data/locations.py`); our decoded master topology (3,517 zones / 10,742 links, `ROADMAP.md:124`) is broader but still offline candidate data, not current-client-verified portals.

**Finding:** spiraldb is a richer cross-zone evidence source, but **CC BY-NC-SA 4.0** prevents verbatim incorporation into our GPL repository without a licence decision, and share-alike would constrain derived databases. No copy was made. A future claim could define a *computed, independently derived* zone-edge fixture with hashes and explicit source attribution, subject to owner approval of the licence terms.

## 4. NAV files

katsuba's `katsuba-nav` parses zone NAV graphs into `NavigationNode { location: [f32;3], ... }`. Our `NavigationMap.cs` already parses binary zone.nav data with hard bounds (32,767 vertices / 250,000 edges) and hash-pins the source. Again: parser exists; qualification (edge direction, clearance, current build) remains the open gate (`W101-NAV-AUDIT-103.md:25,46`).

## 5. Conclusion / next gates

No integration is authorized by this study. The actionable ideas are: (a) add role provenance to our BCD evidence through an owned reader if/when a claim allows, and (b) decide licence terms before any spiraldb-derived zone-edge fixture. Neither changes current navigation gates: identity/zone binding, connector clearance, bounded pulses, verified arrival and Idle cleanup.
