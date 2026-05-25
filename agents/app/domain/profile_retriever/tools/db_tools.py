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

ACTIVITY_TYPES = {
    "extracurricular",
    "extra curricular",
    "extra-scolaire",
    "extra scolaire",
    "extrascolaire",
    "parascolaire",
    "para scolaire",
}

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


def _norm(value: object) -> str:
    return str(value or "").strip().lower()


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
                    SELECT nom, prenom, email,
                           id_utilisateur AS profil_id,
                           titre_poste    AS titre,
                           resume_professionnel AS resume,
                           telephone, ville,
                           photo_url,
                           lien_linkedin AS linkedin,
                           lien_github AS github,
                           lien_portfolio AS portfolio
                    FROM utilisateur
                    WHERE keycloak_id = :uid OR id_utilisateur::text = :uid
                    LIMIT 1
                """),
                {"uid": user_id},
            )).mappings().first()

            if not row:
                logger.warning("[Tool:db] Profil introuvable pour user_id=%s", user_id)
                return {"user_id": user_id, "competences": [], "experiences": [], "activities": []}



            pid = row["profil_id"]

            # ── Compétences ─────────────────────────────────
            try:
                comps = (await db.execute(
                    text("SELECT nom, type_competence, niveau FROM competence WHERE id_utilisateur = :pid"),
                    {"pid": pid},
                )).mappings().all()
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
                               type_contrat AS type,
                               taches
                        FROM experience WHERE id_utilisateur = :pid
                        ORDER BY date_debut DESC
                    """),
                    {"pid": pid},
                )).mappings().all()
                exps = [
                    {
                        **dict(exp),
                        "taches": (
                            [str(t).strip() for t in exp.get("taches", []) if str(t).strip()]
                            if isinstance(exp.get("taches"), list)
                            else [t.strip() for t in str(exp.get("taches") or "").split("\n") if t.strip()]
                        ),
                    }
                    for exp in exps
                ]
            except Exception as e:
                logger.error("[Tool:db] Erreur expériences : %s", str(e))
                exps = []

            activities = [
                {
                    "title": str(exp.get("titre") or exp.get("entreprise") or "").strip(),
                    "role": str(exp.get("entreprise") or "").strip() or None,
                    "description": str(exp.get("description") or "").strip() or None,
                    "date_debut": exp.get("date_debut"),
                    "date_fin": exp.get("date_fin"),
                }
                for exp in exps
                if _norm(exp.get("type")) in ACTIVITY_TYPES
            ]

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
                projs_raw = (await db.execute(
                    text("""
                        SELECT titre_projet AS titre,
                                description,
                                technologies_utilisees AS technologies,
                                taches
                        FROM projet WHERE id_utilisateur = :pid
                    """),
                    {"pid": pid},
                )).mappings().all()
                
                projs = []
                for p in projs_raw:
                    p_dict = dict(p)
                    # Parsing des technologies (string -> list) si nécessaire
                    techs = p_dict.get("technologies")
                    if isinstance(techs, str):
                        p_dict["technologies"] = [t.strip() for t in techs.split(",") if t.strip()]
                    elif techs is None:
                        p_dict["technologies"] = []
                    tasks = p_dict.get("taches")
                    if isinstance(tasks, list):
                        p_dict["taches"] = [str(t).strip() for t in tasks if str(t).strip()]
                    elif isinstance(tasks, str):
                        p_dict["taches"] = [t.strip() for t in tasks.split("\n") if t.strip()]
                    else:
                        p_dict["taches"] = []
                    projs.append(p_dict)
            except Exception as e:
                logger.error("[Tool:db] Erreur projets : %s", str(e))
                raise  # On laisse remonter pour déclencher le fallback

        return {
            "user_id": user_id,
            "nom":       row["nom"],
            "prenom":    row["prenom"],
            "email":     row["email"],
            "titre":     row["titre"],
            "resume":    row["resume"],
            "telephone": row["telephone"],
            "ville":     row["ville"],
            "photo_url": row["photo_url"],
            "linkedin":  row["linkedin"],
            "github":    row["github"],
            "portfolio": row["portfolio"],
            "competences":    [dict(c) for c in comps],
            "experiences":    [dict(e) for e in exps],
            "activities":     activities,
            "formations":     [dict(f) for f in forms],
            "certifications": [dict(c) for c in certs],
            "projets":        projs,
        }

    except Exception as e:
        logger.error("[Tool:db] get_user_profile_from_db — Erreur : %s", str(e))
        return {"user_id": user_id, "error": str(e), "competences": [], "experiences": [], "activities": []}


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
