# MaelstromToolkit Recon Notes

Date: 2025-12-30

CLI shape (Program.cs)
- Commands: init; policy init; tags init; stewardship init; ux init (framework arg); ci add (provider/profile); handoff; selftest.
- Global flags parsed: --out, --force, --dry-run, --verbose, --help, --version.
- Help text lists basic usage; no per-command help yet.

Templates and manifest
- Manifest path: MaelstromToolkit/Templates/manifest.json
- Current manifest JSON shape: { "schemaVersion": 1, "templates": [ { "name": "...", "folder": "..." }, ... ] }
- Schema version file: MaelstromToolkit/Templates/schema_version.txt with value "1".
- Templates resolved by combining Templates/<folder>/<name> (folder derived in code, not from manifest fields).
- Placeholders observed: only {{FRAMEWORK}} in UX_STYLE_GUIDE.md (Templates/UX/UX_STYLE_GUIDE.md). Other templates have no placeholders today.

Safety guards
- --out required for write commands; out path must not be filesystem root; symlinks rejected.
- Writes: WriteFile creates directory, writes temp file with UTF-8 (no BOM), moves to target; dry-run deletes temp and logs.
- No strict required-vars enforcement yet; no manifest-driven output path or var validation; template folder inferred via template name.

Selftest (current behavior)
- Command: selftest
- Checks presence of schema_version.txt and manifest.json.
- Verifies hardcoded list of templates exists.
- Copies all templates to a temp dir (with framework "winforms" where needed) then deletes temp dir.
- Reports SELFTEST PASS/FAIL; returns 0 on pass, 1 on missing template/schema/manifest.

Open observations
- Exit codes are not standardized; most errors return 1, some validations return 2 in ValidateOut.
- No template variable enforcement; placeholders other than {{FRAMEWORK}} are not in use today.
- Manifest lacks required_vars/optional_vars or output paths; folder/name used from code.
