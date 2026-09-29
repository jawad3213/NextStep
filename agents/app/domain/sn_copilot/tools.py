# ============================================================
# agents/app/domain/sn_copilot/tools.py
#
# Candidature tools for SN Executive AI Copilot, through the backend's internal
# Applications endpoints (the agents never write the backend's tables):
#   - get_recent_candidatures: Read up to N (default 10) candidatures
#   - update_candidature_status: Update status & insert history
#   - add_candidature_note: Add recruiter / follow-up note
#   - create_candidature: Create / apply for new candidature
#   - get_candidature_stats: Aggregated conversion metrics
#   - get_pending_follow_ups: Stale applications (>7 days)
# ============================================================
import logging
from typing import Any, Dict, List, Optional

from app.core import backend_client

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

# Statuses the backend and the applications board know. Anything else is refused
# instead of being written as-is.
VALID_STATUSES = {
    "BROUILLON", "ENVOYE", "ACCUSE_RECEPTION", "EN_ATTENTE", "EN_COURS_EXAMEN",
    "RELANCE_NECESSAIRE", "RELANCE_ENVOYEE", "REPONSE_RECUE", "TEST_TECHNIQUE",
    "ENTRETIEN_PROPOSE", "ENTRETIEN_EFFECTUE", "OFFRE_RECUE", "ACCEPTE", "REFUSE", "ABANDONNE",
}


def normalize_status(value: str) -> Optional[str]:
    """Backend status for a user wording ("entretien", "refusé", "ENVOYE"...), or None."""
    clean = (value or "").strip()
    status = STATUS_NORMALIZATION_MAP.get(clean.lower(), clean.upper().replace(" ", "_"))
    return status if status in VALID_STATUSES else None


def find_candidature(candidatures: List[Dict[str, Any]], query: str) -> Dict[str, Any]:
    """
    The one candidature a user query designates: its id, its company, or its role.
    Returns {"status": "found", "candidature"}, or "not_found" / "ambiguous" with a message.
    An empty query never matches (it used to match the most recent candidature).
    """
    q = (query or "").strip().lower()
    if not q:
        return {"status": "not_found", "message": "Précisez l'entreprise ou le poste de la candidature."}

    def company(c):
        return (c.get("entreprise") or "").strip().lower()

    def role(c):
        return (c.get("role") or "").strip().lower()

    levels = [
        [c for c in candidatures if (c.get("id") or "").lower() == q],
        [c for c in candidatures if company(c) and company(c) == q],
        [c for c in candidatures if company(c) and (q in company(c) or (len(company(c)) >= 3 and company(c) in q))],
        [c for c in candidatures if role(c) and q in role(c)],
    ]
    for matches in levels:
        if len(matches) == 1:
            return {"status": "found", "candidature": matches[0]}
        if len(matches) > 1:
            names = ", ".join(f"{m.get('entreprise') or '?'} ({m.get('role') or '?'})" for m in matches[:5])
            return {"status": "ambiguous", "message": f"Plusieurs candidatures correspondent à '{query}' : {names}. Laquelle ?"}
    return {"status": "not_found", "message": f"Impossible de trouver une candidature correspondant à '{query}'."}


def _to_card(c: Dict[str, Any]) -> Dict[str, Any]:
    """Backend AgentCandidatureDto (camelCase) → the copilot's candidature dict."""
    return {
        "id": str(c.get("id") or ""),
        "id_offre": str(c["idOffre"]) if c.get("idOffre") else None,
        "entreprise": c.get("entreprise") or "Entreprise non précisée",
        "role": c.get("role") or "Poste non précisé",
        "statut": c.get("statut") or "ENVOYE",
        "channel": c.get("channel") or "EMAIL",
        "channel_url": c.get("channelUrl"),
        "application_date": str(c.get("applicationDate") or "")[:10],
        "has_response": bool(c.get("hasResponse")),
        "response_status": c.get("responseStatus") or "EN_ATTENTE",
        "notes": c.get("notes") or "",
        "response_summary": c.get("responseSummary"),
        "recommended_action": c.get("recommendedAction"),
        "follow_up_needed": bool(c.get("followUpNeeded")),
    }


async def get_recent_candidatures(user_id: str, limit: int = 10) -> List[Dict[str, Any]]:
    """The user's latest candidatures (most recent first), with company and role resolved."""
    logger.info("[Tool:SN] get_recent_candidatures — user_id=%s, limit=%d", user_id, limit)
    try:
        return [_to_card(c) for c in await backend_client.list_candidatures(user_id, limit)]
    except backend_client.BackendError as e:
        logger.error("[Tool:SN] get_recent_candidatures error: %s", e.message)
        return []


async def update_candidature_status(user_id: str, query: str, new_status: str) -> Dict[str, Any]:
    """
    Locate a candidature by company, role or id and update its status (the backend
    records it in the status history, source "ai_sn").
    """
    logger.info("[Tool:SN] update_candidature_status — user_id=%s, query=%s, status=%s", user_id, query, new_status)
    normalized_status = normalize_status(new_status)
    if normalized_status is None:
        return {
            "status": "error",
            "message": f"Statut '{new_status}' inconnu. Statuts possibles : {', '.join(sorted(VALID_STATUSES))}.",
        }

    all_cands = await get_recent_candidatures(user_id, limit=50)
    if not all_cands:
        return {"status": "error", "message": "Aucune candidature trouvée pour cet utilisateur."}
    found = find_candidature(all_cands, query)
    if found["status"] != "found":
        return found
    matched = found["candidature"]

    try:
        await backend_client.update_candidature_status(user_id, matched["id"], normalized_status)
    except backend_client.BackendError as e:
        return {"status": "error", "message": e.message}

    return {
        "status": "success",
        "id": matched["id"],
        "entreprise": matched["entreprise"],
        "role": matched["role"],
        "old_status": matched["statut"],
        "new_status": normalized_status,
        "message": f"Statut de la candidature chez {matched['entreprise']} mis à jour en '{normalized_status}'.",
    }


async def add_candidature_note(user_id: str, query: str, note_text: str) -> Dict[str, Any]:
    """Add a note to the candidature a company, role or id designates."""
    logger.info("[Tool:SN] add_candidature_note — user_id=%s, query=%s", user_id, query)
    all_cands = await get_recent_candidatures(user_id, limit=50)
    if not all_cands:
        return {"status": "error", "message": "Aucune candidature trouvée."}
    found = find_candidature(all_cands, query)
    if found["status"] != "found":
        return found
    matched = found["candidature"]

    try:
        await backend_client.add_candidature_note(user_id, matched["id"], note_text.strip())
    except backend_client.BackendError as e:
        return {"status": "error", "message": e.message}

    return {
        "status": "success",
        "id": matched["id"],
        "entreprise": matched["entreprise"],
        "note": note_text,
        "message": f"Note ajoutée avec succès pour {matched['entreprise']}.",
    }


async def create_candidature(
    user_id: str,
    entreprise: str,
    poste: str,
    type_contrat: str = "CDI",
    channel: str = "EMAIL",
    notes: str = ""
) -> Dict[str, Any]:
    """Register a new candidature (action 'postuler'), already sent (status ENVOYE)."""
    logger.info("[Tool:SN] create_candidature — user_id=%s, entreprise=%s, poste=%s", user_id, entreprise, poste)
    try:
        created = await backend_client.create_candidature(user_id, entreprise, poste, channel.upper(), notes)
    except backend_client.BackendError as e:
        return {"status": "error", "message": e.message}
    if not created:
        return {"status": "error", "message": "Utilisateur introuvable."}

    card = _to_card(created)
    return {
        "status": "success",
        "id": card["id"],
        "entreprise": entreprise,
        "poste": poste,
        "channel": card["channel"],
        "statut": card["statut"],
        "message": f"Candidature chez {entreprise} pour le poste '{poste}' enregistrée avec succès !",
    }


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
