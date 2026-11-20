# Pet Dance player

Open **Wizard101 → Observe & teach → Pet Dance player**, or use the Pet Dance
player button in the Game workspace. Select the intended Wizard101 window.
Opening the player does not start game control.
The player keeps the selected game's process identity. If Wizard101 restarts
while the player opens, refresh the window selection before trying again.
Enable **Game controls** in Maelstrom Tools before opening the player. The
installed launcher and its child player honor that setting; turning it off
stops further game input. Each visit still requires its own visible Start.

The current preset is qualified at **1280 × 720, windowed, Large UI**, with the
Windows display scale used during calibration (125%). Other UI sizes have
partial calibration evidence; other resolutions need separate calibration.

1. Walk to the Pet Dance sigil and open its normal game menu. Select the first
   arena. Leave the game ready at **Play**.
2. In Maelstrom's player setup, select **Use selected game audio as backup** if
   wanted. This captures only the selected game's sound; it does not record the
   microphone or change any volume setting.
3. Choose **Open player**, then **Start** in the visible trial window. Switch to
   Wizard101 and click **Play**. Leave keyboard and mouse quiet during the game.
4. Maelstrom waits through loading, watches each arrow sequence, and responds at
   Go. It plays one bounded game. **Stop**, Escape, desktop activity or a changed
   target stops input; the player never takes focus back automatically.
5. With **Return to native Idle** enabled, the new player operation attempts
   **Next → Finish without feeding → game menu → Idle**, verifying each screen.
   This new path has offline replay coverage. Its first installed live check
   timed out before admitting postgame frames; the capture-handoff repair needs
   live validation before automatic cleanup can be considered reliable.
   If it cannot complete, or the option is off, review the result and use
   **Next → Finish** to leave without feeding a snack,
   open the game menu and select **Idle**. After seeing **YOU ARE IDLE**, click
   **Confirm native Idle** in Maelstrom before the two-minute confirmation period
   ends. Its visible countdown covers loading and menu navigation; capture and
   game input have already stopped. A missed confirmation is reported as a failed
   visit even if gameplay was perfect.

The player remembers arrow appearances instead of replaying a fixed script.
Fades and repeated arrows are tracked across frames. When enabled, independently
recognized preview notes can supply a complete sequence if visual evidence is
incomplete. Audio is grouped by Watch/Go boundaries, not fixed-duration chunks;
response tones are not treated as the next preview.
If the optional sound stream stops or falls behind, its cues are disabled for
the rest of that game. The player continues with complete visual sequences;
it does not fill missing arrows with guesses. Sound analysis never holds up
visual observation. Reports retain the audio failure and its timing.

The internal response target is five seconds from the first observed Go; the
longest seven-key answer in development checks took about 2.15 seconds. This
target is distinct from the game's observed unanswered timeout, approximately
six seconds for the tested three-cue rounds.

Runs are saved under your Maelstrom data folder in **reports/pet-dance**.
Each records observations, proposed directions, actual key releases, timings,
outcomes and cleanup. Audio-enabled runs also save game-only audio and timing
sidecars. This is local training evidence; review storage use before many runs.
Updates and uninstall preserve this data. The bundled preset is stored separately
with application files and does not replace your saved packs or recordings.

Travel and arena selection remain manual. Automatic feeding is unavailable;
snack/attribute/school and Energy planning require verified observations and are
not yet connected to live inventory reading. Crown Shop actions are excluded
from this player. The new postgame operation is described in
[Native Idle operation](PET_DANCE_POSTGAME_OPERATION.md). Repeat
tests should begin a new run after confirming Idle, retaining failures as well as
successes. A sequence of perfect games is evidence for the tested settings,
not a guarantee for every future session. This preview does not run an unattended
endless farming loop.
