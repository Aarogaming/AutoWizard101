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

    def test_target_window_discovery_uses_native_owner_without_connecting(self):
        result = {
            "ok": True,
            "windows": [
                {
                    "handle": 1234,
                    "processId": 5678,
                    "startedUtcTicks": 9012,
                    "processName": "WizardGraphicalClient",
                    "title": "Wizard101",
                }
            ],
        }
        with mock.patch(
            "maelstrom_deck_client.call_deck", return_value=result
        ) as bridge:
            self.assertEqual(
                result,
                self.server.handle_tool_call("maelstrom_list_target_windows", {}),
            )
            bridge.assert_called_once_with("windows", {})

        unavailable = {"ok": False, "error": "owner unavailable"}
        with mock.patch(
            "maelstrom_deck_client.call_deck", return_value=unavailable
        ) as bridge:
            self.assertEqual(
                unavailable,
                self.server.handle_tool_call("maelstrom_list_target_windows", {}),
            )
            bridge.assert_called_once_with("windows", {})

        with mock.patch("maelstrom_deck_client.call_deck") as bridge:
            reply = self.server.handle_tool_call(
                "maelstrom_list_target_windows", {"connect": True}
            )
            self.assertFalse(reply["ok"])
            bridge.assert_not_called()

        definition = next(
            tool
            for tool in self.server.get_tool_definitions()
            if tool["name"] == "maelstrom_list_target_windows"
        )
        self.assertTrue(definition["inputSchema"]["additionalProperties"] is False)
        self.assertIn("does not connect", definition["description"])

    def test_observation_session_requires_explicit_target_binding_and_opt_ins(self):
        args = {
            "handle": 1234,
            "process_id": 5678,
            "started_utc_ticks": 9012,
            "enable_client_observation": True,
            "enable_ui_tree": True,
        }
        expected = {
            "handle": 1234,
            "processId": 5678,
            "startedUtcTicks": 9012,
            "workspace": str(ROOT.resolve()),
            "enableClientObservation": True,
            "enableUiTree": True,
            "enableQuestActions": False,
            "enableTravelActions": False,
            "enableResourceObservation": False,
        }
        result = {"ok": True, "status": "connected"}
        with mock.patch(
            "maelstrom_deck_client.call_deck", return_value=result
        ) as bridge:
            self.assertEqual(
                result,
                self.server.handle_tool_call(
                    "maelstrom_connect_observation_target", args
                ),
            )
            bridge.assert_called_once_with("connect", expected)

        invalid_args = [
            {key: value for key, value in args.items() if key != "handle"},
            {**args, "enable_ui_tree": False},
            {**args, "enable_client_observation": False},
            {**args, "handle": True},
            {**args, "process_id": 0},
            {**args, "extra": "reject"},
        ]
        for invalid in invalid_args:
            with self.subTest(invalid=invalid), mock.patch(
                "maelstrom_deck_client.call_deck"
            ) as bridge:
                reply = self.server.handle_tool_call(
                    "maelstrom_connect_observation_target", invalid
                )
                self.assertFalse(reply["ok"])
                bridge.assert_not_called()

    def test_observation_session_requires_confirmation_and_preserves_native_results(
        self,
    ):
        for args in ({}, {"confirm_read_only_observation": False}, {"extra": True}):
            with self.subTest(args=args), mock.patch(
                "maelstrom_deck_client.call_deck"
            ) as bridge:
                reply = self.server.handle_tool_call(
                    "maelstrom_observe_connected_target", args
                )
                self.assertFalse(reply["ok"])
                bridge.assert_not_called()

        queued = {"ok": True, "status": "Queued", "completed": False}
        with mock.patch(
            "maelstrom_deck_client.call_deck", return_value=queued
        ) as bridge:
            self.assertEqual(
                queued,
                self.server.handle_tool_call(
                    "maelstrom_observe_connected_target",
                    {"confirm_read_only_observation": True},
                ),
            )
            bridge.assert_called_once_with("observe", {})

    def test_observation_disconnect_is_explicit_and_does_not_stop_operations(self):
        with mock.patch(
            "maelstrom_deck_client.call_deck",
            return_value={"ok": False, "error": "operation active"},
        ) as bridge:
            reply = self.server.handle_tool_call(
                "maelstrom_disconnect_observation_target", {}
            )
            self.assertEqual(reply["error"], "operation active")
            bridge.assert_called_once_with("disconnect", {})

        with mock.patch("maelstrom_deck_client.call_deck") as bridge:
            reply = self.server.handle_tool_call(
                "maelstrom_disconnect_observation_target", {"force": True}
            )
            self.assertFalse(reply["ok"])
            bridge.assert_not_called()

    def test_observation_progress_only_reports_exact_active_id(self):
        operation_id = "a" * 32
        with mock.patch(
            "maelstrom_deck_client.call_deck",
            return_value={"ok": True, "result": {"observerOperation": operation_id}},
        ) as bridge:
            reply = self.server.handle_tool_call(
                "maelstrom_check_observation_progress",
                {"operation_id": operation_id},
            )
            self.assertEqual(reply["status"], "active")
            self.assertFalse(reply["outcome_known"])
            bridge.assert_called_once_with("state", {})

        with mock.patch(
            "maelstrom_deck_client.call_deck",
            return_value={"ok": True, "result": {"observerOperation": operation_id}},
        ):
            reply = self.server.handle_tool_call(
                "maelstrom_check_observation_progress",
                {"operation_id": operation_id.upper()},
            )
            self.assertEqual(reply["status"], "active")

        for native in (
            {"ok": True, "result": {"observerOperation": None}},
            {"ok": True, "result": {"observerOperation": "b" * 32}},
            {"ok": True, "result": {}},
        ):
            with self.subTest(native=native), mock.patch(
                "maelstrom_deck_client.call_deck", return_value=native
            ) as bridge:
                reply = self.server.handle_tool_call(
                    "maelstrom_check_observation_progress",
                    {"operation_id": operation_id},
                )
                self.assertEqual(reply["status"], "outcome_unknown")
                self.assertFalse(reply["outcome_known"])
                self.assertIn("does not retain", reply["note"])
                bridge.assert_called_once_with("state", {})

    def test_observation_progress_rejects_bad_ids_and_preserves_owner_errors(self):
        for args in (
            {},
            {"operation_id": "not-an-id"},
            {"operation_id": "g" * 32},
            {"operation_id": "a" * 32, "extra": True},
        ):
            with self.subTest(args=args), mock.patch(
                "maelstrom_deck_client.call_deck"
            ) as bridge:
                reply = self.server.handle_tool_call(
                    "maelstrom_check_observation_progress", args
                )
                self.assertFalse(reply["ok"])
                bridge.assert_not_called()

        unavailable = {"ok": False, "error": "owner unavailable"}
        with mock.patch(
            "maelstrom_deck_client.call_deck", return_value=unavailable
        ) as bridge:
            self.assertEqual(
                unavailable,
                self.server.handle_tool_call(
                    "maelstrom_check_observation_progress",
                    {"operation_id": "a" * 32},
                ),
            )
            bridge.assert_called_once_with("state", {})

    def test_observation_tool_schemas_are_strict_and_descriptions_are_bounded(self):
        definitions = {
            tool["name"]: tool for tool in self.server.get_tool_definitions()
        }
        for name in (
            "maelstrom_connect_observation_target",
            "maelstrom_observe_connected_target",
            "maelstrom_check_observation_progress",
            "maelstrom_disconnect_observation_target",
        ):
            self.assertFalse(definitions[name]["inputSchema"]["additionalProperties"])
        self.assertIn(
            "exact window identity",
            definitions["maelstrom_connect_observation_target"]["description"],
        )
        self.assertIn(
            "not a completed reading",
            definitions["maelstrom_observe_connected_target"]["description"],
        )
        self.assertIn(
            "never means success",
            definitions["maelstrom_check_observation_progress"]["description"],
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

    def test_read_circuit_snapshot_returns_unwired_scaffold_error(self):
        res = self.server.handle_tool_call(
            "maelstrom_read_circuit_snapshot", {"lease_active": True}
        )
        self.assertFalse(res["ok"])
        self.assertIn("unwired_circuit_scaffold", res["error"])

    def test_propose_bazaar_action_respects_deadman_switch(self):
        snapshot = {
            "status_word": 0,
            "lease_active": False,
            "client_focused": True,
            "bazaar_open": True,
            "player_gold": 100000,
        }
        res = self.server.handle_tool_call(
            "maelstrom_propose_bazaar_action",
            {
                "snapshot": snapshot,
                "target_item": "Sunstone",
                "max_unit_price": 3000,
            },
        )
        self.assertTrue(res["ok"])
        self.assertEqual("HOLD_LEASE_INACTIVE", res["status"])
        self.assertIsNone(res["proposed_action"])

    def test_propose_bazaar_action_gold_floor_aborts(self):
        snapshot = {
            "lease_active": True,
            "client_focused": True,
            "bazaar_open": True,
            "player_gold": 5000,
        }
        res = self.server.handle_tool_call(
            "maelstrom_propose_bazaar_action",
            {
                "snapshot": snapshot,
                "target_item": "Sunstone",
                "max_unit_price": 3000,
                "min_gold_reserve_floor": 10000,
            },
        )
        self.assertTrue(res["ok"])
        self.assertEqual("ABORT_GOLD_FLOOR", res["status"])
        self.assertIsNone(res["proposed_action"])

    def test_propose_bazaar_action_selects_row_and_confirms(self):
        snapshot = {
            "lease_active": True,
            "client_focused": True,
            "bazaar_open": True,
            "confirm_modal_open": False,
            "player_gold": 50000,
            "rows": [
                {
                    "row_index": 2,
                    "item_name": "Sunstone",
                    "quantity": 10,
                    "unit_price_gold": 2400,
                    "confidence": 0.95,
                    "row_bounds": {"x": 0.26, "y": 0.36, "w": 0.42, "h": 0.044},
                }
            ],
        }
        res = self.server.handle_tool_call(
            "maelstrom_propose_bazaar_action",
            {
                "snapshot": snapshot,
                "target_item": "Sunstone",
                "max_unit_price": 3000,
            },
        )
        self.assertTrue(res["ok"])
        self.assertEqual("SELECT_TARGET_ROW", res["status"])
        self.assertEqual(2, res["proposed_action"]["row_index"])

        # When modal is open, it confirms
        snapshot["confirm_modal_open"] = True
        res_modal = self.server.handle_tool_call(
            "maelstrom_propose_bazaar_action",
            {
                "snapshot": snapshot,
                "target_item": "Sunstone",
                "max_unit_price": 3000,
            },
        )
        self.assertEqual("CONFIRM_PURCHASE", res_modal["status"])
        self.assertEqual(
            "bazaar_buy_confirm_dialog_button",
            res_modal["proposed_action"]["anchor_id"],
        )

    def test_read_circuit_bus_returns_unwired_scaffold_error(self):
        res = self.server.handle_tool_call("maelstrom_read_circuit_bus", {})
        self.assertFalse(res["ok"])
        self.assertIn("unwired_circuit_scaffold", res["error"])

    def test_propose_anchor_actuation_verifies_anchors_and_lease(self):
        # 1. Unknown target anchor rejects
        bad_target = self.server.handle_tool_call(
            "maelstrom_propose_anchor_actuation",
            {
                "anchor_id": "non_existent_button",
                "receipt_anchor_id": "native_idle_banner",
            },
        )
        self.assertFalse(bad_target["ok"])
        self.assertIn("Unknown target anchor_id", bad_target["error"])

        # 2. Unknown receipt anchor rejects
        bad_receipt = self.server.handle_tool_call(
            "maelstrom_propose_anchor_actuation",
            {
                "anchor_id": "bazaar_refresh_button",
                "receipt_anchor_id": "non_existent_banner",
            },
        )
        self.assertFalse(bad_receipt["ok"])
        self.assertIn("Unknown receipt_anchor_id", bad_receipt["error"])

        # 3. Deadman switch: Lease inactive -> HOLD_LEASE_INACTIVE
        held = self.server.handle_tool_call(
            "maelstrom_propose_anchor_actuation",
            {
                "anchor_id": "bazaar_buy_confirm_dialog_button",
                "receipt_anchor_id": "bazaar_item_table",
                "bus_snapshot": {
                    "lease_active": False,
                    "active_bit_names": [],
                },
            },
        )
        self.assertTrue(held["ok"])
        self.assertEqual("HOLD_LEASE_INACTIVE", held["status"])
        self.assertFalse(held["action_permitted"])

        # 4. Valid anchors + Lease active -> READY_FOR_EXECUTION
        ready = self.server.handle_tool_call(
            "maelstrom_propose_anchor_actuation",
            {
                "anchor_id": "bazaar_buy_confirm_dialog_button",
                "receipt_anchor_id": "bazaar_player_gold_label",
                "bus_snapshot": {
                    "lease_active": True,
                    "active_bit_names": ["LeaseActive"],
                },
            },
        )
        self.assertTrue(ready["ok"])
        self.assertEqual("READY_FOR_EXECUTION", ready["status"])
        self.assertTrue(ready["action_permitted"])
        self.assertEqual("actuate-anchor", ready["executive_intent"]["command"])
        self.assertIn("actuate-anchor", ready["cli_invocation"])


if __name__ == "__main__":
    unittest.main()
