# Using AutoWizard101 Tools

## Open it

Double-click **Launch Tools.cmd** in your AutoWizard101 folder. This checkout already has a prepared build and the isolated tool environments.

You can also open **Tools & companions** from the trainer's sidebar. Its background task pump and input bridge pause while that window is open. If a native bot or script is active, finish it before starting another gameplay helper.

## Everyday actions

**Guided runs are now available:** choose **Choose activity…**, **Set up an activity**, or **Set up pet dance**. Follow the four-step setup described in [Guided activities](GUIDED_ACTIVITIES.md). It validates options, checks the live connection, saves reusable setups and starts the selected backend routines directly.

1. **Open companion** opens the boss guides, quest tracker, gear planner and deck tools. Guides work without game controls. OCR and online fetching are optional features not installed by the initial core setup.
2. **Update boss library** copies active bosses from Companion into the trainer's local knowledge library. The source database is read-only during the transfer, and your primary trainer library is preserved.
3. **Open Wizard101** uses the existing game launcher finder. Sign in yourself.
4. For pet training or questing/farming, use the guided setup buttons, complete the prerequisites, and choose **Start activity**. The guide supports real backend settings, run limits, progress and Stop. Pet dance requires windowed mode and HUGE UI size, at 800×600 or 1280×800.

Only one external gameplay helper can own input through the hub at a time. A game process must be present before these helpers open. Installed status does not establish compatibility with the current game patch.

## Stop and close

Use a tool's **Stop** button or **Stop all tools**. Closing the Tools window also closes its owned tools and their helper processes. Save any plans in Companion before stopping it. The game launcher itself is not owned or closed by these Stop controls.

The app now owns tool process groups, so helper processes cannot simply outlive their launcher when the group closes. A real Companion launch/import/stop check passed with the prepared build. This ownership applies to tools opened by this window; separately launched programs and other trainer instances are outside it.

## Setup and troubleshooting

- **Needs setup:** choose the AutoWizard101 folder containing the installed tools, then press **Refresh status**. This locates an existing installation; it does not download a fresh tool stack.
- **Game controls off:** turn on the checkbox when you want to use a gameplay helper. Turning it off stops the gameplay helpers opened by this window. The choice is saved in this app installation's execution policy.
- **Tool closes with an error:** open **Setup & details** to see its output. Game updates can require tool-specific compatibility fixes.
- **wizSDK movement adaptation needed:** this library is deliberately not presented as a ready-to-run helper. Its legacy movement methods require further work.

## Maintenance

The **Maintenance** tab provides file pickers for comparing patch XML files, comparing saved type dumps, saving client type data and decoding a saved capture. Reports require a new file name, preserving existing reports. Capture decoding needs appropriate message definitions and unencrypted data; no capture driver or game decryption was added.

## What was verified

The prepared app detected all eight tool entries, imported 1,851 bosses, opened Companion offscreen and stopped it successfully. Automated tests cover policy messages, dependency states, paths containing spaces, cancellation and terminating descendant processes. The screen was rendered and inspected at its default size; smaller windows scroll the cards.

Validation after guided activities: all 63 .NET tests and all 24 Python integration tests pass. Changed C# files pass formatting and analyzer checks. The tool and guided-setup screens were visually inspected. A live connection/read/release check passed on the signed-in wizard without starting an activity.

Live pet gameplay, questing/farming behavior and current client offsets still require testing against a running, signed-in game. Game controls remain off by default. The deeper interface and overlap notes are in [the integration guide](WIZARD101_INTEGRATION_GUIDE.md).
