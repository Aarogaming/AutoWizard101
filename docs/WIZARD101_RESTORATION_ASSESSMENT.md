# Wizard101 restoration and integration assessment

Assessed October 30, 2026. Scope: restore and assess before changing application behavior.

## Restored baseline

- Repository: https://github.com/Aarogaming/AutoWizard101
- Local checkout: `F:\Modlists\AutoWizard101`
- Restored commit: `5b5c2400809dbcb9a146254036ffb86fce148094`
- Assessment branch: `maelstrom/restore-integrations`
- Existing application name: Aaroneous Automation Suite / ProjectMaelstrom.
- No application code, execution policy, bundled upstream source, or launch behavior changed in this assessment.
- External Python dependencies were not installed; no game session or third-party executable was launched.

The repository is recoverable and the main application builds. Bundled source, working launchers, installed dependencies, and compatibility with today's game client are separate readiness checks. Passing the existing tests does not establish game compatibility.

## Findings by tool

| Tool | Local state | Best use in AutoWizard101 | Work needed before integration |
| --- | --- | --- | --- |
| Deimos-Wizard101 | Source and requirements bundled; manifest is `reference` and points to `README.md`. | Optional external questing/farming application; use its workflow ideas when designing native tasks. | Pin a tested revision and its fork dependencies in a separate environment; provide a real launcher and stop tracking. Validate against the installed game revision. |
| wizwalker | Source bundled, version 1.8.1; `run.bat` looks for a nonexistent `main.py`, then opens the README. | Low-level Python backend for navigation and client state. | Choose an actively usable fork/revision, test its API, then write a task adapter. The GitHub project now points to Codeberg. Do not assume the old snapshot or CLI remains compatible. |
| wizSDK | Source bundled, version 1.5.0; reference wrapper falls back to README. Manifest's `wizwalker/wizsdk` URL returned 404. | Legacy examples for farming, questing, selling, bazaar and pet workflows. | Correct provenance to UnderpaidDev1/wizSDK in a future metadata change. Its `wizwalker = ^1.x.x` dependency and old NumPy/OpenCV constraints require compatibility testing; do not install into Deimos's environment. |
| wizard101-companion | Not bundled. | Highest-priority candidate for boss guidance, quest tracking, gear/deck planning and OCR overlay assistance. | Evaluate in its own Python 3.12 environment; inspect installation and data formats first. Start with a separate launch option, then consider explicit import/export of guides and loadouts. No documented shared API was established. |
| Wizard101_DanceBot | App launcher exists, but expected `Scripts/Wizard101_DanceBot/dist/petdance/petdance.exe` bundle is absent. A separate `Scripts/PetDanceBot.exe` exists with unverified provenance. | Optional visual pet-training tool. | Verify provenance and license before redistributing; restore a complete upstream release bundle or build from reviewed source. Do not rename the existing EXE and assume equivalence. Upstream requires windowed mode and HUGE UI; pet level-up requires manual stop. |
| moonlight | Not bundled; mentioned by bundled wizproxy documentation. | Developer tool for offline decoding of unencrypted packet captures. | Requires matching DML message definitions; optional type information improves decoding. It cannot decrypt KI game traffic. Keep capture files and protocol inputs separate from player-facing automation. |
| wiztype | Not bundled. | Developer diagnostics: produce versioned class/type dumps to investigate client updates. | Requires a running Wizard101 instance. Preserve dump format/version and client revision. Its `--version 1` output targets wizwalker; a type dump alone is not a compatibility fix. |
| wizdiff | Not bundled. | Developer update investigation and change reports. | Establish its actual inputs, configuration and output contract from source before scheduling or connecting it to release checks. README documents installation, not a complete integration API. Do not assume it automatically repairs offsets. |

Local evidence: `ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/{Deimos-Wizard101,wizwalker,wizSDK}`, their manifests, wrappers and dependency files; `ProjectMaelstrom/ProjectMaelstrom/PetDanceBot.cs`; application project content-copy rules.

## Corrections to the original breakdown

- Deimos is an application built on low-level tooling; wizwalker is the underlying library. They need separate lifecycle and dependency handling.
- wizSDK is an old wrapper, not an immediately compatible extension of every current wizwalker fork.
- Companion's documented features substantiate OCR and planning tools. An AI service integration was not established by the README reviewed here.
- DanceBot reads images and sends inputs. That makes it external to game memory, but it still automates gameplay; visual operation does not establish game compatibility.
- Moonlight decodes unencrypted captures; it is not a decryption solution for current live traffic.
- wiztype and wizdiff support update investigation. Neither is evidence that bundled automation remains usable after a game patch.

## Recommended implementation sequence

1. **Repair readiness reporting.** Show source-only/reference, installed, dependencies missing, launchable, and tested-against-client states separately. Correct source URLs. Record upstream commit, Python version and dependency lock per tool. Replace placeholder entry points only after real launch paths are tested.
2. **Evaluate Companion and DanceBot separately.** Companion offers the broadest immediate player utility without duplicating a bot framework. DanceBot has an existing UI entry but needs its full package, settings validation and process lifecycle handling.
3. **Select one automation backend.** Compare a pinned Deimos fork against a pinned wizwalker revision for the actual desired quest/farm tasks. Keep separate environments during evaluation. Use wizSDK as a source of reusable workflow ideas until its dependencies and API have been proven compatible.
4. **Define a shared task contract.** Reuse the existing OCR snapshot bridge where useful. Specify start, status, cancellation, errors and resource limits. Require one owner of keyboard/mouse input at a time. Validate policy before every external process launch; `ScriptLibraryService` currently supports an optional `PreflightCheck`, while `PetDanceBot` launches directly. Existing executor guards alone do not prove that every external launcher is covered.
5. **Build update diagnostics.** Record game revision, capture type dumps with wiztype when appropriate, use wizdiff for inspected change reports, and use moonlight only for suitable offline captures. Require a compatibility result before enabling a backend for a new game revision.

Recommended environment boundaries: Companion (Python 3.12), DanceBot (complete bundle or separately pinned source environment), Deimos (its own fork stack), legacy wizSDK (isolated evaluation only), diagnostics (separate tools/environments as their dependencies require). These are proposed boundaries, not installed integrations.

## Acceptance checks for the next implementation phase

- Fresh installation identifies missing packages accurately and never reports a README-opening wrapper as a working bot.
- Each enabled tool records source, revision, license evidence, dependency lock and tested client revision.
- Launch succeeds with paths containing spaces; exit and cancellation stop all owned child processes; failed starts produce useful messages.
- Simulation does not start a live external tool. Policy checks apply to external launch paths as well as native executors.
- Companion data exchange is explicit and preserves user data; OCR readings include timestamps and stale readings are handled.
- DanceBot is checked at the documented UI settings and handles focus loss and pet level-up.
- Quest/farm adapters prove API compatibility and stop/resource handling in a controlled test before being considered ready.
- Patch diagnostics retain previous outputs and produce a reviewable report; no automatic offset repair is assumed.

License evidence must be reviewed per pinned revision before copying or distributing additional code. GPL licenses are visible for bundled Deimos/wizwalker/wizSDK and upstream moonlight. DanceBot's repository page did not expose a license in the reviewed listing; redistribution permission remains unresolved. Other license conclusions remain pending inspection of the selected revisions.

## Validation performed

- Repeat local file/manifest checks with `pwsh -File tools/check_wizard101_readiness.ps1` (add `-AsJson` for structured output). This reads expected checkout paths only; it does not discover global installations or import upstream code.
- Readiness script validation passed: PowerShell syntax parsing, eight-tool inventory, empty checkout, malformed manifest, and missing entry point fixtures. `git diff --check` passed.
- `dotnet test ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj --verbosity minimal`: built the main application and passed all 40 tests, zero skipped.
- `dotnet run --project MaelstromToolkit/MaelstromToolkit.csproj -- selftest`: `SELFTEST PASS`.
- `dotnet format ProjectMaelstrom/ProjectMaelstrom.sln --verify-no-changes --no-restore --verbosity quiet`: failed on existing whitespace in `Main.cs`, `SettingsForm.cs`, and `StateManager.cs`. No formatting was applied.
- No interactive UI, OCR, game, external bot, packet decoding or patch workflow was exercised. Optional projects beyond those built by these commands are not claimed validated.

## Primary sources reviewed

- [AutoWizard101](https://github.com/Aarogaming/AutoWizard101)
- [Deimos README](https://github.com/Deimos-Wizard101/Deimos-Wizard101)
- [wizwalker GitHub relocation notice](https://github.com/wizwalker/wizwalker) and [Codeberg destination](https://codeberg.org/wizwalker/wizwalker/)
- [wizSDK upstream](https://github.com/UnderpaidDev1/wizSDK)
- [Companion installation and features](https://github.com/Cochonnes/wizard101-companion)
- [DanceBot usage and settings](https://github.com/kennyhngo/Wizard101_DanceBot)
- [Moonlight commands and decoding limitations](https://github.com/kronos-project/moonlight)
- [wiztype commands and dump format](https://github.com/wizspoil/wiztype)
- [wizdiff installation and development commands](https://github.com/wizspoil/wizdiff)

Upstream pages were reviewed on the assessment date. This is a source/dependency and launch-path assessment, not a security audit or a claim of runtime compatibility.
