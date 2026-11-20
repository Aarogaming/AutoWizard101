# Recording facts for conditional replay

2026-11-12 · CORE-DELIVERY · offline verified, not installed

Continuous W101 telemetry import now retains canonical nativeIdle, dialogue, menu and interactionPrompt facts alongside existing raw ui.* evidence. Only matching current/stable boolean readings qualify, including false. Stable objective text and parsed quest action/area/target use the existing intent parser. Loading, null/conflicting states and unavailable numeric producers do not invent values.

Teacher-action export now carries before/after recorded_facts envelopes using the existing target/context and acquisition-time observation clock, shifted to the video origin. Receipts and outcomes remain required. Older reports without detailed readings keep unknown conditions. No original recording is rewritten.

Verification: 2,570 native tests pass; scoped whitespace and analyzers pass; warnings-as-errors compilation in test run. Tests cover true/false, conflicting/null/missing/loading states, stable quest projection, full conditional receipt/outcome replay and unavailable energy. Saved real teacher trace re-exported to `.local/recording-facts-48/fresh-export`: Idle true/menu false becomes Idle false/menu true; unconditional replay completed with one receipt and zero desktop dispatch. Conditional replay was tested using fixtures; no new live recording or game interaction occurred.

Remaining: package/install separately; qualify a new continuous recording when requested. Typed aliases and API value subscriptions should extend the existing fact catalog with canonical references, declared storage types/ranges, source/time/quality metadata. Do not duplicate values or start client observation by declaring an alias. BOOL, signed INT16/DINT32 and decimal/string types need explicit validation; missing values must remain unknown rather than zero or false.
