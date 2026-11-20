# Windowless recovery and adapter qualification — 2026-11-12

Fixed the circular CLI recovery dependency on foreground ownership. Explicit CLI recovery now reads asynchronous input state from the active accessible same desktop (DESKTOP_READOBJECTS|DESKTOP_HOOKCONTROL), with stable nonzero foreground during each reading. GUI recovery keeps its prior foreground requirement. No simulated input, focus change, admin elevation, registry workaround or deleted warning. Existing stopped-worker hold, repeated fresh quiet samples, global exclusive recovery transaction and durable evidence are retained.

W101 and the helper reported medium integrity (8192). The initial elevation-field diagnostic returned invalid data and is not evidence of elevation status; no administrator launch was performed. Foreground token inspection was unavailable. The successful fix did not require elevation.

Live recovery: .local/w101-cycle-06/recovery-21.json stage Restored. Durable record: recovery-history/97cd5624d34644a2b3e46f9b09525b00 in the normal Maelstrom session-lock directory.

Live new-profile observation: operation0feeb02dac6243dd9992b2009c88858c, 2 UI trees,8 loading samples, no loading, cleanupConfirmed and hookRestored true. Optional pose candidate XYZ328.905,411.104,0.00003 yaw6.27657. No game input. Idle overlay structure is present but idleText is empty under archived text decoding; nativeIdle remains unknown. Do not infer movement or Idle qualification. Root-hook/scratch restoration and bounded pose observation passed; text layout/Idle recognition remains the next gate.

Validation:208 focused recovery tests and2394 total native tests passed; scoped format/analyzers passed. Added negative coverage for inactive/unreadable/wrong desktop, unstable foreground, unknown input and held controls. Background mode is explicit; GUI default unchanged.

Architecture clarification: Maelstrom remains background. A nonactivating target-relative overlay provides notices; foreground belongs to W101 while playing. An interactive panel must pause gameplay before taking focus. An overlay is not a replacement for target identity, input ownership or desktop/session validation.

Installed repair: Setup-20261112-131449-602233, active version5aa3125e1eba44b0802fe9131f0b8f2f. CLI transaction installed:true. Installed recovery follow-up detected desktop activity and stopped before checks (installed-recovery-21.json); no focus/input. Source-built live recovery passed above; installed quiet-path repeat remains pending.
