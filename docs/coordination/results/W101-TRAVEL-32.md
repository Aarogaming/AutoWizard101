# Travel repeat and navigation admission - 2026-11-12

One controller at a time; existing source CLI, exact target identity, countdown before travel actions, no desktop-use session or models.

## Live results

- Observer 50dd607216d74ec79535d35c23d646a6: World Gate prompt, native Idle, clean disconnect.
- Travel c2bfbc788bbd494ea017fb54e0e535d6: second configured Zafaria roundtrip completed. Selection/loading verified, Home returned to house origin, native Idle verified. Prior success: c87172cb2a0f47319f004456c3bd7be0. Independent machine-readable destination identity and menu paging remain unqualified.
- Home-ready attempt 0 was ready after 2.45 seconds of observation. Post-Home read-10 and three Idle traces show txtGoHomeTimer visible; pre-Home samples show hidden.
- Observer a26bf548a06f48c6adb9ca4ebefe8799 started too late: all 35 trees show timer hidden. Clean disconnect, no inputs. Inactive/active/inactive evidence exists, but NOT an exact 30-second measurement or live wait-then-dispatch qualification.
- Waypoint 10ec5d408100488b93a26f08ae760df5 rejected before input: not foreground. One quiet-activation retry ef3d5357667b4f5aa9fab8472d87529c rejected before input on desktop activity. No route success claimed; further input paused after takeover. Last observed game state was native Idle, with no cleanup input after takeover.

## Next qualification

Owner clarified: cooldown is 30 seconds from last Commons/Home teleport, shown on grayed buttons. Backend enabled alone is insufficient; actual timer/readiness evidence controls dispatch. Commons mapping remains unqualified.

Attach the next bounded cooldown observation directly after Home inside the same owner to avoid assistant/tool latency missing the transition. Observe blocked-to-ready without an extra teleport, preserve Idle, report actual timestamps. Do not substitute a blind sleep.

Retry the short measured house waypoint only with fresh quiet/foreground admission, then establish a fixed named landmark route with independent arrival evidence. Relative waypoint calibration is not arbitrary navigation; repeat world travel is not a walking route.

No production changes, installation, purchases, snacks, volume changes, restart or network manipulation. Frozen reports: .local/w101-cycle-06. Rust checks tracked separately under RUST-CONFORMANCE-32.
