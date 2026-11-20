# BACKGROUND-ACTION-CONSENT-01 — where background actions become approvable

Status: **DESIGN — owner approval required; no code or live work.**
Date: 2026-11-17 · owner: root · workstream: `CORE-DELIVERY`

## 1. Principle

Owner consent can authorize a background action, but consent must be **explicit, per-session, scoped to an operation class, revocable, and checked at admission**. It must never be inferred from "the window is unfocused" (that is an observation fact, not permission), and it must never waive takeover, recovery holds, cleanup verification or single ownership.

## 2. The existing choke points (no new controller)

| Layer | Current gate | Where consent belongs |
| --- | --- | --- |
| `W101InteractionPolicy.cs:3-41` | Classifies tool/method (`VisibleUi`, `ClientInternals`, `LiveGameTraffic`, `UnreviewedCode`); live traffic always blocked | Add a **background-input permission class**, default denied. Consent flips it only for a named session/operation; never for `LiveGameTraffic` or game-file writes |
| `OperationScheduler.cs:10,107,215,253` | `OperationRequest.WritesGame`, target/pins/budget validation, recovery generation, `GameInputLease.Acquire` at dispatch | Add `BackgroundConsent` (session ID + scope + expiry) to `OperationRequest`; validate at **admission** (`Submit`) and recheck at dispatch. Missing/expired/revoked consent rejects with a typed reason; no silent fallback to foreground mode |
| `GameInputLease.cs:30-49` | One lease per user, `CanDispatch`, recovery hold latch | Add an internal `background` mode that keeps **one** owner, requires a valid consent token, and still reacts to activity/takeover hooks even when W101 is unfocused. No second lease, no quiet-period auto-resume |
| `JobDeck.cs:8-11,101` / `JobDeckRoutines` | Card settings, `FactBlock`, trusted adapters, no code from cards | Add a card-level `background:false` default. A deck card can request background execution only if its adapter is trusted *and* the session has matching consent; otherwise it stays unavailable with a visible reason |
| `JobFactCatalog` / capability catalog | Fact availability + producer flags | Advertise `input.background` as unavailable until consent; the UI/MCP must show the same availability as the native owner |
| `MaelstromCli.cs` / MCP tools | Explicit opt-in flags, exact target identity | Any background flag must be an explicit per-invocation argument with bounded scope; never a sticky global setting |

## 3. Required invariants when approved

1. **One owner**: background mode uses the same scheduler/lease; no second controller.
2. **Takeover still wins**: keyboard/mouse activity, Escape and explicit Stop cancel immediately even if the game is unfocused; no "resume after 3 s quiet" for input actions (the NORMAL quiet-resume rule applies to dispatch gating in the manual-capture controller, not to waiving takeover).
3. **Recovery latch**: any uncertain cleanup blocks all future background actions until verified restore — no self-clear.
4. **Target fence**: exact HWND/PID/start-ticks/image hash; process replacement invalidates consent with the session.
5. **Bounded**: operation class, duration, action count, resource budgets and explicit expiry on the consent token.
6. **No network manipulation, no game-file writes, no Crown/snack/feed/volume/restart actions** — unchanged.

## 4. Proposed consent contract (data only)

```
BackgroundConsent(session_id, allowed_operations[], target_identity, image_sha256,
                  issued_utc, expires_utc, revoked_utc?, owner_confirmation)
```

Revocation is immediate and checked at each dispatch, not only at admission. Receipts record which consent authorized each background operation.

## 5. Product rule amendment for owner approval

> "Background input actions are permitted only under an explicit, revocable, per-session consent that names the operation class, exact target and expiry. Consent is validated at admission and re-checked at dispatch by the single native owner; it never waives takeover, recovery holds, cleanup verification, or the exclusions on network manipulation, purchases, feeding and game-file writes."

## 6. Limits

This document changes no code or policy. Implementation requires a new claim with source/build authority and focused tests, and any live use requires separate owner authorization. Observation without foreground is already qualified (`W101-BG-QUAL-LIVE-01.md`) and needs no background-input consent.
