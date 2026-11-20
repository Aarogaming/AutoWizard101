# Commons route landmark study

Status: observation-only prototype; route remains unqualified.

The user authorized two notified CLI observations. First run
8c8cf3e009214e29917cf370923097ad stopped at post-read readiness after activity,
with cleanup confirmed and zero inputs. Second run
150a1aa9dddb456fb260cfc479915c70 completed after a ten-second focus-switch delay.
Its game-only screenshot shows native Idle beside the Commons lamppost, matching
the user's zone confirmation. No movement or assistant desktop control occurred.

Saved demonstration: human-route-01/video/capture.mp4, SHA256
07B52BE94A5C1471E817F84B09C37AFF2C15958460950404119880FC265321EF.
Derived study: .local/w101-route-study/route-landmarks-13/. Extracted84 frames at
2fps from approximately4–46seconds; source/input alignment is not certified.
Seven references were visually checked: Commons anchor/path, bridge, Pavilion
approach/tunnel/entry and Dance approach. Reference images are learning examples,
not held-out evaluation. The native client currently has different heading/UI.

`tools/route_landmarks.py` uses already installed OpenCV with one worker, at most
500 ORB features per image, bounded image/reference counts, ratio filtering,
unique correspondences and bounded RANSAC. Fixed UI/avatar exclusions are study
masks, not semantic segmentation. No model, input dispatcher or production game
adapter is added. Source hashes, reference hashes and scores are retained.
Every result explicitly denies movement authority, live qualification and arrival.

Result:25/84 frames produced a landmark candidate,59 Unknown. This is coverage,
not accuracy: reference frames are included and frames can contain menus/players.
The current live screenshot returned Unknown with zero usable correspondences.
Do not lower thresholds or use this output to walk blindly. The failed current
match establishes the need for starting-view alignment and more varied data.

Imported steered_walker/path_navigator prototypes were reviewed, not activated.
They expect an external pose feed and their direct window-input dispatcher does
not implement the shared owner/target/release contract. No qualified spatial feed
was found in the reviewed UI reader. Their descriptive claims are not live proof.

Checks:4 unit tests passed; Black with one worker and compileall passed. Only the
verified stalled formatter process was stopped; unrelated Python work was left
alone. No installer, game restart, model or volume change occurred.

Next user-notified check: one existing CLI wake/D300/Idle visit, no forward movement,
to collect an alignment view. Await Ready before this test. A complete route,
interaction, varied-start repetition and fluent automation remain outstanding.

## Notified alignment check and detector comparison

After the user's Ready reply and ten-second delay, CLI visit
388ce9a9ec4a467c9de390879cf038b6 completed wake → D300ms → native Idle. All three
stage receipts are Completed. The post-turn saved frame faces farther toward
Shopping District; final03-idle/module-frame-02.png visibly shows YOU ARE IDLE.
The user could not independently verify this run; record user confirmation as
unavailable. No forward input, assistant desktop control or subsequent live
movement was sent. The original operation report is immutable.

The ORB study still returned Unknown on the post-turn frame. A bounded SIFT
alternative using the same installed OpenCV,1000features and one thread improved
the match without lowering the candidate threshold. Preliminary two-image local
comparison took about100ms; this is not end-to-end reaction latency.

| Saved input | SIFT candidate | Inliers / unique matches |
| --- | --- | --- |
| Earlier current view before turn | Unknown | 7 / 10 |
| Post-turn frame | Commons anchor | 21 / 27 |
| Final native Idle frame | Commons anchor | 19 / 26 |

Across the original84frames SIFT returned27candidates/57Unknown, versus
ORB25/59. Reference frames remain included; these counts are coverage, not
accuracy or held-out route performance. Existing masks are not semantic static
world segmentation. Landmark identity alone grants neither path clearance nor
exact pose, movement authority or arrival verification.

`--detector sift` opts into this offline study; default remains ORB for reproducible
comparison. Five unit tests, Black(singleworker) and compileall pass. No new
production dependency, model, installation or live control path was added.
Next: qualify route-relative alignment/progress cues and a bounded owned route
runner before authorizing forward movement. Preserve the source/reference hashes
and require another attention notification before any live test.
