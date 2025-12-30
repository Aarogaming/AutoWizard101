# UX Changelog

## Overview
- Track cosmetic-only UX changes on branch ux/beautify-v1.

## Screens touched
- Screen 1 (Main form shell): spacing/hierarchy, typography alignment.
- Screen 2 (SettingsForm — Developer Options -> Minigames): spacing/hierarchy, cohesion, accessibility.

## Commits
- ux: introduce design tokens (no visual change)
- ux: screen 1 spacing and hierarchy (Main form)
- ux: screen 1 accessibility polish (focus/readability/state text)
- ux: add screen 2 plan (settings minigames)
- ux: screen 2 spacing and hierarchy (settings minigames)
- ux: screen 2 component cohesion (settings minigames)

## Notes
- UX changes are cosmetic-only; gold baselines remain untouched.

## Screen 2 (SettingsForm — Developer Options -> Minigames)
### Goals
- Improve spacing/hierarchy for filters and list
- Improve component cohesion (chips/buttons/badges) using tokens
- Maintain readability at 175% DPI

### Do not change
- Behavior, filtering logic, data loading
- Tab order
- Gold baselines

### Progress
- Spacing/hierarchy: aligned filters to 8px rhythm, applied Section/Body typography, added breathing room around list and details box.
- Component cohesion: unified filter fonts, dark-surface background on list/details, consistent body typography across filters and list.
