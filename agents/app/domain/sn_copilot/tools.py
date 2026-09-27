# ============================================================
# agents/app/domain/sn_copilot/tools.py
#
# Direct PostgreSQL tools for SN Executive AI Copilot:
#   - get_recent_candidatures: Read up to N (default 10) candidatures
#   - update_candidature_status: Update status & insert history
#   - add_candidature_note: Add recruiter / follow-up note
#   - create_candidature: Create / apply for new candidature
#   - get_candidature_stats: Aggregated conversion metrics
#   - get_pending_follow_ups: Stale applications (>7 days)
# ============================================================
import json
import logging
from typing import Any, Dict, List, Optional
from sqlalchemy import text
from app.core.database import AsyncSessionFactory

logger = logging.getLogger(__name__)

STATUS_NORMALIZATION_MAP = {
    "brouillon": "BROUILLON",
    "draft": "BROUILLON",
    "envoye": "ENVOYE",
    "envoyé": "ENVOYE",
    "sent": "ENVOYE",
    "en attente": "EN_ATTENTE",
    "en_attente": "EN_ATTENTE",
    "pending": "EN_ATTENTE",
    "relance": "RELANCE_NECESSAIRE",
    "relance_necessaire": "RELANCE_NECESSAIRE",
    "entretien": "ENTRETIEN_PROPOSE",
    "entretien_propose": "ENTRETIEN_PROPOSE",
    "interview": "ENTRETIEN_PROPOSE",
    "test tech": "TEST_TECHNIQUE",
    "test_technique": "TEST_TECHNIQUE",
    "accepte": "ACCEPTE",
    "accepté": "ACCEPTE",
    "accepted": "ACCEPTE",
    "refuse": "REFUSE",
    "refusé": "REFUSE",
    "rejected": "REFUSE",
}


def _extract_company_and_role(row: Dict[str, Any]) -> tuple[str, str]:
    """Extract company and job title from analyse_json or notes."""
    company = ""
    role = ""

    # 1. Try analyse_json
    raw_json = row.get("analyse_json")
    if raw_json:
        if isinstance(raw_json, str):
            try:
                raw_json = json.loads(raw_json)
            except Exception:
                raw_json = {}
        if isinstance(raw_json, dict):
            company = raw_json.get("entreprise") or raw_json.get("company_name") or ""
            role = raw_json.get("titre_poste") or raw_json.get("job_title") or raw_json.get("titre") or ""

    # 2. Try notes lines if missing
    notes = str(row.get("notes") or "").strip()
    if notes:
        lines = [line.strip() for line in notes.split("\n") if line.strip()]
        if not company and len(lines) >= 1:
            company = lines[0]
        if not role and len(lines) >= 2:
            role = lines[1]

    if not company:
        company = "Entreprise non précisée"
    if not role:
        role = "Poste non précisé"

    return company, role


async def _resolve_user_id(session, user_id: Optional[str]) -> str:
    """
    Resolve the caller's id (Keycloak subject or id_utilisateur UUID) to an
    id_utilisateur. Never falls back to another user: an unknown id raises,
    so one user's data can never be served to someone else.
    """
    uid_str = str(user_id or "").strip()
    if uid_str:
        res = (await session.execute(
            text("SELECT id_utilisateur::text FROM public.utilisateur WHERE id_utilisateur::text = :uid OR keycloak_id = :uid LIMIT 1"),
            {"uid": uid_str}
        )).scalar()
        if res:
            return str(res)
    raise ValueError(f"Unknown user id: {uid_str!r}")


async def get_recent_candidatures(user_id: str, limit: int = 10) -> List[Dict[str, Any]]:
    """
    Retrieve the user's latest candidatures (default 10) directly from PostgreSQL.
    Joins candidature and offres_emploi for rich context.
    """
    logger.info("[Tool:SN] get_recent_candidatures — user_id=%s, limit=%d", user_id, limit)
    try:
        async with AsyncSessionFactory() as session:
            resolved_uid = await _resolve_user_id(session, user_id)
            query = text("""
                SELECT 
                    c.id_candidature::text AS id,
                    c.id_offre::text AS id_offre,
                    c.statut,
                    c.channel,
                    c.channel_url,
                    c.channel_contact,
                    c.application_date::text AS application_date,
                    c.date_creation::text AS date_creation,
                    c.has_response,
                    c.response_status,
                    c.last_response_snippet,
                    c.response_summary,
                    c.recommended_action,
                    c.follow_up_needed,
                    c.notes,
                    o.analyse_json
                FROM public.candidature c
                LEFT JOIN public.offres_emploi o ON c.id_offre = o.id
                WHERE (
                    c.id_utilisateur::text = :uid 
                    OR :uid IS NULL
                    OR c.id_utilisateur IN (
                        SELECT id_utilisateur FROM public.utilisateur 
                        WHERE keycloak_id = :uid OR id_utilisateur::text = :uid
                    )
                )
                ORDER BY c.application_date DESC NULLS LAST, c.date_creation DESC
                LIMIT :limit
            """)
            result = await session.execute(query, {"uid": str(resolved_uid), "limit": limit})
            rows = result.mappings().all()

            candidatures = []
            for r in rows:
                r_dict = dict(r)
                company, role = _extract_company_and_role(r_dict)
                candidatures.append({
                    "id": r_dict.get("id"),
                    "id_offre": r_dict.get("id_offre"),
                    "entreprise": company,
                    "role": role,
                    "statut": r_dict.get("statut") or "ENVOYE",
                    "channel": r_dict.get("channel") or "EMAIL",
                    "channel_url": r_dict.get("channel_url"),
                    "application_date": (r_dict.get("application_date") or r_dict.get("date_creation") or "")[:10],
                    "has_response": bool(r_dict.get("has_response")),
                    "response_status": r_dict.get("response_status") or "EN_ATTENTE",
                    "notes": r_dict.get("notes") or "",
                    "response_summary": r_dict.get("response_summary"),
                    "recommended_action": r_dict.get("recommended_action"),
                    "follow_up_needed": bool(r_dict.get("follow_up_needed")),
                })
            return candidatures
    except Exception as e:
        logger.error("[Tool:SN] get_recent_candidatures error: %s", e)
        return []


async def update_candidature_status(user_id: str, query: str, new_status: str) -> Dict[str, Any]:
    """
    Locate a candidature by company name or UUID and update its status.
    Records an entry in candidature_status_history.
    """
    logger.info("[Tool:SN] update_candidature_status — user_id=%s, query=%s, status=%s", user_id, query, new_status)
    normalized_status = STATUS_NORMALIZATION_MAP.get(new_status.strip().lower(), new_status.upper())

    try:
        async with AsyncSessionFactory() as session:
            # 1. Fetch user candidatures to match query
            all_cands = await get_recent_candidatures(user_id, limit=50)
            if not all_cands:
                return {"status": "error", "message": "Aucune candidature trouvée pour cet utilisateur."}

            q_lower = query.strip().lower()
            matched = None

            # Check direct UUID match first
            for c in all_cands:
                if c["id"].lower() == q_lower:
                    matched = c
                    break

            # Check company match
            if not matched:
                for c in all_cands:
                    if q_lower in c["entreprise"].lower() or c["entreprise"].lower() in q_lower:
                        matched = c
                        break

            # Check role / notes match
            if not matched:
                for c in all_cands:
                    if q_lower in c["role"].lower() or q_lower in (c["notes"] or "").lower():
                        matched = c
                        break

            if not matched:
                return {
                    "status": "not_found",
                    "message": f"Impossible de trouver une candidature correspondant à '{query}'."
                }

            cid = matched["id"]
            old_status = matched["statut"]

            # Update candidature
            await session.execute(
                text("UPDATE public.candidature SET statut = :statut WHERE id_candidature::text = :cid"),
                {"statut": normalized_status, "cid": cid}
            )

            # Insert status history
            await session.execute(
                text("""
                    INSERT INTO public.candidature_status_history (
                        id, id_candidature, ancien_statut, nouveau_statut, source, details, created_at
                    ) VALUES (
                        gen_random_uuid(), CAST(:cid AS uuid), :old_s, :new_s, 'ai_sn', 'Mis à jour par SN Copilot', now()
                    )
                """),
                {"cid": cid, "old_s": old_status, "new_s": normalized_status}
            )
            await session.commit()

            return {
                "status": "success",
                "id": cid,
                "entreprise": matched["entreprise"],
                "role": matched["role"],
                "old_status": old_status,
                "new_status": normalized_status,
                "message": f"Statut de la candidature chez {matched['entreprise']} mis à jour en '{normalized_status}'."
            }
    except Exception as e:
        logger.error("[Tool:SN] update_candidature_status error: %s", e)
        return {"status": "error", "message": str(e)}


async def add_candidature_note(user_id: str, query: str, note_text: str) -> Dict[str, Any]:
    """
    Add a note to a candidature matching company name or UUID.
    """
    logger.info("[Tool:SN] add_candidature_note — user_id=%s, query=%s", user_id, query)
    try:
        async with AsyncSessionFactory() as session:
            all_cands = await get_recent_candidatures(user_id, limit=50)
            if not all_cands:
                return {"status": "error", "message": "Aucune candidature trouvée."}

            q_lower = query.strip().lower()
            matched = None
            for c in all_cands:
                if c["id"].lower() == q_lower or q_lower in c["entreprise"].lower() or c["entreprise"].lower() in q_lower:
                    matched = c
                    break

            if not matched:
                return {"status": "not_found", "message": f"Candidature '{query}' introuvable."}

            cid = matched["id"]
            await session.execute(
                text("""
                    INSERT INTO public.candidature_note (id, id_candidature, contenu, auteur, created_at)
                    VALUES (gen_random_uuid(), CAST(:cid AS uuid), :content, 'ai', now())
                """),
                {"cid": cid, "content": note_text.strip()}
            )
            # Also append to notes column for quick search
            await session.execute(
                text("""
                    UPDATE public.candidature 
                    SET notes = COALESCE(notes, '') || E'\n[Note IA]: ' || :note
                    WHERE id_candidature::text = :cid
                """),
                {"cid": cid, "note": note_text.strip()}
            )
            await session.commit()

            return {
                "status": "success",
                "id": cid,
                "entreprise": matched["entreprise"],
                "note": note_text,
                "message": f"Note ajoutée avec succès pour {matched['entreprise']}."
            }
    except Exception as e:
        logger.error("[Tool:SN] add_candidature_note error: %s", e)
        return {"status": "error", "message": str(e)}


async def create_candidature(
    user_id: str,
    entreprise: str,
    poste: str,
    type_contrat: str = "CDI",
    channel: str = "EMAIL",
    notes: str = ""
) -> Dict[str, Any]:
    """
    Register a new candidature (action 'postuler') directly in the database.
    """
    logger.info("[Tool:SN] create_candidature — user_id=%s, entreprise=%s, poste=%s", user_id, entreprise, poste)
    try:
        async with AsyncSessionFactory() as session:
            # 1. Resolve internal user UUID
            resolved_uid = await _resolve_user_id(session, user_id)

            composite_notes = f"{entreprise}\n{poste}"
            if notes:
                composite_notes += f"\n{notes}"

            # Insert candidature
            res = await session.execute(
                text("""
                    INSERT INTO public.candidature (
                        id_candidature, id_utilisateur, statut, channel, application_date,
                        applied_manually, notes, date_creation, response_status, has_response
                    ) VALUES (
                        gen_random_uuid(), CAST(:uid AS uuid), 'ENVOYE', :channel, now(),
                        true, :notes, now(), 'EN_ATTENTE', false
                    ) RETURNING id_candidature::text
                """),
                {
                    "uid": str(resolved_uid),
                    "channel": channel.upper(),
                    "notes": composite_notes
                }
            )
            new_id = res.scalar_one()

            # Insert initial status history
            await session.execute(
                text("""
                    INSERT INTO public.candidature_status_history (
                        id, id_candidature, ancien_statut, nouveau_statut, source, details, created_at
                    ) VALUES (
                        gen_random_uuid(), CAST(:cid AS uuid), NULL, 'ENVOYE', 'ai_sn', 'Créée via SN Copilot (Action Postuler)', now()
                    )
                """),
                {"cid": new_id}
            )
            await session.commit()

            return {
                "status": "success",
                "id": new_id,
                "entreprise": entreprise,
                "poste": poste,
                "channel": channel,
                "statut": "ENVOYE",
                "message": f"Candidature chez {entreprise} pour le poste '{poste}' enregistrée avec succès !"
            }
    except Exception as e:
        logger.error("[Tool:SN] create_candidature error: %s", e)
        return {"status": "error", "message": str(e)}


async def get_candidature_stats(user_id: str) -> Dict[str, Any]:
    """
    Calculate executive conversion statistics for the user.
    """
    logger.info("[Tool:SN] get_candidature_stats — user_id=%s", user_id)
    candidatures = await get_recent_candidatures(user_id, limit=200)
    total = len(candidatures)
    if total == 0:
        return {
            "total": 0,
            "envoyees": 0,
            "entretiens": 0,
            "en_attente": 0,
            "acceptees": 0,
            "refusees": 0,
            "taux_reponse": "0%",
            "taux_entretien": "0%"
        }

    envoyees = sum(1 for c in candidatures if "ENVOYE" in c["statut"])
    entretiens = sum(1 for c in candidatures if "ENTRETIEN" in c["statut"])
    en_attente = sum(1 for c in candidatures if "ATTENTE" in c["statut"] or c["response_status"] == "EN_ATTENTE")
    acceptees = sum(1 for c in candidatures if "ACCEPTE" in c["statut"])
    refusees = sum(1 for c in candidatures if "REFUSE" in c["statut"])
    avec_reponse = sum(1 for c in candidatures if c.get("has_response") or c["statut"] in ["ENTRETIEN_PROPOSE", "ACCEPTE", "REFUSE"])

    taux_reponse = f"{round((avec_reponse / total) * 100)}%"
    taux_entretien = f"{round((entretiens / total) * 100)}%"

    return {
        "total": total,
        "envoyees": envoyees,
        "entretiens": entretiens,
        "en_attente": en_attente,
        "acceptees": acceptees,
        "refusees": refusees,
        "taux_reponse": taux_reponse,
        "taux_entretien": taux_entretien
    }


async def get_pending_follow_ups(user_id: str) -> List[Dict[str, Any]]:
    """
    Identify candidatures requiring follow-up (sent > 7 days ago or marked follow_up_needed).
    """
    logger.info("[Tool:SN] get_pending_follow_ups — user_id=%s", user_id)
    candidatures = await get_recent_candidatures(user_id, limit=50)
    follow_ups = []
    for c in candidatures:
        if c.get("follow_up_needed") or (c.get("statut") == "ENVOYE" and not c.get("has_response")):
            follow_ups.append({
                "id": c["id"],
                "entreprise": c["entreprise"],
                "role": c["role"],
                "application_date": c["application_date"],
                "channel": c["channel"]
            })
    return follow_ups[:5]
