# ============================================================
# app/domain/offer/agents/scorer.py
# Agent 4 — Scorer (Calcul mathématique, SANS LLM)
#
# Score ATS  : pondération positionnelle (titre/résumé/compétences/expériences)
# Score Match: Jaccard pondéré 70% compétences requises + 30% souhaitées
# ============================================================
import logging
from langchain_core.messages import AIMessage
from app.domain.offer.schemas.state import OfferState
from app.domain.offer.agents.normalizer import normalize_skills, normalize_text

logger = logging.getLogger(__name__)

# ─── Pondération positionnelle ATS (selon README NextStep) ────
#   Mot-clé dans le TITRE        → +5 pts
#   Mot-clé dans le RÉSUMÉ       → +3 pts
#   Mot-clé dans les COMPÉTENCES → +2 pts
#   Mot-clé dans les EXPÉRIENCES → +1 pt
POSITION_BONUS = {"titre": 5, "resume": 3, "competences": 2, "experiences": 1}
MAX_BONUS_PAR_KW = sum(POSITION_BONUS.values())  # 11 pts max par mot-clé


# ─── Score ATS ────────────────────────────────────────────────

def _ats_score(
    keywords: list[str],
    profile_titre: str,
    profile_resume: str,
    profile_skills_text: str,
    exp_text: str,
) -> tuple[int, list[str], list[str]]:
    """
    Calcule le score ATS avec pondération positionnelle.

    Algorithme :
      1. Normaliser les deux textes (fait en amont par Agent 3)
      2. Pour chaque keyword → chercher dans titre / résumé / compétences / expériences
      3. score_base = (nb_trouvés / total) × 100
      4. score_bonus = (total_bonus / max_bonus) × 20   [bonus positionnel]
      5. score_final = min(score_base + score_bonus, 100)

    Returns:
        (score_ats, keywords_presents, keywords_manquants)
    """
    if not keywords:
        return 0, [], []

    presents, manquants = [], []
    total_bonus = 0

    for kw in keywords:
        bonus = 0
        if kw in profile_titre:        bonus += POSITION_BONUS["titre"]
        if kw in profile_resume:       bonus += POSITION_BONUS["resume"]
        if kw in profile_skills_text:  bonus += POSITION_BONUS["competences"]
        if kw in exp_text:             bonus += POSITION_BONUS["experiences"]

        if bonus > 0:
            presents.append(kw)
            total_bonus += bonus
        else:
            manquants.append(kw)

    n = len(keywords)
    score_base  = int(len(presents) / n * 100) if n else 0
    max_bonus   = n * MAX_BONUS_PAR_KW
    score_bonus = int(total_bonus / max_bonus * 20) if max_bonus else 0
    return min(score_base + score_bonus, 100), presents, manquants


# ─── Score Matching ───────────────────────────────────────────

def _matching_score(
    offer_required: list[str],
    offer_optional: list[str],
    profile_skills: list[str],
) -> tuple[int, list[str], list[str]]:
    """
    Calcule le score de matching Jaccard pondéré 70/30.

    Algorithme :
      s_req = (|Profil ∩ Requis| / |Requis|) × 100
      s_opt = (|Profil ∩ Optionnel| / |Optionnel|) × 100
      score = s_req × 0.70 + s_opt × 0.30

    Si une liste est vide → score partiel = 100% (pas pénalisé).

    Returns:
        (score_matching, competences_matching, competences_manquantes)
    """
    profil_set = set(profile_skills)
    req_set    = set(offer_required)
    opt_set    = set(offer_optional)

    matched_req = req_set & profil_set
    matched_opt = opt_set & profil_set

    s_req = (len(matched_req) / len(req_set) * 100) if req_set else 100.0
    s_opt = (len(matched_opt) / len(opt_set) * 100) if opt_set else 100.0

    score      = min(int(s_req * 0.70 + s_opt * 0.30), 100)
    matching   = list(matched_req | matched_opt)
    manquantes = list(req_set - profil_set)
    return score, matching, manquantes


# ─── Recommandations ──────────────────────────────────────────

def _recommendations(
    kw_manquants: list[str],
    comp_manquantes: list[str],
    score_ats: int,
    score_matching: int,
) -> list[str]:
    """Génère des recommandations actionnables basées sur les scores."""
    recs: list[str] = []

    # Compétences manquantes
    if comp_manquantes:
        top = ", ".join(comp_manquantes[:4])
        recs.append(f"Ajouter ces compétences dans votre profil : {top}")

    # Mots-clés ATS manquants
    if kw_manquants:
        top = ", ".join(kw_manquants[:5])
        recs.append(f"Intégrer ces mots-clés ATS dans votre CV : {top}")

    # Conseil score ATS
    if score_ats < 40:
        recs.append("Score ATS très faible — enrichissez votre résumé avec les termes de l'offre")
    elif score_ats < 60:
        recs.append("Score ATS moyen — ajoutez les compétences manquantes dans votre profil")
    elif score_ats < 80:
        recs.append("Bon score ATS — vérifiez que votre titre reflète bien le poste visé")
    else:
        recs.append("Excellent score ATS — votre profil est bien aligné avec cette offre !")

    # Conseil score matching
    if score_matching < 50:
        recs.append("Matching faible — cette offre est peu adaptée à votre profil actuel")

    return recs


# ─── Nœud principal ───────────────────────────────────────────

async def scorer_node(state: OfferState) -> dict:
    """
    Nœud LangGraph — Agent 4 : Calcul des scores matching + ATS.

    ┌────────────────────────────────────────────────────────────┐
    │  Entrées state  │  normalized_offer_skills                │
    │                 │  normalized_profile_skills              │
    │                 │  normalized_keywords                    │
    │                 │  profile_full_text                      │
    │                 │  analyzed_offer (compétences souhaitées)│
    │                 │  profile_data (titre, résumé)           │
    │  Sorties state  │  match_result, messages                 │
    │  LLM            │  aucun — calcul mathématique pur        │
    └────────────────────────────────────────────────────────────┘
    """
    logger.info("📈 Agent 4 [Scorer] — Démarrage")

    offer_skills   = state.get("normalized_offer_skills")   or []
    profile_skills = state.get("normalized_profile_skills") or []
    keywords       = state.get("normalized_keywords")       or []
    full_text      = state.get("profile_full_text")         or ""

    offer   = state.get("analyzed_offer")  or {}
    profile = state.get("profile_data")    or {}

    # Normaliser les compétences souhaitées (pas encore normalisées)
    offer_optional = normalize_skills(offer.get("competences_souhaitees", []))

    # Extraire les zones de texte spécifiques depuis profile_full_text
    # On re-normalise titre + résumé pour la pondération positionnelle ATS
    profile_titre       = normalize_text(profile.get("titre") or "")
    profile_resume      = normalize_text(profile.get("resume") or "")
    profile_skills_text = " ".join(profile_skills)

    # ── Calculs ────────────────────────────────────────────────
    score_ats, kw_presents, kw_manquants = _ats_score(
        keywords, profile_titre, profile_resume, profile_skills_text, full_text
    )
    score_matching, comp_matching, comp_manquantes = _matching_score(
        offer_skills, offer_optional, profile_skills
    )
    recs = _recommendations(kw_manquants, comp_manquantes, score_ats, score_matching)

    result = {
        "score_matching":        score_matching,
        "score_ats":             score_ats,
        "keywords_presents":     kw_presents,
        "keywords_manquants":    kw_manquants,
        "recommandations":       recs,
        "competences_matching":  comp_matching,
        "competences_manquantes": comp_manquantes,
    }

    logger.info(
        "Agent 4 ✅ — Score matching=%d%% | Score ATS=%d%% | %d/%d keywords",
        score_matching, score_ats,
        len(kw_presents), len(kw_presents) + len(kw_manquants),
    )

    summary = (
        f"[Agent 4] Scores : Matching={score_matching}% | ATS={score_ats}% "
        f"({len(kw_presents)}/{len(kw_presents)+len(kw_manquants)} keywords)"
    )
    return {
        "match_result": result,
        "messages": [AIMessage(content=summary, name="scorer")],
    }
