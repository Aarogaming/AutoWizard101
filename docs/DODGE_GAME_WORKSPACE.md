# Dodge game workspace

The Maelstrom home screen now includes **Dodge the Creeps · game workspace**.
It groups existing capabilities into four modules. Opening the workspace starts
no game, capture, model, input loop or experiment.

| Module | Existing operation it opens |
| --- | --- |
| Observe and record | The shared Observe window, scoped to matching local Dodge windows. Recording and optional input/audio collection retain explicit controls. |
| Specialist and isolated tests | Advice-only visual tracking in Observe; the existing local-game, headless and private WSL test controls. |
| Experiments | Existing frozen baseline/candidate comparisons, score distributions, footage and evidence preservation. |
| Game packs and reports | Existing Workshop callbacks, Dodge pack selection, and actual report folders. |

These are navigation modules, not an editable timeline or a new game engine.
Named actions/rules remain in the existing pack editor. No fake routine playback
or unimplemented clip editing is presented.

## Selection and ownership

`GameWorkspaceForm.PreferDodge()` filters its window list to known local Godot
Dodge titles. An unrelated Godot editor, browser or Wizard101 window is not
selected when Dodge is absent. The user must still choose **Observe** before
capture starts. A title/process match is a routing hint, not authentication;
existing capture/input identity checks remain authoritative.

An active Observe or review session is not switched silently. Its status asks
the user to finish that session before changing games. Selecting Wizard101
through its own workspace restores the generic window list and its Pet Dance
entry points. Selecting Dodge hides those Wizard101-only entry points.

The main home screen supplies its existing Observe, Experiments and Workshop
callbacks. Repeated navigation reuses those windows and preserves the existing
Workshop process ownership. The optional standalone Dodge form falls back to
the same Observe and Experiments classes; Workshop controls remain disabled
unless a host provides their callback.

## Tests and evidence

The Dodge-specific test dialog hides Reflex and keeps the default stepped speed
at 25%. Opening it does not launch a test. Private WSL visual tests, headless
engine checks and desktop tests remain distinct; none proves success for the
other modes. Stop controls and cancellation are the existing implementations.

Reports open their real storage locations:

- Recordings, game packs and experiments: the existing Maelstrom data folder.
- Desktop Dodge tests: `dodge-runs` under that data folder.
- Headless tests: `dodge-native-runs` under that data folder.
- Private visual tests: `.local/wsl-dodge-runs` under the configured source
  workspace.

Missing folders are reported without creating empty results. Shared pack and
recording folders can contain other games; selecting a pack through this
workspace checks its `game_id` before handing it to Workshop. This metadata
check is not full pack validation; Workshop and execution adapters retain
their own validators.

Unit coverage checks Dodge window selection, exclusion of unrelated windows,
pack game-identity ambiguity and the one-MiB routing-read limit. Visual layout,
cross-window navigation and packaging require the coordinating test pass.
