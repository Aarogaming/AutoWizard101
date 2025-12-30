# Architecture Overview

## High-level system
- WinForms app (C#/.NET) with script runner and OCR-driven utilities.
- Capability-based plugin host: plugins declare capabilities; policy enforces allowed/blocked actions.
- Offline-first: simulation-only by default; no live automation; external data is cached/optional.
- Separation of concerns:
  - Runtime logic (gold): immutable baseline (v1.0.0-gold)
  - UX line: cosmetic-only improvements (v1.1.0-ux)
  - Tooling: DevTools-only handoff/automation (v1.0.2-tools)

## Capability + policy enforcement
- Capabilities gated by profile/policy (Public default, Experimental gated; live automation disabled by default).
- Executors respect policy boundary (ALLOW_LIVE_AUTOMATION=false) and do not bypass it.
- Plugin capabilities (e.g., MinigameCatalog) are read-only; live integration is explicitly excluded.

## Data + offline posture
- OCR + template matching run locally; no network required at runtime (except optional OCR key).
- Cached/reference data (e.g., wiki data) is used offline; no live scraping in production.
- Secret hygiene: no tokens stored; secret scan tooling in place.

## UI and UX tracks
- Gold UI baselines are immutable; UX V1 is cosmetic-only and tagged separately.
- UX artifacts (captures/zips) live under artifacts/ux/** and are ignored.
- No tab-order/behavior changes in UX line; cosmetic diffs only.

## Tooling separation
- DevTools (handoff bridge, selftest, CI gate) are tagged separately (v1.0.x-tools) and excluded from portable builds.
- Tooling outputs remain in artifacts/handoff/** and are ignored.

## Release boundaries
- Gold tag never changes.
- UX tags capture cosmetic milestones without affecting gold.
- Tooling tags capture DevTools versions; CI/selftest guarded.
