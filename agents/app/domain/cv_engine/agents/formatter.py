import logging
from typing import Dict, Any, List

from app.domain.cv_engine.schemas.models import (
    QuestPDFCvData,
    QuestPDFCandidate,
    QuestPDFExperience,
    QuestPDFProject,
    QuestPDFEducation,
    QuestPDFSkill,
)

logger = logging.getLogger(__name__)

def _fuzzy_match_title(t1: str, t2: str) -> bool:
    """Comparaison basique (case-insensitive et sans espaces) pour trouver l'expérience originale."""
    if not t1 or not t2:
        return False
    return t1.strip().lower() == t2.strip().lower()

def build_questpdf_payload(original_profile: Dict[str, Any], optimized_cv: Dict[str, Any]) -> QuestPDFCvData:
    """
    Fusionne le profil original et les données optimisées pour créer le payload exact de QuestPDF.
    Cette fonction est 100% algorithmique pour éviter les hallucinations.
    """
    logger.info("Début de la fusion algorithmique CV Engine.")
    
    # 1. Contact (Candidat)
    first_name = original_profile.get("first_name", "")
    last_name = original_profile.get("last_name", "")
    candidate = QuestPDFCandidate(
        name=f"{first_name} {last_name}".strip(),
        email=original_profile.get("email", ""),
        phone=original_profile.get("phone", ""),
        location=original_profile.get("location", ""),
        # LinkedIn/GitHub etc. si présents dans le dict original
        linked_in=original_profile.get("linkedin"),
        git_hub=original_profile.get("github"),
        portfolio=original_profile.get("portfolio")
    )

    # 2. Summary
    resume_opt = optimized_cv.get("resume_optimise", {})
    summary = resume_opt.get("contenu", original_profile.get("summary", ""))

    # 3. Experiences
    quest_experiences: List[QuestPDFExperience] = []
    original_exps = original_profile.get("experiences", [])
    opt_exps = optimized_cv.get("experiences_optimisees", [])
    
    for opt_exp in opt_exps:
        opt_title = opt_exp.get("titre", "")
        opt_company = opt_exp.get("entreprise", "")
        opt_desc = opt_exp.get("description_optimisee", "")
        
        # Recherche des dates dans l'original
        start_date = None
        end_date = None
        for orig_exp in original_exps:
            # Match sur le titre
            if _fuzzy_match_title(orig_exp.get("title", ""), opt_title):
                start_date = orig_exp.get("start_date")
                end_date = orig_exp.get("end_date")
                break
                
        # Split par \n ou - pour faire des puces
        bullets = [b.strip().lstrip("-").strip() for b in opt_desc.split("\n") if b.strip()]
        if not bullets and opt_desc:
            bullets = [opt_desc]
            
        quest_experiences.append(QuestPDFExperience(
            role=opt_title,
            company=opt_company,
            start=start_date,
            end=end_date,
            bullets=bullets
        ))

    # 4. Projets
    quest_projects: List[QuestPDFProject] = []
    opt_projects = optimized_cv.get("projets_optimises", [])
    for opt_proj in opt_projects:
        bullets = [b.strip().lstrip("-").strip() for b in opt_proj.get("description_optimisee", "").split("\n") if b.strip()]
        quest_projects.append(QuestPDFProject(
            title=opt_proj.get("titre", ""),
            description=None,  # On met tout en bullets
            bullets=bullets
        ))

    # 5. Educations
    quest_educations: List[QuestPDFEducation] = []
    original_edus = original_profile.get("educations", [])
    opt_edus = optimized_cv.get("formations_optimisees", [])
    for opt_edu in opt_edus:
        # Match pour l'année
        year = None
        for orig_edu in original_edus:
            if _fuzzy_match_title(orig_edu.get("degree", ""), opt_edu.get("diplome", "")):
                year = orig_edu.get("start_date") # Ou end_date selon structure
                break
                
        quest_educations.append(QuestPDFEducation(
            degree=opt_edu.get("diplome", ""),
            institution=opt_edu.get("etablissement", ""),
            year=year
        ))

    # 6. Certifications & Languages
    # Récupérer les certifs optimisées ou originales
    opt_certs = optimized_cv.get("certifications_optimisees", [])
    cert_list = [f"{c.get('nom')} - {c.get('organisme')}" for c in opt_certs]
    
    # Langues
    lang_list = [l.get("language", "") for l in original_profile.get("languages", []) if isinstance(l, dict)]

    # 7. Skills
    quest_skills: List[QuestPDFSkill] = []
    opt_skills = optimized_cv.get("competences_reordonnees", [])
    for skill_name in opt_skills:
        quest_skills.append(QuestPDFSkill(
            name=skill_name,
            level=3, # Default level
            is_matched=True
        ))

    # Assemblage Final
    payload = QuestPDFCvData(
        candidate=candidate,
        summary=summary,
        experience=quest_experiences,
        education=quest_educations,
        skills=quest_skills,
        projects=quest_projects,
        certifications=cert_list,
        languages=lang_list,
        activities=[],
        # Metadata
        ats_score=0, # À injecter plus tard si besoin
        matching_score=0,
        ats_coverage_pct=0.0
    )
    
    logger.info("Fusion algorithmique CV Engine terminée avec succès.")
    return payload
