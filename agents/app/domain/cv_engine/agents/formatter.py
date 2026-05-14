import logging
from typing import Dict, Any, List, Optional

from app.domain.cv_engine.schemas.models import (
    QuestPDFCvData,
    QuestPDFCandidate,
    QuestPDFExperience,
    QuestPDFProject,
    QuestPDFEducation,
    QuestPDFSkill,
    QuestPDFActivity,
)

logger = logging.getLogger(__name__)

LANGUAGE_TYPES = {"language", "langue", "lang"}

def build_questpdf_payload(
    original_profile: Dict[str, Any],
    optimized_cv: Dict[str, Any],
    matched_skills: Optional[List[str]] = None,
    offer_skills: Optional[List[str]] = None,
) -> QuestPDFCvData:
    logger.info("Debut de la fusion algorithmique CV Engine.")

    # 1. Contact
    first_name = original_profile.get("prenom", "")
    last_name = original_profile.get("nom", "")
    candidate = QuestPDFCandidate(
        name=f"{first_name} {last_name}".strip(),
        email=original_profile.get("email", ""),
        phone=original_profile.get("telephone", ""),
        location=original_profile.get("ville", ""),
        linked_in=original_profile.get("linkedin"),
        git_hub=original_profile.get("github"),
        portfolio=original_profile.get("portfolio"),
    )

    # 2. Summary
    resume_opt = optimized_cv.get("resume_optimise", {})
    summary = resume_opt.get("contenu", original_profile.get("resume", ""))

    # 3. Experiences
    quest_experiences: List[QuestPDFExperience] = []
    original_exps = original_profile.get("experiences", [])
    opt_exps = optimized_cv.get("experiences_optimisees", [])

    for opt_exp in opt_exps:
        opt_title = opt_exp.get("titre", "")
        opt_company = opt_exp.get("entreprise", "")
        opt_desc = opt_exp.get("description_optimisee", "")

        start_date = None
        end_date = None
        orig_tasks = []
        for orig_exp in original_exps:
            if orig_exp.get("titre", "").strip().lower() == opt_title.strip().lower():
                start_date = orig_exp.get("date_debut")
                end_date = orig_exp.get("date_fin")
                orig_tasks = orig_exp.get("taches") or orig_exp.get("tasks") or []
                break

        bullets = []
        if orig_tasks:
            if isinstance(orig_tasks, list):
                bullets = [str(t).strip() for t in orig_tasks if t]
            elif isinstance(orig_tasks, str):
                bullets = [b.strip().lstrip("-").strip() for b in orig_tasks.split("\n") if b.strip()]

        if not bullets:
            bullets = [b.strip().lstrip("-").strip() for b in opt_desc.split("\n") if b.strip()]
        if not bullets and opt_desc:
            bullets = [opt_desc]

        quest_experiences.append(QuestPDFExperience(
            role=opt_title,
            company=opt_company,
            start=start_date,
            end=end_date,
            bullets=bullets,
        ))

    offer_techs = {s.lower() for s in (offer_skills or [])}
    quest_projects: List[QuestPDFProject] = []
    opt_projects = optimized_cv.get("projets_optimises", [])
    original_projs = original_profile.get("projets", [])
    
    scored_projects = []

    for opt_proj in opt_projects:
        opt_title = opt_proj.get("titre", "")
        project_techs = {t.lower() for t in opt_proj.get("technologies", [])}
        score = 0
        if offer_techs:
            matched_techs = project_techs & offer_techs
            score = len(matched_techs)

        # Match with original project to get taches
        orig_tasks = []
        for orig_p in original_projs:
            if orig_p.get("titre", "").strip().lower() == opt_title.strip().lower():
                orig_tasks = orig_p.get("taches") or orig_p.get("tasks") or []
                break

        bullets = []
        if orig_tasks:
            if isinstance(orig_tasks, list):
                bullets = [str(t).strip() for t in orig_tasks if t]
            elif isinstance(orig_tasks, str):
                bullets = [b.strip().lstrip("-").strip() for b in orig_tasks.split("\n") if b.strip()]

        if not bullets:
            bullets = [b.strip().lstrip("-").strip() for b in opt_proj.get("description_optimisee", "").split("\n") if b.strip()]

        scored_projects.append((score, QuestPDFProject(
            title=opt_title,
            description=opt_proj.get("description_optimisee", ""),
            bullets=bullets,
        )))
        
    # Sort projects by match score descending
    scored_projects.sort(key=lambda x: x[0], reverse=True)
    quest_projects = [p[1] for p in scored_projects]

    # 5. Educations
    quest_educations: List[QuestPDFEducation] = []
    original_edus = original_profile.get("formations", [])
    opt_edus = optimized_cv.get("formations_optimisees", [])

    for opt_edu in opt_edus:
        degree = opt_edu.get("diplome", "")
        institution = opt_edu.get("etablissement", "")
        year = None
        for orig_edu in original_edus:
            if orig_edu.get("diplome", "").strip().lower() == degree.strip().lower():
                year = orig_edu.get("annee")
                break
        quest_educations.append(QuestPDFEducation(
            degree=degree,
            institution=institution,
            year=str(year) if year else None,
        ))

    # 6. Certifications
    opt_certs = optimized_cv.get("certifications_optimisees", [])
    cert_list = [f"{c.get('nom')} - {c.get('organisme')}" for c in opt_certs if c.get("nom")]

    # 7. Languages — extracted from competences
    all_competences = original_profile.get("competences", [])
    lang_list: List[str] = []
    lang_names: set = set()
    non_lang_competences: List[dict] = []
    for comp in all_competences:
        ctype = (comp.get("type_competence") or "").lower().strip()
        if ctype in LANGUAGE_TYPES:
            lang_list.append(comp.get("nom", ""))
            lang_names.add(comp.get("nom", "").lower().strip())
        else:
            non_lang_competences.append(comp)

    # 8. Skills with matching and algorithmic sorting
    matched_set = {s.lower().strip() for s in (matched_skills or [])}
    opt_skills = optimized_cv.get("competences_reordonnees", [])
    
    seen: set = set()
    matched_skills_list: List[QuestPDFSkill] = []
    other_skills_list: List[QuestPDFSkill] = []
    
    for skill_name in opt_skills:
        key = skill_name.lower().strip()
        # Skip duplicates or if it's already considered a language
        if key in seen or key in lang_names:
            continue
        seen.add(key)
        
        is_matched = key in matched_set
        skill_obj = QuestPDFSkill(
            name=skill_name,
            level=3,
            is_matched=is_matched,
        )
        if is_matched:
            matched_skills_list.append(skill_obj)
        else:
            other_skills_list.append(skill_obj)
            
    # Algorithms: Matched skills first, then others
    quest_skills = matched_skills_list + other_skills_list

    # 9. Activities — extract from experiences (Extracurricular) or projets (association)
    activities: List[QuestPDFActivity] = []
    
    # Extract from experiences
    for exp in original_profile.get("experiences", []):
        if exp.get("type", "").lower() == "extracurricular":
            activities.append(QuestPDFActivity(
                title=exp.get("entreprise", ""),
                role=exp.get("titre", ""),
                description=exp.get("description", ""),
            ))
            
    # Extract from projets (legacy or alternative mapping)
    for proj in original_profile.get("projets", []):
        if proj.get("categorie") == "association" or proj.get("type") == "extracurricular":
            activities.append(QuestPDFActivity(
                title=proj.get("titre", ""),
                role=proj.get("role"),
                description=proj.get("description"),
            ))

    # 10. ATS Score
    if offer_skills:
        total = len(offer_skills)
        matched_count = sum(1 for s in offer_skills if s.lower().strip() in matched_set)
        ats_score = round((matched_count / total) * 100) if total > 0 else 0
    else:
        ats_score = 0

    payload = QuestPDFCvData(
        candidate=candidate,
        summary=summary,
        experience=quest_experiences,
        education=quest_educations,
        skills=quest_skills,
        projects=quest_projects,
        certifications=cert_list,
        languages=lang_list,
        activities=activities,
        ats_score=ats_score,
        matching_score=ats_score,
        ats_coverage_pct=float(ats_score),
    )

    logger.info("Fusion algorithmique CV Engine terminee avec succes.")
    return payload
