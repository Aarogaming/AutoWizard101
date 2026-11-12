# Wizard101 fairground minigame research

Researched October 30, 2026. Scope: the eight games in the user's selection screen. This is a research baseline, not a claim that any new adapter has been implemented or gameplay tested.

## Findings and suggested order

Official Wizard101 player-guide pages document all eight games. They provide enough information to design initial observers without discovering the rules from recordings. Captures are still needed to verify the current client's board layout, animation timing, input behavior and rewards.

The difficulty ratings below are engineering estimates from the documented mechanics, not measured success rates. Recommended order: Conjuration for a first reliable solver; Potion Motion for practical refill testing; Sorcery Stones; Choo-Choo Zoo; then the four action games. Doodle Doug, Dueling Diego, Skull Riders and Hot Shots have different timing problems, so their relative difficulty will depend on actual captures.

| Game | Documented rules and controls | Proposed recognition and decision method | Estimated difficulty |
| --- | --- | --- | --- |
| Conjuration Concentration | Study the initially exposed cards, then click matching cards before time expires. Completing a board advances the level; consecutive matches and bonus cards improve scoring. | Locate cards, group their pictures by visual similarity, remember coordinates, then click groups and verify removal. Detect the preview and new-board transitions. | Lowest initial complexity |
| Potion Motion | Drag a whole row horizontally or column vertically. Connect at least three matching potions through horizontal/vertical neighbors; diagonals do not count. Matches extend the timer. | Recognize potion types and the grid, simulate row/column shifts, score connected groups, drag once and observe the result. Verify wrapping in the current client. | Moderate |
| Sorcery Stones | Swap neighboring stones to form horizontal or vertical lines of three or more. The board rises; reaching the top ends play. Z accelerates and P pauses. Later special stones change behavior. | Classify cells, track the rising grid, evaluate swaps and cascades, prioritize height reduction, and recognize special states. Confirm legal swap directions experimentally. | Moderate to high |
| Choo-Choo Zoo | Rotate tiles clockwise with left click or counterclockwise with right click. Collect animals and then reach the castle. Occupied tiles cannot be rotated; leaving the path loses. | Convert tiles to a connectivity graph, recognize animal/train/castle positions, plan rotations ahead of the moving train, and replan after tile changes. | Moderate to high |
| Doodle Doug | Move with arrows or WASD, collect every gem, avoid enemies. Space or Ctrl plants a bomb. Bombs start at six and explode after three seconds, potentially killing Doug too. | Detect the maze, player, gems and enemies; plan routes while updating danger areas and bomb countdowns. | High |
| Dueling Diego | Left/right move, Space jumps, A/S/D attack high/middle/low. Navigate obstacles and enemies; bosses gate progression. | Track enemy class, distance, attack pose and obstacles; select attack height or jump using a state machine. | High |
| Skull Riders | Left/right move, Up lifts temporarily, Space fires. Shoot riders, collect skulls and avoid mounted enemies and the bone pit. | Track player altitude, moving enemies, skulls and platforms; predict collisions and aim/fire opportunities. | High |
| Hot Shots | Move the mouse to aim, hold left mouse to charge, release to shoot. Longer charging makes faster arrows. Defend against enemies and incoming shots; gems add points. | Track targets/projectiles, estimate trajectory versus charge time, prioritize imminent threats, and aim with feedback. | High |

Sources for the table: [Conjuration](https://www.wizard101.com/w101playersguide/conjuration-concentration), [Potion Motion](https://www.wizard101.com/w101playersguide/potion-motion), [Sorcery Stones](https://www.wizard101.com/w101playersguide/sorcery-stones), [Choo-Choo Zoo](https://www.wizard101.com/w101playersguide/choo-choo-zoo), [Doodle Doug](https://www.wizard101.com/w101playersguide/doodle-doug), [Dueling Diego](https://www.wizard101.com/w101playersguide/dueling-diego), [Skull Riders](https://www.wizard101.com/w101playersguide/mini-games), [Hot Shots](https://www.wizard101.com/w101playersguide/hot-shots).

## Important details from additional guides

- **Potion Motion is a connected-group puzzle.** A solver should allow bent groups, rather than require a straight line. A 2013 player guide describes rapid dragging as a way to refill potions quickly. This is useful historical evidence, not a measured speed guarantee. [The Friendly Necromancer](https://thefriendlynecromancer.blogspot.com/2013/12/awesome-thing-13-potion-motion.html)
- **Later Conjuration boards may require groups larger than two.** Model matching groups rather than hard-code pairs. The same historical overview discusses differences between enemy types, warns that Skull Riders can respawn uncollected skulls as stronger foes, and describes changing tiles in Choo-Choo Zoo. These details need current-client confirmation. [Matt Plourde's overview](https://www.mmorpg.com/wizard101/system-focus/a-look-at-mini-games-2000116465)
- **Sorcery Stones needs more than color matching.** The wiki documents wildcards, bombs, jewels, reinforced blocks and frozen blocks, as well as restrictions during falling animations. These require explicit visual states. [Wizard101 Central: Sorcery Stones](https://wiki.wizard101central.com/wiki/Basic:Minigames#Sorcery_Stones)
- **Rewards need independent verification.** Wizard101 Central documents a 20-game daily reward limitation while mana/potion refills continue. Treat that as a documented limit to check, rather than assume endless gold/item farming. [Wizard101 Central: Minigames](https://wiki.wizard101central.com/wiki/Basic:Minigames)
- **Refill thresholds are inconsistent in community advice.** A July 2026 thread includes a 1,050-point Potion Motion claim; older advice commonly uses roughly 1,000–1,200 or a level target. Do not build completion around an unverified score. Record the actual potion/mana state before and after a finished round. The same thread mentions a repeatable starting board, also unverified here. [July 2026 player discussion](https://www.reddit.com/r/Wizard101/comments/1v7klxj/go_to_potion_filling/)
- **Potion Motion wrapping is worth testing early.** A July 2026 discussion explains that rows/columns wrap at the edges. That behavior expands the legal move model and explains why apparently immovable boards can have matches. [Player explanations](https://www.reddit.com/r/Wizard101/comments/1uvzt6r/impossible_potion_motion_game/)
- **Diego bosses need separate logic.** A dedicated community page describes level-specific enemies, defenses and boss behavior. It is useful for designing states but should not be treated as frame-accurate timing data. [Dueling Diego guide](https://wizard101.fandom.com/wiki/Dueling_Diego)

## Tutorials and evidence quality

- [Wizard101 Dueling Diego Walkthrough — MegaBigbud11](https://www.youtube.com/watch?v=fFNtf3KcLms): approximately seven minutes, published in 2012. Follow-up visual review sampled all four stages and stepped through the first and final boss encounters. This was a keyframe review, not continuous viewing or calibrated input-timing measurement. Current-client behavior and rewards remain unverified.
- Official player-guide pages above include screenshots and explicit controls for every game. These are the strongest starting references found.
- Wizard101 Central offers one consolidated description of all eight, including extra mechanics absent from shorter official summaries.
- The MMORPG overview and Friendly Necromancer article are historical player accounts. Their strategy details can inform hypotheses; old bug reports and completion-time claims are not current guarantees.
- A Choo-Choo Zoo blog article was discovered, but retrieval failed. It is not used as evidence: [Minigame Madness Chapter 4](https://our-wizards-keep.blogspot.com/2014/04/minigame-madness-chapter4.html).
- Searches for each game did not yield equally useful dedicated video tutorials. Unrelated search matches and generic cheat pages were excluded from the baseline.

## What to build from this baseline

1. **One shared observer:** locate the game window and play area; recognize instructions, preview, active play, transition and results screens. Keep screenshot coordinates relative to the detected window. Record only the game area and timestamps when needed.
2. **Visual recognition first:** use card/potion/tile imagery and geometry for gameplay. OCR is appropriate for score, timer, instructions and results text, rather than as the primary detector of game pieces.
3. **Conjuration prototype:** capture the exposed board, cluster matching card images, store groups and coordinates, and suggest clicks without sending input. Check board recognition across successive layouts, including groups larger than two.
4. **Supervised input:** once suggestions match the visible board, reuse the existing exclusive-input ownership, explicit Start/Stop, focus checks and run limits. Send one verified action at a time. Stop if the board cannot be read confidently or the expected change does not appear.
5. **Potion Motion prototype:** build a connected-component evaluator and row/column move simulator; verify wrap behavior and drag distances. Distinguish a score increase from an actual potion refill.
6. **Expand deliberately:** reuse board detection for Sorcery Stones and tile geometry for Choo-Choo Zoo, but give each its own rules. Action games need continuous motion tracking and measured timing before control.

Short current-client captures remain necessary to calibrate recognition and timing. We do not need long recordings just to rediscover rules that the guides already supply. No game inputs were sent and no new minigame adapter was enabled during this research.
