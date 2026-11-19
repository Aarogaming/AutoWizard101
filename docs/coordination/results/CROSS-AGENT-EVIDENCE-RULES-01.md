# CROSS-AGENT-EVIDENCE-RULES-01 — relayed results need the evidence packet

Date: 2026-11-19 · owner: root · claim: `CROSS-AGENT-EVIDENCE-RULES-01`
Source pattern: `D:\aaroneous-devtools\governance\CROSS_AGENT_COORDINATION_PROTOCOL.md`
(MIT; rules extracted, no prose copied).

## What changed

Added rule 7 to the "Evidence contract" section of
`docs/coordination/EXTERNAL_AGENT_GUIDE.md`: a subagent's or another session's
"done"/"gate green" is not evidence in a report until the exact command, exit
code or quoted success line, and a retained log path or commit hash, are carried
across the identity boundary. This closes the gap the source protocol documented
(five recurring self-certification incidents): rule 1 covered only numbers the
reporter ran *themselves*, leaving relayed results unchecked.

## Proposal not adopted

The source protocol also expires claims after 24 hours without an update. Not
inserted: `claims.json` has no expiry mechanism, and an unenforced rule is worse
than none. If wanted, it needs a board-checker change under its own claim.

## Evidence

- Edit is one numbered rule in an existing section; no code changed.
- `python tools/check_project_board.py` → valid, 30 workstreams.
- `python tools/check_text_encoding.py --quiet` → 0 violations.
- `python tools/check_response_format.py --strict` → 0 warnings.

## Limits

- Documentation only: the rule binds agents by review, not by gate (the gates
  check artifacts, not who produced them).
- Not proven to change any agent's behaviour; effectiveness is unmeasured.
