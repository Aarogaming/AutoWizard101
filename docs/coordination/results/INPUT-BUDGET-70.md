# Bounded offline input comparison logs

Status: source-only; no installation.

Input scoring previously read entire input and prediction files into memory with no budget. Both now use one shared bounded binary read (32 MiB plus one sentinel byte), checked before JSON parsing. This also bounds reads if a file grows during access. BOM support is preserved and non-object JSON roots produce an explicit validation error rather than an attribute failure.

Four comparison tests passed, including boundary-sized objects, oversized logs with a small mocked limit, BOM and non-object roots. Black one-worker formatting/check and compileall passed. No large fixture, game, capture, model, host or installation activity.

Oversized logs require segmentation before scoring; no silent truncation or partial accuracy is reported. This bounds file ingestion, not all downstream computation. Next priority: avoid quadratic prediction-to-injected-event searches for long recordings, preserving focus-interval matching.
