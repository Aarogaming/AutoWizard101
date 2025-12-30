# UX V1 Release Notes

Tag to create: v1.1.0-ux

Summary
- Tokens foundation (UiColor/Spacing/Radius/Typography) applied with no value drift.
- Screen 1 (Main form): spacing/hierarchy, component cohesion, accessibility/state readability; behavior and tab order unchanged.
- Screen 2 (Settings ? Minigames): spacing/hierarchy, component cohesion (filters/list/details), accessibility; behavior and tab order unchanged.

Constraints (unchanged)
- Gold v1.0.0-gold remains immutable.
- Cosmetic-only; no runtime/policy/executor/plugin/packaging/test changes.
- No baseline refresh; artifacts under artifacts/ux/** ignored and excluded from release.

Known risks
- WinForms DPI/resize drift at extreme sizes; dark surfaces rely on default ForeColor.
- Segoe UI availability; fallback fonts may slightly shift sizing.

Verification
- dotnet build ProjectMaelstrom/ProjectMaelstrom.sln -c Debug (PASS)
- Recommended: spot-check Screen 1 and Screen 2 at 175% DPI and narrow window width (manual, not tracked).
