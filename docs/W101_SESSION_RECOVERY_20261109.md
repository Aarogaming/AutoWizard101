# W101 session recovery: observed restart comparison

**2026-11-09.** After the user restarted Wizard101, the next supervised Pet Dance
attempt reached gameplay and its reward screen, then returned to the courtyard
in native Idle. The earlier client had remained on loading. This establishes an
observed recovery after restart; it does **not** establish an internal cause,
memory leak, server fault or a required restart interval.

This note covers visible state recovery only. Sound-to-arrow matching is a
separate investigation described in [Pet Dance audio analysis](PET_DANCE_AUDIO_ANALYSIS.md).
No game action, audio playback or new capture was performed to write this note.

## Evidence and chronology

The [session comparison receipt](../.local/w101-audio-study/dance-02/session-comparison.json)
preserves the two process identities, creation times, recording start times and
the supervising session's observed outcomes. Times below are UTC. Process age
is elapsed time since client creation, not measured uninterrupted gameplay time.

| Evidence | Earlier client / `dance-01` | Restarted client / `dance-02` |
| --- | --- | --- |
| Client created | 2026-11-09 04:36:38.572 UTC | 2026-11-09 18:59:39.965 UTC |
| Recording started | 2026-11-09 17:20:53.926 UTC | 2026-11-09 19:01:54.492 UTC |
| Client age at recording start | 764.256 minutes, about 12 h 44 min | 2.242 minutes, about 2 min 15 s |
| Observed transition | Loading persisted during and beyond the recording, including after the recorder stopped. | Dance gameplay and reward appeared. |
| Recorded outcome | No qualified Dance round or arrow/note pairing. | Three unanswered rounds, then the **TRY HARDER** reward, as observed by the supervisor. |
| End-state evidence | Supervisor observed native Idle banner over the loading scene. This did not establish a return to gameplay. | Supervisor verified native Idle after return to the courtyard. |

The earlier [audio receipt](../.local/w101-audio-study/dance-01/video/audio-timing.json)
saved 108.12 seconds with zero writer queue drops. Its
[finished receipt](../.local/w101-audio-study/dance-01/finished.json) reports verified
video, no recorder-sent inputs and released hooks. The
[sparse visual inspection](../.local/w101-audio-study/dance-01/review/offline-inspection-01/inspection.json)
sampled eight points through 105 seconds; it is not an every-frame review. The
supervising session observed loading for roughly four minutes overall and
recorded [passive cleanup evidence](../.local/w101-audio-study/w101-idle-after-dance-01/finished.json)
after selecting native Idle. Recorder completion and game progress are separate
outcomes.

The new [demonstration receipt](../.local/w101-audio-study/dance-02/demonstration.json)
reports complete opted-in audio, verified video and a 1280 × 720 client. Its
[finished receipt](../.local/w101-audio-study/dance-02/finished.json) reports stopped
capture, released hooks and no recorder-sent inputs. The supervisor handled
ordinary menu interactions; **no direction inputs were sent and no snacks were
fed**. Therefore the failed reward is expected evidence from an unanswered game,
not a measured failure of a playing policy. The receipt explicitly leaves route
qualification, verified labels, automatic training eligibility and exact
input/video alignment false.

The user performed the restart. The comparison records no game-memory reading,
game-file modification or volume change. The selected-process recorder did not
acquire permission for gameplay merely by recording it. Live interaction remains
under the [visible UI boundary](W101_UI_ONLY_BOUNDARY.md).

## What the comparison supports

The fresh session successfully crossed the transition that had previously
remained on loading. This makes restarting a useful **bounded recovery option
after a persistent observed loading failure**, subject to saving work and user
authorization. It is one before/after observation, not a controlled experiment
holding all other conditions constant. The client's age is context, not proof
of causation or a threshold after which a session becomes unreliable.

Do not derive scheduled restarts, an inactivity limit, a resource-leak diagnosis,
a server diagnosis or claims about game protection from these two runs. Do not
describe the earlier receipt as proof of stale capture: the supervisor also
observed loading outside the recorder. Equally, the later successful transition
does not prove every capture path or game state is now qualified.

## Practical UI-only recovery

These are operating requirements for the recovery workflow, not a claim that a
complete automatic recovery dispatcher is implemented.

1. **Bind and observe before acting.** Check the selected process/session,
   window geometry and visible starting scene. After a restart, invalidate the
   old binding, queued proposals and incomplete sequences. Rebind explicitly
   and reobserve; do not reuse old menu coordinates or assume a previous
   character, location or level selection survived.
2. **Treat explicit loading as a pause.** Release owned held inputs, withhold
   further gameplay/menu proposals and keep bounded observation running. Do not
   repeat Play, Next or interact commands because their outcome is delayed.
   Preserve evidence of what was actually observed and when.
3. **Keep a cumulative wait limit.** Loading does not consume the gameplay
   budget, but it must consume a finite startup/loading budget. Repeated loads,
   ambiguous scenes or missing frames must not reset the deadline. A timeout
   means the expected state was not observed within the allowance; it is not
   evidence of the game's internal cause.
4. **Stop and reconcile before recovery.** On exhaustion or manual takeover,
   stop owned input/capture workers and preserve their receipts. If the visible
   menu is available, use ordinary menu controls to request native Idle and
   verify its banner. Record whether the background is the courtyard or still
   loading. Do not label an unobserved cleanup action successful.
5. **Restart only as a distinct operation.** This comparison used a restart
   performed by the user. It does not enable unattended client termination,
   relaunch, credential access or login. If a login screen appears, stop at that
   boundary; never retrieve credentials from a clipboard, file or saved prompt.
6. **Start a new bounded visit from fresh evidence.** After a verified recovery,
   create a new visit identity, confirm the actual screen and level, and retain
   the previous attempt as incomplete. Return to native Idle after the new
   attempt and verify it rather than assuming a click succeeded. Any repetition
   remains separately bounded and requires observed resource/context checks.

The existing [GameplayPauseClock](../ProjectMaelstrom/ProjectMaelstrom/Utilities/GameplayPauseClock.cs)
implements cumulative startup/loading and gameplay accounting. Its default
budgets are 45 and 75 seconds respectively; these are software defaults, **not
measured W101 loading guarantees**. Advance it on missing-frame iterations too.
Unknown scenes cannot authorize gameplay: before gameplay starts they count
toward waiting; after start, unless explicit loading was observed, active time
continues. A clock's `GameplayAllowed` value is not fresh-frame or input-ownership
authority.

The [Pet Dance lifecycle planner](PET_DANCE_LIFECYCLE.md) keeps loading, unknown,
round feedback, whole-game result, outside return and native Idle distinct. It
rejects duplicated logical transitions and emits proposals only. Detector
qualification, dispatch receipts, takeover handling and verified cleanup remain
necessary around it; this successful supervised recovery does not make those
integrations automatic.

## Next evidence gates

- Compare the fresh recording's preview audio and visual arrows independently;
  keep unanswered-round feedback separate from demonstration inputs.
- Record capture success, game-transition success, gameplay result and cleanup
  as separate outcomes. A failed reward can coexist with successful observation
  and safe cleanup.
- Preserve process/session age as a diagnostic field on future attempts without
  turning it into a restart policy. Additional varied observations are needed
  before attributing failures to session age or any internal mechanism.
- Qualify a bounded recovery dispatcher separately before enabling it for
  unattended repetition. This note does not enable a new routine or change any
  installed application behavior.

Raw `.local` evidence is workspace-only and may not exist in a clean checkout.
Retain the cited receipts with any exported research bundle; no game files,
credentials or private clipboard content belong in that bundle.
