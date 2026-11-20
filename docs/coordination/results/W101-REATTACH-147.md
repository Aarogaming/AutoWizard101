# W101 reattach and Commons checkpoint 147

2026-11-15. User restarted W101 and asked to continue. Maelstrom's documented `windows` command enumerated the new target identity (metadata only): HWND 1681917634, PID 120576, process start ticks 639303573548729501. This differed from the prior target PID 21044. The same new identity was passed explicitly to the next operations; this demonstrates a fresh identity refresh/rebind workflow, not unattended automatic retargeting.

## Observation after restart

A five-second `ui-tree-watch` completed against the new identity, observing five UI trees and twenty loading samples. Stable native Idle was true, Loading false; hook restoration and cleanup were confirmed. A second five-second observation with pose context reported a candidate pose in `WizardCity/WC_Hub`, wizard subject ownership matched at read endpoints, and native Idle remained true. Position candidate was (736,-204,-27.6100), heading 3.975 rad. The pose/layout remain candidates and `contextQualified=false`; no game input was sent in these observations.

Artifacts: `.local/windows-reattach-146.json`; operation receipts under `%LOCALAPPDATA%/Maelstrom/operations/bd6b31cf38ba44ec90d04e041391625c` and `.../e784cbbedc5a41ffb1485a301b6654b9`.

## Bounded continuation

After countdown, `navigation-visit` ran a shared-owner feedback waypoint with 60 ms maximum forward pulses and 100-unit waypoint distance, then performed native Idle cleanup. Run `9d21b165089a4fa2b1c0f77c4debf395` completed and final Idle was independently observed. It issued two pulses; pose changed from (736,-204,-27.6100) to (815.1450,-132.1115,-27.9758), a measured net planar displacement of about 96.2 units. Feedback result reason was `coordinate-arrival`; it is a local waypoint result only. The run explicitly reports `arrivalVerified=false` and `routeQualified=false`.

This does not prove the next connector, Commons-to-Pavilion traversal, collision box dimensions/height, or a complete route. No client files were modified, no volume/snacks/Crowns touched, and no install occurred.
