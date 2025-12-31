# UX Maintenance (Template)
- Scope: UX updates; avoid unintended behavior or tab-order changes.
- Artifacts under artifacts/ux/** remain ignored; do not refresh baselines.
- Gates for UX fixes: dotnet build -c Debug; spot-check affected screens at 175% DPI and narrow window width.
- Keep gold immutable; UX tags capture UX milestones.
