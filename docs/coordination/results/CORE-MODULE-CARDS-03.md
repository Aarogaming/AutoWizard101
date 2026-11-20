# Shared module cards

2026-11-11, source-only. Four new game-neutral cards expose existing navigation,
sigil, menu and Auto-Idle compilers/runners through PreparedDeckJob and the shared
deck owner. Their explicit modes are labelled offline replay and saved-screen
review. They never advertise live qualification or input permissions. The old
W101 Auto-Idle card retains its identity and unavailable live status.

Configuration validates mode, timeout and required files. Screen review refuses
a redundant separate bundle path. The adapter validates kind membership and pins
saved source dependencies. No new production dependency or controller is added.

Added tests for each module completing through a real JobDeckService and one
OperationScheduler using synthetic evidence from an independent game profile;
missing receipts do not complete a card. Invalid configurations and preserving
the older live card are covered. No native builds/tests were run by this agent;
the coordinator owns formatting, analyzer and full-suite verification.

Owned files: JobDeckRoutines.cs, ModuleJobCardTests.cs, docs/JOB_DECK.md and this
report, plus coordinator-approved JobDeckForm.cs. The explicit Add card picker
lets older saved decks discover newly registered cards, filtering by game or
game-neutral capabilities and adding only undealt defaults. It preserves prior
cards/history and uses existing revision checks. No game, desktop, installation
or model operations were performed.
