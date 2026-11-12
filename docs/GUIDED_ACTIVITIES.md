# Guided questing, farming and pet dance

Open **Launch Tools.cmd**, then **Choose activity…**, **Set up an activity**, or **Set up pet dance**.

The setup now connects to the real routines. It does more than open an external configuration window.

## Four steps

1. **Choose an activity.** Follow the quest already tracked in-game, farm your current dungeon entrance, or play pet dance. Other minigames are explicitly unavailable.
2. **Configure the run.** Choose a time limit and activity-specific options. Save/load setup files if you want to reuse them.
3. **Check prerequisites.** The offline check validates the actual adapter, combat syntax or arrow templates. Sign in on one wizard and press **Check game connection**. Confirm your deck/location/game settings and explicitly enable game controls.
4. **Review and run.** Read the summary, then press **Start activity**. Status and Stop controls remain in this window. Closing it requests a stop too.

Saved setups contain options, not signed-in/preparation confirmations. Loading a setup does not start it. Each actual run receives its own immutable setup file and private progress/stop files under the app's `Scripts/.cache/activities/runs/` directory.

## Questing

Choose **Follow my tracked quest**. Equip your intended deck and track the quest in Wizard101.

- **Damage cards:** use available damage cards against enemies.
- **Area damage first:** prefer area damage, then other damage.
- **Custom combat strategy:** enter Deimos combat configuration syntax; the installed backend parses it during setup validation.
- Set health/mana stop thresholds, potion use and whether potion purchases with gold are allowed. Purchases default off. A threshold of 0 disables that threshold.

The adapter activates the actual Deimos combat, dialogue and questing callbacks once the client is connected. It does not build a deck or select a quest for you. Its separate configuration window is suppressed for guided runs; the trainer owns the settings and progress view. The original Deimos UI remains available through its unguided entry point.

Resource thresholds are checked before enabling a routine outside battle and then while running outside battle. Runs have a bounded active time and a two-minute connection timeout. The underlying questing behavior still depends on the game patch and the quest itself; a successful connection check is not proof that every quest is supported.

## Dungeon farming

Choose **Farm my current dungeon entrance** and stand at that entrance. A suitable quest must already be tracked for the backend's navigation logic. The connection check requires the entrance's Team Up prompt to be visible. Team Up participation itself is optional.

The adapter starts the real Deimos sigil routine with your chosen combat/resource settings. The label field is only a name for your run log; it does not navigate to a named boss or dungeon. This is a time-limited repeat routine, not a verified catalog of dungeon-specific strategies or guaranteed drop targets.

## Pet dance

Choose **Play pet dance** and select:

- Number of games, 1–100, plus an active time limit.
- **1280×800 or 800×600**, matching the actual game window.
- One or more dance locations.
- Optional snack positions 1–5. Feeding is available only at **1280×800**; it is blocked at 800×600 because that coordinate path is not implemented upstream.

Use windowed mode, HUGE UI size, the intended pet, sufficient energy and the intended snacks. Stand near the dance spiral with the game menu closed. The helper brings the game forward and moves it to the top-left.

The adapter directly calls the installed DanceBot's setup, image recognition and finish/feed methods. Its old configuration window is skipped. It stops on lost game focus, the configured time limit, a Stop request, the PyAutoGUI corner failsafe, or a 90-second period without a new dance round. The **Q** key requests a stop. Pet level-up or exhausted energy can require attention; no automatic energy purchase or level-up dialog handling is claimed.

## Progress and stopping

The run view reports backend state, elapsed active time, observed zone/resources, battles ended, or dance games finished/rounds observed. Battles ended are not reported as victories. Quest completion percentages and dungeon win counts are not fabricated.

**Stop activity** first requests cooperative cancellation so Deimos can release hooks. If the backend does not exit within eight seconds, the app closes its owned process group. Time/resource/focus stops report their reason. A run that stops or fails is never automatically resumed. Game changes, crashes and forced termination can still require restarting the backend or game.

The launcher and backend both retain execution-policy and exclusive input-owner checks. Connection probes take the same exclusive lease but do not start gameplay routines. Exactly one game client is supported in this guided flow.

## Verification on October 30, 2026

- Real installed Deimos combat parsing and DanceBot arrow-template preparation pass for the tested presets.
- A live Deimos connection activated hooks, read the signed-in wizard's zone/health, and released the client successfully. No questing, farming or pet routine was started during that connection-only check.
- Automated tests exercise saved setups, unsupported activity/resolution rejection, actual callback ordering through test backends, resource stops before starting, time limits, cooperative cancellation and observed dance game counters.
- The configured, prerequisite and review screens are rendered for visual inspection. Full gameplay outcomes still require activity-specific live runs with the intended deck, destination and game settings.
- Final automated checks: 63 .NET tests and 24 Python integration tests pass; changed C# files pass formatting and analyzer checks. Both supported dance template resolutions and the combat presets passed real installed-backend validation.

Implementation: `tools/activity_runtime.py`, `tools/activity_runner.py`, bundled Deimos adapter hooks, `Models/ActivityProfile.cs`, `GuidedActivityForm.cs`, and `ToolIntegrationService.cs`. Original upstream licenses remain in place. Guided Deimos runs use a private INI and preserve the user's original `Deimos-config.ini`.
