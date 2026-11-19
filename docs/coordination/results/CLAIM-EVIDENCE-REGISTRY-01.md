# CLAIM-EVIDENCE-REGISTRY-01 — claims-registry checks in the claim-evidence gate

Date: 2026-11-19 · owner: root · claim: `CLAIM-EVIDENCE-REGISTRY-01`
Ported pattern: `D:\aaroneous-devtools\scripts\check_coordination_evidence.py` (MIT,
read as reference; no code copied verbatim).

## What changed

Extended `tools/check_claim_evidence.py` (which already checked result-doc citations)
with always-on claims-registry checks:

- a released claim must carry a `release_note` containing an evidence marker
  (commit hash, exit code, command or log path);
- every owned path of a released claim must exist on disk and be committed
  (`.local/` paths skipped: gitignored data);
- legacy violations are grandfathered in `tools/claim_evidence_baseline.txt`
  (297 entries generated 2026-11-19);
- a baseline entry that no longer violates is itself an error — the baseline
  can only shrink;
- an unreadable `claims.json` fails the check instead of passing silently.

Result-doc warnings keep their old advisory/`--strict` semantics; registry
violations fail regardless of `--strict`.

## Evidence

- Generated baseline: 297 legacy task IDs, all "release_note has no evidence
  marker"; zero "owned path missing" violations (the `W101-BACKGROUND-API-02`
  phantom paths were already repaired upstream before this run).
- After baselining: `python tools/check_claim_evidence.py --strict` →
  `0 warning(s), 0 new registry violation(s), 0 stale baseline entr(ies)`, exit 0.
- `python -m unittest tools.tests.test_check_claim_evidence` → **17 tests, OK**
  (7 pre-existing report-check tests + 10 new registry tests).

## Limits

- The evidence marker is a heuristic (same class as the upstream script): a note
  containing any 7-40 char hex token or the word "command" passes; it does not
  verify the cited artifact exists.
- Baseline granularity is per task ID: a grandfathered claim that gains a *new*
  kind of violation under the same ID is still grandfathered.
- Nothing here is live qualification; offline tooling only.
