# Dodge tracking frames

`trail-left.png` and `trail-down.png` are lossless copies of frames from two earlier private WSL runs of the local Godot Dodge demo. They contain the real player and translucent motion trails that caused the previous recognizer to return unknown. The regression test checks the approximate visible player position; these examples are development fixtures, not an independent accuracy evaluation. The demo's upstream license and asset attribution remain with the local test-game copy.

`menu.png`, `ready.png`, and `game-over.png` are private-display frames used to generate the Dodge pack's binary state signatures and verify recognition of those specific examples. They are training/reference examples, not independent state-recognition accuracy measurements.

`top-edge.png` is a later baseline comparison frame where the player was visible near the top edge but the old viewport crop excluded it.
