# Local reference sources

Downloaded 2026-10-30 with the owner's authorization into
`F:/Modlists/AutoWizard101/.local/reference-repos/`.

All 16 other repositories in `repository-reuse-inventory.json` were cloned
successfully. AutoWizard101 is the existing workspace, so it was not cloned again.
Each clone has full (non-shallow) Git history, a clean working tree and a HEAD
matching the reviewed revision. Git submodules were not initialized and LFS object
downloads were skipped. Repository setup scripts were not run.

The directory is excluded by the existing `.local/` ignore rule. These are source
references, not application dependencies. Private repository contents remain local.
Detailed revisions and download results are in the local `download-inventory.json`;
individual clone logs are alongside it. Nothing was pushed or merged.

## Initial older Maelstrom comparison

Compared tracked `.cs`, `.csproj` and `.resx` files under older Maelstrom's
`src/ProjectMaelstrom` against the current working application under
`ProjectMaelstrom/ProjectMaelstrom`, excluding Scripts/bin/obj and normalizing CRLF:

- 55 files identical, including MinigameCatalog.cs.
- 42 files different, including recognition, execution, replay and overlay code.
- 21 files absent at the corresponding local application path.

The earlier differing Git blob hashes for MinigameCatalog were a line-ending
difference, not a substantive implementation difference.

The available histories have no common commit between older Maelstrom HEAD
`bb6e3608ec0e85cc4d07bb1cda9810a19d505fd0` and this workspace's base
`5b5c2400809dbcb9a146254036ffb86fce148094`. This is consistent with code having been
copied into a new repository; it does not contradict the owner's source lineage.
Selective file comparisons are needed rather than assuming a straightforward merge.

Promising missing-file candidates include AsyncBotExecutor, AsyncBotTaskScheduler,
IAsyncBot, ImitationCaptureManager/Service, ImitationEventExtractor, NativeInput and
RuntimeOwnershipPlan. These names identify candidates, not verified functionality.
Their dependencies, tests and behavior must be inspected before backporting.
The additional gRPC/Hub/Guild pieces must not make external services mandatory.

Detailed lists are in local `maelstrom-comparison.json`. This comparison is
one-directional and deliberately scoped; it does not count new local-only files,
test projects, assets or scripts. Current local changes were preserved.
