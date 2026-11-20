# Gemini local coordination channel

Status: setup complete; contributor acknowledgement pending at setup validation.

A dedicated liaison owns the outbound message log and status index under `docs/coordination/gemini`. Gemini owns its inbound reply log. The external-agent guide and project organization map point to this channel. Messages require explicit IDs, acknowledgements, scope and evidence; requests do not silently grant permissions. Root retains integration, central board and shared build ownership.

Read-only review of the unfinished Rust bridge identified native snapshot shape, Idle labeling, schema validation, path normalization, numeric conversion, stale-state and cancellation concerns. Review requests are recorded in the channel; they are not claims of repaired or tested behavior.

The liaison checks at bounded active-work intervals, not as a persistent service. Gemini must adopt the channel with an acknowledgement; absence of a reply remains pending. One focused offline Rust test is explicitly granted after acknowledgement with at most two workers; no host or game launch.

Validation: project-board validator and scoped whitespace check run for this documentation-only slice. No source edits, build, gameplay, desktop control, installation or model execution.
