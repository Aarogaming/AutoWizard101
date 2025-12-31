# POLICY TXT SPEC (aas.policy.txt)

## Format
- Plain text, INI-like.
- Comments start with `#` or `;`.
- Sections use `[section]`; keys use `key = value`.
- Unknown keys: allowed if `denyUnknownKeys=false`; otherwise errors.

## Required sections/keys
[global]
- schemaVersion = 1
- activeProfile = <profile name>
- onInvalidConfig = keepLastKnownGood
- requireAllProfilesValid = true
- denyUnknownCapabilities = true
- denyUnknownKeys = false
- liveMeansLive = true

[ethics]
- purpose = educational_research
- requireConsentForEnvironmentControl = true
- prohibit = comma-separated prohibitions
- privacy.noSecretsInLogs = true
- privacy.storeScreenshots = false
- privacy.storeAudio = false

[profile <name>] (required: catalog, simulation, live_advisory, live_pilot)
- mode = catalog|simulation|live
- autonomy = advisory|pilot|full (required for live profiles)

[ai]
- enabled = true|false
- provider = openai|http|none
- apiKeyEnv = <ENV VAR> (required when provider=openai)
- model = gpt-5.2-pro (example)
- temperature = 0
- allowSendScreenshotsToModel = false
- allowSendAudioToModel = false
- endpoint = <url> (optional, recommended when provider=http)

## Validation rules
- schemaVersion must be 1.
- requireAllProfilesValid=true: missing/invalid required profiles cause errors.
- Unknown capabilities denied when denyUnknownCapabilities=true.
- Unknown keys error when denyUnknownKeys=true; warn otherwise.
- AI provider must be openai|http|none; apiKeyEnv required for openai.
- LIVE means LIVE: if active profile mode=live, OperatingMode stays LIVE; blocked/degraded is reported, no fallback.

## LKG and non-bricking edits
- Invalid edits are rejected; diagnostics are deterministic and sorted.
- Last Known Good snapshot written to `--out/system/policy.lkg.txt` and hash to `--out/system/policy.lkg.sha256`.
- Rejected diagnostics written to `--out/system/policy.rejected.txt`.
- Tooling never overwrites aas.policy.txt automatically.
