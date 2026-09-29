# ============================================================
# app/domain/profile_retriever/tools/profile_source.py
#
# Loads the candidate profile from the backend's Profile module
# (GET /internal/agents/profiles/{user}) and converts it to the agents' profile format.
# ============================================================
import logging
from typing import Any

from app.core import backend_client

logger = logging.getLogger(__name__)

ACTIVITY_TYPES = {
    "extracurricular",
    "extra curricular",
    "extra-scolaire",
    "extra scolaire",
    "extrascolaire",
    "parascolaire",
    "para scolaire",
}


def _norm(value: object) -> str:
    return str(value or "").strip().lower()


def _date(value: Any) -> str | None:
    """"2023-01-01T00:00:00" → "2023-01-01"."""
    text = str(value or "").strip()
    return text[:10] or None


def _lines(value: Any) -> list[str]:
    """Tasks: a list, or text with one task per line."""
    if isinstance(value, list):
        return [str(t).strip() for t in value if str(t).strip()]
    return [t.strip() for t in str(value or "").split("\n") if t.strip()]


def to_agent_profile(user_id: str, dto: dict) -> dict:
    """Backend FullProfileDto (camelCase) → the agents' profile dict (French snake_case keys)."""
    info = dto.get("personalInfo") or {}

    experiences = sorted(
        (
            {
                "titre": e.get("poste") or "",
                "entreprise": e.get("entreprise") or "",
                "date_debut": _date(e.get("dateDebut")),
                "date_fin": _date(e.get("dateFin")),
                "description": e.get("missions"),
                "ville": e.get("ville"),
                "type": e.get("type"),
                "taches": _lines(e.get("taches")),
            }
            for e in dto.get("experiences") or []
        ),
        key=lambda e: e["date_debut"] or "",
        reverse=True,
    )

    return {
        "user_id": user_id,
        "preferred_language": dto.get("preferredLanguage") or "en",
        "nom": info.get("nom"),
        "prenom": info.get("prenom"),
        "email": info.get("email"),
        "titre": info.get("titrePoste"),
        "resume": info.get("resumeProfessionnel"),
        "telephone": info.get("telephone"),
        "ville": info.get("ville"),
        "photo_url": info.get("photoUrl"),
        "linkedin": info.get("lienLinkedin"),
        "github": info.get("lienGithub"),
        "portfolio": info.get("lienPortfolio"),
        "competences": [
            {"nom": c.get("nom") or "", "type_competence": c.get("typeCompetence"), "niveau": c.get("niveau")}
            for c in dto.get("competences") or []
            if c.get("nom")
        ],
        "experiences": experiences,
        "activities": [
            {
                "title": (e["titre"] or e["entreprise"]).strip(),
                "role": e["entreprise"].strip() or None,
                "description": (e["description"] or "").strip() or None,
                "date_debut": e["date_debut"],
                "date_fin": e["date_fin"],
            }
            for e in experiences
            if _norm(e["type"]) in ACTIVITY_TYPES
        ],
        "formations": [
            {
                "diplome": f.get("diplome") or "",
                "etablissement": f.get("etablissement") or "",
                "annee": f.get("annee"),
                "annee_fin": f.get("anneeFin"),
                "ville": f.get("ville"),
            }
            for f in dto.get("formations") or []
        ],
        "certifications": [
            {"nom": c.get("titre") or "", "organisme": c.get("organisation") or "", "date_obtention": _date(c.get("dateObtention"))}
            for c in dto.get("certifications") or []
        ],
        "projets": [
            {
                "titre": p.get("titreProjet") or "",
                "description": p.get("description"),
                "technologies": [t.strip() for t in str(p.get("technologiesUtilisees") or "").split(",") if t.strip()],
                "taches": _lines(p.get("taches")),
            }
            for p in dto.get("projets") or []
        ],
    }


async def get_user_profile(user_ref: str) -> dict:
    """
    The candidate's full profile (`user_ref` = local user id or Keycloak id).
    Returns {"user_id", "error"} when it cannot be loaded.
    """
    logger.info("[Profile] get_user_profile — user=%s", user_ref)
    try:
        result = await backend_client.get_full_profile(user_ref)
    except backend_client.BackendError as e:
        return {"user_id": user_ref, "error": e.message}
    if not result:
        return {"user_id": user_ref, "error": "Profil introuvable"}
    return to_agent_profile(str(result.get("userId") or user_ref), result.get("profile") or {})
