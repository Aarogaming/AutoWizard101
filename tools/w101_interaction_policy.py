"""Wizard101 adapter admission; not a sandbox for programs launched outside Maelstrom.

Only trusted code assigns a method. Imported manifests and generic live-control
permission cannot authorize memory, injection, game traffic or unknown executables.
Native screen-based routines are implemented in the app, not these legacy launchers.
"""

_OFFLINE_TOOLS = frozenset({"companion", "moonlight", "wizdiff"})
_PROCESS_OBSERVATION_TOOLS = frozenset({"wiztype"})
_CLIENT_INTERNAL_TOOLS = frozenset({"deimos", "wizwalker", "wizsdk"})

_process_observation_allowed = False


def set_process_observation_allowed(allowed: bool) -> None:
    global _process_observation_allowed
    _process_observation_allowed = bool(allowed)


def is_process_observation_allowed() -> bool:
    return _process_observation_allowed


def execution_block_reason(tool: str) -> str | None:
    """Classify fixed adapter identities; Moonlight here decodes saved files only."""
    if tool in _OFFLINE_TOOLS:
        return None
    if tool in _PROCESS_OBSERVATION_TOOLS:
        if _process_observation_allowed:
            return None
        return (
            "Wizard101 UI-only mode: process-memory observation is not enabled. "
            "It requires separate evidence and a scope decision; use screen "
            "observation meanwhile."
        )
    if tool in _CLIENT_INTERNAL_TOOLS:
        return (
            "Wizard101 UI-only mode: client hooks, injection and memory "
            "modification are not enabled. Use visible screens and ordinary "
            "game controls."
        )
    return (
        "Wizard101 UI-only mode: unreviewed legacy executables cannot run. "
        "Use a native screen-based routine or keep this source for offline review."
    )


def require_tool_execution(tool: str) -> None:
    reason = execution_block_reason(tool)
    if reason is not None:
        raise ValueError(reason)
