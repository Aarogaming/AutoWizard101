# Decisions

## Key decisions
- Gold baseline immutability: v1.0.0-gold is the canonical runtime/policy baseline; never retagged.
- Capability-driven enforcement: policy gates capabilities; live automation remains disabled by default (ALLOW_LIVE_AUTOMATION=false).
- Offline-first posture: no live scraping; optional OCR key; cached/reference data only.
- UX line isolation: cosmetic-only UX work tagged separately (v1.1.0-ux); no behavior/tab-order changes.
- Tooling isolation: DevTools handoff/automation tagged separately (v1.0.2-tools); CI/selftest guarded; excluded from portable builds.

## Rejected alternatives
- Merging UX into gold: rejected to preserve canonical baseline and audit clarity.
- Allowing live automation by default: rejected to maintain safety and policy compliance.
- Embedding tokens/secrets in repo: rejected; secret scan + redaction enforced.
- Combining tooling with runtime packaging: rejected; DevTools stay out of portable builds to reduce risk and size.

## How this prevents scope creep
- Gold stays frozen; new work happens on branches/tags with explicit scope (UX/tooling).
- Policy boundary remains explicit (live disabled by default); capabilities stay read-only where required.
- Tooling changes are gated by CI/selftest and tagged separately; they cannot alter runtime.
- UX tags capture cosmetic milestones without altering behavior, keeping audit trails clean.
