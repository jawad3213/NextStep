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
ACTIVITY_KEYWORDS = {
    "hackathon", "club", "association", "organisateur", "organizer", "membre",
    "volunteer", "bénévole", "benevole", "event", "community", "communaut",
    "it day", "prize", "prix", "participant", "formateur", "trainer",
    "formation", "solihackathon", "itwave", "ids"
}
ACTIVITY_TYPES = {
    "extracurricular",
    "extra curricular",
    "extra-scolaire",
    "extra scolaire",
    "extrascolaire",
    "parascolaire",
    "para scolaire",
}
RELEVANCE_SCORES = {"high": 3, "medium": 2, "low": 1}

def _norm(value: Any) -> str:
    return str(value or "").strip().lower()

def _clean_str(value: Any, default: str = "") -> str:
    if value is None:
        return default
    return str(value).strip()

def _clean_optional_str(value: Any) -> Optional[str]:
    clean = _clean_str(value)
    return clean or None

def _first_non_empty(*values: Any) -> str:
    for value in values:
        clean = _clean_str(value)
        if clean:
            return clean
    return ""

def _first_optional(*values: Any) -> Optional[str]:
    for value in values:
        clean = _clean_optional_str(value)
        if clean:
            return clean
    return None

def _profile_personal_info(profile: Dict[str, Any]) -> Dict[str, Any]:
    if not isinstance(profile, dict):
        return {}
    personal = (
        profile.get("personalInfo")
        or profile.get("personal_info")
        or profile.get("personal")
        or {}
    )
    return personal if isinstance(personal, dict) else {}

def _looks_like_activity(role: str, company: str, desc: str) -> bool:
    text = f"{_norm(role)} {_norm(company)} {_norm(desc)}"
    if "stage" in text or "intern" in text:
        return False
    return any(k in text for k in ACTIVITY_KEYWORDS)

def _is_activity_type(value: Any) -> bool:
    return _norm(value) in ACTIVITY_TYPES

def _dedupe_strings(values: List[str], limit: Optional[int] = None) -> List[str]:
    seen = set()
    result: List[str] = []
    for value in values or []:
        clean = " ".join(str(value or "").strip().split())
        key = _norm(clean)
        if not clean or key in seen:
            continue
        seen.add(key)
        result.append(clean)
        if limit and len(result) >= limit:
            break
    return result

def _same_meaning(a: str, b: str) -> bool:
    left = _norm(a)
    right = _norm(b)
    if not left or not right:
        return False
    return left == right or left in right or right in left

def _split_bullets(value: Any) -> List[str]:
    if isinstance(value, list):
        return [str(item).strip().lstrip("-").strip() for item in value if str(item).strip()]
    text = str(value or "").replace("\r", "\n").strip()
    if not text:
        return []
    bullets: List[str] = []
    for line in text.split("\n"):
        clean = line.strip()
        if not clean:
            continue
        bullets.append(clean.lstrip("-").strip())
    return [bullet for bullet in bullets if bullet]

def _relevance_score(value: Any) -> int:
    return RELEVANCE_SCORES.get(_norm(value), 0)

def build_questpdf_payload(
    original_profile: Dict[str, Any],
    optimized_cv: Dict[str, Any],
    matched_skills: Optional[List[str]] = None,
    offer_skills: Optional[List[str]] = None,
) -> QuestPDFCvData:
    logger.info("Debut de la fusion algorithmique CV Engine.")
    personal_info = _profile_personal_info(original_profile)

    # 1. Contact
    first_name = _first_non_empty(
        original_profile.get("prenom"),
        personal_info.get("prenom"),
        personal_info.get("firstName"),
        personal_info.get("first_name"),
    )
    last_name = _first_non_empty(
        original_profile.get("nom"),
        personal_info.get("nom"),
        personal_info.get("lastName"),
        personal_info.get("last_name"),
    )
    candidate = QuestPDFCandidate(
        name=_first_non_empty(
            f"{first_name} {last_name}".strip(),
            original_profile.get("nomComplet"),
            original_profile.get("fullName"),
            personal_info.get("nomComplet"),
            personal_info.get("fullName"),
            personal_info.get("name"),
        ),
        email=_first_non_empty(
            original_profile.get("email"),
            personal_info.get("email"),
            personal_info.get("mail"),
        ),
        phone=_first_optional(
            original_profile.get("telephone"),
            personal_info.get("telephone"),
            personal_info.get("phone"),
        ),
        location=_first_optional(
            original_profile.get("ville"),
            personal_info.get("ville"),
            personal_info.get("city"),
            personal_info.get("location"),
        ),
        photo_url=_first_optional(
            original_profile.get("photo_url"),
            original_profile.get("photoUrl"),
            personal_info.get("photo_url"),
            personal_info.get("photoUrl"),
            personal_info.get("profilePhoto"),
            personal_info.get("profile_photo"),
            personal_info.get("avatar"),
        ),
        linked_in=_first_optional(
            original_profile.get("linkedin"),
            personal_info.get("linkedin"),
            personal_info.get("linkedIn"),
            personal_info.get("lienLinkedin"),
        ),
        git_hub=_first_optional(
            original_profile.get("github"),
            personal_info.get("github"),
            personal_info.get("gitHub"),
            personal_info.get("lienGithub"),
        ),
        portfolio=_first_optional(
            original_profile.get("portfolio"),
            personal_info.get("portfolio"),
            personal_info.get("lienPortfolio"),
        ),
    )

    # 2. Summary
    resume_opt = optimized_cv.get("resume_optimise", {})
    summary = resume_opt.get(
        "contenu",
        _first_non_empty(
            original_profile.get("resume"),
            personal_info.get("resumeProfessionnel"),
            personal_info.get("summary"),
        ),
    )

    # 3. Experiences
    quest_experiences: List[QuestPDFExperience] = []
    original_exps = original_profile.get("experiences", [])
    opt_exps = optimized_cv.get("experiences_optimisees", [])

    seen_exp_keys: set = set()
    extracted_activities: List[QuestPDFActivity] = []

    scored_experiences = []
    for index, opt_exp in enumerate(opt_exps):
        opt_title = opt_exp.get("titre", "")
        opt_company = opt_exp.get("entreprise", "")
        opt_desc = opt_exp.get("description_optimisee", "")
        exp_key = f"{_norm(opt_title)}|{_norm(opt_company)}"
        if not exp_key or exp_key == "|":
            continue
        if exp_key in seen_exp_keys:
            continue

        start_date = None
        end_date = None
        orig_tasks = []
        optimized_tasks = _split_bullets(opt_exp.get("taches_optimisees"))
        orig_desc = ""
        orig_type = ""
        for orig_exp in original_exps:
            if orig_exp.get("titre", "").strip().lower() == opt_title.strip().lower():
                start_date = orig_exp.get("date_debut")
                end_date = orig_exp.get("date_fin")
                orig_tasks = orig_exp.get("taches") or orig_exp.get("tasks") or []
                orig_desc = str(orig_exp.get("description") or "")
                orig_type = str(orig_exp.get("type") or "")
                break

        bullets = optimized_tasks
        if not bullets:
            bullets = _split_bullets(orig_tasks)
        if not bullets:
            bullets = _split_bullets(opt_desc)
        if not bullets and opt_desc:
            bullets = [opt_desc]
        bullets = _dedupe_strings(bullets, limit=4)

        # Keep extracurricular / community entries out of professional experience
        if _is_activity_type(orig_type) or _looks_like_activity(opt_title, opt_company, f"{opt_desc} {orig_desc}"):
            extracted_activities.append(QuestPDFActivity(
                title=opt_title or opt_company,
                role=opt_company or None,
                description=(opt_desc or orig_desc or "").strip(),
                start=start_date,
                end=end_date,
            ))
            continue

        scored_experiences.append((_relevance_score(opt_exp.get("niveau_pertinence")), -index, QuestPDFExperience(
            role=opt_title,
            company=opt_company,
            start=start_date,
            end=end_date,
            bullets=bullets,
        )))
        seen_exp_keys.add(exp_key)

    scored_experiences.sort(key=lambda item: (item[0], item[1]), reverse=True)
    quest_experiences = [item[2] for item in scored_experiences]

    offer_techs = {s.lower() for s in (offer_skills or [])}
    quest_projects: List[QuestPDFProject] = []
    opt_projects = optimized_cv.get("projets_optimises", [])
    original_projs = original_profile.get("projets", [])
    
    scored_projects = []

    for index, opt_proj in enumerate(opt_projects):
        opt_title = opt_proj.get("titre", "")
        project_techs = {t.lower() for t in opt_proj.get("technologies", [])}
        score = _relevance_score(opt_proj.get("niveau_pertinence"))
        if offer_techs:
            matched_techs = project_techs & offer_techs
            score = (score * 10) + len(matched_techs)

        # Match with original project to get taches
        orig_tasks = []
        optimized_tasks = _split_bullets(opt_proj.get("taches_optimisees"))
        for orig_p in original_projs:
            if orig_p.get("titre", "").strip().lower() == opt_title.strip().lower():
                orig_tasks = orig_p.get("taches") or orig_p.get("tasks") or []
                break

        bullets = optimized_tasks
        if not bullets:
            bullets = _split_bullets(orig_tasks)
        if not bullets:
            bullets = _split_bullets(opt_proj.get("description_optimisee", ""))

        description = " ".join(str(opt_proj.get("description_optimisee", "") or "").split())
        bullets = [b for b in _dedupe_strings(bullets, limit=4) if not _same_meaning(b, description)]
        if bullets:
            description = None

        scored_projects.append((score, -index, QuestPDFProject(
            title=opt_title,
            description=description,
            bullets=bullets,
        )))
        
    # Sort projects by match score descending
    scored_projects.sort(key=lambda x: (x[0], x[1]), reverse=True)
    quest_projects = [p[2] for p in scored_projects[:4]]

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
    highlighted_set = {
        s.lower().strip()
        for s in (optimized_cv.get("competences_mises_en_avant", []) or [])
        if str(s or "").strip()
    }
    opt_skills = optimized_cv.get("competences_reordonnees", [])
    
    seen: set = set()
    highlighted_skills_list: List[QuestPDFSkill] = []
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
        if key in highlighted_set:
            highlighted_skills_list.append(skill_obj)
        elif is_matched:
            matched_skills_list.append(skill_obj)
        else:
            other_skills_list.append(skill_obj)
            
    # Algorithms: highlighted skills first, then matched, then others
    quest_skills = highlighted_skills_list + matched_skills_list + other_skills_list

    # 9. Activities — extract from experiences (Extracurricular) or projets (association)
    activities: List[QuestPDFActivity] = []
    seen_activity_keys: set = set()

    for activity in original_profile.get("activities", []):
        title = _clean_str(activity.get("title") or activity.get("titre"))
        role = _clean_optional_str(activity.get("role") or activity.get("entreprise"))
        description = _clean_optional_str(activity.get("description"))
        if not title and not description:
            continue
        akey = f"{_norm(title)}|{_norm(role)}"
        if akey in seen_activity_keys:
            continue
        activities.append(QuestPDFActivity(
            title=title or (role or ""),
            role=role,
            description=description,
            start=activity.get("start") or activity.get("startDate") or activity.get("date_debut"),
            end=activity.get("end") or activity.get("endDate") or activity.get("date_fin"),
        ))
        seen_activity_keys.add(akey)

    # Extract from experiences
    for exp in original_profile.get("experiences", []):
        role = exp.get("titre", "")
        company = exp.get("entreprise", "")
        desc = exp.get("description", "")
        if _is_activity_type(exp.get("type", "")) or _looks_like_activity(role, company, desc):
            akey = f"{_norm(role)}|{_norm(company)}"
            if akey in seen_activity_keys:
                continue
            activities.append(QuestPDFActivity(
                title=role or company,
                role=company or None,
                description=_first_optional(desc, " ".join(_split_bullets(exp.get("taches")))),
                start=exp.get("date_debut"),
                end=exp.get("date_fin"),
            ))
            seen_activity_keys.add(akey)
            
    # Extract from projets (legacy or alternative mapping)
    for proj in original_profile.get("projets", []):
        if proj.get("categorie") == "association" or proj.get("type") == "extracurricular":
            akey = f"{_norm(proj.get('role'))}|{_norm(proj.get('titre'))}"
            if akey in seen_activity_keys:
                continue
            activities.append(QuestPDFActivity(
                title=proj.get("titre", ""),
                role=proj.get("role"),
                description=proj.get("description"),
            ))
            seen_activity_keys.add(akey)

    # Merge activities extracted during optimized-exp pass + profile pass (deduplicated)
    for a in extracted_activities:
        akey = f"{_norm(a.role)}|{_norm(a.title)}"
        if akey in seen_activity_keys:
            continue
        activities.append(a)
        seen_activity_keys.add(akey)

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
