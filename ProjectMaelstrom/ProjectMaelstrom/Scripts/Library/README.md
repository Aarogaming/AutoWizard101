# Script Library Notes

- The app writes OCR snapshots to `Scripts/.cache/snapshot.json` every ~5s. Use `snapshot_reader.py` to consume health/mana/gold/sync data from Python scripts.
- Imported third-party projects are kept outside this repository (the `Archive/imported-projects/` folder beside the repository root) and are reference only. Only first-party wrappers and the repos listed below stay here; they need their own dependencies.
- To hook a script into shared state:
  1) Import `snapshot_reader` (adjacent) and call `load_snapshot()` instead of doing your own OCR.
  2) Avoid sending inputs if `Dry run` is checked in the UI.
- Repos included:
  - CrownsTriviaBot (FreekiGames-Trivia-Bot) — needs manual clone.
  - SeleniumCaptchaBot (Wizard101Bot-Selenium-Captcha) — needs manual clone.
  - wizwalker moved to `third_party/wizwalker/` (pinned read-only dependency).
  - Wizard101-Farming
  - Wizard101-Utilities

If a repo folder is missing, the launcher opens its README/folder. Install dependencies per each repo’s instructions.
