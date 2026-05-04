# ============================================================
# app/domain/offer/tools/db_tools.py
#
# Outils LangChain (@tool) du domaine OFFER :
#   - Accès direct à PostgreSQL pour récupérer le profil candidat
#   - Sauvegarde de l'analyse d'offre
#   - Vérification santé backend .NET
# ============================================================
import logging
import httpx
from langchain_core.tools import tool
from sqlalchemy import text
from app.core.config import settings
from app.core.database import AsyncSessionFactory

logger = logging.getLogger(__name__)

# ─── Client HTTP singleton vers le backend .NET ────────────────
_http_client: httpx.AsyncClient | None = None


def _get_http_client() -> httpx.AsyncClient:
    """Retourne ou crée le client HTTP partagé."""
    global _http_client
    if _http_client is None or _http_client.is_closed:
        _http_client = httpx.AsyncClient(
            base_url=settings.DOTNET_BACKEND_URL,
            timeout=30.0,
            headers={"Content-Type": "application/json"},
        )
    return _http_client


# ─────────────────────────────────────────────────────────────
# TOOL 1 — Chargement profil depuis PostgreSQL (accès direct)
# ─────────────────────────────────────────────────────────────

@tool
async def get_user_profile_from_db(user_id: str) -> dict:
    """
    Récupère le profil complet d'un candidat directement depuis PostgreSQL.

    Utilisé par Agent 2 (Profile Retriever) pour éviter un aller-retour HTTP
    vers le backend .NET et accéder aux données les plus fraîches.

    Requêtes SQL exécutées :
        1. utilisateurs + profils (JOIN sur keycloak_id)
        2. competences
        3. experiences (ORDER BY date_debut DESC)
        4. formations
        5. certifications
        6. projets

    Args:
        user_id: Identifiant Keycloak (uuid) de l'utilisateur.

    Returns:
        Dict profil complet ou dict vide avec les clés minimales si non trouvé.
    """
    logger.info("[Tool:db] get_user_profile_from_db — user_id=%s", user_id)
    try:
        async with AsyncSessionFactory() as db:
            # ── Profil de base ──────────────────────────────
            row = (await db.execute(
                text("""
                    SELECT nom, prenom,
                           id_utilisateur AS profil_id,
                           titre_poste    AS titre, 
                           resume_professionnel AS resume, 
                           telephone, ville
                    FROM utilisateur
                    WHERE keycloak_id = :uid
                    LIMIT 1
                """),
                {"uid": user_id},
            )).mappings().first()

            if not row:
                logger.warning("[Tool:db] Profil introuvable pour user_id=%s", user_id)
                return {"user_id": user_id, "competences": [], "experiences": []}

            pid = row["profil_id"]

            # ── Compétences ─────────────────────────────────
            try:
                comps = (await db.execute(
                    text("SELECT nom, type_competence FROM competence WHERE id_utilisateur = :pid"),
                    {"pid": pid},
                )).mappings().all()
                
                # LOG DE DEBUG pour voir la structure réelle
                if comps:
                    print(f"DEBUG DB_TOOLS - Première compétence: {comps[0]}")
                    print(f"DEBUG DB_TOOLS - Clés disponibles: {list(comps[0].keys())}")
            except Exception as e:
                logger.error("[Tool:db] Erreur compétences : %s", str(e))
                comps = []

            # ── Expériences ─────────────────────────────────
            try:
                exps = (await db.execute(
                    text("""
                        SELECT poste AS titre, entreprise,
                               date_debut::text, date_fin::text,
                               missions AS description,
                               type_contrat AS type
                        FROM experience WHERE id_utilisateur = :pid
                        ORDER BY date_debut DESC
                    """),
                    {"pid": pid},
                )).mappings().all()
            except Exception as e:
                logger.error("[Tool:db] Erreur expériences : %s", str(e))
                exps = []

            # ── Formations ──────────────────────────────────
            try:
                forms = (await db.execute(
                    text("SELECT diplome, etablissement, annee FROM formation WHERE id_utilisateur = :pid"),
                    {"pid": pid},
                )).mappings().all()
            except Exception as e:
                logger.error("[Tool:db] Erreur formations : %s", str(e))
                forms = []

            # ── Certifications ──────────────────────────────
            try:
                certs = (await db.execute(
                    text("SELECT titre AS nom, organisation AS organisme FROM certification WHERE id_utilisateur = :pid"),
                    {"pid": pid},
                )).mappings().all()
            except Exception as e:
                logger.error("[Tool:db] Erreur certifications : %s", str(e))
                certs = []

            # ── Projets ─────────────────────────────────────
            try:
                projs = (await db.execute(
                    text("""
                        SELECT titre_projet AS titre,
                               description,
                               technologies_utilisees AS technologies
                        FROM projet WHERE id_utilisateur = :pid
                    """),
                    {"pid": pid},
                )).mappings().all()
            except Exception as e:
                logger.error("[Tool:db] Erreur projets : %s", str(e))
                projs = []

        return {
            "user_id": user_id,
            "nom":       row["nom"],
            "prenom":    row["prenom"],
            "titre":     row["titre"],
            "resume":    row["resume"],
            "telephone": row["telephone"],
            "ville":     row["ville"],
            "competences":    [dict(c) for c in comps],
            "experiences":    [dict(e) for e in exps],
            "formations":     [dict(f) for f in forms],
            "certifications": [dict(c) for c in certs],
            "projets":        [dict(p) for p in projs],
        }

    except Exception as e:
        logger.error("[Tool:db] get_user_profile_from_db — Erreur : %s", str(e))
        return {"user_id": user_id, "error": str(e), "competences": [], "experiences": []}


# ─────────────────────────────────────────────────────────────
# TOOL 2 — Récupération profil via API .NET (fallback HTTP)
# ─────────────────────────────────────────────────────────────

@tool
async def get_user_profile_from_api(user_id: str) -> dict:
    """
    Récupère le profil d'un candidat via l'API REST du backend .NET.
    Fallback si l'accès direct à la DB échoue.

    Args:
        user_id: Identifiant Keycloak de l'utilisateur.

    Returns:
        JSON profil retourné par GET /api/profile.
    """
    logger.info("[Tool:api] get_user_profile_from_api — user_id=%s", user_id)
    try:
        client = _get_http_client()
        response = await client.get(
            "/api/profile",
            headers={"X-User-Id": user_id},
        )
        if response.status_code == 404:
            return {"user_id": user_id, "competences": [], "experiences": []}
        response.raise_for_status()
        return response.json()
    except httpx.HTTPError as e:
        logger.error("[Tool:api] get_user_profile_from_api — Erreur : %s", str(e))
        return {"user_id": user_id, "error": str(e), "competences": [], "experiences": []}


# ─────────────────────────────────────────────────────────────
# TOOL 3 — Sauvegarde résultat analyse offre
# ─────────────────────────────────────────────────────────────

@tool
async def save_offer_analysis(offer_id: str, analysis_json: str) -> bool:
    """
    Sauvegarde le résultat de l'analyse LLM (Agent 1) dans le backend .NET.

    Args:
        offer_id:      UUID de l'offre à mettre à jour (PATCH /api/offers/{id}/analysis).
        analysis_json: JSON sérialisé du résultat Agent 1.

    Returns:
        True si la sauvegarde a réussi, False sinon.
    """
    logger.info("[Tool:api] save_offer_analysis — offer_id=%s", offer_id)
    try:
        client = _get_http_client()
        response = await client.patch(
            f"/api/offers/{offer_id}/analysis",
            content=analysis_json,
        )
        return response.is_success
    except httpx.HTTPError as e:
        logger.error("[Tool:api] save_offer_analysis — Erreur : %s", str(e))
        return False


# ─────────────────────────────────────────────────────────────
# TOOL 4 — Health check backend .NET
# ─────────────────────────────────────────────────────────────

@tool
async def check_backend_health() -> dict:
    """
    Vérifie que le backend .NET est disponible et répond.

    Returns:
        {"healthy": bool, "status": int} ou {"healthy": False, "error": str}
    """
    try:
        client = _get_http_client()
        response = await client.get("/health", timeout=5.0)
        return {"healthy": response.is_success, "status": response.status_code}
    except Exception as e:
        return {"healthy": False, "error": str(e)}


# ── Registre des outils disponibles pour ce domaine ──────────
OFFER_TOOLS = [
    get_user_profile_from_db,
    get_user_profile_from_api,
    save_offer_analysis,
    check_backend_health,
]
