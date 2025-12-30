# UX Tokens (WinForms) — Project Maelstrom (ux/beautify-v1)

Framework: WinForms (`UseWindowsForms=true` in ProjectMaelstrom/ProjectMaelstrom/ProjectMaelstrom.csproj). Tokens live in `ProjectMaelstrom/ProjectMaelstrom/Utilities/`.

## Colors (UiColorTokens)
- AccentGreen/AccentRed/AccentAmber/AccentGold (+ AccentGreenText/AccentRedText)
- Surfaces/Text: SurfaceDeep, SurfaceDark, SurfaceMuted, TextMuted
- Status rows: StatusExternalBack/Text, StatusReferenceBack/Text, StatusDeprecatedBack/Text, StatusDefaultBack/Text
- Profiles/Chips: ProfilePublicBack, ProfileExperimentalBack, FilterChipBack

## Spacing (UiSpacingTokens)
- SpaceXs=2, SpaceS=4, SpaceM=8, SpaceL=12, SpaceXl=16
- Padding helpers: PaddingXs/S/M/L/Xl

## Radius (UiRadiusTokens)
- RadiusNone=0, RadiusS=2, RadiusM=4, RadiusL=6

## Typography (UiTypographyTokens)
- FontFamily="Segoe UI"
- TitleSize=14, SectionSize=12, BodySize=10.5, CaptionSize=9

Notes
- Tokens are mapped to existing values; intent is zero visual drift for this foundation pass.
- Prefer tokens for new UI work; avoid reintroducing hard-coded values.
