# Maintenance & Resilience (Aaroneous Automation Suite)

## LTS Policy
- Gold build is immutable; changes land on branches only.
- Supported target: net9.0-windows10.0.22621.0 on Windows 10/11.
- Default policy: Public profile; `ALLOW_LIVE_AUTOMATION=false`.

## Support Matrix
- Core app: Supported on Windows 10/11, 1280x720 primary resolution.
- Portable build: DevTools excluded; samples optional.
- Plugins: Manifest-only support; no live automation capabilities permitted by default.

## Deprecation Rules (metadata/plugins only)
- Deprecate via manifest/status fields; no runtime removal in gold.
- No gameplay logic changes in maintenance; metadata-only updates are allowed on content branches.

## Automation & Checks (recommended cadence)
- Weekly: build, UI regression (no baseline update), FunctionalTestRunner, secret scan.
- Archive CI logs per run/version for traceability.

## Determinism & Checksums
- Verify portable build checksums before submission/sharing.
- Keep build inputs pinned (package versions locked).

## Failure Triage
1) Re-run final_verify.ps1.
2) Inspect UI diff report (no baseline change).
3) Check FunctionalTestRunner output.
4) Check secret scan output.
5) If regression confirmed, open maintenance branch; do not touch gold.

## Rollback Procedure
- Prefer revert on maintenance branch; never rewrite gold history.
- If a portable build is bad, discard and rebuild from gold commit.

## Emergency Freeze
- Set branch protections; halt merges.
- Announce freeze and criteria to lift it.

## Dependency Hygiene
- Lock versions; audit licenses periodically.
- Avoid automatic upgrades without review.
