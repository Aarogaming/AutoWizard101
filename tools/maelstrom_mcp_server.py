"""Maelstrom Model Context Protocol (MCP) Server.

Provides a standard JSON-RPC 2.0 stdio MCP server for agentic interaction with
Project Maelstrom: game pack discovery, routine graph validation, combat damage
evaluation, minigame solving, and telemetry policy inspection.
"""

from __future__ import annotations

import json
from pathlib import Path
import sys
from typing import Any, Dict, List, Optional

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

# Optional imports from tools
try:
    import w101_interaction_policy as policy
except ImportError:
    policy = None


def _calculate_damage(
    base_damage: float,
    enchant: float = 0.0,
    wizard_flat: float = 0.0,
    wizard_percent: float = 0.0,
    wizard_pierce: float = 0.0,
    boss_resist: float = 0.0,
    boss_boost: float = 0.0,
    modifiers: Optional[List[Dict[str, Any]]] = None,
    critical: float = 1.0,
) -> Dict[str, Any]:
    """Pure arithmetic evaluation matching repaired KursovaLibrary."""
    # Modifiers: list of {"type": "blade"|"shield"|"trap"|"weakness", "percent": float}
    # Applied in reverse order (LIFO)
    mods = list(modifiers or [])
    mods.reverse()

    # Pierce vs Shields & Resist
    remaining_pierce = wizard_pierce
    effective_mods = []

    # First pass: find and pierce shields
    for m in mods:
        m_type = m.get("type", "").lower()
        val = float(m.get("percent", 0.0))
        if m_type == "shield":
            # shield reduces damage: Damage multiplier = 1 - val/100
            if remaining_pierce > 0:
                if remaining_pierce >= val:
                    remaining_pierce -= val
                    continue  # shield completely pierced
                else:
                    val -= remaining_pierce
                    remaining_pierce = 0.0
            effective_mods.append(1.0 - val / 100.0)
        elif m_type == "blade":
            effective_mods.append(1.0 + val / 100.0)
        elif m_type == "trap":
            effective_mods.append(1.0 + val / 100.0)
        elif m_type == "weakness":
            effective_mods.append(1.0 - val / 100.0)
        else:
            effective_mods.append(1.0)

    # Remaining resist after pierce
    if boss_resist > 0:
        net_resist = max(0.0, boss_resist - remaining_pierce)
        if net_resist > 0:
            effective_mods.append(1.0 - net_resist / 100.0)

    # Base damage + flat enchant
    damage = base_damage + enchant

    # Multiply modifiers
    for mult in effective_mods:
        damage *= mult

    # Stats: wizard percent and boss boost
    stat_mult = 1.0
    if wizard_percent != 0:
        stat_mult *= 1.0 + wizard_percent / 100.0
    if boss_boost != 0:
        stat_mult *= 1.0 + boss_boost / 100.0
    damage *= stat_mult

    # Flat damage jewel
    damage += wizard_flat

    final_damage = int(damage)
    crit_damage = int(damage * critical)

    return {
        "base_damage": base_damage,
        "enchant": enchant,
        "calculated_normal": final_damage,
        "calculated_crit": crit_damage,
        "critical_multiplier": critical,
    }


def _validate_routine_graph(graph_data: Any) -> Dict[str, Any]:
    """Ask the native reader; local preflight can reject, never confirm validity."""
    from maelstrom_deck_client import call_deck

    def unconfirmed(message: str) -> Dict[str, Any]:
        return {
            "valid": False,
            "confirmed": False,
            "errors": [message],
            "live_qualified": False,
        }

    if not isinstance(graph_data, dict):
        return unconfirmed("Root must be a JSON object; native validation was not run.")
    try:
        encoded = json.dumps(graph_data, allow_nan=False)
    except (ValueError, TypeError, RecursionError):
        return unconfirmed("Provide finite JSON data; native validation was not run.")
    # Match the native reader's conservative character budget before transport.
    if len(encoded) > 65536:
        return unconfirmed(
            "Routine exceeds 64K characters; native validation was not run."
        )
    reply = call_deck("validate-routine", {"graph": graph_data})
    if not isinstance(reply, dict) or reply.get("ok") is not True:
        reason = reply.get("error") if isinstance(reply, dict) else None
        return unconfirmed(
            "Native validation unavailable or unconfirmed: "
            + (reason[:700] if isinstance(reason, str) else "no valid native reply.")
        )
    result = reply.get("result")
    if (
        type(reply.get("schemaVersion")) is not int
        or reply["schemaVersion"] != 1
        or not isinstance(result, dict)
        or type(result.get("valid")) is not bool
        or result.get("confirmed") is not True
        or result.get("authority") != "native-routine-graphs-v1"
        or result.get("liveQualified") is not False
        or not isinstance(result.get("errors"), list)
        or any(not isinstance(error, str) for error in result["errors"])
        or (result["valid"] and result["errors"])
        or (not result["valid"] and not result["errors"])
        or (
            result["valid"]
            and (
                not isinstance(result.get("routineId"), str)
                or not result["routineId"]
                or type(result.get("nodeCount")) is not int
                or not 1 <= result["nodeCount"] <= 128
            )
        )
    ):
        return unconfirmed(
            "Native validation reply was malformed; validity is unconfirmed."
        )
    return {
        "valid": result["valid"],
        "confirmed": True,
        "authority": result["authority"],
        "routine_id": result.get("routineId"),
        "node_count": result.get("nodeCount"),
        "errors": result["errors"],
        "live_qualified": False,
    }


class MaelstromMcpServer:
    """In-memory MCP protocol dispatcher over JSON-RPC 2.0."""

    def __init__(self, root_dir: Optional[Path] = None):
        self.root_dir = root_dir or ROOT

    def get_tool_definitions(self) -> List[Dict[str, Any]]:
        return [
            {
                "name": "list_game_workspaces",
                "description": "Discover available game workspaces and packs within Maelstrom.",
                "inputSchema": {
                    "type": "object",
                    "properties": {
                        "category": {
                            "type": "string",
                            "description": "Optional category filter (e.g. 'w101', 'dodge')",
                        }
                    },
                },
            },
            {
                "name": "get_w101_policy_state",
                "description": "Inspect only the Python generic legacy-launcher policy. This does not inspect or enable the native Maelstrom adapter/session or its optional W101 client-side permissions.",
                "inputSchema": {
                    "type": "object",
                    "properties": {},
                },
            },
            {
                "name": "validate_routine_graph",
                "description": "Validate a routine graph with the native replay reader through the configured local owner. Requires MAELSTROM_EXE and MAELSTROM_DATA_ROOT. Structural acceptance only; no connection, input or live qualification. An unavailable owner leaves validity unconfirmed.",
                "inputSchema": {
                    "type": "object",
                    "required": ["routine_graph"],
                    "properties": {
                        "routine_graph": {
                            "type": "object",
                            "description": "The native schema-1 graph: id, game_id, start_node, timeout_ms, max_actions, pause_scenes and nodes with declared edges/completion evidence.",
                        }
                    },
                },
            },
            {
                "name": "evaluate_combat_damage",
                "description": "Evaluate spell damage, blades, shields, pierce, boost, and criticals using the repaired pure arithmetic engine.",
                "inputSchema": {
                    "type": "object",
                    "required": ["base_damage"],
                    "properties": {
                        "base_damage": {
                            "type": "number",
                            "description": "Base spell damage",
                        },
                        "enchant": {
                            "type": "number",
                            "default": 0,
                            "description": "Flat damage enchant (e.g. +300 for Epic)",
                        },
                        "wizard_flat": {
                            "type": "number",
                            "default": 0,
                            "description": "Wizard flat damage stat",
                        },
                        "wizard_percent": {
                            "type": "number",
                            "default": 0,
                            "description": "Wizard damage percentage",
                        },
                        "wizard_pierce": {
                            "type": "number",
                            "default": 0,
                            "description": "Wizard pierce percentage",
                        },
                        "boss_resist": {
                            "type": "number",
                            "default": 0,
                            "description": "Target resistance percentage",
                        },
                        "boss_boost": {
                            "type": "number",
                            "default": 0,
                            "description": "Target boost percentage against this school",
                        },
                        "modifiers": {
                            "type": "array",
                            "description": "List of active blades/shields/traps/weaknesses in order of application",
                            "items": {
                                "type": "object",
                                "properties": {
                                    "type": {
                                        "type": "string",
                                        "enum": ["blade", "shield", "trap", "weakness"],
                                    },
                                    "percent": {"type": "number"},
                                },
                                "required": ["type", "percent"],
                            },
                        },
                        "critical": {
                            "type": "number",
                            "default": 2.0,
                            "description": "Critical multiplier (default 2.0)",
                        },
                    },
                },
            },
            {
                "name": "solve_potion_motion_grid",
                "description": "Analyze an 8x8 Potion Motion grid to identify valid row/column moves and scoring connected groups.",
                "inputSchema": {
                    "type": "object",
                    "required": ["board"],
                    "properties": {
                        "board": {
                            "type": "array",
                            "description": "2D grid of potion color names (strings), e.g. 8 rows of 8 columns",
                            "items": {"type": "array", "items": {"type": "string"}},
                        },
                        "allow_wrapping": {
                            "type": "boolean",
                            "default": False,
                            "description": "Whether edge wrapping is enabled for rows/columns",
                        },
                    },
                },
            },
            {
                "name": "plan_combat_turn",
                "description": "Propose one bounded rule-based combat action from explicit known facts. No game input, optimality or live outcome is implied; unsupported mechanics are rejected.",
                "inputSchema": {
                    "type": "object",
                    "required": ["player", "enemies", "hand"],
                    "properties": {
                        "player": {
                            "type": "object",
                            "description": "Player combatant state: id, name, current_hp, max_hp, pips, power_pips, school, gear_damage_boost, pierce, active_blades, etc.",
                        },
                        "enemies": {
                            "type": "array",
                            "description": "List of enemy combatant states: id, name, current_hp, max_hp, school, boost, resist, active_traps, active_shields, is_boss",
                        },
                        "hand": {
                            "type": "array",
                            "description": "Explicit known cards: name, school, spell_type, pip_cost, accuracy, is_aoe and type-specific effect fields. Hybrid effects and unsupported target modes are excluded.",
                        },
                        "effect_order": {
                            "type": "string",
                            "enum": ["application"],
                            "description": "Required when any effect array is nonempty. The caller must supply effects in verified application order; no sorting or source-order inference occurs.",
                        },
                        "emergency_heal_threshold": {
                            "type": "number",
                            "default": 0.35,
                            "description": "HP ratio below which emergency healing is prioritized",
                        },
                    },
                },
            },
            {
                "name": "maelstrom_get_session_telemetry",
                "description": "Read the active native owner's observed facts, source and timestamp. Unobserved vitals are unknown.",
                "inputSchema": {"type": "object", "properties": {}},
            },
            {
                "name": "maelstrom_inspect_routine_deck",
                "description": "Read the native deck's collection, dealt cards, budgets, progress and deferral reasons.",
                "inputSchema": {"type": "object", "properties": {}},
            },
            {
                "name": "maelstrom_deal_job_card",
                "description": "Save a configurable card through the native owner. Requires the current revision; never starts gameplay.",
                "inputSchema": {
                    "type": "object",
                    "required": ["card", "revision"],
                    "properties": {
                        "card": {
                            "type": "object",
                            "description": "JobCardSpec: id, routineId, title, inDeck, priority, runs, attemptLimit, cooldownSeconds, settings, conditions, filters.",
                        },
                        "revision": {"type": "integer"},
                    },
                },
            },
            {
                "name": "maelstrom_step_deck_loop",
                "description": "Admit one eligible registered card through the existing scheduler. Returns admission/state, not an invented completed outcome. Reuse the request ID after a lost response.",
                "inputSchema": {
                    "type": "object",
                    "required": ["request_id"],
                    "properties": {
                        "request_id": {"type": "string", "pattern": "^[a-f0-9]{32}$"},
                    },
                },
            },
            {
                "name": "maelstrom_deck_command",
                "description": "Native deck API: inspect catalog, preview conditions, replace a deck, start a bounded loop, pause/stop, or reset counters. Does not auto-connect or launch a game.",
                "inputSchema": {
                    "type": "object",
                    "required": ["command"],
                    "properties": {
                        "command": {
                            "type": "string",
                            "enum": [
                                "catalog",
                                "preview",
                                "replace",
                                "start",
                                "pause",
                                "stop",
                                "reset",
                            ],
                        },
                        "arguments": {"type": "object"},
                        "request_id": {"type": "string"},
                    },
                },
            },
            {
                "name": "maelstrom_get_capability_catalog",
                "description": "Read the native owner's shared routine and typed fact catalog. Definitions and producer support are not live readings or permission to act. Does not connect, observe, start or refresh gameplay.",
                "inputSchema": {
                    "type": "object",
                    "properties": {},
                    "additionalProperties": False,
                },
            },
            {
                "name": "compile_macro_demonstration",
                "description": "Compile raw user inputs and telemetry snapshots into a robust, deterministic macro graph (waypoints, UI triggers, combat gates).",
                "inputSchema": {
                    "type": "object",
                    "properties": {
                        "inputs": {
                            "type": "array",
                            "description": "Observed input events: list of {time_ms, type, control, x, y}",
                            "items": {"type": "object"},
                        },
                        "telemetry": {
                            "type": "array",
                            "description": "Observed game telemetry frames: list of {time_ms, x, y, z, yaw, in_combat, active_window, etc.}",
                            "items": {"type": "object"},
                        },
                        "title": {"type": "string", "default": "compiled-macro"},
                        "waypoint_epsilon": {"type": "number", "default": 35.0},
                    },
                    "required": ["inputs", "telemetry"],
                },
            },
        ]

    def handle_tool_call(self, name: str, args: Dict[str, Any]) -> Dict[str, Any]:
        if name == "list_game_workspaces":
            packs_dir = self.root_dir / "tools/fixtures/game_packs"
            packs = []
            if packs_dir.exists():
                for p in packs_dir.iterdir():
                    if p.is_dir() and (p / "pack.json").exists():
                        try:
                            meta = json.loads(
                                (p / "pack.json").read_text(encoding="utf-8")
                            )
                            packs.append(
                                {
                                    "id": p.name,
                                    "name": meta.get("name", p.name),
                                    "version": meta.get("version", 1),
                                    "actions": len(meta.get("actions", {})),
                                }
                            )
                        except Exception:
                            packs.append({"id": p.name, "name": p.name})
            return {"workspaces": packs, "count": len(packs)}

        elif name == "get_w101_policy_state":
            is_allowed = False
            if policy and hasattr(policy, "is_process_observation_allowed"):
                is_allowed = policy.is_process_observation_allowed()
            return {
                "scope": "legacy-generic-launcher-only",
                "process_observation_exception_active": is_allowed,
                "client_hooks_and_writes_allowed": False,
                "network_packet_manipulation_allowed": False,
                "game_files_read_only": True,
                "primary_interaction_baseline": "Visible UI and owned ordinary input",
                "native_opt_in_state": "not_observed",
                "reviewed_native_exception_policy": "docs/W101_CLIENT_SIDE_EXCEPTION_20261110.md",
                "note": "These booleans describe the legacy launcher only. The documented W101 native-adapter exception is separate from current session opt-ins, which this tool does not read or change.",
            }

        elif name == "validate_routine_graph":
            graph = args.get("routine_graph", {})
            return _validate_routine_graph(graph)

        elif name == "evaluate_combat_damage":
            return _calculate_damage(
                base_damage=float(args.get("base_damage", 0)),
                enchant=float(args.get("enchant", 0)),
                wizard_flat=float(args.get("wizard_flat", 0)),
                wizard_percent=float(args.get("wizard_percent", 0)),
                wizard_pierce=float(args.get("wizard_pierce", 0)),
                boss_resist=float(args.get("boss_resist", 0)),
                boss_boost=float(args.get("boss_boost", 0)),
                modifiers=args.get("modifiers"),
                critical=float(args.get("critical", 2.0)),
            )

        elif name == "solve_potion_motion_grid":
            try:
                from potion_motion_board import PotionMotionBoard

                board = PotionMotionBoard(args.get("board", []))
                allow_wrapping = bool(args.get("allow_wrapping", False))
                proposals = board.find_best_moves(
                    allow_wrapping=allow_wrapping, limit=5
                )
                return {
                    "best_moves": [p.to_dict() for p in proposals],
                    "total_candidates": len(proposals),
                    "allow_wrapping": allow_wrapping,
                }
            except ImportError:
                return {"error": "potion_motion_board module not yet loaded."}

        elif name == "plan_combat_turn":
            try:
                import combat_turn_planner

                return combat_turn_planner.plan_combat_from_dict(args)
            except ImportError:
                return {"error": "combat_turn_planner module not available."}
            except (ValueError, TypeError) as exc:
                return {
                    "error": f"Combat input is incomplete or unsupported: {exc}",
                    "ready": False,
                }

        elif name == "maelstrom_get_capability_catalog":
            from maelstrom_deck_client import call_deck

            if args:
                return {
                    "ok": False,
                    "error": "Capability discovery takes no arguments.",
                }
            return call_deck("capabilities", {})
        elif name in {
            "maelstrom_get_session_telemetry",
            "maelstrom_inspect_routine_deck",
            "maelstrom_deal_job_card",
            "maelstrom_step_deck_loop",
            "maelstrom_deck_command",
        }:
            from maelstrom_deck_client import call_deck

            commands = {
                "maelstrom_get_session_telemetry": "snapshot",
                "maelstrom_inspect_routine_deck": "state",
                "maelstrom_deal_job_card": "deal",
                "maelstrom_step_deck_loop": "step",
            }
            command = commands.get(name, args.get("command"))
            if name == "maelstrom_deck_command" and command not in {
                "catalog",
                "preview",
                "replace",
                "start",
                "pause",
                "stop",
                "reset",
            }:
                return {"ok": False, "error": "Unsupported deck command."}
            arguments = (
                {"card": args.get("card"), "revision": args.get("revision")}
                if command == "deal"
                else args.get("arguments", {})
            )
            return call_deck(command, arguments, args.get("request_id"))
        elif name == "compile_macro_demonstration":
            try:
                import macro_pipeline

                raw_inputs = [
                    macro_pipeline.RawInputEvent(
                        time_ms=float(i.get("time_ms", i.get("TimeMs", 0))),
                        type=str(i.get("type", i.get("Type", "unknown"))),
                        control=i.get("control", i.get("Control")),
                        x=i.get("x", i.get("X")),
                        y=i.get("y", i.get("Y")),
                    )
                    for i in args.get("inputs", [])
                ]
                telemetry = [
                    macro_pipeline.TelemetrySnapshot(
                        time_ms=float(s.get("time_ms", s.get("TimeMs", 0))),
                        x=float(s.get("x", s.get("X", 0))),
                        y=float(s.get("y", s.get("Y", 0))),
                        z=float(s.get("z", s.get("Z", 0))),
                        yaw=float(s.get("yaw", s.get("Yaw", 0))),
                        zone=str(s.get("zone", s.get("Zone", ""))),
                        in_combat=bool(s.get("in_combat", s.get("InCombat", False))),
                        is_my_turn=bool(s.get("is_my_turn", s.get("IsMyTurn", False))),
                        dialog_open=bool(
                            s.get("dialog_open", s.get("DialogOpen", False))
                        ),
                        active_window=s.get("active_window", s.get("ActiveWindow")),
                    )
                    for s in args.get("telemetry", [])
                ]
                rec = macro_pipeline.MacroRecorder()
                rec.inputs = raw_inputs
                rec.telemetry_history = telemetry
                paired = rec.pair_streams()
                compiler = macro_pipeline.MacroCompiler(
                    waypoint_epsilon=float(args.get("waypoint_epsilon", 35.0))
                )
                nodes = compiler.compile(paired)
                macro_dict = macro_pipeline.macro_to_dict(
                    nodes, title=str(args.get("title", "compiled-macro"))
                )
                return {
                    "macro_graph": macro_dict,
                    "node_count": len(nodes),
                    "summary": {
                        "navigate_nodes": sum(
                            1 for n in nodes if n.kind == "navigate_path"
                        ),
                        "interact_nodes": sum(
                            1 for n in nodes if n.kind == "interact_key"
                        ),
                        "ui_click_nodes": sum(1 for n in nodes if n.kind == "click_ui"),
                        "combat_gates": sum(
                            1 for n in nodes if n.kind == "combat_gate"
                        ),
                    },
                }
            except Exception as exc:
                return {"error": f"Failed to compile macro demonstration: {exc}"}

        else:
            raise ValueError(f"Unknown tool: '{name}'")

    def handle_request(self, request: Dict[str, Any]) -> Optional[Dict[str, Any]]:
        req_id = request.get("id")
        method = request.get("method")
        params = request.get("params", {})

        if method == "initialize":
            return {
                "jsonrpc": "2.0",
                "id": req_id,
                "result": {
                    "protocolVersion": "2024-11-05",
                    "capabilities": {"tools": {}},
                    "serverInfo": {
                        "name": "maelstrom-mcp-server",
                        "version": "1.0.0",
                    },
                },
            }

        elif method == "notifications/initialized":
            return None

        elif method == "tools/list":
            return {
                "jsonrpc": "2.0",
                "id": req_id,
                "result": {"tools": self.get_tool_definitions()},
            }

        elif method == "tools/call":
            tool_name = params.get("name")
            tool_args = params.get("arguments", {})
            try:
                result = self.handle_tool_call(tool_name, tool_args)
                return {
                    "jsonrpc": "2.0",
                    "id": req_id,
                    "result": {
                        "content": [
                            {"type": "text", "text": json.dumps(result, indent=2)}
                        ]
                    },
                }
            except Exception as ex:
                return {
                    "jsonrpc": "2.0",
                    "id": req_id,
                    "isError": True,
                    "result": {
                        "content": [
                            {
                                "type": "text",
                                "text": f"Error executing tool '{tool_name}': {ex}",
                            }
                        ]
                    },
                }

        else:
            return {
                "jsonrpc": "2.0",
                "id": req_id,
                "error": {
                    "code": -32601,
                    "message": f"Method not found: '{method}'",
                },
            }

    def run_stdio(self):
        """Standard JSON-RPC 2.0 line-delimited stdio listener."""
        for line in sys.stdin:
            line = line.strip()
            if not line:
                continue
            try:
                req = json.loads(line)
                resp = self.handle_request(req)
                if resp is not None:
                    sys.stdout.write(json.dumps(resp) + "\n")
                    sys.stdout.flush()
            except json.JSONDecodeError as err:
                err_resp = {
                    "jsonrpc": "2.0",
                    "id": None,
                    "error": {"code": -32700, "message": f"Parse error: {err}"},
                }
                sys.stdout.write(json.dumps(err_resp) + "\n")
                sys.stdout.flush()


if __name__ == "__main__":
    MaelstromMcpServer().run_stdio()
