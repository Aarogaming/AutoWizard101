"""Publish a validated offline report without exposing partial JSON to readers."""

import json
import os
from pathlib import Path
import tempfile

from check_diego_report import MAX_BYTES, load_report


def save_report(path, data):
    """Replace only after validation; an ordinary failure preserves the old report.

    A sibling temporary file keeps replacement on one filesystem. There is no
    automatic retry: a locked destination is an error the user can resolve.
    This is atomic publication, not a power-loss durability guarantee.
    """
    raw = (json.dumps(data, indent=2, allow_nan=False) + "\n").encode("utf-8")
    if len(raw) > MAX_BYTES:
        raise ValueError("Report exceeds the local 256 KiB limit.")
    destination = Path(path)
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(
            dir=destination.parent, prefix=".diego-report-", suffix=".tmp", delete=False
        ) as stream:
            temporary = Path(stream.name)
            stream.write(raw)
            stream.flush()
            os.fsync(stream.fileno())
        # Validate the serialized bytes, including extensions, before publication.
        load_report(temporary)
        temporary.replace(destination)
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)
