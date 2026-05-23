import logging

logger = logging.getLogger(__name__)

def calculate_mathematical_score(
    matched_skills_count: int,
    missing_skills_count: int,
    experience_years: float,
    required_years: float,
    cert_match: bool
) -> float:
    """
    Calcule un score de pertinence mathématique déterministe.
    
    Pondération :
    - Compétences : 50%
    - Expérience : 30%
    - Certifications : 20%
    """
    
    # 1. Score Compétences (50%)
    total_skills = matched_skills_count + missing_skills_count
    skill_score = 0.0
    if total_skills > 0:
        skill_score = (matched_skills_count / total_skills) * 0.5
    else:
        # Si aucune compétence n'est mentionnée ni demandée, on neutralise cette part
        skill_score = 0.25 # Score neutre
        
    # 2. Score Expérience (30%)
    exp_score = 0.0
    if required_years > 0:
        # Ratio exp_reelle / exp_requise
        ratio = experience_years / required_years
        # On plafonne à 1.2 pour ne pas trop booster, et on multiplie par le poids 0.3
        exp_score = min(ratio, 1.2) * 0.3
    else:
        # Si aucune expérience n'est requise, on donne le plein de points
        exp_score = 0.3
        
    # 3. Score Certifications (20%)
    cert_score = 0.2 if cert_match else 0.0
    
    # Total
    final_score = skill_score + exp_score + cert_score
    
    # Arrondi à 2 décimales et clamp entre 0 et 1
    return round(max(0.0, min(1.0, final_score)), 2)

def determine_flag(score: float, gap_years: float) -> str:
    """Détermine le flag de risque basé sur le score mathématique."""
    if score >= 0.85 and gap_years <= 0.5:
        return "perfect_match"
    elif score >= 0.60 and gap_years < 2.0:
        return "minor_gap"
    else:
        return "critical_gap"
