# SCOPE-CLARIFICATION-01 — background tasks vs. authorized live play

Status: **PROPOSED RULE CLARIFICATION — owner approval required before AGENTS.md edit.**
Date: 2026-11-17 · owner: root · workstream: `CORE-DELIVERY`

## 1. What the rules actually say today

- `AGENTS.md:41`: "Do not touch volume, feed snacks, spend Crowns, restart the client, or take desktop control **as part of a background task**."
- `AGENTS.md:42`: live tests need explicit coordinator scope and current user authorization.
- `docs/PRODUCT.md:74`: snack preferences default to no consumption; **Crown Shop purchases are excluded** from the Pet Dance operation.

So the blanket reading ("Maelstrom may never feed or restart") is **not** the written rule. Those acts are forbidden in *background* work; in an explicitly authorized live session they are policy choices, bounded by the owner's scope.

## 2. Owner direction 2026-11-17

The owner explicitly said they have no problem with game restarts or pet feeding, and that the rules were perhaps made too strict in the effort to maximize safety: "We're just making a game player." This is a scope clarification, not a removal of takeover, ownership, cleanup, or evidence rules.

## 3. Proposed AGENTS.md amendment (verbatim, for approval)

Replace the line with:

> Do not touch volume, spend Crowns, take desktop control, feed snacks, or restart the client **as part of a background task**. In an explicitly authorized live session, bounded snack feeding and client restart are permitted only when the owner's scope names them: feeding requires an exact item/quantity policy, verified inventory and observed post-condition; restart requires a clean release, invalidation of the old target identity and re-verification before any further action. Crown Shop purchases remain excluded by the product charter. Neither permission waives user takeover, cancellation, recovery holds or verified cleanup.

## 4. What does NOT change

- One scheduler / one input owner; takeover, Escape/Stop, recovery latches, cleanup verification.
- No network manipulation; installed game files read-only.
- No background-task-desktop control; live tests still need explicit scope and current authorization.
- Default snack policy remains no consumption; Crown Shop stays excluded unless the owner separately amends the charter.

## 5. Downstream work after approval

Update `docs/PRODUCT.md` scope notes, the Pet Dance no-feed default (keep default, allow explicit policy), and any card/routine capability flags that currently hard-block feeding/restart. Focused offline tests first; live use only under a named owner scope. No such implementation is authorized by this document.
