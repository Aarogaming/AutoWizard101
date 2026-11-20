"""Check private-game audit bytes against a capture-time digest when available."""

import hashlib


def verify_audit_digest(report, audit_bytes):
    """Return the digest and sealing status; older reports may be unsealed."""
    if not isinstance(report, dict) or not isinstance(audit_bytes, bytes):
        raise ValueError("Invalid episode audit input")
    actual = hashlib.sha256(audit_bytes).hexdigest()
    if "evaluation_audit_sha256" not in report:
        return actual, False
    expected = report["evaluation_audit_sha256"]
    if (
        not isinstance(expected, str)
        or len(expected) != 64
        or any(c not in "0123456789abcdef" for c in expected)
        or expected != actual
    ):
        raise ValueError("Episode audit does not match its capture-time digest")
    return actual, True
