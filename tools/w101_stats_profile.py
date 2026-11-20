"""Offline, non-authoritative comparison of saved W101 stats and visual references.

CLI: python tools/w101_stats_profile.py --observation ui-result.json
     --reference reference.json --output new-comparison.json

Reference schema (camelCase; native PascalCase observation envelopes also work):
{"schemaVersion":1,"source":"character-tab-ocr" or "manual",
 "target":{"handle":123,"processId":456,"startedUtcTicks":123456,
           "processName":"WizardGraphicalClient","imagePath":"...exe",
           "imageSha256":"<64 hex>"}, "observedUtcTicks":123456,
 "fields":{"m_baseHitpoints":{"value":100,"confidence":1.0},
           "health.maximum":{"value":200,"confidence":0.98}},
 "evidence":{"imagePath":"relative-or-absolute.png","imageSha256":"<64 hex>"}}

Evidence is optional, but both image fields and a matching local image are required
when supplied. References name exact namedFields properties or health/mana/energy
current/maximum keys from resourceCandidate.uiGlobes, or potions.current/maximum
from resourceCandidate.uiPotions. No formulas or inferred aliases are applied.
Confidence must be 0..1 and >=0.95; ambiguous:true is rejected.
Each source field must be within two seconds of the reference's acquisition time.
Numeric comparison has absolute tolerance 1e-6, with no relative tolerance.
Files are bounded to 1 MiB each, images to 16 MiB, and references to 128 fields.
Output preserves exact file hashes and agreements/disagreements/unknowns. It never
updates profiles, live facts, cards, hooks, or permissions. Agreement is supporting
evidence, not proof of field semantics or qualification across game states.

Optional subject on BOTH documents: {"profileId":"local-profile-id",
 "name":"Luke Skywalker","school":"Death","generation":1,
 "verifiedCharacterId":"optional-stable-id","observedUtcTicks":123456}.
Identity strings and positive generation must match exactly; level is ignored.
The subject timestamp defaults to the containing document's statesObservedUtcTicks
or observedUtcTicks. Subject/reference/field acquisition times must be within two
seconds. Legacy subjectless comparisons remain supported but subjectVerified is
false. Even subjectVerified:true is only comparison of supplied evidence, never
independent character recognition or permission to promote fields.
"""

import argparse
import hashlib
import json
import math
from pathlib import Path
import re

MAX_JSON_BYTES = 1024 * 1024
MAX_IMAGE_BYTES = 16 * 1024 * 1024
MAX_FIELDS = 128
MAX_AGE_TICKS = 20_000_000
MIN_CONFIDENCE = 0.95
TARGET_KEYS = (
    "handle",
    "processId",
    "startedUtcTicks",
    "processName",
    "imagePath",
    "imageSha256",
)


def member(value, name, default=None):
    """Accept the existing native PascalCase envelope, never conflicting aliases."""
    if not isinstance(value, dict):
        return default
    matches = [v for k, v in value.items() if k.lower() == name.lower()]
    if len(matches) > 1:
        raise ValueError("Ambiguous JSON member casing")
    return matches[0] if matches else default


def finite(value):
    try:
        return type(value) in (int, float) and math.isfinite(value)
    except OverflowError:
        return False


def digest_valid(value):
    return isinstance(value, str) and re.fullmatch(r"[a-fA-F0-9]{64}", value)


def target(value):
    result = {key: member(value, key) for key in TARGET_KEYS}
    if any(type(result[key]) is not int or result[key] <= 0 for key in TARGET_KEYS[:3]):
        raise ValueError("Exact target identity required")
    if any(
        not isinstance(result[key], str) or not result[key] for key in TARGET_KEYS[3:]
    ):
        raise ValueError("Exact target identity required")
    if not digest_valid(result["imageSha256"]):
        raise ValueError("Exact executable hash required")
    result["imageSha256"] = result["imageSha256"].lower()
    return result


def subject(value):
    """Return bounded identity only; mutable level and timestamps are not identity."""
    if not isinstance(value, dict):
        return None
    result = {
        key: member(value, key)
        for key in ("profileId", "name", "school", "generation", "verifiedCharacterId")
    }
    for key, limit in (("profileId", 80), ("name", 100), ("school", 30)):
        text = result[key]
        if (
            not isinstance(text, str)
            or not text.strip()
            or len(text) > limit
            or any(ord(c) < 32 for c in text)
        ):
            return None
    if (
        type(result["generation"]) is not int
        or not 0 < result["generation"] <= 9223372036854775807
    ):
        return None
    character_id = result["verifiedCharacterId"]
    if character_id is not None and (
        not isinstance(character_id, str)
        or not character_id.strip()
        or len(character_id) > 128
        or any(ord(c) < 32 for c in character_id)
    ):
        return None
    return result


def subject_stamp(document, value):
    return member(
        value,
        "observedUtcTicks",
        member(
            document, "statesObservedUtcTicks", member(document, "observedUtcTicks")
        ),
    )


def near_time(first, second):
    return (
        type(first) is int
        and type(second) is int
        and first > 0
        and second > 0
        and abs(first - second) <= MAX_AGE_TICKS
    )


def compare_documents(observation, reference):
    """Pure comparison. Use compare_files to verify image evidence and file hashes."""
    if not isinstance(observation, dict) or not isinstance(reference, dict):
        raise ValueError("JSON objects required")
    if member(reference, "schemaVersion") != 1 or member(reference, "source") not in (
        "character-tab-ocr",
        "manual",
    ):
        raise ValueError("Unsupported visual-reference schema or source")
    fields = member(reference, "fields")
    if not isinstance(fields, dict) or not 1 <= len(fields) <= MAX_FIELDS:
        raise ValueError("Provide 1..128 explicit reference fields")
    if any(
        not isinstance(k, str) or not re.fullmatch(r"[A-Za-z][A-Za-z0-9_.]{0,79}", k)
        for k in fields
    ):
        raise ValueError("Invalid reference field name")
    reference_target = target(member(reference, "target"))
    observed_target = target(member(observation, "target"))
    reference_stamp = member(reference, "observedUtcTicks")
    if type(reference_stamp) is not int or reference_stamp <= 0:
        raise ValueError("Positive reference acquisition timestamp required")
    rejected = []
    if observed_target != reference_target:
        rejected.append("target-or-build-mismatch")
    observed_raw_subject = member(observation, "subject")
    reference_raw_subject = member(reference, "subject")
    observed_subject = subject(observed_raw_subject)
    reference_subject = subject(reference_raw_subject)
    subject_times = None
    subject_supplied = (
        observed_raw_subject is not None or reference_raw_subject is not None
    )
    if subject_supplied:
        if observed_raw_subject is None or reference_raw_subject is None:
            rejected.append("subject-counterpart-missing")
        elif observed_subject is None or reference_subject is None:
            rejected.append("subject-invalid")
        elif observed_subject != reference_subject:
            rejected.append("subject-mismatch")
        else:
            subject_times = (
                subject_stamp(observation, observed_raw_subject),
                subject_stamp(reference, reference_raw_subject),
            )
            observation_anchor = member(
                observation,
                "statesObservedUtcTicks",
                member(observation, "observedUtcTicks"),
            )
            if (
                not all(near_time(stamp, reference_stamp) for stamp in subject_times)
                or not near_time(*subject_times)
                or not near_time(subject_times[0], observation_anchor)
            ):
                rejected.append("subject-time-mismatch")
    if (
        member(observation, "status") != "observed"
        or member(observation, "loading") is not False
        or member(observation, "cleanupConfirmed") is not True
        or member(observation, "hookRestored") is not True
    ):
        rejected.append("observation-not-complete-and-clean")
    candidate = member(observation, "resourceCandidate", {})
    if member(candidate, "status") != "candidate":
        rejected.append("candidate-unavailable")
    candidates = {}
    named = member(candidate, "namedFields", [])
    if not isinstance(named, list) or len(named) > MAX_FIELDS:
        raise ValueError("Named field inventory exceeds budget")
    for item in named:
        name = member(item, "name")
        if isinstance(name, str):
            candidates.setdefault(name, []).append(
                (
                    member(item, "value"),
                    member(item, "quality") == "candidate",
                    member(candidate, "observedUtcTicks"),
                    "namedFields",
                )
            )
    globes = member(candidate, "uiGlobes", {})
    for name in ("health", "mana", "energy", "potions"):
        pair = (
            member(candidate, "uiPotions", {})
            if name == "potions"
            else member(globes, name, {})
        )
        origin = "uiPotions" if name == "potions" else "uiGlobes"
        for part in ("current", "maximum"):
            value = member(pair, part)
            if value is not None:
                candidates.setdefault(name + "." + part, []).append(
                    (value, True, member(candidate, "uiObservedUtcTicks"), origin)
                )
    rows = []
    for name, visual in fields.items():
        row = {"field": name, "outcome": "unknown", "reason": None}
        rows.append(row)
        value, confidence = member(visual, "value"), member(visual, "confidence")
        if rejected:
            row["reason"] = rejected[0]
        elif not finite(value):
            row["reason"] = "visual-value-not-finite-number"
        elif member(visual, "ambiguous", False) is not False:
            row["reason"] = "visual-value-ambiguous"
        elif not finite(confidence) or not MIN_CONFIDENCE <= confidence <= 1:
            row["reason"] = "visual-confidence-insufficient"
        elif name not in candidates:
            row["reason"] = "candidate-field-missing"
        elif len(candidates[name]) != 1:
            row["reason"] = "candidate-field-ambiguous"
        else:
            actual, valid, stamp, origin = candidates[name][0]
            if not valid or not finite(actual):
                row["reason"] = "candidate-field-unavailable"
            elif (
                type(stamp) is not int
                or stamp <= 0
                or abs(stamp - reference_stamp) > MAX_AGE_TICKS
            ):
                row["reason"] = "candidate-reference-time-mismatch"
            elif subject_times is not None and not all(
                near_time(stamp, subject_time) for subject_time in subject_times
            ):
                row["reason"] = "candidate-subject-time-mismatch"
            else:
                row.update(
                    outcome=(
                        "agreement"
                        if math.isclose(actual, value, rel_tol=0, abs_tol=1e-6)
                        else "disagreement"
                    ),
                    reason="numeric-comparison-only",
                    observedValue=actual,
                    referenceValue=value,
                    confidence=confidence,
                    observedUtcTicks=stamp,
                    candidateSource=origin,
                )
    return {
        "schemaVersion": 1,
        "kind": "w101-stats-profile-comparison",
        "status": "rejected" if rejected else "compared",
        "reasons": rejected,
        "source": member(reference, "source"),
        "target": observed_target,
        "subject": observed_subject,
        "subjectVerified": subject_supplied
        and observed_subject is not None
        and not rejected,
        "referenceObservedUtcTicks": reference_stamp,
        "fields": rows,
        "counts": {
            outcome: sum(r["outcome"] == outcome for r in rows)
            for outcome in ("agreement", "disagreement", "unknown")
        },
        "executable": False,
        "liveQualified": False,
        "canPromote": False,
        "note": "Offline supporting evidence only; no facts, profiles or permissions changed.",
    }


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON key")
        result[key] = value
    return result


def bounded_read(path, maximum):
    with Path(path).open("rb") as stream:
        raw = stream.read(maximum + 1)
    if len(raw) > maximum:
        raise ValueError("Evidence file exceeds size budget")
    return raw


def compare_files(observation_path, reference_path):
    """Read bounded local evidence, verify optional image hash, return audit report."""
    raw_observed = bounded_read(observation_path, MAX_JSON_BYTES)
    raw_reference = bounded_read(reference_path, MAX_JSON_BYTES)
    observation = json.loads(raw_observed, object_pairs_hook=unique_object)
    reference = json.loads(raw_reference, object_pairs_hook=unique_object)
    report = compare_documents(observation, reference)
    report["inputs"] = {
        "observationSha256": hashlib.sha256(raw_observed).hexdigest(),
        "referenceSha256": hashlib.sha256(raw_reference).hexdigest(),
    }
    evidence = member(reference, "evidence")
    if evidence is not None:
        name, expected = member(evidence, "imagePath"), member(evidence, "imageSha256")
        if not isinstance(name, str) or not name or not digest_valid(expected):
            raise ValueError("Image evidence requires path and SHA256")
        path = Path(name)
        if not path.is_absolute():
            path = Path(reference_path).resolve().parent / path
        if path.suffix.lower() not in {".png", ".jpg", ".jpeg", ".bmp", ".webp"}:
            raise ValueError("Unsupported evidence image extension")
        actual = hashlib.sha256(bounded_read(path, MAX_IMAGE_BYTES)).hexdigest()
        if actual != expected.lower():
            raise ValueError("Image evidence hash mismatch")
        report["inputs"]["imageSha256"] = actual
        report["imageEvidenceVerified"] = True
    else:
        report["imageEvidenceVerified"] = False
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--observation", required=True)
    parser.add_argument("--reference", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args(argv)
    try:
        report = compare_files(args.observation, args.reference)
        with Path(args.output).open("x", encoding="utf-8") as stream:
            json.dump(report, stream, indent=2, allow_nan=False)
            stream.write("\n")
    except (ValueError, OSError, RecursionError) as error:
        parser.exit(
            2,
            f"Comparison not saved: {type(error).__name__}. Check bounded evidence and output path.\n",
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
