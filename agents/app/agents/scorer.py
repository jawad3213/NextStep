# ============================================================
# app/agents/scorer.py
# Agent 4 — Scorer (Calcul mathématique, pas de LLM)
#
# Score ATS avec pondération positionnelle (titre/résumé/compétences/expériences)
# Score Matching : Jaccard pondéré 70% requis + 30% optionnel
# ============================================================
import logging
from langchain_core.messages import AIMessage
from app.schemas.state import AgentState

logger = logging.getLogger(__name__)

# ─── Pondération positionnelle ATS (README NextStep) ───
POSITION_BONUS = {"titre": 5, "resume": 3, "competences": 2, "experiences": 1}
MAX_BONUS_PAR_KW = sum(POSITION_BONUS.values())  # 11


def _in_text(keyword: str, text: str) -> bool:
    return keyword in text


def _ats_score(
    keywords: list[str],
    profile_titre: str,
    profile_resume: str,
    profile_skills_text: str,
    exp_text: str,
) -> tuple[int, list[str], list[str]]:
    """Calcule le score ATS avec pondération positionnelle."""
    if not keywords:
        return 0, [], []

    presents, manquants = [], []
    total_bonus = 0

    for kw in keywords:
        bonus = 0
        if _in_text(kw, profile_titre):    bonus += POSITION_BONUS["titre"]
        if _in_text(kw, profile_resume):   bonus += POSITION_BONUS["resume"]
        if _in_text(kw, profile_skills_text): bonus += POSITION_BONUS["competences"]
        if _in_text(kw, exp_text):         bonus += POSITION_BONUS["experiences"]

        if bonus > 0:
            presents.append(kw)
            total_bonus += bonus
        else:
            manquants.append(kw)

    n = len(keywords)
    score_base = int(len(presents) / n * 100) if n else 0
    max_bonus = n * MAX_BONUS_PAR_KW
    score_bonus = int(total_bonus / max_bonus * 20) if max_bonus else 0
    return min(score_base + score_bonus, 100), presents, manquants


def _matching_score(
    offer_required: list[str],
    offer_optional: list[str],
    profile_skills: list[str],
) -> tuple[int, list[str], list[str]]:
    """Calcule le score matching Jaccard pondéré 70/30."""
    profil_set = set(profile_skills)
    req_set = set(offer_required)
    opt_set = set(offer_optional)

    matched_req = req_set & profil_set
    matched_opt = opt_set & profil_set

    s_req = (len(matched_req) / len(req_set) * 100) if req_set else 100
    s_opt = (len(matched_opt) / len(opt_set) * 100) if opt_set else 100

    score = min(int(s_req * 0.70 + s_opt * 0.30), 100)
    matching = list(matched_req | matched_opt)
    manquantes = list(req_set - profil_set)
    return score, matching, manquantes


def _recommendations(kw_manquants: list[str], comp_manquantes: list[str], score_ats: int) -> list[str]:
    recs = []
    if comp_manquantes:
        recs.append(f"Ajouter ces compétences : {', '.join(comp_manquantes[:3])}")
    if kw_manquants:
        recs.append(f"Intégrer ces mots-clés ATS : {', '.join(kw_manquants[:5])}")
    if score_ats < 50:
        recs.append("Score ATS faible : enrichissez votre résumé avec les termes de l'offre")
    elif score_ats < 70:
        recs.append("Score ATS moyen : ajoutez les compétences manquantes dans la section compétences")
    else:
        recs.append("Bon score ATS ! Vérifiez que le titre de votre CV reflète le poste visé")
    return recs


async def scorer_node(state: AgentState) -> dict:
    """
    Nœud LangGraph — Agent 4 : Calcul des scores matching + ATS.

    Entrées depuis state :
        - normalized_offer_skills, normalized_profile_skills, normalized_keywords
        - profile_full_text
        - analyzed_offer (pour compétences souhaitées)
        - profile_data (pour titre et résumé)

    Mise à jour de state :
        - match_result : dict avec scores + recommandations
        - messages : message AI
    """
    logger.info("📊 Agent 4 [Scorer] — Démarrage")

    offer_skills = state.get("normalized_offer_skills") or []
    profile_skills = state.get("normalized_profile_skills") or []
    keywords = state.get("normalized_keywords") or []
    full_text = state.get("profile_full_text") or ""

    # Récupérer compétences souhaitées (normalisées inline)
    offer = state.get("analyzed_offer") or {}
    profile = state.get("profile_data") or {}

    from app.agents.normalizer import normalize_skills, normalize_text
    offer_optional = normalize_skills(offer.get("competences_souhaitees", []))
    profile_titre = normalize_text(profile.get("titre") or "")
    profile_resume = normalize_text(profile.get("resume") or "")
    profile_skills_text = " ".join(profile_skills)

    # ─── Scoring ───
    score_ats, kw_presents, kw_manquants = _ats_score(
        keywords, profile_titre, profile_resume, profile_skills_text, full_text
    )
    score_matching, comp_matching, comp_manquantes = _matching_score(
        offer_skills, offer_optional, profile_skills
    )
    recs = _recommendations(kw_manquants, comp_manquantes, score_ats)

    result = {
        "score_matching": score_matching,
        "score_ats": score_ats,
        "keywords_presents": kw_presents,
        "keywords_manquants": kw_manquants,
        "recommandations": recs,
        "competences_matching": comp_matching,
        "competences_manquantes": comp_manquantes,
    }

    logger.info(
        "Agent 4 ✅ — Score matching=%d%% | Score ATS=%d%% | %d/%d keywords trouvés",
        score_matching, score_ats, len(kw_presents), len(kw_presents) + len(kw_manquants),
    )

    summary = (
        f"[Agent 4] Scores calculés : Matching={score_matching}% | ATS={score_ats}% "
        f"({len(kw_presents)}/{len(kw_presents)+len(kw_manquants)} keywords trouvés)"
    )
    return {
        "match_result": result,
        "messages": [AIMessage(content=summary, name="scorer")],
    }
