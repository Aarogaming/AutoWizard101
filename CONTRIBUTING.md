# Contributing to Project Maelstrom

Thanks for helping improve the project! This quick guide covers the basics.

## Prerequisites
- Windows 10/11
- .NET 9 SDK
- VS Code (recommended) with these extensions:
  - Git Graph (`mhutchie.git-graph`) for visualizing history/branches (recommended by `.vscode/extensions.json`)
  - C# Dev Kit (optional, but helps with C# tooling)

## Setup
```bash
git clone https://github.com/yourusername/AutoWizard101.git
cd AutoWizard101/ProjectMaelstrom
dotnet restore
dotnet build -c Release
```

## Git workflow
- Create a branch per feature/bugfix.
- Keep commits focused; write clear messages.
- Use Git Graph (VS Code: Command Palette → “Git Graph: View Git Graph”) to visualize history, check diffs, and manage branches.
- Rebase/merge as needed to keep your branch up to date.

## Coding guidelines
- Target: C# 9 / .NET 9, Windows Forms.
- Prefer small, focused changes; keep UI responsive and stable.
- Add concise comments only when logic isn’t obvious.
- Maintain portability: avoid hardcoding user-specific paths; keep external dependencies bundled or optional.

## Testing
- Run `dotnet build -c Release` before raising PRs.
- If you touch image recognition or memory hooks, verify against a running Wizard101 client when possible; otherwise, stub with safe fallbacks.

## Submitting changes
- Open a PR with a short summary and testing notes.
- Mention any new configs/env vars required.
- If you add tools/scripts, note any permissions needed.

## Support / Questions
- File an issue in the repo with repro steps, expected vs. actual behavior, and logs (`bot_log.txt` where relevant).
