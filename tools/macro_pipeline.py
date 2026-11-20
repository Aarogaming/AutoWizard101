"""Deterministic 'Read and Repeat' Macro Pipeline for Maelstrom.

Captures player demonstrations (keyboard/mouse + game telemetry), distills raw
inputs into robust semantic macro graphs (waypoints, UI triggers, combat gates),
and deterministically executes macros without runtime AI overhead.
"""

from __future__ import annotations

import argparse
from dataclasses import asdict, dataclass, field
import json
import math
from pathlib import Path
import sys
from typing import Any, Callable, Dict, List, Optional, Tuple


@dataclass(frozen=True)
class TelemetrySnapshot:
    """Instantaneous game state captured alongside inputs."""

    time_ms: float
    x: float
    y: float
    z: float
    yaw: float
    zone: str = ""
    in_combat: bool = False
    is_my_turn: bool = False
    dialog_open: bool = False
    active_window: Optional[str] = None


@dataclass(frozen=True)
class RawInputEvent:
    """Low-level observed input event from keyboard or mouse."""

    time_ms: float
    type: str  # "key_down", "key_up", "click", "mouse_move"
    control: Optional[str] = (
        None  # Key name (e.g. "W", "X", "Space") or button ("Left")
    )
    x: Optional[int] = None  # Client-relative X pixel
    y: Optional[int] = None  # Client-relative Y pixel


@dataclass
class PairedDemonstrationStep:
    """Synchronized step pairing an input event with concurrent telemetry."""

    input_event: RawInputEvent
    telemetry: TelemetrySnapshot


# ---------------------------------------------------------------------------
# Semantic Macro Actions & Nodes
# ---------------------------------------------------------------------------


@dataclass
class Waypoint:
    x: float
    y: float
    z: float
    tolerance: float = 40.0


@dataclass
class MacroNode:
    """Compiled step in a deterministic macro routine."""

    node_id: str
    kind: str  # "navigate_path", "interact_key", "click_ui", "wait_condition", "combat_gate"
    payload: Dict[str, Any] = field(default_factory=dict)
    timeout_s: float = 30.0


def macro_to_dict(
    nodes: List[MacroNode], title: str = "compiled-macro"
) -> Dict[str, Any]:
    """Serialize macro nodes to schema dictionary."""
    return {
        "schema": "maelstrom.macro-graph.v1",
        "title": title,
        "node_count": len(nodes),
        "nodes": [
            {
                "node_id": n.node_id,
                "kind": n.kind,
                "payload": n.payload,
                "timeout_s": n.timeout_s,
            }
            for n in nodes
        ],
    }


def macro_from_dict(data: Dict[str, Any]) -> List[MacroNode]:
    """Deserialize macro nodes from dictionary."""
    nodes = []
    for item in data.get("nodes", []):
        nodes.append(
            MacroNode(
                node_id=item["node_id"],
                kind=item["kind"],
                payload=item.get("payload", {}),
                timeout_s=float(item.get("timeout_s", 30.0)),
            )
        )
    return nodes


# ---------------------------------------------------------------------------
# Path Simplification (Ramer-Douglas-Peucker for Waypoint Distillation)
# ---------------------------------------------------------------------------


def _perpendicular_distance_2d(
    pt: Tuple[float, float],
    line_start: Tuple[float, float],
    line_end: Tuple[float, float],
) -> float:
    dx = line_end[0] - line_start[0]
    dy = line_end[1] - line_start[1]
    line_len_sq = dx * dx + dy * dy
    if line_len_sq < 1e-6:
        return math.hypot(pt[0] - line_start[0], pt[1] - line_start[1])
    t = max(
        0.0,
        min(
            1.0,
            ((pt[0] - line_start[0]) * dx + (pt[1] - line_start[1]) * dy) / line_len_sq,
        ),
    )
    proj_x = line_start[0] + t * dx
    proj_y = line_start[1] + t * dy
    return math.hypot(pt[0] - proj_x, pt[1] - proj_y)


def simplify_path_rdp(
    points: List[Tuple[float, float, float]], epsilon: float = 35.0
) -> List[Tuple[float, float, float]]:
    """Simplify a 3D coordinate trace into essential waypoints using 2D RDP."""
    if len(points) <= 2:
        return points

    start = (points[0][0], points[0][1])
    end = (points[-1][0], points[-1][1])

    max_dist = 0.0
    index = 0
    for i in range(1, len(points) - 1):
        pt = (points[i][0], points[i][1])
        d = _perpendicular_distance_2d(pt, start, end)
        if d > max_dist:
            max_dist = d
            index = i

    if max_dist > epsilon:
        left = simplify_path_rdp(points[: index + 1], epsilon)
        right = simplify_path_rdp(points[index:], epsilon)
        return left[:-1] + right
    else:
        return [points[0], points[-1]]


# ---------------------------------------------------------------------------
# Macro Recorder: Ingests & Synchronizes Raw Streams
# ---------------------------------------------------------------------------


class MacroRecorder:
    """Records synchronized demonstrations from raw inputs and telemetry."""

    def __init__(self) -> None:
        self.inputs: List[RawInputEvent] = []
        self.telemetry_history: List[TelemetrySnapshot] = []

    def record_input(
        self,
        time_ms: float,
        event_type: str,
        control: Optional[str] = None,
        x: Optional[int] = None,
        y: Optional[int] = None,
    ) -> None:
        self.inputs.append(RawInputEvent(time_ms, event_type, control, x, y))

    def record_telemetry(self, snapshot: TelemetrySnapshot) -> None:
        self.telemetry_history.append(snapshot)

    def pair_streams(self) -> List[PairedDemonstrationStep]:
        """Pairs each input event with the closest telemetry snapshot in time."""
        if not self.inputs or not self.telemetry_history:
            return []

        paired = []
        t_idx = 0
        t_count = len(self.telemetry_history)

        for inp in self.inputs:
            while t_idx + 1 < t_count and abs(
                self.telemetry_history[t_idx + 1].time_ms - inp.time_ms
            ) <= abs(self.telemetry_history[t_idx].time_ms - inp.time_ms):
                t_idx += 1
            paired.append(
                PairedDemonstrationStep(
                    input_event=inp,
                    telemetry=self.telemetry_history[t_idx],
                )
            )
        return paired


# ---------------------------------------------------------------------------
# Macro Compiler: Synthesizes Robust Macro Graph from Demonstration
# ---------------------------------------------------------------------------


class MacroCompiler:
    """Compiles a recorded demonstration into a deterministic Macro Routine Graph.

    Distills:
    - Continuous W/A/S/D movement -> Waypoint corridors via RDP.
    - Interaction keys (X, Space, etc.) -> Condition-gated trigger nodes.
    - Clicks -> UI-anchored or client-relative click nodes.
    - Combat entries -> Intercept gate delegating to combat routine.
    """

    def __init__(self, waypoint_epsilon: float = 40.0) -> None:
        self.waypoint_epsilon = waypoint_epsilon

    def compile(self, paired_steps: List[PairedDemonstrationStep]) -> List[MacroNode]:
        if not paired_steps:
            return []

        nodes: List[MacroNode] = []
        node_counter = 0

        def next_id(prefix: str) -> str:
            nonlocal node_counter
            node_counter += 1
            return f"{prefix}_{node_counter:03d}"

        # Group consecutive movement steps vs discrete interactions
        movement_pts: List[Tuple[float, float, float]] = []

        def flush_movement():
            nonlocal movement_pts
            if len(movement_pts) >= 2:
                simplified = simplify_path_rdp(movement_pts, self.waypoint_epsilon)
                waypoints = [
                    {"x": round(pt[0], 2), "y": round(pt[1], 2), "z": round(pt[2], 2)}
                    for pt in simplified
                ]
                nodes.append(
                    MacroNode(
                        node_id=next_id("nav"),
                        kind="navigate_path",
                        payload={"waypoints": waypoints, "tolerance": 45.0},
                        timeout_s=max(10.0, len(waypoints) * 6.0),
                    )
                )
            movement_pts = []

        for step in paired_steps:
            inp = step.input_event
            telem = step.telemetry

            # 1. Check for combat transition
            if telem.in_combat:
                flush_movement()
                if not nodes or nodes[-1].kind != "combat_gate":
                    nodes.append(
                        MacroNode(
                            node_id=next_id("combat"),
                            kind="combat_gate",
                            payload={"resume_on_peace": True},
                            timeout_s=300.0,
                        )
                    )
                continue

            # 2. Movement keys (W, A, S, D)
            if inp.type in ("key_down", "key_up") and inp.control in (
                "W",
                "A",
                "S",
                "D",
            ):
                # Accumulate spatial position for path reconstruction
                pt = (telem.x, telem.y, telem.z)
                if (
                    not movement_pts
                    or math.hypot(
                        pt[0] - movement_pts[-1][0], pt[1] - movement_pts[-1][1]
                    )
                    > 5.0
                ):
                    movement_pts.append(pt)
                continue

            # 3. Discrete interaction key (e.g. "X" to interact, "Space" to jump)
            if inp.type == "key_down" and inp.control in ("X", "Space", "M", "N", "R"):
                flush_movement()
                nodes.append(
                    MacroNode(
                        node_id=next_id("interact"),
                        kind="interact_key",
                        payload={
                            "key": inp.control,
                            "trigger_telemetry": {
                                "x": round(telem.x, 2),
                                "y": round(telem.y, 2),
                                "z": round(telem.z, 2),
                                "dialog_open": telem.dialog_open,
                            },
                        },
                        timeout_s=5.0,
                    )
                )
                continue

            # 4. Mouse click
            if inp.type == "click" or (
                inp.type == "mouse_down" and inp.control == "Left"
            ):
                flush_movement()
                payload: Dict[str, Any] = {
                    "client_x": inp.x,
                    "client_y": inp.y,
                }
                if telem.active_window:
                    payload["ui_window"] = telem.active_window
                nodes.append(
                    MacroNode(
                        node_id=next_id("click"),
                        kind="click_ui",
                        payload=payload,
                        timeout_s=5.0,
                    )
                )
                continue

        flush_movement()
        return nodes


# ---------------------------------------------------------------------------
# Macro Engine: Playback and Runtime Execution
# ---------------------------------------------------------------------------


class MacroExecutor:
    """Executes a compiled macro routine step-by-step deterministically.

    Emits concrete dispatch commands (PostMessage keys, clicks, or steered walker updates).
    """

    def __init__(self, nodes: List[MacroNode]) -> None:
        self.nodes = list(nodes)
        self.current_index = 0
        self.active_path_index = 0
        self.status = "IDLE"

    @property
    def is_finished(self) -> bool:
        return self.current_index >= len(self.nodes)

    def current_node(self) -> Optional[MacroNode]:
        if self.is_finished:
            return None
        return self.nodes[self.current_index]

    def tick(self, telemetry: TelemetrySnapshot) -> Dict[str, Any]:
        """Processes one evaluation tick against live telemetry.

        Returns an action dictionary to be dispatched to the game window.
        """
        if self.is_finished:
            return {"action": "IDLE", "status": "FINISHED"}

        node = self.nodes[self.current_index]

        # 1. Combat Gate Intercept
        if node.kind == "combat_gate":
            if telemetry.in_combat:
                return {
                    "action": "DELEGATE_COMBAT",
                    "routine": "combat-turn-decision",
                    "node_id": node.node_id,
                }
            else:
                # Combat ended, advance to next node
                self.current_index += 1
                return {"action": "RESUME_MACRO", "status": "COMBAT_RESOLVED"}

        # If we enter combat unexpectedly during non-combat node:
        if telemetry.in_combat:
            return {
                "action": "INTERCEPT_COMBAT",
                "routine": "combat-turn-decision",
                "status": "COMBAT_PAUSE",
            }

        # 2. Navigation Node (Waypoint corridor)
        if node.kind == "navigate_path":
            waypoints = node.payload.get("waypoints", [])
            tolerance = node.payload.get("tolerance", 45.0)

            if self.active_path_index >= len(waypoints):
                # Path completed
                self.active_path_index = 0
                self.current_index += 1
                return {"action": "PATH_COMPLETE", "status": "ADVANCING"}

            target = waypoints[self.active_path_index]
            dist = math.hypot(telemetry.x - target["x"], telemetry.y - target["y"])

            if dist <= tolerance:
                self.active_path_index += 1
                if self.active_path_index >= len(waypoints):
                    self.active_path_index = 0
                    self.current_index += 1
                    return {"action": "PATH_COMPLETE", "status": "ADVANCING"}
                target = waypoints[self.active_path_index]

            # Compute required heading
            target_bearing = math.atan2(
                target["y"] - telemetry.y, target["x"] - telemetry.x
            )
            heading_err = target_bearing - telemetry.yaw
            while heading_err > math.pi:
                heading_err -= 2 * math.pi
            while heading_err < -math.pi:
                heading_err += 2 * math.pi

            keys_held = ["W"]
            if heading_err > 0.15:
                keys_held.append("A")
            elif heading_err < -0.15:
                keys_held.append("D")

            return {
                "action": "STEER_WALK",
                "node_id": node.node_id,
                "target_waypoint": target,
                "distance": round(dist, 1),
                "heading_err_deg": round(math.degrees(heading_err), 1),
                "keys": keys_held,
            }

        # 3. Interact Key
        if node.kind == "interact_key":
            key = node.payload.get("key", "X")
            self.current_index += 1
            return {
                "action": "SEND_KEY",
                "node_id": node.node_id,
                "key": key,
                "duration_ms": 100,
            }

        # 4. Click UI
        if node.kind == "click_ui":
            cx = node.payload.get("client_x", 0)
            cy = node.payload.get("client_y", 0)
            ui_window = node.payload.get("ui_window")
            self.current_index += 1
            return {
                "action": "SEND_CLICK",
                "node_id": node.node_id,
                "x": cx,
                "y": cy,
                "ui_window": ui_window,
            }

        return {"action": "UNKNOWN", "node_id": node.node_id}


# ---------------------------------------------------------------------------
# File Helpers & CLI
# ---------------------------------------------------------------------------


def load_inputs(path: Path) -> List[RawInputEvent]:
    raw = json.loads(path.read_text(encoding="utf-8"))
    events = raw.get("Events", raw) if isinstance(raw, dict) else raw
    parsed = []
    for e in events:
        parsed.append(
            RawInputEvent(
                time_ms=float(e.get("TimeMs", e.get("time_ms", 0))),
                type=str(e.get("Type", e.get("type", "unknown"))),
                control=e.get("Control", e.get("control")),
                x=e.get("X", e.get("x")),
                y=e.get("Y", e.get("y")),
            )
        )
    return parsed


def load_telemetry(path: Path) -> List[TelemetrySnapshot]:
    raw = json.loads(path.read_text(encoding="utf-8"))
    entries = raw.get("snapshots", raw) if isinstance(raw, dict) else raw
    parsed = []
    for s in entries:
        parsed.append(
            TelemetrySnapshot(
                time_ms=float(s.get("time_ms", s.get("TimeMs", 0))),
                x=float(s.get("x", s.get("X", 0))),
                y=float(s.get("y", s.get("Y", 0))),
                z=float(s.get("z", s.get("Z", 0))),
                yaw=float(s.get("yaw", s.get("Yaw", 0))),
                zone=str(s.get("zone", s.get("Zone", ""))),
                in_combat=bool(s.get("in_combat", s.get("InCombat", False))),
                is_my_turn=bool(s.get("is_my_turn", s.get("IsMyTurn", False))),
                dialog_open=bool(s.get("dialog_open", s.get("DialogOpen", False))),
                active_window=s.get("active_window", s.get("ActiveWindow")),
            )
        )
    return parsed


def main() -> int:
    parser = argparse.ArgumentParser(description="Maelstrom Macro Compiler & Replayer")
    subparsers = parser.add_subparsers(dest="command")

    compile_p = subparsers.add_parser(
        "compile", help="Compile demonstration into a macro graph"
    )
    compile_p.add_argument(
        "--inputs", required=True, type=Path, help="Path to input-events.json"
    )
    compile_p.add_argument(
        "--telemetry", required=True, type=Path, help="Path to telemetry.json"
    )
    compile_p.add_argument(
        "--out", required=True, type=Path, help="Output macro JSON path"
    )
    compile_p.add_argument(
        "--epsilon", type=float, default=35.0, help="RDP waypoint tolerance"
    )

    inspect_p = subparsers.add_parser("inspect", help="Inspect a compiled macro graph")
    inspect_p.add_argument("macro", type=Path, help="Path to macro JSON")

    args = parser.parse_args()

    if args.command == "compile":
        inputs = load_inputs(args.inputs)
        telemetry = load_telemetry(args.telemetry)
        recorder = MacroRecorder()
        recorder.inputs = inputs
        recorder.telemetry_history = telemetry
        paired = recorder.pair_streams()

        compiler = MacroCompiler(waypoint_epsilon=args.epsilon)
        nodes = compiler.compile(paired)
        macro_dict = macro_to_dict(nodes, title=args.out.stem)
        args.out.write_text(json.dumps(macro_dict, indent=2), encoding="utf-8")
        print(f"Successfully compiled {len(nodes)} nodes to {args.out}")
        return 0

    elif args.command == "inspect":
        data = json.loads(args.macro.read_text(encoding="utf-8"))
        nodes = macro_from_dict(data)
        print(f"Macro: {data.get('title', 'unnamed')}")
        print(f"Schema: {data.get('schema', 'unknown')}")
        print(f"Total Nodes: {len(nodes)}")
        for idx, node in enumerate(nodes, 1):
            if node.kind == "navigate_path":
                wps = node.payload.get("waypoints", [])
                print(
                    f"  {idx}. [{node.kind}] {len(wps)} waypoints (start={wps[0] if wps else 'none'} end={wps[-1] if wps else 'none'})"
                )
            elif node.kind == "interact_key":
                print(f"  {idx}. [{node.kind}] Key='{node.payload.get('key')}'")
            elif node.kind == "click_ui":
                print(
                    f"  {idx}. [{node.kind}] ({node.payload.get('client_x')}, {node.payload.get('client_y')}) window='{node.payload.get('ui_window')}'"
                )
            elif node.kind == "combat_gate":
                print(
                    f"  {idx}. [{node.kind}] ResumeOnPeace={node.payload.get('resume_on_peace')}"
                )
        return 0

    parser.print_help()
    return 1


if __name__ == "__main__":
    sys.exit(main())
