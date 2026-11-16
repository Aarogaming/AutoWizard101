# Dance replay pairing check 143

Date: 2026-11-15. Offline only. Existing native executable; no source edit, build, installation, game process, or desktop dispatch.

## Inputs and exact result

Routine resource `pet-dance-visit.json` (`routine_id=pet-dance-visit`) was replayed against the Dance session fixture (`session_id=routine-dance-synthetic`). It stopped after one proposal with `dispatch-unconfirmed`: the fixture receipts name `routine-dance-synthetic/pet-dance-visit-draft/1`, not `.../pet-dance-visit/1`. Report: `.local/dance-visit-replay-heartbeat-20261115.json`, graph SHA-256 `c81e20c7f9551da51417f493ab2fcd55bdf19b327de507cb439fccedc291e914`, event SHA-256 `a886acd01b6bd64c0e3c912d2629dbe8f21b99dcc2b53cfb6e2d1b46fe86c884`.

The matching fixture `pet-dance-visit-draft.json` with the same Dance session completed `observed-completion`: 8 proposals, 8 receipts, 0 desktop dispatches, `live_qualified=false`. Report: `.local/dance-draft-replay-heartbeat-20261115.json`, graph SHA-256 `a560e439f8da1ae80a9e807cf0f4c52f904a9cb763efe4ffb6f335a4ccca48c2`; same event hash.

## Interpretation

The replay guard correctly rejects mismatched proposal IDs. The fixture only qualifies the draft graph's synthetic transition contract. It does not qualify the fuller `pet-dance-visit` routine, entry detectors, first-arrow continuity, live approach, result handling, or a real Dance visit. Preserve both outcomes; do not rewrite fixture IDs to force the newer graph to pass without deriving the expected transitions from reviewed evidence.

Next concrete gap: create a reviewed labelled session for the exact full-visit graph or explicitly map the draft fixture to its supported scope in the editor/replay UI so users do not mistake a successful draft replay for qualification of the full visit.
