# ============================================================
# app/domain/cv_engine/tools/db_tools.py
# Outils DB du domaine CV_ENGINE
#
# Réutilise le pattern d'accès direct PostgreSQL du domaine OFFER.
# Charge le profil complet incluant email, liens, et pays.
# ============================================================
import logging
from langchain_core.tools import tool
from sqlalchemy import text
from app.core.database import AsyncSessionFactory

logger = logging.getLogger(__name__)


@tool
async def load_full_profile(user_id: str) -> dict:
    """
    Charge le profil complet d'un candidat depuis PostgreSQL.

    Inclut tous les champs nécessaires au CV :
    - Infos personnelles (nom, prénom, email, téléphone, ville, pays, liens)
    - Compétences (avec niveau et type)
    - Expériences professionnelles
    - Formations
    - Certifications
    - Projets

    Args:
        user_id: Identifiant Keycloak (UUID) de l'utilisateur.

    Returns:
        Dict profil complet ou dict minimal si non trouvé.
    """
    logger.info("[CvEngine:db] load_full_profile — user_id=%s", user_id)
    try:
        async with AsyncSessionFactory() as db:
            # ── Profil de base ──────────────────────────────
            row = (await db.execute(
                text("""
                    SELECT nom, prenom,
                           id_utilisateur AS profil_id,
                           email,
                           titre_poste AS titre,
                           resume_professionnel AS resume,
                           telephone, ville, pays,
                           lien_linkedin, lien_github, lien_portfolio
                    FROM utilisateur
                    WHERE keycloak_id = :uid
                    LIMIT 1
                """),
                {"uid": user_id},
            )).mappings().first()

            if not row:
                logger.warning("[CvEngine:db] Profil introuvable pour user_id=%s", user_id)
                return {
                    "user_id": user_id,
                    "competences": [], "experiences": [],
                    "formations": [], "certifications": [], "projets": [],
                }

            pid = row["profil_id"]

            # ── Compétences ─────────────────────────────────
            try:
                comps = (await db.execute(
                    text("""
                        SELECT nom, niveau, type_competence
                        FROM competence WHERE id_utilisateur = :pid
                    """),
                    {"pid": pid},
                )).mappings().all()
            except Exception as e:
                logger.error("[CvEngine:db] Erreur compétences : %s", str(e))
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
                logger.error("[CvEngine:db] Erreur expériences : %s", str(e))
                exps = []

            # ── Formations ──────────────────────────────────
            try:
                forms = (await db.execute(
                    text("""
                        SELECT diplome, etablissement, annee, annee_fin
                        FROM formation WHERE id_utilisateur = :pid
                    """),
                    {"pid": pid},
                )).mappings().all()
            except Exception as e:
                logger.error("[CvEngine:db] Erreur formations : %s", str(e))
                forms = []

            # ── Certifications ──────────────────────────────
            try:
                certs = (await db.execute(
                    text("""
                        SELECT titre AS nom, organisation AS organisme
                        FROM certification WHERE id_utilisateur = :pid
                    """),
                    {"pid": pid},
                )).mappings().all()
            except Exception as e:
                logger.error("[CvEngine:db] Erreur certifications : %s", str(e))
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
                logger.error("[CvEngine:db] Erreur projets : %s", str(e))
                projs = []

        return {
            "user_id":        user_id,
            "nom":            row["nom"] or "",
            "prenom":         row["prenom"] or "",
            "titre":          row["titre"] or "",
            "email":          row["email"] or "",
            "resume":         row["resume"] or "",
            "telephone":      row["telephone"] or "",
            "ville":          row["ville"] or "",
            "pays":           row["pays"] or "",
            "lien_linkedin":  row["lien_linkedin"] or "",
            "lien_github":    row["lien_github"] or "",
            "lien_portfolio": row["lien_portfolio"] or "",
            "competences":    [dict(c) for c in comps],
            "experiences":    [dict(e) for e in exps],
            "formations":     [dict(f) for f in forms],
            "certifications": [dict(c) for c in certs],
            "projets":        [dict(p) for p in projs],
        }

    except Exception as e:
        logger.error("[CvEngine:db] load_full_profile — Erreur : %s", str(e))
        return {
            "user_id": user_id, "error": str(e),
            "competences": [], "experiences": [],
            "formations": [], "certifications": [], "projets": [],
        }


# ── Registre des outils disponibles ──────────────────────────
CV_ENGINE_TOOLS = [load_full_profile]
