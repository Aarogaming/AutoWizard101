# ONTOLOGY-CENSUS-01 — Phase 1 repository census and ontology anchor

Date: 2026-11-18 · owner: root · state: released
Claim registered at `c2c3431`; docs committed with this release.

## Scope (as claimed)

Owner-directed after review of the third-party normalization manifesto and the
aligned architecture audit. Phase 1 only: create `docs/architecture/` with the
six-primitive anchor and a verified subsystem census. No code changes.

## Deliverables

- `docs/architecture/ONTOLOGY_NORMALIZATION.md` — Facts != Authority != Actions
  invariant with enforcement points; six-primitives table; leak register (both
  recorded leaks with the fabricated `expiresUtcTicks` field explicitly corrected
  and the nonexistent `Qualified` fact tier called out); provenance section.
- `docs/architecture/REPOSITORY_CENSUS.md` — 57-row census table (subsystem, path,
  purpose, category, primitives, evidence) plus caveats. Gitignored `.local` data
  flagged as non-evidence for clean checkouts.
- `docs/INDEX.md` — two new entries in the documentation map.

## Verification evidence

- **Path existence**: automated check over every path mentioned in the census —
  **165 checked, 0 missing** (run 2026-11-18, output recorded in the claim session).
- **Named-class existence** for the audit's cited helpers: `ZoneTopologyService`,
  `CircuitSnapshot`, `CapturedWindowFrame`, `PathFollower`, `WaypointFeedback`,
  `OperationFocusBudget`, `ManualCaptureController` — all located; none missing.
- **Corrections to the source audits** (recorded, not propagated): no `Qualified`
  fact tier (`W101FactsStore.cs:5` is `{Observed, Candidate, Unknown}`); no
  `expiresUtcTicks` field; `UiCompatibilityError` is Python-only; three fabricated
  file references in the Gemini status report (see
  `ARCH-REF-REVIEW-84.md`).
- **Gates**: `python tools/check_project_board.py` → valid, 30 workstreams;
  `python tools/check_text_encoding.py --quiet` → 0 violations.

## Not claimed

No refactor, no restructure, no code change, no test-suite change, no live/game
work. Each entry in the leak register needs its own claim and evidence before any
change is made.
