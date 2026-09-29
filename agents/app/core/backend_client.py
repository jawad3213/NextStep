# ============================================================
# app/core/backend_client.py
# Calls to the backend's internal endpoints (/internal/agents/...).
#
# The agents never read or write the backend modules' tables (profile, applications):
# they go through these endpoints, so the backend's rules, status history and module
# boundaries apply. Authentication: the shared secret AGENTS_API_KEY.
# ============================================================
import asyncio
import logging
from typing import Any, Optional

import httpx

from app.core.config import settings

logger = logging.getLogger(__name__)

API_KEY_HEADER = "X-Internal-Api-Key"
_TIMEOUT = 20.0

_client: httpx.AsyncClient | None = None
_client_loop: asyncio.AbstractEventLoop | None = None


class BackendError(RuntimeError):
    """The backend refused the call or could not be reached. `message` is safe to show."""

    def __init__(self, message: str, status_code: int | None = None):
        super().__init__(message)
        self.message = message
        self.status_code = status_code


def _http() -> httpx.AsyncClient:
    """One client per event loop (a client cannot be used from another loop)."""
    global _client, _client_loop
    loop = asyncio.get_running_loop()
    if _client is None or _client.is_closed or _client_loop is not loop:
        _client = httpx.AsyncClient(base_url=settings.DOTNET_BACKEND_URL, timeout=_TIMEOUT)
        _client_loop = loop
    return _client


async def _request(method: str, path: str, **kwargs) -> Optional[Any]:
    """JSON body of the answer; None on 404 or 204. Raises BackendError otherwise."""
    headers = {API_KEY_HEADER: settings.AGENTS_API_KEY}
    try:
        response = await _http().request(method, path, headers=headers, **kwargs)
    except httpx.HTTPError as e:
        logger.error("[Backend] %s %s unreachable: %s", method, path, e)
        raise BackendError("Le backend est injoignable.") from e

    if response.status_code in (204, 404):
        return None
    if response.status_code >= 400:
        try:
            message = response.json().get("error") or response.text
        except ValueError:
            message = response.text
        logger.warning("[Backend] %s %s -> %s: %s", method, path, response.status_code, message)
        raise BackendError(str(message)[:300] or "Erreur du backend.", response.status_code)
    return response.json() if response.content else None


# ── Profile module ──────────────────────────────────────────────────────────

async def get_user(user_ref: str) -> Optional[dict]:
    """{userId, firstName, lastName, email} for a local id or Keycloak subject, or None."""
    return await _request("GET", f"/internal/agents/users/{user_ref}") if user_ref else None


async def get_full_profile(user_ref: str) -> Optional[dict]:
    """{userId, profile: FullProfileDto} (camelCase, as GET /api/profile), or None."""
    return await _request("GET", f"/internal/agents/profiles/{user_ref}") if user_ref else None


# ── Applications module ─────────────────────────────────────────────────────

async def list_candidatures(user_ref: str, limit: int = 50) -> list[dict]:
    return await _request("GET", f"/internal/agents/users/{user_ref}/candidatures", params={"limit": limit}) or []


async def create_candidature(user_ref: str, entreprise: str, poste: str, channel: str, notes: str = "") -> dict:
    body = {"entreprise": entreprise, "poste": poste, "channel": channel, "notes": notes}
    return await _request("POST", f"/internal/agents/users/{user_ref}/candidatures", json=body)


async def update_candidature_status(user_ref: str, candidature_id: str, status: str, details: str | None = None) -> Optional[dict]:
    body = {"nouveauStatut": status, "details": details}
    return await _request("POST", f"/internal/agents/users/{user_ref}/candidatures/{candidature_id}/status", json=body)


async def add_candidature_note(user_ref: str, candidature_id: str, content: str) -> None:
    await _request("POST", f"/internal/agents/users/{user_ref}/candidatures/{candidature_id}/notes", json={"contenu": content})


async def get_offer_analysis(user_ref: str, offer_id: str) -> Optional[dict]:
    """The analysis the backend stored for the user's offer, or None."""
    return await _request("GET", f"/internal/agents/users/{user_ref}/offers/{offer_id}/analysis")
