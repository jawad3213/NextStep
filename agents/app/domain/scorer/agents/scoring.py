# ============================================================
# app/domain/offer/agents/scorer/scoring.py
# Algorithmes de scoring — isolés pour tests unitaires faciles
#
# Fonctions pures (sans état LangGraph) :
#   - ats_score()       : Score ATS positionnel
#   - matching_score()  : Score Jaccard pondéré 70/30
#   - recommendations() : Recommandations actionnables
# ============================================================
import re

# ─── Pondération positionnelle ATS ────────────────────────────
#   Mot-clé dans le TITRE        → +5 pts
#   Mot-clé dans le RÉSUMÉ       → +3 pts
#   Mot-clé dans les COMPÉTENCES → +2 pts
#   Mot-clé dans les EXPÉRIENCES → +1 pt
POSITION_BONUS = {"titre": 5, "resume": 3, "competences": 2, "experiences": 1}
MAX_BONUS_PAR_KW = sum(POSITION_BONUS.values())   # 11 pts max par mot-clé


def ats_score(
    keywords: list[str],
    profile_titre: str,
    profile_resume: str,
    profile_skills_text: str,
    exp_text: str,
) -> tuple[int, list[str], list[str]]:
    """
    Calcule le score ATS avec pondération positionnelle.

    Algorithme :
      1. Pour chaque keyword → chercher dans titre / résumé / compétences / expériences
      2. score_base  = (nb_trouvés / total) × 100
      3. score_bonus = (total_bonus / max_bonus) × 20
      4. score_final = min(score_base + score_bonus, 100)

    Returns:
        (score_ats, keywords_presents, keywords_manquants)
    """
    if not keywords:
        return 0, [], []

    presents, manquants = [], []
    total_bonus = 0

    for kw in keywords:
        # Recherche par mot entier (évite "Java" dans "JavaScript")
        pattern = rf"\b{re.escape(kw)}\b"

        bonus = 0
        if re.search(pattern, profile_titre):       bonus += POSITION_BONUS["titre"]
        if re.search(pattern, profile_resume):      bonus += POSITION_BONUS["resume"]
        if re.search(pattern, profile_skills_text): bonus += POSITION_BONUS["competences"]
        if re.search(pattern, exp_text):            bonus += POSITION_BONUS["experiences"]

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


def matching_score(
    offer_required: list[str],
    offer_optional: list[str],
    profile_skills: list[str],
    profile_full_text: str = "",
) -> tuple[int, list[str], list[str]]:
    """
    Calcule le score de matching Jaccard pondéré 70/30 avec fallback textuel.

    Algorithme :
      s_req = |Profil ∩ Requis| / |Requis| × 100   (+ match contextuel)
      s_opt = |Profil ∩ Optionnel| / |Optionnel| × 100
      score = s_req × 0.70 + s_opt × 0.30

    Returns:
        (score_matching, competences_matching, competences_manquantes)
    """
    profil_set  = set(profile_skills)
    matched_req = set()
    matched_opt = set()
    manquantes  = []

    # 1. Vérification des compétences requises
    for req in offer_required:
        pattern = rf"\b{re.escape(req)}\b"
        if req in profil_set:
            matched_req.add(req)
        elif profile_full_text and re.search(pattern, profile_full_text):
            matched_req.add(req)  # Match contextuel
        else:
            manquantes.append(req)

    # 2. Vérification des compétences optionnelles
    for opt in offer_optional:
        pattern = rf"\b{re.escape(opt)}\b"
        if opt in profil_set:
            matched_opt.add(opt)
        elif profile_full_text and re.search(pattern, profile_full_text):
            matched_opt.add(opt)

    # 3. Calcul pondéré
    n_req = len(offer_required)
    n_opt = len(offer_optional)

    s_req = (len(matched_req) / n_req * 100) if n_req else 100.0
    s_opt = (len(matched_opt) / n_opt * 100) if n_opt else 100.0

    score    = min(int(s_req * 0.70 + s_opt * 0.30), 100)
    matching = list(matched_req | matched_opt)
    return score, matching, manquantes


def recommendations(
    kw_manquants: list[str],
    comp_manquantes: list[str],
    score_ats: int,
    score_matching: int,
) -> list[str]:
    """Génère des recommandations actionnables basées sur les scores."""
    recs: list[str] = []

    if comp_manquantes:
        top = ", ".join(comp_manquantes[:4])
        recs.append(f"Ajouter ces compétences dans votre profil : {top}")

    if kw_manquants:
        top = ", ".join(kw_manquants[:5])
        recs.append(f"Intégrer ces mots-clés ATS dans votre CV : {top}")

    if score_ats < 40:
        recs.append("Score ATS très faible — enrichissez votre résumé avec les termes de l'offre")
    elif score_ats < 60:
        recs.append("Score ATS moyen — ajoutez les compétences manquantes dans votre profil")
    elif score_ats < 80:
        recs.append("Bon score ATS — vérifiez que votre titre reflète bien le poste visé")
    else:
        recs.append("Excellent score ATS — votre profil est bien aligné avec cette offre !")

    if score_matching < 50:
        recs.append("Matching faible — cette offre est peu adaptée à votre profil actuel")

    return recs
