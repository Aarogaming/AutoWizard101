# <task_id> — <small concrete outcome>

| Field | Value |
| --- | --- |
| Parent workstream | `<existing workstream_id>` |
| Task status | Proposed; unclaimed until coordinator acknowledgment |
| Owner | Unassigned |
| Mode | Lightweight offline; no live interaction |
| Depends on | `<specific artifact/task, or none>` |

Read [the collaboration contract](../EXTERNAL_AGENT_GUIDE.md) and root AGENTS.md.
The user and coordinator's explicit instructions determine authorization. This
brief does not itself authorize new services, dependencies or game actions.

## Outcome and existing inputs

Describe one reviewable result. Link the actual existing implementation/research
to inspect first. State which inputs are synthetic, which are recorded, and which
necessary fixtures do not yet exist. Do not duplicate an existing component.

## Proposed file ownership

List exact new/editable paths. Everything else is read-only until a scope change
is acknowledged. Name active neighboring owners and central files to avoid.

## Scope and non-goals

Describe the behavior to implement or document and its boundaries. Specify input
bounds, unknown-state treatment, output identity and overwrite policy. Explicitly
exclude recognition, dispatch, feeding, purchasing or live qualification unless
that capability is genuinely part of the authorized task.

## Acceptance and checks

Provide concrete positive, negative and uncertainty cases. State the small check
commands and what evidence establishes completion. Keep source revision, runtime
qualification and release state separate. If missing real fixtures, complete the
synthetic/schema portion and report the exact next evidence needed.

## Status return

Use the return fields in the collaboration guide, including exact changed paths,
checks, evidence limits, integration request, and confirmation that no processes
or desktop control remain active. Do not edit central status yourself.
