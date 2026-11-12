@echo off
setlocal
cd /d "%~dp0"
echo Starting Maelstrom MCP Server (JSON-RPC 2.0 stdio)...
python tools\maelstrom_mcp_server.py
