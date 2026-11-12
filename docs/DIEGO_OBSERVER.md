# Dueling Diego observer — offline prototype

Open **Launch Diego Observer.cmd** in the repository folder, or choose **Diego observer…** in Tools. It uses the already installed isolated vision environment; no new production packages are needed. When opened through Tools, its process is included in **Stop all tools** and closes with Tools.

This version reads saved images only. It does not connect to Wizard101, capture your screen, record video, or send any game inputs. No gameplay trial has been performed. A local historical tutorial starter pack is available through **Tutorial starter**. It contains 19 manually labeled examples from eight reviewed video frames, covering Diego, barrels, several enemies, projectiles and a falling stalactite. This is example matching, not a trained neural network, and it has not been validated against your current game.

## Try it

1. Choose **Try demo**, then **Replay frames**. The colored patterns are synthetic subjects, explicitly labeled as a demo. They exercise matching and moving-object association, not real game recognition.
   **Tutorial starter** instead opens the real historical reference images and their starter pack. The current local pack uses eight training frames and a separate 0:43 frame for checking. At the default strict threshold, that check recognized both barrels but missed Diego's changed pose. This is a known coverage gap, not a passing gameplay-accuracy result. Lowering the threshold to 0.92 recognized Diego in that frame, while 0.90 introduced false projectile matches; the default remains 0.94. The check has therefore also informed threshold exploration and is not an untouched final test set.
2. For real review, choose **Open frames…** and select consecutive images from a recording or saved screenshots. Images are sorted by filename; use zero-padded names such as `frame-0001.png`. Keep the same resolution and game layout throughout a pack.
3. Choose **New example pack…** and select an empty folder. Packs persist between sessions. **Open example pack…** loads an existing one.
4. Drag around the minigame's play area and choose **Mark play area**. This excludes unrelated parts of the image from matching.
5. Drag tightly around Diego, choose **Diego**, and select **Add example**. Repeat for barrels or other visible subjects. Include multiple examples for different animation poses. Labels include shield positions and the pirate's windup, based on the tutorial research.
6. Use **Inspect frame**, **Next frame**, or **Replay frames** to review outlined matches and suggestions. **Stop review** or Escape stops replay. **Save report…** exports the current result as JSON.

Teach from some frames and check different frames, including different enemy orders, to assess actual recognition. A perfect match on the image used to teach an example does not validate generalization.

## Reactive design

The tutorial informs subject labels and response candidates, never an enemy timetable. Enemies are processed in their observed order; there is no level script saying what must spawn next.

- Each frame is matched independently inside the marked play area.
- Current matches are associated by label and proximity across consecutive frames. A label/pose change is reacquired rather than assumed to be the old pose.
- A response prompt requires one Diego and corroboration in a second consecutive frame. Re-inspecting the same frame does not count as additional evidence.
- Disappeared subjects are removed immediately. Skipped or backward frames reset confirmation. Unknown subjects do not become the next expected enemy.
- Relative movement is derived from the observed gap to Diego, accounting for player position changes. It is an approach/recession cue, not a physical velocity or time-to-collision estimate.
- Suggestions prioritize nearby recognized subjects ahead of Diego. They remain review prompts. Attack reach, jump lead time, projectile trajectory and collision timing are uncalibrated.

## Limits and review rules

Example matching uses image similarity, not a trained classifier or a calibrated probability. The default similarity threshold is 0.94. Low-detail examples are rejected; overlapping matches with similarly strong conflicting labels are suppressed. Objects in unseen poses, changed lighting, scaling, occlusion or overlapping crowds can be missed. The observer cannot establish that a scene is safe just because it finds no known threats.

Packs are tied to the original image size and selected play area. Start a new pack for another layout. Tight examples and multiple poses improve matching but still require held-out-frame checks. No timing is inferred from the replay interval, which is only a comfortable review speed.

Tutorial evidence: [research baseline](MINIGAME_RESEARCH.md) and [Dueling Diego walkthrough](https://www.youtube.com/watch?v=fFNtf3KcLms). The footage is a historical reference; present-client behavior and rewards need separate verification.

The starter pack and cropped frames are local artifacts under `.local/diego-observer/`, not redistributed repository assets. `tools/diego_tutorial_annotations.json` records the manual labels and source. `tools/build_diego_tutorial_pack.py` rebuilds the pack from those exact local crops into an empty destination and records provenance and check results. It does not download video. Additional independent recordings and negative examples are needed before claiming robust recognition. OCR has not been added; it can later supplement sprite recognition with enemy-name and score readings.

## Developer checks

```powershell
.local/envs/dance/Scripts/python.exe -m unittest discover -s tools/tests -p test_diego_vision.py -v
.local/envs/dance/Scripts/python.exe tools/diego_observer.py --demo --output .local/artifacts/diego-demo.json
```

A headless report for a real saved image is also available:

```powershell
.local/envs/dance/Scripts/python.exe tools/diego_observer.py --pack .local/diego-observer/examples --frame path/to/frame.png --output .local/artifacts/diego-frame.json
```

Single-frame command-line reports have no temporal confirmation and explicitly report that limitation. The GUI applies consecutive-frame confirmation. No module contains an input-sending or game-capture implementation.
