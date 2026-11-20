# Owned incremental trace transport — 133

2026-11-15. Offline only; no input, focus, client observation or installation.

Added W101TraceTail and an optional trace callback inside WizwalkerWorker's existing process/control lifetime. Each poll reads at most 64 KiB, total consumption is capped at 8 MiB, and an individual pending record at 256 KiB. Only newline-complete, strict UTF-8 records are delivered. Truncation, malformed encoding, callback errors and budget overruns terminate the tail. It is disposed with the worker. Cancellation or control loss prevents further delivery. No extra process or polling task is started.

Tests cover partial records, no duplicate delivery, truncation, malformed encoding and oversized unterminated records. The transport deliberately does not drain historical remaining records after worker exit as live evidence.

## Still required

The production UI runtime has not yet supplied the trace callback or established a trusted stream-start handshake. Thus the checked pose consumer is NOT yet connected to production telemetry. Establish owner-pinned run/stream/target and independent wizard/zone context before exposing observations to a follower. Candidate zone labels and run IDs alone do not establish wizard identity or detect missed same-zone loading epochs. No Olde Town transition qualification is claimed.

Gemini remains report-only under GEM-CONNECTOR-125; LIA-0082 requests review of the transport-to-consumer handshake and loading invalidation gaps.
