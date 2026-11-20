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
| Rust component reuse | **Superseded 2026-11-19** by "Language selection" below. Was: defer until a concrete component is justified and qualified. | Conversion now follows the per-operation efficiency rule, gated by parity evidence; still no speculative FFI. |
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

## Language selection

Accepted by the user, 2026-11-19. Supersedes the "Rust component reuse" row above.

**Rule:** each operation is implemented in the language best suited to perform it with
the greatest efficiency. Existing code is not kept in a language merely because it
already exists there; migration risk is handled by parity evidence and staged cutover,
not by staying put.

Efficiency is judged per operation on:

1. Runtime cost and latency determinism (CPU, memory, garbage-collection pauses).
2. Boundary cost: every process launch, pipe hop or serialization between C#, Python
   and Rust inside a frequently repeated loop counts against the design. A hot loop
   should run end to end in one language and one process.
3. For code that runs rarely (gates, offline analysis, one-off tooling), authoring and
   iteration speed dominate.

Current assignment under this rule (direction, not a completed migration):

| Operation | Target language | Reason |
| --- | --- | --- |
| Control loop: steering, kinematics, costmap, path following, scheduler tick | Rust | Deterministic timing, no GC pauses; most logic already in `maelstrom-core`. |
| Input dispatch and the single input-ownership lease | Rust | Same process as the control loop; Win32 through the existing `windows` crate in `maelstrom-platform`. |
| In-loop frame capture and pixel checks | Rust | Zero-copy buffers and SIMD without a Python hop. |
| Client memory reads (currently wizwalker via a spawned `python.exe`) | Rust | Removes the costliest boundary; the pinned wizwalker source stays as the parity oracle. |
| Facts store, GOAP, quest interpreter, routine replay | Rust | Pure, frequently run, replayable logic. |
| In-game overlay/HUD | Rust (`egui`) | Per-frame redraw beside the Rust loop; `egui` is already used by `maelstrom-ide`. |
| Model training, vision experiments, data analysis | Python | NumPy/OpenCV are native underneath; fastest iteration. |
| Repo gates, offline tooling, MCP server, wiki/data scripts | Python | Rarely run; authoring speed dominates. |
| Desktop forms (settings, workshop and other WinForms screens) | C# for now | Most efficient path for form-heavy Windows UI; not performance-critical. Revisit if a Rust UI covers them. |
| Installer | C# | No performance stake. |

Conversion constraints (unchanged safety rules from `AGENTS.md`):

- **One input owner.** Input ownership moves from C# to Rust as a single cutover, never
  two concurrent owners. Until cutover, Rust components act only behind the C# lease.
- **Parity before retirement.** A component moves only after identical results on the
  same fixtures and replay traces in both implementations; the old copy is then removed.
  This also retires existing duplicates (deck encoder, combat geometry, pathing exist in
  two or three languages).
- **Dependencies.** New production crates or packages (for example a capture crate or an
  ONNX runtime) still need explicit approval.
- **Evidence.** A passing port or replay is not live success; live qualification keeps
  its own authorization and evidence requirements.
- Each conversion is a claimed, bounded task with its own result file.

Suggested first conversion: the client memory reader, which removes a process launch per
read and already has a reference implementation to test against.
