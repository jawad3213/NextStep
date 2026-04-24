# ============================================================
# app/tools/backend_api.py — Outils LangChain (@tool)
#
# Ces fonctions permettent aux agents d'interagir avec le backend
# .NET et la base de données. Elles sont décorées avec @tool pour
# être utilisables par les agents LangChain.
# ============================================================
import logging
import httpx
from langchain_core.tools import tool
from sqlalchemy import text
from app.core.config import settings
from app.core.database import AsyncSessionFactory

logger = logging.getLogger(__name__)


# ─────────────────────────────────────────────────────
# Client HTTP partagé (singleton)
# ─────────────────────────────────────────────────────
_http_client: httpx.AsyncClient | None = None


def get_http_client() -> httpx.AsyncClient:
    """Retourne ou crée le client HTTP vers le backend .NET."""
    global _http_client
    if _http_client is None or _http_client.is_closed:
        _http_client = httpx.AsyncClient(
            base_url=settings.DOTNET_BACKEND_URL,
            timeout=30.0,
            headers={"Content-Type": "application/json"},
        )
    return _http_client


# ─────────────────────────────────────────────────────
# Outils LangChain — Appels au backend .NET
# ─────────────────────────────────────────────────────

@tool
async def get_user_profile(user_id: str) -> dict:
    """
    Récupère le profil complet d'un utilisateur depuis le backend .NET.

    Args:
        user_id: L'identifiant Keycloak de l'utilisateur.

    Returns:
        Dictionnaire contenant le profil complet (compétences, expériences, etc.)
        ou un dict vide si le profil n'est pas trouvé.
    """
    logger.info("[Tool] get_user_profile — user_id=%s", user_id)
    try:
        client = get_http_client()
        response = await client.get(
            "/api/profile",
            headers={"X-User-Id": user_id},
        )
        if response.status_code == 404:
            logger.warning("[Tool] get_user_profile — Profil introuvable pour %s", user_id)
            return {"user_id": user_id, "competences": [], "experiences": [], "formations": []}
        response.raise_for_status()
        return response.json()
    except httpx.HTTPError as e:
        logger.error("[Tool] get_user_profile — Erreur HTTP : %s", str(e))
        return {"user_id": user_id, "error": str(e)}


@tool
async def get_user_profile_from_db(user_id: str) -> dict:
    """
    Récupère directement le profil d'un utilisateur depuis PostgreSQL.
    Utilisé par Agent 2 (Profile Retriever) comme alternative directe à la DB.

    Args:
        user_id: L'identifiant Keycloak de l'utilisateur.

    Returns:
        Dictionnaire profil complet ou vide si non trouvé.
    """
    logger.info("[Tool] get_user_profile_from_db — user_id=%s", user_id)
    try:
        async with AsyncSessionFactory() as db:
            # Profil de base
            row = (await db.execute(
                text("""
                    SELECT u.nom, u.prenom, p.id AS profil_id,
                           p.titre, p.resume, p.telephone, p.ville
                    FROM utilisateurs u
                    JOIN profils p ON p.utilisateur_id = u.id
                    WHERE u.keycloak_id = :uid LIMIT 1
                """),
                {"uid": user_id},
            )).mappings().first()

            if not row:
                return {"user_id": user_id, "competences": [], "experiences": []}

            pid = row["profil_id"]

            # Compétences
            comps = (await db.execute(
                text("SELECT nom, niveau FROM competences WHERE profil_id = :pid"),
                {"pid": pid},
            )).mappings().all()

            # Expériences
            exps = (await db.execute(
                text("""
                    SELECT titre, entreprise, date_debut::text, date_fin::text, description
                    FROM experiences WHERE profil_id = :pid ORDER BY date_debut DESC
                """),
                {"pid": pid},
            )).mappings().all()

            # Formations
            forms = (await db.execute(
                text("SELECT diplome, etablissement, annee FROM formations WHERE profil_id = :pid"),
                {"pid": pid},
            )).mappings().all()

            # Certifications
            certs = (await db.execute(
                text("SELECT nom, organisme FROM certifications WHERE profil_id = :pid"),
                {"pid": pid},
            )).mappings().all()

            # Projets
            projs = (await db.execute(
                text("SELECT titre, description, technologies FROM projets WHERE profil_id = :pid"),
                {"pid": pid},
            )).mappings().all()

        return {
            "user_id": user_id,
            "nom": row["nom"],
            "prenom": row["prenom"],
            "titre": row["titre"],
            "resume": row["resume"],
            "telephone": row["telephone"],
            "ville": row["ville"],
            "competences": [dict(c) for c in comps],
            "experiences": [dict(e) for e in exps],
            "formations": [dict(f) for f in forms],
            "certifications": [dict(c) for c in certs],
            "projets": [dict(p) for p in projs],
        }
    except Exception as e:
        logger.error("[Tool] get_user_profile_from_db — Erreur : %s", str(e))
        return {"user_id": user_id, "error": str(e), "competences": [], "experiences": []}


@tool
async def save_offer_analysis(offer_id: str, analysis_json: str) -> bool:
    """
    Sauvegarde le résultat d'analyse d'une offre dans le backend .NET.

    Args:
        offer_id: UUID de l'offre à mettre à jour.
        analysis_json: JSON sérialisé de l'analyse.

    Returns:
        True si la sauvegarde a réussi, False sinon.
    """
    logger.info("[Tool] save_offer_analysis — offer_id=%s", offer_id)
    try:
        client = get_http_client()
        response = await client.patch(
            f"/api/offers/{offer_id}/analysis",
            content=analysis_json,
        )
        return response.is_success
    except httpx.HTTPError as e:
        logger.error("[Tool] save_offer_analysis — Erreur : %s", str(e))
        return False


@tool
async def check_backend_health() -> dict:
    """
    Vérifie que le backend .NET est disponible.

    Returns:
        Dictionnaire avec le statut du backend.
    """
    try:
        client = get_http_client()
        response = await client.get("/health", timeout=5.0)
        return {"healthy": response.is_success, "status": response.status_code}
    except Exception as e:
        return {"healthy": False, "error": str(e)}


# ─── Liste de tous les outils disponibles ───
ALL_TOOLS = [
    get_user_profile,
    get_user_profile_from_db,
    save_offer_analysis,
    check_backend_health,
]
