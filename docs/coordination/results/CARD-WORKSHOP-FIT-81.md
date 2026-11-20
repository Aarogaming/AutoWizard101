# Card Workshop display-fit repair — task 81

Date: 2026-11-16  
Scope: offline UI source repair; verification incomplete.

## Finding and change

An offscreen render of the existing Card Workshop at 1120×790 showed that its fixed startup height exceeds the available vertical area on a 1280×720 display (Windows taskbar reduces the usable area further). The card's settings side is scrollable, but the fixed top-level window could extend past the screen and hide controls.

`CardPrototypeForm` now derives its initial size from the working area of the display under the pointer for headless/offscreen rendering, capped at 1120×790. When shown with an owner window, it recalculates against the owner's monitor and centers itself in that working area. A pure sizing test covers 1280×680 and 1024×720 working areas.

## Evidence and limits

- The existing pre-change offscreen renderer successfully produced front/back images (69,141 and 32,343 bytes); these exposed the fixed-size baseline and are stored in `.local/card-workshop-smoke-157-*.png`.
- After the source change, the focused `CardPrototypeTests` command emitted only the TestProcess build line and no further output for about 25 seconds; it was interrupted. A direct warnings-as-errors build of the main app likewise emitted no output for about 15 seconds and was interrupted. Neither command is a pass or a source/test failure result.
- The new sizing code and regression test are therefore pending compilation, test execution and a refreshed offscreen render. The pre-change images do not validate the repair.
- No install, live app launch, desktop, game, or input operation occurred.

Task 81 remains active pending successful build/test and refreshed front/back rendering.
