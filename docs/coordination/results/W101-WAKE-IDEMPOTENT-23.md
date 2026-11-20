# Wake idempotence and dorm movement — 2026-11-12

Source repair: wake-only mode accepts a fresh, bound, controlled AlreadyAwake observation at entry with zero inputs. Round-trip mode still requires its own wake receipt. Pending/accepted actions cannot use this entry shortcut. Negative tests reject stale, wrong-target and unowned observations.

Validation:54 focused tests;2395full native tests; scoped formatting/analyzers pass.

Live62c60b61a3844fbdaf53488bba1b22d6: foreground and countdown completed, zero inputs. AwakeCandidate was deliberately mapped to Unknown by the wake-only graph because no wake receipt existed; this was not stale evidence. Desktop activity revision2 then stopped the run. No Idle cleanup after takeover.

Dual-monitor check: Windows reported target3410314 foreground; DISPLAY10 primary, DISPLAY11 right-hand display. The PowerShell bounds query was DPI virtualized and is not a pixel-calibration result. No Maelstrom GUI process was present during the check. User-reported yellow square is unidentified; do not label it the new dark countdown without evidence.

Liveaea16c7770434cd08a4f98300d55cb79 after explicit renewed readiness: countdown runtime completed; wake-only completed without action from awake. W100ms sent and released. BeforeXYZ329.551636,313.306488,0; after329.988037,247.307922,0; unchanged yaw6.276573. Displacement66.000008units aligned with forward heading.40unit target overshot by26units; candidateReached:false. Idle finalizer completed and independently observed nativeIdle:true with restored/disconnected adapter. This qualifies positive Idle recognition for this profile in this run, not every UI/game build.

Run correctly reported Failed/teaching-visit-incomplete-native-idle-observed, not arrival. No route qualification. User confirmation pending for actual countdown monitor placement and audible cue; runtime completion alone is not visual/audio proof. Requested permission for one shorter60ms pulse, not yet run.

Current state: native Idle verified; no live controller remains. Source repair not yet installed.

## Follow-up banner repair and completed short trial

User did not see the prior countdown. Changed the notice to a wide client-relative banner using GetClientRect/ClientToScreen and explicit SetWindowPos SHOWWINDOW|NOACTIVATE. This addresses hidden CLI startup visibility rather than changing focus. Added monitor-placement cases for primary, right and negative-coordinate displays. Own-form render and placement metadata are saved as evidence; they do not claim composited desktop visibility or audible output.

Live5f8a620d76454000a607ded4d1334c00 after explicit readiness: Completed/teaching-visit-collected-and-native-idle-observed. Wake from native Idle, W60ms, return to native Idle all completed. Displacement35.999978units;40unit target residual4.000022units; waypointCandidateReached:true. arrivalVerified:false and routeQualified:false remain honest limitations of this candidate pose-based teaching trial. End state native Idle verified; no controller left running.

Banner placement reported visible:true at25,57 size1248x120 inside W101 client origin9,41. Saved countdown-render.png inspected: legible dark full-width banner and white countdown text. User missed this run's banner; remote compositing remains unconfirmed. User is remote and cannot hear sound; soundRequested:true is not audibility evidence.

Final checks:2398native tests and scoped formatting/analyzers pass. PortableMaelstrom-20261112-134900-814557; Setup-20261112-134928-73fbf7 installed:true, active versione486e4b43ac54c55a31874c1d1802757. Installed observation-only check installed-idle-23.json completed, with restored hook and clean disconnect. No further movement.
