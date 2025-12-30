# UX Style Guide (Skeleton)

Framework
- WinForms (UseWindowsForms=true in ProjectMaelstrom/ProjectMaelstrom/ProjectMaelstrom.csproj).

## Tokens
- Defined under ProjectMaelstrom/ProjectMaelstrom/Utilities/:
  - UiColorTokens.cs (accents, surfaces, status/profile colors)
  - UiSpacingTokens.cs (SpaceXs/S/M/L/XL, padding helpers)
  - UiRadiusTokens.cs (RadiusNone/S/M/L)
  - UiTypographyTokens.cs (font family + Title/Section/Body/Caption sizes)
- Usage must keep visuals unchanged unless explicitly noted.

## Layout rules
- Base grid: 8px rhythm. Use UiSpacingTokens (SpaceXs/S/M/L/XL) for margins/padding.
- Root/top panels: use SpaceL padding; inner panels/cards: SpaceM; small gaps SpaceS.
- Filters/lists (Settings -> Minigames): SpaceS between filter controls, SpaceM between filter row and list; keep list padding consistent with SpaceM top/bottom; details areas use SpaceS top margin and SpaceM bottom margin.
- Align labels/inputs on consistent left edges; prefer padding over borders.
- Section headers: UiTypographyTokens.Section (Segoe UI 12, bold). Titles: UiTypographyTokens.Title (Segoe UI 14, bold).

## Components
- To be defined (buttons, inputs, lists, badges, overlays).

## Accessibility
- Keyboard navigation: Tab/Shift+Tab through all interactive controls; do not alter tab order in Designer.
- Focus visibility: ensure focus rectangle visible on buttons/inputs at 100–175% DPI.
- Text legibility: Section/Body/Caption tokens at 175% must remain readable.
- Status communication: do not rely on color-only; keep accompanying text labels (status, profiles).
- Disabled readability: disabled text must remain legible.
- WinForms limitation: global corner radius requires custom paint; keep radius usage minimal unless custom drawn.

## DPI notes
- To be defined (100/125/150/175 scaling expectations).
