# MaelstromToolkit Phase 1 Spec (Exit Codes, Strict Vars, Plan Output)

Scope
- Tooling/docs only for MaelstromToolkit (net8.0). Do not touch Project Maelstrom runtime/policy/UI/tests. No baseline refresh. No gold retagging.

Current reality to align with (from manifest/templates)
- Manifest: MaelstromToolkit/Templates/manifest.json (schemaVersion=1). Fields today: name, folder; no required_vars/output paths yet.
- Schema version: Templates/schema_version.txt = "1".
- Placeholder syntax: {{FRAMEWORK}} observed in Templates/UX/UX_STYLE_GUIDE.md; other templates have no placeholders.
- Resolution: code currently derives folder from template name; manifest is not yet authoritative for paths/vars.

Exit codes (to implement)
- 0 success
- 1 args/usage
- 2 validation (manifest/schema/vars)
- 3 IO/write failures
- 4 selftest failures

Strict template variables (future changes)
- required_vars must be enforced (render fails before writing if missing).
- Flags: --vars <json or path>, --set key=value (repeatable).
- Precedence: defaults < --vars < --set.
- Missing/unresolved vars: deterministic error listing missing vars (Ordinal sort), no files written.

Plan output / dry-run (future changes)
- For each write command, emit a plan with CREATE / OVERWRITE / SKIP and reason.
- --dry-run prints plan only; writes nothing.
- --fail-on-existing blocks overwrites (unless --force).
- Optional: --plan-json for machine-readable plan.

Per-command help (future changes)
- Each command documents: short description, 2–3 examples with --out, files written (from manifest when available), stop conditions.

Open questions
- Manifest shape expansion: add output_relative_path, required_vars, optional_vars? (not present today).
- Should template folder resolution switch to manifest-driven instead of name-based?
- Vars defaults: none defined yet; clarify if any global defaults are needed beyond {{FRAMEWORK}} substitutions.
