# Wizard101: visible UI boundary and legacy admission

**Current decision, 2026-11-10:** the owner expanded the W101 exception to
reviewed client-side functionality, including necessary hooks, injection and
memory reads/writes. Network manipulation remains excluded; installed game files
stay read-only. See [the current decision](W101_CLIENT_SIDE_EXCEPTION_20261110.md).
The earlier scope below is historical; its technical findings remain evidence,
not a restriction overriding the owner's expanded approval. No adapter is made
operational merely by this documentation update.

Audit and scoped admission patch: **2026-11-09**. The user's constraint at that audit was
that live Wizard101 interaction uses visible game screens and ordinary player
keys/clicks. Installation files are read-only research material. This work read
repository source only; it did not inspect game process memory, game traffic or
the installed game's files, launch a backend, or use the desktop.

The user subsequently asked about read-only process-memory observation. That is
a **distinct capability**, currently not enabled in UI-only mode and pending a
separate evidence/scope decision. It must not be conflated with memory writes,
injection, network manipulation or reading ordinary installation files. A question
about that capability does not enable it. This document makes no claim about
whether a particular memory-reading method is harmless or detectable.

## Policy and scope

The new trusted-code admission policy is
[`W101InteractionPolicy`](../ProjectMaelstrom/ProjectMaelstrom/Utilities/W101InteractionPolicy.cs),
with a corresponding [Python adapter policy](../tools/w101_interaction_policy.py).
Imported manifests, ownership of an upstream repository, the generic Allow game
controls switch and scheduler/input leases do not establish UI-only behavior.

| Method | Current admission |
| --- | --- |
| Visible screens + ordinary keys/clicks | Native reviewed routes remain available, subject to their existing target, freshness, consent, takeover and resource guards. |
| Offline research | Metadata, catalogue, source review, saved reports/captures and installation-file research remain separate from live controls. Installation source files must stay read-only. |
| Process-memory observation | Distinct capability, currently not enabled; wiztype receives this reason. |
| Client hooks, injection or memory modification | Disabled in UI-only mode; Deimos/wizwalker/wizSDK execution is classified here. |
| Live game traffic | Not enabled. Decoding an already saved capture is a separate offline task, not permission to acquire live traffic. |
| Unreviewed executable code | No generic legacy script or plugin assembly is admitted as UI-only merely because its name/manifest says native, overlay, offline or approved. |

This is an application admission boundary, **not an operating-system sandbox**.
Source files remain on disk, and a person could run upstream scripts outside
Maelstrom. The patch does not police unrelated programs, unload code already
executed in an older process, or prove an external program cannot interact with
W101. A fresh build/process and regression verification are needed before treating
the installed application as updated.

## Reachable paths found and changes made

| Entry point / path | Before this patch | Scoped change / retained behavior |
| --- | --- | --- |
| W101 workspace → Legacy library | A catalogue of reviewed alternatives, with no launch buttons. | Unchanged. Catalogue validation requires `Catalogue only` and `LiveQualified=false`; no execution permission is derived from it. |
| Routine builder | Saves 1–8 named A/D/W/S motion actions into a draft pack. | Unchanged. It is not an executable-code import. Farming/Pets/Potions/Quests categories do not qualify those full activities. |
| Native Observe, Auto-Idle and visual Pet Dance | Screens and ordinary input through their native implementations. | Unchanged by this patch. Their live qualification/cleanup evidence remains separate. |
| Development tools → Questing/farming | Guided Deimos run gated by generic live/experimental policy and shared ownership. | C# service admission rejects before setup/backend launch; hub and direct activity runner also reject. The generic toggle cannot override the boundary. |
| Guided activity → Check game connection | `--probe` reached `client.activate_hooks()`, zone/stat reads before policy validation. | Denied before backend path/import/subprocess in the hub/runner. `probe_deimos` itself rejects before importing wizwalker; the old hook implementation was removed from that function. |
| Maintenance → Save client type dump | wiztype was allowed with installed tool + game open, even when gameplay controls were off. | Denied at GUI/service/hub execution admission. Its distinct message is process-memory observation not enabled. Saved type-dump comparison remains available. |
| Raw/guided legacy Dance launcher | Starts an external Python trainer; generic UI claimed key/click behavior but did not confine the child. | Not admitted as a legacy executable. Native visual Pet Dance remains available; structural offline setup checks and image-asset study remain available. |
| Legacy Main / Manage Scripts / SmartPlay ScriptRun | Any script entrypoint could execute after generic policy; names/status hid some items but were not enforcement. | `ScheduleScript`, `StartScript` and the final `LaunchProcess` sink reject unreviewed code. Dry-run scheduling/simulation remains available; no executable is launched. |
| SmartPlay input tasks / InputBridge | Bounded ordinary input and generic executor paths. | Native ordinary-input controller remains intact. Generic live backend DLL loading is denied; it falls back to NullLiveBackend. Saved-image template clicks remain simulation-only. |
| `plugins/live-backends/*.dll` | First suitable assembly could be loaded/constructed without a UI-only method check. | Admission returns the null backend before assembly loading. Future supported adapters need reviewed trusted integration, not an arbitrary DLL drop. |
| Plugin manifests and PluginHost | Declared capabilities/profile governed assembly initialization; a public overlay declaration could permit arbitrary code. | Assemblies are blocked regardless of declared capability, and PluginHost rechecks before load. Manifest-only metadata is still viewable. |
| Moonlight saved-capture decoder | Takes explicit local capture/definitions and invokes decode mode. | Retained as offline research. No live network collection is added or authorized. |
| Companion, saved comparisons, status, previews | Planning and offline helper operations; status inspects package metadata. | Retained. A launch preview does not execute its command. These are not an assurance that arbitrary substituted external helper code is confined. |

The current app exposes Development tools when it can find a repository tools
workspace. Explicit `--tools` also opens it; `--legacy` opens the old Main form.
Therefore hiding old cards from the default home screen would not have been a
sufficient fix. A changed display label or a `status=reference` manifest is also
insufficient: enforcement is needed at the service and process-launch boundary.

## Defaults and remaining integration considerations

- Execution policy defaults to live disabled / AcademicSimulation. Existing
  persisted settings can enable live/experimental mode. The new boundary still
  applies when those flags are true.
- `ScriptLibraryService.DryRun` is not a persistent security boundary; its
  default boolean is false. Admission also runs at the final launch sink so a
  mutable DryRun setting cannot authorize a process between checks.
- `PLAYER_PREVIEW_MODE` defaults false. Reference-name filtering is presentation,
  not a capability check; supported entries must be identified by trusted code.
- Setup `--check` now validates profiles and source/asset structure without
  importing the legacy backend. It no longer proves that a legacy backend can
  initialize or play. The catalogue and UI must retain that limitation.
- The existing project file includes `Scripts/**` as content. This patch changes
  admission, not packaging or archive preservation. Audit actual release contents
  separately; the files' presence is not authorization to execute them.
- Legacy labels should progressively route users toward native UI workflows and
  research-only alternatives. Historical configuration views may remain useful;
  their live/probe operations must remain denied at the service as well as UI.
- The C# and Python policy maps intentionally cover the current W101 legacy
  adapters. Add parity tests whenever an adapter is added. Do not expand the
  offline allowlist to a live tool merely because its manifest says read-only.
- Read-only installation research needs its own output-root rules: derived
  indexes/reports belong in Maelstrom data, not in the installation. No source
  reading or extraction operation was performed as part of this audit.

## Code evidence

- [W101 catalogue](../ProjectMaelstrom/ProjectMaelstrom/Utilities/W101LegacyCatalog.cs),
  [library view](../ProjectMaelstrom/ProjectMaelstrom/W101LegacyLibrary.cs),
  [routine drafts](../ProjectMaelstrom/ProjectMaelstrom/Utilities/W101Routine.cs).
- [ToolCenter](../ProjectMaelstrom/ProjectMaelstrom/ToolCenterForm.cs),
  [guided connection UI](../ProjectMaelstrom/ProjectMaelstrom/GuidedActivityForm.cs),
  [tool service](../ProjectMaelstrom/ProjectMaelstrom/Utilities/ToolIntegrationService.cs).
- [Hub launch/probe dispatch](../tools/wizard101_hub.py),
  [direct activity runner](../tools/activity_runner.py).
- [Generic script service](../ProjectMaelstrom/ProjectMaelstrom/Utilities/ScriptLibraryService.cs),
  [SmartPlay](../ProjectMaelstrom/ProjectMaelstrom/Utilities/SmartPlayManager.cs),
  [ordinary-input controller](../ProjectMaelstrom/ProjectMaelstrom/PlayerController.cs).
- [Live backend loading](../ProjectMaelstrom/ProjectMaelstrom/Utilities/LiveBackend.cs),
  [plugin gate](../ProjectMaelstrom/ProjectMaelstrom/Utilities/Plugins.cs),
  [plugin host](../ProjectMaelstrom/ProjectMaelstrom/Utilities/PluginHost.cs).

## Validation and next gate

New [C# admission tests](../ProjectMaelstrom/ProjectMaelstrom.Tests/W101InteractionPolicyTests.cs)
cover service/probe rejection before workspace/backend access, manifest-claim
bypasses, separate memory-observation classification and retained native/offline
admission. Existing ToolCenter expectations were updated to the new boundary.
[Python tests](../tools/tests/test_w101_interaction_policy.py) cover hub and direct
runner admission before subprocess/import, direct probe rejection, offline
source checks and unknown-tool rejection. No game or backend execution is needed.
The seven new Python tests passed; each changed Python file was formatted with
the installed Black. Eighteen local documentation links were checked. The Python
used for this check does not contain Pyflakes; no package was installed. The
parent session owns that lint check in its existing tool environment.

Six former RunnerLogic batch-launch tests now verify refusal before any process
or output side effect, including spaced arguments, large output, stderr and a
nonzero-exit script. They do not introduce a test-only executable bypass.

The parent session owns build/full regression/packaging validation. This document
records scoped source changes, not an already installed or comprehensively
isolated application. Keep the ordinary-input teacher's live qualification and
native Idle cleanup evidence separate from admission-test results.
