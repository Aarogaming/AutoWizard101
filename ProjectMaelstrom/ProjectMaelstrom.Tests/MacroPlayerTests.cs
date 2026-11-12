using ProjectMaelstrom.Utilities;
using Xunit;

namespace ProjectMaelstrom.Tests;

public sealed class MacroPlayerTests
{
    [Fact]
    public void ParseMacroCommands_LegacyArray_ReturnsCommands()
    {
        var legacyJson = """
        [
            { "Type": "key_down", "Key": "W", "DelayMs": 0 },
            { "Type": "delay", "DelayMs": 250 },
            { "Type": "key_up", "Key": "W", "DelayMs": 0 },
            { "Type": "click", "X": 300, "Y": 400, "DelayMs": 0 }
        ]
        """;

        var commands = MacroPlayer.ParseMacroCommands(legacyJson);
        Assert.NotNull(commands);
        Assert.Equal(4, commands.Count);
        Assert.Equal("key_down", commands[0].Type);
        Assert.Equal("W", commands[0].Key);
        Assert.Equal("click", commands[3].Type);
        Assert.Equal(300, commands[3].X);
        Assert.Equal(400, commands[3].Y);
    }

    [Fact]
    public void ParseMacroCommands_ConditionedMacroGraph_ExtractsSemanticNodes()
    {
        var graphJson = """
        {
            "schema": "maelstrom.macro-graph.v1",
            "title": "quest-farm-01",
            "node_count": 4,
            "nodes": [
                {
                    "node_id": "nav_001",
                    "kind": "navigate_path",
                    "payload": {
                        "waypoints": [{ "x": 100.0, "y": 200.0, "z": 0.0 }],
                        "tolerance": 45.0
                    }
                },
                {
                    "node_id": "interact_002",
                    "kind": "interact_key",
                    "payload": { "key": "X" }
                },
                {
                    "node_id": "click_003",
                    "kind": "click_ui",
                    "payload": { "client_x": 540, "client_y": 680, "ui_window": "wndDialogMain" }
                },
                {
                    "node_id": "combat_004",
                    "kind": "combat_gate",
                    "payload": { "resume_on_peace": true }
                }
            ]
        }
        """;

        var commands = MacroPlayer.ParseMacroCommands(graphJson);
        Assert.NotNull(commands);
        // nav (1) + interact (key_down, delay, key_up = 3) + click (1) + combat_gate (1) = 6
        Assert.Equal(6, commands.Count);
        Assert.Equal("navigate_waypoints", commands[0].Type);
        Assert.Equal("nav_001", commands[0].Key);

        Assert.Equal("key_down", commands[1].Type);
        Assert.Equal("X", commands[1].Key);
        Assert.Equal("delay", commands[2].Type);
        Assert.Equal("key_up", commands[3].Type);
        Assert.Equal("X", commands[3].Key);

        Assert.Equal("click", commands[4].Type);
        Assert.Equal(540, commands[4].X);
        Assert.Equal(680, commands[4].Y);

        Assert.Equal("combat_gate", commands[5].Type);
    }

    [Fact]
    public void ParseMacroCommands_ExportedStandardZoneRoute_ParsesWaypoints()
    {
        var routeFile = Path.Combine(AppContext.BaseDirectory, "../../../../ProjectMaelstrom/Resources/Macros/commons-to-pet-pavilion.macro.json");
        if (!File.Exists(routeFile))
        {
            // Fallback for direct execution
            routeFile = Path.GetFullPath("ProjectMaelstrom/ProjectMaelstrom/Resources/Macros/commons-to-pet-pavilion.macro.json");
        }
        if (File.Exists(routeFile))
        {
            var json = File.ReadAllText(routeFile);
            var commands = MacroPlayer.ParseMacroCommands(json);
            Assert.NotNull(commands);
            Assert.Single(commands);
            Assert.Equal("navigate_waypoints", commands[0].Type);
            Assert.Equal("nav_commons-to-pet-pavilion", commands[0].Key);
        }
    }

    [Fact]
    public void ParseMacroCommands_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(MacroPlayer.ParseMacroCommands(""));
        Assert.Null(MacroPlayer.ParseMacroCommands("   "));
        Assert.Null(MacroPlayer.ParseMacroCommands("{ invalid json }"));
    }
}
