# W101 keep-active reuse check — 2026-11-08

The root ZIP files are local source references, not installed runtimes. This
focused review reads source directly from the archives without executing it.
Full archive hashes and matched file/line records are stored locally in
`.local/w101-keep-active-archive-review.json`. This complements the broader
[repository reuse inventory](REPOSITORY_REUSE_REVIEW.md).

| Source | Relevant material | Decision for this workflow |
| --- | --- | --- |
| `afk-wizard-main.zip` | `main.py`: screen template matching and 1–2 second opposing movement holds in an unbounded farming loop | Useful historical reference for the observe/action loop. It lacks selected-target checks, takeover and bounded runs; do not run its main loop as our keep-active worker. No license file was found in this archive. |
| `wizSDK-master.zip` | `wizsdk/keyboard.py`: per-window key tasks and releases; `window.py`: focus properties; spellbook/confirmation image assets | Candidate for a separately qualified W101 adapter and labeled UI assets. A documented claim of background delivery is not proof it works in this client. Do not replace the validated takeover gate based on that claim. GPL-3.0 license text is present. |
| `wizwalker-master.zip` | `client.py` / `utils.py`: timed input, foreground helpers and game-client services | Keep behind a replaceable W101 adapter; not necessary for the lightweight keep-active test. Its memory/camera features are not part of this change. GPL-3.0 license text is present. |
| `Deimos-Wizard101-main.zip` | `Deimos.py` / `src/utils.py`: foreground and game-control orchestration | Reference for explicit game context and specialist coordination, not a reason to start its entire runtime for a few bounded turns. GPL-3.0 license text is present. |
| `Automatus-v2-master.zip` | `func/functions.py`, mapping/debug bots: window input handling | Reference only pending dedicated compatibility tests. No license file found in this archive. |
| `Wizard101-Utilities-master.zip` | No keep-active/input-routing match in the bounded source scan | No direct reuse for this particular fix; not a judgment of the other utilities. |

No upstream code or new production dependency was imported in this change.
Maelstrom already owns the bounded input primitive, window capture, cancellation,
manual takeover, and evidence recording needed here. The correction is to compare
compact HUD anchors rather than floor pixels, and to expose persistent Spin,
Step and Routine choices. Spin defaults to 60 ms A-only pulses; D-only and
opposing turns remain explicit options. The real failed capture is a regression fixture.

The most useful reuse is now in the **game itself**: this installed W101 client has
**Escape → Idle**, visibly confirmed by its **YOU ARE IDLE** banner. That requires
no recurring Maelstrom keys and is the preferred keep-connected path while the
user operates another app. The A/D routine remains an optional, separately tested
fallback. A [June 2026 report](https://www.mmorpg.com/news/wizard101s-summer-update-the-blighted-veil-raid-update-is-live-2000138231)
describes the added Idle feature; its linked official update URL currently redirects
to the game landing page, so the actual client observation is the primary evidence
for its availability here. Duration tests and movement results must remain distinct.
