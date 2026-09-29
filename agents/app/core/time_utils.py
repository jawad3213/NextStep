from datetime import datetime, timezone


def utc_now() -> datetime:
    """Current UTC time without tzinfo: the agents' columns are TIMESTAMP WITHOUT TIME ZONE.
    (Replaces datetime.utcnow(), deprecated since Python 3.12.)"""
    return datetime.now(timezone.utc).replace(tzinfo=None)
