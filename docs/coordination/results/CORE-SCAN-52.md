# Shared demand scans and predictive activation

2026-11-12. Source-only; not installed.

Cards now accept up to 32 registered DataRequests. Their explicit requests, conditions and filters are resolved to canonical facts and deduplicated. Undealt, completed and exhausted cards contribute no demand. Quest cards add their existing observation dependencies. The host uses this plan through its existing serialized observer and DeckObservationRefresh admission budget, rather than starting a reader per card.

API/MCP scan-profile with arguments {"learning":true} requires an opted-in connected observer and enables up to 60 seconds of broader collection. It does not enable unknown resource producers. Stop/pause and disconnect clear learning; active observer cancellation remains owned by the shared scheduler. Snapshot includes the plan, unavailable fields and learning deadline. Learning is observation-only and does not enable quest actions. Discovery does not start a scan.

Fresh same-game context can activate interaction-preview, dialogue, navigation-preview and combat-preview demands. Quest combat/navigation prediction is scoped to active quest consumers. Stale, future and wrong-game context cannot trigger prediction. Predictions change requested fields only; no inferred values, permission or gameplay outputs. Missing health/mana/potion producers remain unavailable. There is no learned predictor/model dependency.

Current rate remains the existing bounded two-second UI observation operation, admitted at most every five seconds, at most 30 refresh attempts per run. This is not a high-speed multirate telemetry loop. UI and quest reads can be selected; independent resource/combat/navigation producer rates await qualification. Routine-specific extension facts are still unavailable to the standard W101 scan planner until a producer is registered.

Stats compatibility evidence: `.local/scan-52/stats-compatibility.json` records one imported PlayerStatHook signature match in the running process's executable image (SHA256 d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a). This inspected the image file, not captured runtime stats. No new hook was installed. A matching signature does not prove object identity, offset semantics, equipment-adjusted energy maximum, or independent current/max correctness. Live stats qualification is incomplete and resource producers remain disabled. Do not market this as live resource collection.

Verification: focused demand/host tests pass, including alias union, unavailable-producer exclusion, exhausted/undealt cards, learning demand, fresh/stale/future/wrong-game prediction and no fabricated values. Full-suite and scoped formatting/analyzer outcomes are recorded on the board. Existing MCP tests pass. W101 received no inputs or focus changes from this work.
