# Existing test games for Maelstrom

For the optional WSL 2 visual test that keeps Windows focus available, see
[Isolated WSL Dodge](ISOLATED_WSL_DODGE.md).

In the development workspace, open **Game workspace → Local test games…**.
Launch a game, return to Observe, refresh the window list, select the game,
and attach. Use **Gameplay Sampler** to collect still-image teaching or
independent-check sessions. For denser review, use **Record video (preview)**,
then **Sample video for labels…**, and review the extracted frames before
exporting a draft pack. Video can optionally capture selected game keys and mouse
events while the game has focus; still sampling does not capture inputs. See
[Game workspaces](GAME_WORKSPACES.md) for scope, privacy and timing limitations.
The test games are optional local files; the launcher can be pointed at the
AutoWizard101 folder if it cannot find the development workspace. They are not
bundled with the installer and must be prepared separately.

## First choice: Dodge the Creeps

The official Godot demo is a small game with four-direction movement, randomized
enemy spawns and a survival score. It exercises tracking, choosing movement,
holding and releasing controls, and detecting game-over. It is more demanding
than a single reaction cue, but much smaller than Wizard101.

- Upstream: https://github.com/godotengine/godot-demo-projects/tree/4.7-6ad6167/2d/dodge_the_creeps
- Release archive: https://github.com/godotengine/godot-demo-projects/releases/download/4.7-6ad6167/2d_dodge_the_creeps.zip
- SHA-256: `cebca9d95eccd4fafa2b9c3892fd75be527c25044e6e78f91fe92b76ece29052`
- Verified local source: `.local/test-games/godot-dodge-the-creeps-4.7-6ad6167/source/dodge_the_creeps/`
- Published browser build: https://godotengine.github.io/godot-demo-projects/2d/dodge_the_creeps/
- Source demo code is MIT; the downloaded README and license files include the
  music, artwork and font attribution and license details. Preserve those if
  distributing the game. The demo and engine are not Maelstrom dependencies.
- The project declares Godot 4.7 and a 480 × 720 viewport. Godot 4.7.2 was
  downloaded to `.local/test-games/godot-4.7.2/` as a portable test runtime,
  with its archive verified against the official release's SHA-256
  `731980f9608d61333e5baf54a2ef17210acc7a538446c0cb9969f002aca1e953`.
  The project's assets have been imported. The native game was launched and
  captured; the browser build has not been verified in this environment.

Start by observing and recording manually played sessions. Use the screen and
normal input as Maelstrom's interface. Separately inspect the source, score and
game-over events to design an evaluation harness, without feeding hidden game state
to the player. A seeded test mode and precise timing instrumentation would require
additional work; they are not features provided by the downloaded game today.
Evaluate on fresh runs: survival time, false or late actions, perception errors,
input-to-observed-response timing and safe stop behavior. Performance will not by
itself establish readiness for Dueling Diego or Wizard101.

A 30-second recording of the native window produced 1,356 verified frames
(45.1 fps) and zero recorder queue rejections. It contains the menu and a short
round. Two visually reviewed examples (start menu and playing) were exported to a
draft teaching pack and loaded by the recognizer. This is a workflow check, not an
independent accuracy measurement, a movement policy, or a 60-fps qualification.
The packaged preview also attached to the real Dodge window and produced a
25-frame review recording. One application-test run had a transient image-save
failure; the affected test passed alone and the full 112-test suite passed on rerun.

## First reaction benchmark: Reflex

[Reflex — Reaction Time Lab](https://github.com/ayannaautomates/reaction-game)
is an existing browser game with randomized delays, a visible cue, early-response
detection and reported reaction times. It is a candidate for testing one cue and
one response before tackling movement. The author describes the code as MIT;
its Three.js and font resources are separately sourced. Its displayed timing uses
the game's internal state change and input event, not a calibrated measurement of
when pixels appeared in the capture stream.

The source is pinned locally at commit `679450da34d8e4840115a8594a9d53f238724d5c`.
`scripts/prepare_reflex_offline.ps1` creates a separate local copy with external
font and Three.js links removed. This uses the original game's built-in non-WebGL
fallback, leaving the upstream source untouched. The launcher opens the local HTML
in Edge app mode: a local rendering engine is still needed, but the game copy has
no external resource URLs. The game window was opened; its screen, Observe capture,
reaction-time test and training pack have not been independently verified.
