# Shared W101 operations: synthetic replay

This is a contract fixture for `W101SharedOperationsTests`, using the existing
`RoutineRunner` through the shared `ModuleRunner` and `ModuleReplay`. It is not saved gameplay, perception ground
truth, a calibrated Commons route or permission to dispatch any input.

The test compiles two supplied segments: start -> junction -> sigil. The first
segment has one verified progress event before arrival, so it proposes a second
short forward pulse. The second uses a different primitive. Both choices are
synthetic, not learned keys or directions for the real map.

Arrival is a named, newer observation after a matching released receipt. Loading
and Unknown pause proposals. Each motion outcome also names the exact pulse in
`outcome_for_proposal_id`; a held Progress label from the prior pulse cannot
advance the next one. These bindings are authored contract data, not a visual
motion verifier. The remaining sequence interacts with the named
Dance sigil, closes its instructions with the identified Cancel control, opens
the game menu and verifies native Idle. It does not press Play, feed or purchase.

The visit/context/geometry identity is explicit. Observations use the
`module_observation` payload with exact required facts and named controls. Every
verification flag, fact, control and accepted receipt is authored test data, not
an observation made by this fixture. The replay completes four reusable modules
(navigation, sigil, menu and auto-idle), with seven proposals and seven receipts, zero desktop dispatches, and
`live_qualified: false`. Repeating it must produce byte-identical reports.

The 19-event fixture migrated from the older monolithic graph in module revision
`MODULE-NAVIGATION-04`. All seven outcome events now use module-scoped proposal
IDs. It is no longer compatible with `RoutineReplay`; using that older reader
would ignore the module observations and would not verify this workflow.

`bundle.json` is a checked-in four-module definition alongside this session and
its events. A regression test compares its canonical serialized form with
`W101SharedOperations.RouteAndMenuVisit` for the test's supplied route, then
replays the checked-in file directly. Configuration drift cannot silently leave
this artifact representing different behavior from the adapter.

After the native application has been built, run this from the repository root:

```powershell
& .\ProjectMaelstrom\ProjectMaelstrom\bin\Release\net9.0-windows10.0.22621.0\ProjectMaelstrom.exe `
  --replay-modules `
  .\ProjectMaelstrom\ProjectMaelstrom.Tests\TestData\Routines\w101-operations\bundle.json `
  .\ProjectMaelstrom\ProjectMaelstrom.Tests\TestData\Routines\w101-operations\session.json `
  .\.local\w101-module-replay-new.json
```

Choose a new report filename on each run; replay refuses an existing output.
Use the freshly built executable, not an older `win-x64` publish directory.
The command is an offline replay and opens no game or controller.

The coordinator's bounded native test invocation can filter to
`W101SharedOperationsTests`. Tests write reports and any generated comparison
definition to their own temporary directories and remove only those directories.
There is no game launch, recorder, wall-clock sleep or model in this fixture.
