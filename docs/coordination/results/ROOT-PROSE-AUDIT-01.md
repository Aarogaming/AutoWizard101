# ROOT-PROSE-AUDIT-01 — focused claim-vs-source check of coordinator documents

Date: 2026-11-16 · owner: root · claim: `ROOT-PROSE-AUDIT-01`

## 1. Scope limit — read this first

Three parallel auditors were commissioned for a full symmetric audit of ROADMAP, PROJECT_ORGANIZATION, INDEX, PRODUCT, W101_AUTOMATION_READINESS, CODEX_WORK, workstreams.json, claims.json and eight root result reports. **All three failed before producing results** with `OpenCode Go 5 hour usage limit reached`. This report is therefore a **focused coordinator-run spot check**, not the commissioned full audit. It checks a bounded set of high-value claims directly; everything below says what was and was not checked. Do not treat "no material overclaims found" as a clean bill of health for the unexamined documents.

## 2. Checks executed and results

| Claim / check | Primary source | Result |
| --- | --- | --- |
| "all 25 root archives" (`W101_LEGACY_WORK_PACKAGES.md`, `ARCHITECTURE_AUDIT_20261111.md`) | `ls *.zip` → **25** files, matching `research/w101-legacy-work-packages.json` `projects: 25` | ✅ **VERIFIED** |
| "`JobDeckRoutines.Create` has 19 entries" (`ARCHITECTURE_AUDIT_20261111.md:26`) | `sed -n '19,117p' JobDeckRoutines.cs` → 19 list entries | ✅ **VERIFIED** |
| "...of which two have dispatch adapters; the other 17 are preparation-only" (same line) | Current entries include conditional delegates for `w101.pet.dance` (`dance is not null`), module cards (`ValidateModuleCard`), World Gate travel (`travel ?? throw...`) and primed visit runtime | ⚠️ **STRUCTURALLY STALE** — the literal two-adapter/17-preparation split no longer describes the code. The *effective* default availability may still be limited, but the statement should be re-dated or restated |
| Relative markdown links in ROADMAP, PROJECT_ORGANIZATION, INDEX, PRODUCT, W101_AUTOMATION_READINESS | Script resolving each relative link against `docs/`: **157 links checked, 0 broken** (INDEX alone: 116) | ✅ **VERIFIED** |
| Date/chronology in the same direction documents | No entries dated after 2026-11-16; CODEX_WORK's newest entries are 2026-11-16 (today) | ✅ **VERIFIED** (corrects an earlier suspicion from this session — the 11-16 entries are current, not future-dated) |
| ROADMAP header currency | `docs/ROADMAP.md:3` says "Current course: **2026-11-14**", while the same file carries 2026-11-15 checkpoints (`:123`, `:126`) and later work | ⚠️ **MINOR STALE** — the header should say 2026-11-16 or point to the latest checkpoint |
| W101_AUTOMATION_READINESS currency | `docs/W101_AUTOMATION_READINESS.md:3` says "Updated 2026-11-11"; INDEX.md:4 still points to it as the task-by-task progress map | ⚠️ **MINOR STALE** — the document self-labels as a snapshot, so this is not false, but it is the only readiness map offered and predates five days of work |
| "knowledge bundle has 8 sources, 9 claims, 12 evidence edges" (`ARCHITECTURE_AUDIT_20261111.md:146-148`) | No tracked bundle found by `git grep` over `research/`, `ProjectMaelstrom/`, `docs/`; likely a `.local` artifact | ❓ **UNVERIFIED** — not contradicted, but not reproducible from committed evidence. A dated audit may legitimately cite a local artifact; say so explicitly |
| Historical native test counts in ROADMAP/CODEX_WORK (2,059 / 2,795 / 2,837 / 2,912 / 2,941, etc.) | Each is presented as a dated checkpoint result, not as a current count; the only executed full-suite number in this session is **3,048 passed / 0 failed** (2026-11-16) | ✅ **OK AS HISTORICAL** — no document claims 3,048, and none claims its older number is current |
| Core scope agreement between ROADMAP, PRODUCT, INDEX, PROJECT_ORGANIZATION | Read all four: game-neutral workbench, W101 first pack, shared scheduler/owner, evidence separation | ✅ **CONSISTENT** |

## 3. Verdict

Focused subset: **no material overclaim found**. Two minor staleness items (ROADMAP header date; the readiness map's currency), one structurally stale count statement (19 entries verified; the 2/17 adapter split is outdated), and one claim that cannot be reproduced from committed files (knowledge bundle counts).

## 4. Required follow-up

1. Re-run the **full** three-group audit when model capacity is available: direction documents, board/claims/CODEX_WORK consistency (including evidence-path existence for every workstream and claim), and the eight named root result reports plus `GEM-HONESTY-AUDIT-01.md` itself. This report is not a substitute.
2. Fix the two minor staleness items in ROADMAP and the readiness map when an owner claim next touches those paths.
3. Re-date or restate `ARCHITECTURE_AUDIT_20261111.md:26` if it is still used as a current capability statement; it is a dated audit and should be read as historical.
4. If the knowledge-bundle counts matter, commit the bundle or state in the audit that they came from an uncommitted `.local` artifact.

## 5. Method and limits

Direct `read`, `grep`, `git grep`, JSON parsing and a Python link resolver; no source edits, no builds, no live access. The three commissioned auditors produced **zero output** due to the model usage limit, so this report covers only the checks listed in §2. It makes no claim about the unexamined documents, and none about live or installed behavior.
