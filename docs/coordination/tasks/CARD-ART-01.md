# CARD-ART-01 — Wizard101 card artwork for Gemini

Read root AGENTS.md and docs/coordination/EXTERNAL_AGENT_GUIDE.md first. This is an
offline contribution to the existing project, not a new application or handoff.

## Assignment and ownership

Owner: Gemini, assigned by the coordinator for CORE-DELIVERY. Edit only:

- `ProjectMaelstrom/ProjectMaelstrom/Utilities/W101CardArtwork.cs`
- `ProjectMaelstrom/ProjectMaelstrom/Resources/JobCards/artwork-provenance.json`
- `ProjectMaelstrom/ProjectMaelstrom.Tests/W101CardArtworkTests.cs`
- `docs/coordination/results/CARD-ART-01.md`

Do not edit the deck engine, forms, server, MCP, scheduler, policy, central board,
claims or any other files. Root is actively editing those. No build, packaging,
installation, desktop control, game input, process/memory access or model jobs.
No new dependencies. Ask the coordinator before expanding file ownership.

## Deliverable

Prepare a reusable, lightweight native C# drawing helper for the card fronts.
Namespace `ProjectMaelstrom.Utilities`; `internal static class W101CardArtwork`.
Public-to-the-assembly contract:

```csharp
internal static void DrawIcon(System.Drawing.Graphics graphics,
    System.Drawing.RectangleF bounds, string iconId, System.Drawing.Color ink);
internal static string ResolveTitleFontFamily();
```

DrawIcon must preserve/restore Graphics state, never own/dispose caller objects,
dispose its own GDI objects, and safely draw within the supplied bounds. Support
quest, paw, dance, cards, potion, sword, skull, stones, shovel, fire, train,
idle, replay and default. Simple original vector silhouettes are acceptable.
The same icon will appear in opposing card corners, so remain legible at 24px.
No bitmap creation/cache, file I/O, font installation or external calls during
painting. Caller owns rotation for the opposing corner.

ResolveTitleFontFamily should prefer a verified installed game-style font if
available, then a standard readable Windows serif. Do not invent the identity
of the Wizard101 font or bundle a proprietary font without clear reuse rights.
Inspect existing repository assets first; W101 installation-file inspection is
read-only. Do not change or unpack into the installed game directory. Do not
read clipboard, credentials, private chat, account data or logs.

Keep asset/source provenance in the JSON file. Distinguish original drawings,
existing repo assets, verified game assets, and unresolved font/icon questions.
The user wants a W101-themed player, not unsupported claims of exact assets.

## Review notes

The coordinator is building a purple/parchment/gold card collection, a separate
active Routine Deck, and reversible front/settings panels. Categories include
Questing, Pet Games, Minigames, Maintenance and Offline Review. Cards can exist
in the collection without being included in the active deck. The artwork does
not authorize execution or change any card's qualification.

Write focused tests for supported/unknown icons, multiple dimensions and GDI
state preservation. Do not launch test/build processes; request the shared build
slot from the coordinator and report exact files and unverified assumptions.
