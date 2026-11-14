# Gemini coordination channel

This is the local communication path for the active Maelstrom contribution, not a new handoff or a second project plan. Start with root AGENTS.md, ../EXTERNAL_AGENT_GUIDE.md and ../claims.json.

> Current coordination: LIA-0033 supersedes old freezes with the sequential [Gemini queue](QUEUE.md). Confirm root claims and ACK each task, then work without extra permission. Root retains core/build/live/integration ownership.

## Ownership and checks

- Gemini alone appends to FROM_GEMINI.md. The Codex liaison alone appends to TO_GEMINI.md and maintains STATUS.md and this guide. The coordinator alone maintains central claims and the board.
- Read both message files and current claims at task start, before edits, tests or commits, and at task completion. Check at natural work boundaries; do not create a polling service or busy loop.
- Append messages with a unique ID, timestamp including UTC offset, task ID, reply-to ID, kind, evidence, requested action and next step. Preserve previous messages. Use sanitized paths and summaries, never credentials or raw private gameplay data.
- ACK each outbound ID in FROM_GEMINI.md with an explicit accepted scope or conflict. A missing ACK means pending, not acceptance. Mark completion separately from ACK.
- Requests and review recommendations do not grant ownership, new dependencies, live access or a build slot. Only an explicitly attributed coordinator/owner grant does; observe its exact scope and expiry. Ask before expanding.
- The liaison is active during bounded work periods, not a permanent daemon. STATUS records the last actual check and inbound; no reply is not proof someone is watching.

## Current boundaries

The current bounded assignments and exact outputs are in [QUEUE.md](QUEUE.md). Its grants supersede historical read-only/ACK-only notices only for those paths; all other restrictions remain. After READY_FOR_REVIEW, root owns integration and Gemini proceeds to the next preclaimed task.

Historical bridge scope below applies only if that bridge task is explicitly reactivated:

Continue RUST-NATIVE-BRIDGE-57 only within its reserved files. Reuse the native host protocol for capabilities, catalog, state and snapshot. Keep the native scheduler and input owner authoritative. No second input owner, capture process, memory reader rewrite, live game connection, host launch, installer or model. Source/mock success is not live gameplay qualification. Existing simulations must remain visibly simulations.

Request the shared build slot before tests. Any explicit grant in TO_GEMINI.md is bounded and must be acknowledged before use; release it in the completion reply. No new production dependencies without approval. Do not commit another contributor's files.

## Message shape

ID: GEM-0001 (Gemini) / LIA-0001 (liaison)
Time: ISO 8601 with offset
Task: registered task ID
Reply-to: exact ID or none
Kind: ACK / QUESTION / PROGRESS / READY_FOR_REVIEW / BLOCKED (Gemini); REQUEST / REVIEW_REQUEST / COORDINATOR_GRANT (liaison)
Evidence: exact changed paths, commit if any, actual checks and limits
Action: requested decision or work; distinguish proposal from authorization
Next: bounded next step

Use STATUS.md as an index, not as a substitute for the message record.
