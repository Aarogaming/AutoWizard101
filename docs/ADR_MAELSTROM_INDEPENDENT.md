# Decision: Maelstrom owns its delivery

Accepted locally, 2026-10-30. Maelstrom develops and operates independently of
Aaroneous and Aaroneous Devtools. The user's final direction supersedes earlier
handoff requests and any suggestion that a host importer or contract adoption must
precede Maelstrom development. No further handoff exchange is a required step.

References consulted through the configured GitHub connector:

- [Aaroneous README and architecture overview](https://github.com/Aarogaming/aaroneous/blob/a3383f2feeeee32a47bcd11e34698cfc0d138131/README.md),
  revision `a3383f2feeeee32a47bcd11e34698cfc0d138131`, and its LICENSE.
- [Devtools foundation boundary](https://github.com/Aarogaming/aaroneous-devtools/blob/513a246e76aac42b9d49fd522bc14e7c67d5ffc7/FOUNDATION_BOUNDARY.md),
  revision `513a246e76aac42b9d49fd522bc14e7c67d5ffc7`; README and LICENSE also inspected
  from the default branch during this review. Both repositories were accessible.

| Reference idea | Local decision | Implications / verification |
| --- | --- | --- |
| Explicit component boundaries | Adopt as a design principle; map existing C#/Python modules in standalone profile. | No copied crate or new dependency; test actual seams. |
| Independently verified capabilities | Adapt to offline fixtures, connection checks and eventual live evidence. | Passing simulation is never live success; report publication regression tests enforce valid evidence files. |
| Devtools independent boundary | Adopt independence for Maelstrom's build, release and runtime. | No Devtools dependency added; clean-machine installer validation remains planned. |
| Rust component reuse | Defer until a concrete component is justified and qualified. | Existing WinForms/Python functionality stays; no rewrite or speculative FFI. |
| Zero allocation, protection rings, specialized ABI/hardware and naming constraints | Do not adopt for this desktop profile. | No unsupported real-time claims or irrelevant gate imports. |
| Named broader responsibilities | Treat as possible future adapter destinations, not available services. | Do not recreate Guild, Steward, Merlin, Library, Chimera, Marionette, Argus, Cratify, Studio HUD or Ariel locally merely to match names. |

Both inspected root licenses are MIT. No reference implementation code was copied;
any future substantive copy must preserve applicable copyright/license notices and
review transitive dependencies. These root licenses do not settle licensing for
bundled third-party game tools, tutorial media or future distribution packages.

The bounded first improvement is shared validated atomic Diego report publication.
It strengthens the existing standalone observer with no new production dependencies.
The authoritative plan is now `MAELSTROM_STANDALONE_PROFILE.md`; old handoff documents
remain useful historical evidence and report-shape documentation.
