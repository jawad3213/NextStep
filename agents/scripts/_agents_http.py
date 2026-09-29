"""Shared by the manual test scripts: the agents only accept requests that carry the
shared secret AGENTS_API_KEY in the X-Internal-Api-Key header (like the backend's calls)."""
import os
from pathlib import Path

API_KEY_HEADER = "X-Internal-Api-Key"


def _key_from_env_file() -> str | None:
    """AGENTS_API_KEY from the first .env found in agents/ or the project root."""
    here = Path(__file__).resolve().parent
    for env_file in (here.parent / ".env", here.parent.parent / ".env"):
        if not env_file.is_file():
            continue
        for line in env_file.read_text(encoding="utf-8").splitlines():
            name, sep, value = line.strip().partition("=")
            if sep and name.strip() == "AGENTS_API_KEY":
                return value.strip().strip('"').strip("'") or None
    return None


def auth_headers(api_key: str | None = None) -> dict:
    """Headers for a call to the agents. The key comes from --api-key, the AGENTS_API_KEY
    environment variable, or the .env file (in that order). It is never printed."""
    key = api_key or os.getenv("AGENTS_API_KEY") or _key_from_env_file()
    if not key:
        print("[WARN] No AGENTS_API_KEY found (--api-key, environment or .env): the agents will answer 401.")
        return {}
    return {API_KEY_HEADER: key}
