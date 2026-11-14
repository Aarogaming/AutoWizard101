"""Unit tests for Maelstrom MCP Server."""

import json
from pathlib import Path
import sys
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))

from maelstrom_mcp_server import MaelstromMcpServer


class MaelstromMcpServerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.server = MaelstromMcpServer(root_dir=ROOT)

    def test_initialize(self):
        req = {"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {}}
        res = self.server.handle_request(req)
        self.assertEqual(res["id"], 1)
        self.assertEqual(res["result"]["serverInfo"]["name"], "maelstrom-mcp-server")
        self.assertIn("tools", res["result"]["capabilities"])

    def test_capability_discovery_is_read_only_and_preserves_native_result(self):
        for result in (
            {"ok": False, "error": "owner unavailable"},
            {"ok": True, "result": {"facts": [], "routines": []}},
        ):
            with mock.patch(
                "maelstrom_deck_client.call_deck", return_value=result
            ) as bridge:
                self.assertEqual(
                    result,
                    self.server.handle_tool_call(
                        "maelstrom_get_capability_catalog", {}
                    ),
                )
                bridge.assert_called_once_with("capabilities", {})
        with mock.patch("maelstrom_deck_client.call_deck") as bridge:
            reply = self.server.handle_tool_call(
                "maelstrom_get_capability_catalog", {"connect": True}
            )
            self.assertFalse(reply["ok"])
            bridge.assert_not_called()
        self.assertIn(
            "maelstrom_get_capability_catalog",
            [t["name"] for t in self.server.get_tool_definitions()],
        )

    def test_combat_planner_does_not_default_missing_facts(self):
        response = self.server.handle_request(
            {
                "jsonrpc": "2.0",
                "id": 21,
                "method": "tools/call",
                "params": {
                    "name": "plan_combat_turn",
                    "arguments": {"player": {}, "enemies": [], "hand": []},
                },
            }
        )
        payload = json.loads(response["result"]["content"][0]["text"])
        self.assertFalse(payload["ready"])
        self.assertIn("incomplete or unsupported", payload["error"])
        self.assertNotIn("action", payload)

    def test_deck_tools_use_native_owner_and_never_invent_success(self):
        cases = [
            ("maelstrom_get_alias_register", {}, "snapshot", {}),
            ("maelstrom_get_session_telemetry", {}, "snapshot", {}),
            ("maelstrom_inspect_routine_deck", {}, "state", {}),
            (
                "maelstrom_deal_job_card",
                {"card": {"id": "draft"}, "revision": 7},
                "deal",
                {"card": {"id": "draft"}, "revision": 7},
            ),
            ("maelstrom_step_deck_loop", {"request_id": "a" * 32}, "step", {}),
            ("maelstrom_deck_command", {"command": "stop"}, "stop", {}),
        ]
        for name, args, command, expected in cases:
            with self.subTest(name=name), mock.patch(
                "maelstrom_deck_client.call_deck",
                return_value={"ok": False, "error": "offline"},
            ) as bridge:
                res = self.server.handle_request(
                    {
                        "jsonrpc": "2.0",
                        "id": 9,
                        "method": "tools/call",
                        "params": {"name": name, "arguments": args},
                    }
                )
                payload = json.loads(res["result"]["content"][0]["text"])
                self.assertEqual(payload, {"ok": False, "error": "offline"})
                bridge.assert_called_once_with(
                    command, expected, args.get("request_id")
                )

    def test_generic_deck_tool_cannot_route_arbitrary_client_commands(self):
        with mock.patch("maelstrom_deck_client.call_deck") as bridge:
            res = self.server.handle_request(
                {
                    "jsonrpc": "2.0",
                    "id": 10,
                    "method": "tools/call",
                    "params": {
                        "name": "maelstrom_deck_command",
                        "arguments": {"command": "send-key"},
                    },
                }
            )
            payload = json.loads(res["result"]["content"][0]["text"])
            self.assertFalse(payload["ok"])
            bridge.assert_not_called()

    def test_tools_list(self):
        req = {"jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {}}
        res = self.server.handle_request(req)
        tools = res["result"]["tools"]
        tool_names = [t["name"] for t in tools]
        self.assertIn("list_game_workspaces", tool_names)
        self.assertIn("get_w101_policy_state", tool_names)
        self.assertIn("validate_routine_graph", tool_names)
        self.assertIn("evaluate_combat_damage", tool_names)
        self.assertIn("solve_potion_motion_grid", tool_names)

    def test_policy_report_is_legacy_scoped_and_never_infers_native_permission(self):
        for observation_enabled in (False, True):
            with self.subTest(observation_enabled=observation_enabled), mock.patch(
                "maelstrom_mcp_server.policy"
            ) as policy, mock.patch("maelstrom_deck_client.call_deck") as bridge:
                policy.is_process_observation_allowed.return_value = observation_enabled
                payload = self.server.handle_tool_call("get_w101_policy_state", {})
                self.assertEqual(payload["scope"], "legacy-generic-launcher-only")
                self.assertEqual(
                    payload["process_observation_exception_active"], observation_enabled
                )
                self.assertFalse(payload["client_hooks_and_writes_allowed"])
                self.assertEqual(payload["native_opt_in_state"], "not_observed")
                self.assertEqual(
                    payload["reviewed_native_exception_policy"],
                    "docs/W101_CLIENT_SIDE_EXCEPTION_20261110.md",
                )
                policy.is_process_observation_allowed.assert_called_once_with()
                policy.set_process_observation_allowed.assert_not_called()
                policy.require_tool_execution.assert_not_called()
                bridge.assert_not_called()

    def test_list_game_workspaces(self):
        req = {
            "jsonrpc": "2.0",
            "id": 3,
            "method": "tools/call",
            "params": {"name": "list_game_workspaces", "arguments": {}},
        }
        res = self.server.handle_request(req)
        payload = json.loads(res["result"]["content"][0]["text"])
        self.assertIn("workspaces", payload)
        self.assertIn("count", payload)

    def test_evaluate_combat_damage(self):
        req = {
            "jsonrpc": "2.0",
            "id": 4,
            "method": "tools/call",
            "params": {
                "name": "evaluate_combat_damage",
                "arguments": {
                    "base_damage": 100,
                    "enchant": 50,
                    "modifiers": [
                        {"type": "blade", "percent": 40},
                        {"type": "shield", "percent": 50},
                    ],
                    "wizard_percent": 0,
                    "boss_boost": 0,
                    "critical": 1.0,
                },
            },
        }
        res = self.server.handle_request(req)
        payload = json.loads(res["result"]["content"][0]["text"])
        # (100 + 50) = 150. Blade 40% (1.4), Shield 50% (0.5). Reverse order: 150 * 0.5 * 1.4 = 105.
        self.assertEqual(payload["calculated_normal"], 105)

    def validate_graph(self, graph):
        req = {
            "jsonrpc": "2.0",
            "id": 5,
            "method": "tools/call",
            "params": {
                "name": "validate_routine_graph",
                "arguments": {"routine_graph": graph},
            },
        }
        res = self.server.handle_request(req)
        return json.loads(res["result"]["content"][0]["text"])

    @staticmethod
    def routine_graph():
        return {
            "schema_version": 1,
            "id": "test-routine",
            "game_id": "test-game",
            "start_node": "done",
            "timeout_ms": 1000,
            "max_actions": 1,
            "pause_scenes": ["loading"],
            "nodes": [{"id": "done", "edges": [], "complete_scene": "finished"}],
        }

    @staticmethod
    def native_validation(valid=True):
        return {
            "ok": True,
            "schemaVersion": 1,
            "result": {
                "valid": valid,
                "confirmed": True,
                "authority": "native-routine-graphs-v1",
                "errors": [] if valid else ["Unsupported native routine contract."],
                "routineId": "test-routine" if valid else None,
                "nodeCount": 1 if valid else None,
                "liveQualified": False,
            },
        }

    def test_validate_routine_graph_delegates_to_canonical_native_reader(self):
        graph = self.routine_graph()
        with mock.patch(
            "maelstrom_deck_client.call_deck", return_value=self.native_validation()
        ) as bridge:
            payload = self.validate_graph(graph)
        self.assertTrue(payload["valid"])
        self.assertTrue(payload["confirmed"])
        self.assertFalse(payload["live_qualified"])
        self.assertEqual(payload["node_count"], 1)
        self.assertEqual(payload["authority"], "native-routine-graphs-v1")
        bridge.assert_called_once_with("validate-routine", {"graph": graph})

    def test_previous_wrong_shape_is_never_accepted_by_a_local_fallback(self):
        graph = {
            "schema_version": 2,
            "routine_id": "test-routine",
            "nodes": [{"id": "entry", "edges": [{"next_node": "complete"}]}],
        }
        native = self.native_validation(False)
        with mock.patch(
            "maelstrom_deck_client.call_deck", return_value=native
        ) as bridge:
            payload = self.validate_graph(graph)
        self.assertFalse(payload["valid"])
        self.assertTrue(payload["confirmed"])
        self.assertEqual(payload["errors"], native["result"]["errors"])
        bridge.assert_called_once_with("validate-routine", {"graph": graph})

    def test_unavailable_or_old_owner_leaves_validation_unconfirmed(self):
        for reply in (
            {"ok": False, "error": "Owner unavailable"},
            {"ok": False, "error": "Unknown deck command."},
            None,
        ):
            with self.subTest(reply=reply), mock.patch(
                "maelstrom_deck_client.call_deck", return_value=reply
            ):
                payload = self.validate_graph(self.routine_graph())
                self.assertFalse(payload["valid"])
                self.assertFalse(payload["confirmed"])
                self.assertFalse(payload["live_qualified"])
                self.assertTrue(payload["errors"])

    def test_malformed_native_validation_never_becomes_valid(self):
        cases = [
            {"valid": "true"},
            {"confirmed": False},
            {"authority": "another-reader"},
            {"liveQualified": True},
            {"errors": ["unexpected"]},
            {"errors": None},
            {"routineId": None},
            {"nodeCount": True},
            {"nodeCount": 0},
        ]
        replies = []
        for changes in cases:
            reply = self.native_validation()
            reply["result"].update(changes)
            replies.append(reply)
        replies.extend(
            [
                {"ok": True},
                {**self.native_validation(), "schemaVersion": True},
                {**self.native_validation(), "schemaVersion": 2},
            ]
        )
        for reply in replies:
            with self.subTest(reply=reply), mock.patch(
                "maelstrom_deck_client.call_deck", return_value=reply
            ):
                payload = self.validate_graph(self.routine_graph())
                self.assertFalse(payload["valid"])
                self.assertFalse(payload["confirmed"])

    def test_validation_preflight_rejects_unbounded_or_non_json_without_bridge(self):
        for graph in ([], None, {"too_big": "x" * 65537}, {"number": float("nan")}):
            with self.subTest(graph_type=type(graph).__name__), mock.patch(
                "maelstrom_deck_client.call_deck"
            ) as bridge:
                payload = self.validate_graph(graph)
                self.assertFalse(payload["valid"])
                self.assertFalse(payload["confirmed"])
                bridge.assert_not_called()

    def test_compile_macro_demonstration(self):
        inputs = [
            {"time_ms": 10.0, "type": "key_down", "control": "W"},
            {"time_ms": 100.0, "type": "key_down", "control": "X"},
        ]
        telemetry = [
            {"time_ms": 10.0, "x": 0.0, "y": 0.0, "z": 0.0, "yaw": 0.0},
            {"time_ms": 100.0, "x": 50.0, "y": 0.0, "z": 0.0, "yaw": 0.0},
        ]
        req = {
            "jsonrpc": "2.0",
            "id": 7,
            "method": "tools/call",
            "params": {
                "name": "compile_macro_demonstration",
                "arguments": {
                    "inputs": inputs,
                    "telemetry": telemetry,
                    "title": "mcp-test-macro",
                },
            },
        }
        res = self.server.handle_request(req)
        payload = json.loads(res["result"]["content"][0]["text"])
        self.assertIn("macro_graph", payload)
        self.assertEqual(payload["macro_graph"]["title"], "mcp-test-macro")
        self.assertGreaterEqual(payload["node_count"], 1)
        self.assertIn("summary", payload)


if __name__ == "__main__":
    unittest.main()
