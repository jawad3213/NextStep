# ============================================================
# agents/app/domain/sn_copilot/profile_tools.py
#
# Profile & CV Intelligence Tools for SN Executive AI Copilot:
#   - get_user_profile: Full user profile from PostgreSQL
#   - parse_cv_text: LLM-based CV text extraction
#   - match_cv_with_candidatures: CV-to-job scoring
# ============================================================
import json
import logging
from typing import Any, Dict, List, Optional

from sqlalchemy import text
from app.core.database import AsyncSessionFactory
from app.core.config import get_llm
from app.domain.sn_copilot.tools import _resolve_user_id, get_recent_candidatures
from langchain_core.messages import SystemMessage, HumanMessage

logger = logging.getLogger(__name__)

# In-memory session cache for profile data (keyed by user_id)
_profile_cache: Dict[str, Dict[str, Any]] = {}


async def get_user_profile(user_id: str) -> Dict[str, Any]:
    """
    Retrieve the complete user profile from PostgreSQL in a single call.
    Returns personal info, competences, formations, experiences, projects, certifications.
    Results are cached per session to avoid redundant DB calls.
    """
    # Check cache first
    if user_id in _profile_cache:
        logger.info("[Tool:SN] get_user_profile — cache hit for user_id=%s", user_id)
        return _profile_cache[user_id]

    logger.info("[Tool:SN] get_user_profile — fetching full profile for user_id=%s", user_id)
    profile: Dict[str, Any] = {
        "personal_info": {},
        "competences": [],
        "formations": [],
        "experiences": [],
        "projets": [],
        "certifications": [],
    }

    try:
        async with AsyncSessionFactory() as session:
            resolved_uid = await _resolve_user_id(session, user_id)

            # 1. Personal Info from utilisateur table
            user_row = (await session.execute(
                text("""
                    SELECT 
                        nom, prenom, email, telephone, ville, pays,
                        titre_poste, resume_professionnel, objectif,
                        niveau, secteur, lien_linkedin, lien_github,
                        lien_portfolio, profile_score
                    FROM profile.utilisateur 
                    WHERE id_utilisateur::text = :uid
                    LIMIT 1
                """),
                {"uid": str(resolved_uid)}
            )).mappings().first()

            if user_row:
                profile["personal_info"] = {
                    "nom": user_row.get("nom") or "",
                    "prenom": user_row.get("prenom") or "",
                    "email": user_row.get("email") or "",
                    "telephone": user_row.get("telephone") or "",
                    "ville": user_row.get("ville") or "",
                    "pays": user_row.get("pays") or "",
                    "titre_poste": user_row.get("titre_poste") or "",
                    "resume_professionnel": user_row.get("resume_professionnel") or "",
                    "objectif": user_row.get("objectif") or "",
                    "niveau": user_row.get("niveau") or "",
                    "secteur": user_row.get("secteur") or "",
                    "linkedin": user_row.get("lien_linkedin") or "",
                    "github": user_row.get("lien_github") or "",
                    "portfolio": user_row.get("lien_portfolio") or "",
                    "profile_score": user_row.get("profile_score") or 0,
                }

            # 2. Competences
            comp_rows = (await session.execute(
                text("""
                    SELECT nom, niveau, type_competence
                    FROM profile.competence
                    WHERE id_utilisateur::text = :uid
                    ORDER BY niveau DESC
                """),
                {"uid": str(resolved_uid)}
            )).mappings().all()

            profile["competences"] = [
                {
                    "nom": r.get("nom") or "",
                    "niveau": r.get("niveau") or 0,
                    "type": r.get("type_competence") or "technique",
                }
                for r in comp_rows
            ]

            # 3. Formations
            form_rows = (await session.execute(
                text("""
                    SELECT etablissement, diplome, annee, annee_fin, 
                           ville, specialisation, mention
                    FROM profile.formation
                    WHERE id_utilisateur::text = :uid
                    ORDER BY annee DESC
                """),
                {"uid": str(resolved_uid)}
            )).mappings().all()

            profile["formations"] = [
                {
                    "etablissement": r.get("etablissement") or "",
                    "diplome": r.get("diplome") or "",
                    "annee": r.get("annee") or 0,
                    "annee_fin": r.get("annee_fin"),
                    "ville": r.get("ville") or "",
                    "specialisation": r.get("specialisation") or "",
                    "mention": r.get("mention") or "",
                }
                for r in form_rows
            ]

            # 4. Experiences
            exp_rows = (await session.execute(
                text("""
                    SELECT entreprise, poste, date_debut, date_fin,
                           missions, ville, type_contrat, taches
                    FROM profile.experience
                    WHERE id_utilisateur::text = :uid
                    ORDER BY date_debut DESC NULLS LAST
                """),
                {"uid": str(resolved_uid)}
            )).mappings().all()

            profile["experiences"] = [
                {
                    "entreprise": r.get("entreprise") or "",
                    "poste": r.get("poste") or "",
                    "date_debut": str(r.get("date_debut") or "")[:10],
                    "date_fin": str(r.get("date_fin") or "")[:10] if r.get("date_fin") else "En cours",
                    "missions": r.get("missions") or "",
                    "ville": r.get("ville") or "",
                    "type_contrat": r.get("type_contrat") or "",
                    "taches": r.get("taches") or [],
                }
                for r in exp_rows
            ]

            # 5. Projets
            proj_rows = (await session.execute(
                text("""
                    SELECT titre_projet, description, technologies_utilisees,
                           date_realisation, lien_projet, is_university
                    FROM profile.projet
                    WHERE id_utilisateur::text = :uid
                    ORDER BY date_realisation DESC NULLS LAST
                """),
                {"uid": str(resolved_uid)}
            )).mappings().all()

            profile["projets"] = [
                {
                    "titre": r.get("titre_projet") or "",
                    "description": r.get("description") or "",
                    "technologies": r.get("technologies_utilisees") or "",
                    "date": str(r.get("date_realisation") or "")[:10],
                    "lien": r.get("lien_projet") or "",
                    "universitaire": bool(r.get("is_university")),
                }
                for r in proj_rows
            ]

            # 6. Certifications
            cert_rows = (await session.execute(
                text("""
                    SELECT titre, organisation, date_obtention
                    FROM profile.certification
                    WHERE id_utilisateur::text = :uid
                    ORDER BY date_obtention DESC NULLS LAST
                """),
                {"uid": str(resolved_uid)}
            )).mappings().all()

            profile["certifications"] = [
                {
                    "titre": r.get("titre") or "",
                    "organisation": r.get("organisation") or "",
                    "date": str(r.get("date_obtention") or "")[:10],
                }
                for r in cert_rows
            ]

        # Cache the profile
        _profile_cache[user_id] = profile
        logger.info(
            "[Tool:SN] Profile loaded: %d competences, %d formations, %d experiences, %d projets, %d certifications",
            len(profile["competences"]),
            len(profile["formations"]),
            len(profile["experiences"]),
            len(profile["projets"]),
            len(profile["certifications"]),
        )
        return profile

    except Exception as e:
        logger.error("[Tool:SN] get_user_profile error: %s", e)
        return profile


def build_profile_summary(profile: Dict[str, Any]) -> str:
    """
    Build a concise text summary of the user profile for LLM injection.
    Keeps it compact to minimize token usage while maximizing context.
    """
    pi = profile.get("personal_info", {})
    parts = []

    # Identity
    name = f"{pi.get('prenom', '')} {pi.get('nom', '')}".strip()
    if name:
        parts.append(f"Nom : {name}")
    if pi.get("titre_poste"):
        parts.append(f"Titre : {pi['titre_poste']}")
    if pi.get("ville") or pi.get("pays"):
        loc = ", ".join(filter(None, [pi.get("ville"), pi.get("pays")]))
        parts.append(f"Localisation : {loc}")
    if pi.get("objectif"):
        parts.append(f"Objectif : {pi['objectif']}")
    if pi.get("secteur"):
        parts.append(f"Secteur : {pi['secteur']}")
    if pi.get("niveau"):
        parts.append(f"Niveau : {pi['niveau']}")
    if pi.get("resume_professionnel"):
        parts.append(f"Résumé : {pi['resume_professionnel'][:200]}")
    if pi.get("linkedin"):
        parts.append(f"LinkedIn : {pi['linkedin']}")
    if pi.get("github"):
        parts.append(f"GitHub : {pi['github']}")

    # Competences
    comps = profile.get("competences", [])
    if comps:
        tech = [c["nom"] for c in comps if c.get("type") in ("technique", "hard", None, "")]
        soft = [c["nom"] for c in comps if c.get("type") in ("soft", "transversale")]
        if tech:
            parts.append(f"Compétences techniques : {', '.join(tech[:15])}")
        if soft:
            parts.append(f"Compétences transversales : {', '.join(soft[:10])}")

    # Formations
    forms = profile.get("formations", [])
    if forms:
        form_lines = []
        for f in forms[:3]:
            line = f"{f.get('diplome', '')} — {f.get('etablissement', '')}"
            if f.get("specialisation"):
                line += f" ({f['specialisation']})"
            if f.get("annee"):
                line += f" [{f['annee']}]"
            form_lines.append(line)
        parts.append(f"Formations : {' | '.join(form_lines)}")

    # Experiences
    exps = profile.get("experiences", [])
    if exps:
        exp_lines = []
        for e in exps[:4]:
            line = f"{e.get('poste', '')} chez {e.get('entreprise', '')}"
            if e.get("date_debut"):
                line += f" ({e['date_debut']} → {e.get('date_fin', 'En cours')})"
            exp_lines.append(line)
        parts.append(f"Expériences : {' | '.join(exp_lines)}")

    # Projets
    projs = profile.get("projets", [])
    if projs:
        proj_lines = [p.get("titre", "") for p in projs[:5] if p.get("titre")]
        if proj_lines:
            parts.append(f"Projets : {', '.join(proj_lines)}")

    # Certifications
    certs = profile.get("certifications", [])
    if certs:
        cert_lines = [f"{c.get('titre', '')} ({c.get('organisation', '')})" for c in certs[:5]]
        parts.append(f"Certifications : {', '.join(cert_lines)}")

    return "\n".join(parts) if parts else "Profil non renseigné."


async def parse_cv_text(cv_text: str) -> Dict[str, Any]:
    """
    Use LLM to extract structured data from raw CV text pasted/uploaded by the user.
    Returns a structured dict with skills, experiences, education, certifications, summary.
    """
    # ── Handle base64 encoded PDF/Doc upload ──
    if "[cv_upload_base64:" in cv_text.lower():
        try:
            import base64
            import fitz  # PyMuPDF
            b64_str = cv_text.split("]", 1)[1].strip()
            if "base64," in b64_str:
                b64_str = b64_str.split("base64,")[1].strip()
            pdf_bytes = base64.b64decode(b64_str)
            doc = fitz.open(stream=pdf_bytes, filetype="pdf")
            extracted_pages = [page.get_text() for page in doc]
            cv_extracted = "\n".join(extracted_pages).strip()
            if len(cv_extracted) > 30:
                cv_text = cv_extracted
                logger.info("[Tool:SN] PyMuPDF successfully extracted %d chars from PDF CV", len(cv_text))
        except Exception as e:
            logger.error("[Tool:SN] PyMuPDF PDF extraction error: %s", e)

    logger.info("[Tool:SN] parse_cv_text — analyzing %d chars of CV text", len(cv_text))

    if len(cv_text.strip()) < 50:
        return {"error": "Texte trop court pour être un CV valide."}

    extraction_prompt = f"""Analyse ce CV et extrais les informations clés en JSON structuré.

CV :
\"\"\"
{cv_text[:4000]}
\"\"\"

Retourne UNIQUEMENT un objet JSON valide avec cette structure :
{{
  "nom": "Nom complet",
  "titre": "Titre professionnel actuel",
  "resume": "Résumé professionnel en 1-2 phrases",
  "competences": ["skill1", "skill2", ...],
  "experiences": [
    {{"entreprise": "...", "poste": "...", "duree": "..."}}
  ],
  "formations": [
    {{"diplome": "...", "etablissement": "...", "annee": "..."}}
  ],
  "certifications": ["cert1", "cert2"],
  "langues": ["langue1", "langue2"],
  "points_forts": ["point1", "point2", "point3"]
}}
"""

    try:
        llm = get_llm(temperature=0.1, agent_name="default")
        response = await llm.ainvoke([
            SystemMessage(content="Tu es un expert en analyse de CV. Extrais les données structurées. Réponds UNIQUEMENT en JSON valide, sans texte supplémentaire."),
            HumanMessage(content=extraction_prompt),
        ])

        raw = response.content if hasattr(response, "content") else str(response)
        if isinstance(raw, list):
            raw = "".join(item.get("text", "") if isinstance(item, dict) else str(item) for item in raw)

        # Extract JSON from response
        raw = raw.strip()
        if "```json" in raw:
            raw = raw.split("```json")[1].split("```")[0].strip()
        elif "```" in raw:
            raw = raw.split("```")[1].split("```")[0].strip()

        parsed = json.loads(raw)
        logger.info("[Tool:SN] CV parsed successfully: %d skills, %d experiences",
                     len(parsed.get("competences", [])), len(parsed.get("experiences", [])))
        return parsed

    except json.JSONDecodeError as e:
        logger.warning("[Tool:SN] parse_cv_text JSON decode error: %s", e)
        return {"error": "Impossible de parser le CV. Format invalide.", "raw": raw[:500]}
    except Exception as e:
        logger.error("[Tool:SN] parse_cv_text error: %s", e)
        return {"error": str(e)}


async def match_cv_with_candidatures(
    user_id: str,
    cv_data: Optional[Dict[str, Any]] = None,
    profile: Optional[Dict[str, Any]] = None,
) -> Dict[str, Any]:
    """
    Match user CV/profile against active candidatures using LLM scoring.
    Uses either uploaded CV data or the user's stored profile.
    Returns ranked candidatures with match scores and recommendations.
    """
    logger.info("[Tool:SN] match_cv_with_candidatures — user_id=%s", user_id)

    # Get profile if not provided
    if not profile:
        profile = await get_user_profile(user_id)

    # Get candidatures with offer details
    candidatures = await get_recent_candidatures(user_id, limit=20)
    if not candidatures:
        return {"error": "Aucune candidature active trouvée.", "matches": []}

    # Build candidate profile text
    if cv_data and not cv_data.get("error"):
        candidate_text = json.dumps(cv_data, ensure_ascii=False)
        source = "CV uploadé"
    else:
        candidate_text = build_profile_summary(profile)
        source = "profil enregistré"

    # Build candidatures summary for matching
    cand_lines = []
    for i, c in enumerate(candidatures):
        cand_lines.append(
            f"{i+1}. {c['entreprise']} — {c['role']} (statut: {c['statut']}, canal: {c['channel']})"
        )
    cands_text = "\n".join(cand_lines)

    matching_prompt = f"""Analyse la compatibilité entre ce profil candidat et ses candidatures actives.

PROFIL CANDIDAT (source: {source}) :
{candidate_text[:3000]}

CANDIDATURES ACTIVES :
{cands_text}

Pour chaque candidature, évalue la compatibilité sur 100 et donne une recommandation courte.

Retourne UNIQUEMENT un JSON valide :
{{
  "matches": [
    {{
      "rang": 1,
      "entreprise": "...",
      "role": "...",
      "score": 85,
      "points_forts": ["compétence1 alignée", "expérience pertinente"],
      "lacunes": ["compétence manquante"],
      "recommandation": "Action recommandée en 1 phrase"
    }}
  ],
  "conseil_global": "Conseil stratégique global en 1-2 phrases"
}}

Classe par score décroissant. Sois réaliste et précis dans les scores.
"""

    try:
        llm = get_llm(temperature=0.2, agent_name="default")
        response = await llm.ainvoke([
            SystemMessage(content="Tu es un expert en recrutement et matching emploi. Évalue la compatibilité candidat-poste de manière réaliste. Réponds UNIQUEMENT en JSON valide."),
            HumanMessage(content=matching_prompt),
        ])

        raw = response.content if hasattr(response, "content") else str(response)
        if isinstance(raw, list):
            raw = "".join(item.get("text", "") if isinstance(item, dict) else str(item) for item in raw)

        raw = raw.strip()
        if "```json" in raw:
            raw = raw.split("```json")[1].split("```")[0].strip()
        elif "```" in raw:
            raw = raw.split("```")[1].split("```")[0].strip()

        result = json.loads(raw)
        logger.info("[Tool:SN] Matching complete: %d candidatures scored", len(result.get("matches", [])))
        return result

    except json.JSONDecodeError:
        logger.warning("[Tool:SN] match_cv_with_candidatures JSON error")
        return {"error": "Analyse de matching impossible.", "matches": []}
    except Exception as e:
        logger.error("[Tool:SN] match_cv_with_candidatures error: %s", e)
        return {"error": str(e), "matches": []}


def clear_profile_cache(user_id: Optional[str] = None) -> None:
    """Clear cached profile data. If user_id provided, clears only that user."""
    if user_id:
        _profile_cache.pop(user_id, None)
    else:
        _profile_cache.clear()
