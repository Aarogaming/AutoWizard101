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
- Align labels/inputs on consistent left edges; prefer padding over borders.
- Section headers: UiTypographyTokens.Section (Segoe UI 12, bold). Titles: UiTypographyTokens.Title (Segoe UI 14, bold).

## Components
- To be defined (buttons, inputs, lists, badges, overlays).

## Accessibility
- To be defined (contrast, focus, keyboard navigation).

## DPI notes
- To be defined (100/125/150/175 scaling expectations).
