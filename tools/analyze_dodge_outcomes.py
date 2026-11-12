"""Summarize saved private outcome-policy decisions, without starting a game."""

import argparse
import json
import math
from pathlib import Path


def summarize_reports(paths):
    paths = [Path(path) for path in paths]
    if not paths or len(paths) > 100:
        raise ValueError("Choose 1–100 completed reports")
    result = {
        "reports": len(paths),
        "decisions": 0,
        "decisions_with_threats": 0,
        "clearance_overrides": 0,
        "no_safe_options": 0,
        "two_phase_fallbacks": 0,
        "two_phase_early": 0,
        "clearance_fallbacks": 0,
        "fallback_method_unrecorded": 0,
        "quiet_decisions": 0,
        "missing_diagnostics": 0,
        "reference_retained": 0,
        "reference_not_retained": 0,
        "model_selected_after_reference_rejected": 0,
    }
    for path in paths:
        if path.stat().st_size > 12_000_000:
            raise ValueError("Report exceeds the analysis size limit")
        report = json.loads(path.read_text(encoding="utf-8"))
        if (
            report.get("policy")
            not in (
                "directional-outcome-v34",
                "directional-outcome-turn-v36",
                "directional-outcome-reference-v37",
                "directional-outcome-escape-v38",
            )
            or report.get("stop_reason") != "game-over"
        ):
            raise ValueError("Only completed outcome-policy games can be summarized")
        decisions = report.get("decisions")
        if not isinstance(decisions, list) or len(decisions) > 9600:
            raise ValueError("Invalid bounded decision log")
        for row in decisions:
            result["decisions"] += 1
            info = row.get("outcome_diagnostics")
            if info is None:
                result["missing_diagnostics"] += 1
                continue
            if (
                not isinstance(info, dict)
                or type(info.get("ClearanceOverride")) is not bool
                or type(info.get("SafeOptions")) is not int
                or not 0 <= info["SafeOptions"] <= 9
            ):
                raise ValueError("Invalid outcome diagnostics")
            if (
                not isinstance(info.get("Advice"), str)
                or not isinstance(info.get("BeforeClearanceAdvice"), str)
                or info["Advice"] != row.get("advice")
                or info["ClearanceOverride"]
                != (info["Advice"] != info["BeforeClearanceAdvice"])
            ):
                raise ValueError("Inconsistent outcome decision")
            clearance = info.get("SelectedClearance")
            if clearance is None:
                if info["SafeOptions"] != 0 or info["ClearanceOverride"]:
                    raise ValueError("Invalid quiet decision")
                result["quiet_decisions"] += 1
                continue
            if type(clearance) not in (int, float) or not math.isfinite(clearance):
                raise ValueError("Invalid predicted clearance")
            result["decisions_with_threats"] += 1
            if report["policy"] in (
                "directional-outcome-reference-v37",
                "directional-outcome-escape-v38",
            ):
                reference = info.get("ReferenceAdvice")
                retained = info.get("ReferenceRetained")
                if (
                    not isinstance(reference, str)
                    or type(retained) is not bool
                    or (
                        retained
                        and (info["Advice"] != reference or info["SafeOptions"] == 0)
                    )
                ):
                    raise ValueError("Invalid reference-policy diagnostic")
                result[
                    "reference_retained" if retained else "reference_not_retained"
                ] += 1
                if (
                    not retained
                    and info["SafeOptions"] > 0
                    and info.get("Fallback") != "two-phase-early"
                ):
                    result["model_selected_after_reference_rejected"] += 1
                if info.get("Fallback") == "two-phase-early":
                    if (
                        report["policy"] != "directional-outcome-escape-v38"
                        or retained
                        or not 1 <= info["SafeOptions"] <= 3
                    ):
                        raise ValueError("Invalid early escape diagnostic")
                    result["two_phase_early"] += 1
            result["clearance_overrides"] += int(info["ClearanceOverride"])
            result["no_safe_options"] += int(info["SafeOptions"] == 0)
            if info["SafeOptions"] == 0:
                fallback = info.get("Fallback")
                if fallback not in (None, "clearance", "two-phase"):
                    raise ValueError("Invalid fallback diagnostic")
                key = (
                    "two_phase_fallbacks"
                    if fallback == "two-phase"
                    else (
                        "clearance_fallbacks"
                        if fallback == "clearance"
                        else "fallback_method_unrecorded"
                    )
                )
                result[key] += 1
    result["note"] = (
        "These are visual predictions and filter choices, not proof of safety or causal benefit. Overrides include retaining the reference or selecting an early escape instead of the model."
    )
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reports", type=Path, nargs="+")
    args = parser.parse_args()
    print(json.dumps(summarize_reports(args.reports), indent=2))


if __name__ == "__main__":
    main()
