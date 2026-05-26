import json
import logging
import re
import unicodedata
from typing import Any

from langchain_core.messages import AIMessage
from langchain_core.prompts import ChatPromptTemplate
from langchain_core.utils.json import parse_json_markdown

from app.core.config import get_llm
from app.domain.cv_optimizer.agents.prompts import _CV_OPTIMIZER_PROMPT
from app.domain.cv_optimizer.schemas.models import OptimizedCVOutput
from app.domain.cv_optimizer.schemas.state import CVOptimizerState

logger = logging.getLogger(__name__)

ACTION_VERBS = {
    "built", "implemented", "automated", "reduced", "improved", "designed",
    "deployed", "developed", "led", "created", "optimized", "delivered",
    "engineered", "integrated", "migrated", "scaled", "launched", "tested",
    "concu", "concu", "developpe", "automatise", "ameliore", "reduit",
    "deployee", "deploie", "integre", "optimise", "livre", "cree",
    "mis", "pilote", "realise", "teste", "structure",
}
PLACEHOLDER_TOKENS = ("[x]", "[x]%", "[y]", "[z]", "<x>", "<y>", "<z>")
IMPACT_TERMS = {
    "impact", "result", "resultat", "outcome", "performance", "scale",
    "scalable", "quality", "qualite", "coverage", "couverture", "reliability",
    "fiabilite", "faster", "rapide", "reduced", "reduit", "improved",
    "ameliore", "optimized", "optimise", "automated", "automatise",
    "production", "delivery", "livraison", "ci/cd", "testing", "tests",
    "monitoring", "security", "securite", "load", "charge",
}


def _strip_accents(value: str) -> str:
    return "".join(
        char
        for char in unicodedata.normalize("NFKD", str(value or ""))
        if not unicodedata.combining(char)
    )


def _clean_list(values: Any) -> list[str]:
    if isinstance(values, list):
        return [str(item).strip() for item in values if str(item).strip()]
    return []


def _match_by_title(items: list[dict], title: Any) -> dict | None:
    normalized = _normalized_title(title)
    if not normalized:
        return None
    for item in items or []:
        if _normalized_title(item.get("titre", "")) == normalized:
            return item
    return None


def _normalize_optimized_output(output_dict: dict, candidate_cv: dict) -> dict:
    if not isinstance(output_dict, dict):
        return output_dict

    normalized = dict(output_dict)
    normalized["resume_optimise"] = normalized.get("resume_optimise") or {}
    if not isinstance(normalized["resume_optimise"], dict):
        normalized["resume_optimise"] = {"contenu": str(normalized["resume_optimise"] or "").strip()}
    normalized["resume_optimise"]["contenu"] = str(
        normalized["resume_optimise"].get("contenu") or candidate_cv.get("resume") or "Non fourni"
    ).strip() or "Non fourni"

    original_experiences = candidate_cv.get("experiences", []) or []
    normalized_experiences = []
    for exp in normalized.get("experiences_optimisees") or []:
        if not isinstance(exp, dict):
            continue
        source = _match_by_title(original_experiences, exp.get("titre"))
        normalized_experiences.append(
            {
                "titre": str(exp.get("titre") or (source or {}).get("titre") or "").strip(),
                "entreprise": str(
                    exp.get("entreprise")
                    or (source or {}).get("entreprise")
                    or ""
                ).strip(),
                "description_optimisee": str(
                    exp.get("description_optimisee")
                    or (source or {}).get("description")
                    or ""
                ).strip(),
                "taches_optimisees": _split_structured_bullets(
                    exp.get("taches_optimisees")
                    or (source or {}).get("taches")
                    or (source or {}).get("tasks")
                    or (source or {}).get("description")
                ),
                "mots_cles_cibles": _clean_list(exp.get("mots_cles_cibles")),
                "niveau_pertinence": str(exp.get("niveau_pertinence") or "medium").strip().lower() or "medium",
            }
        )
    normalized["experiences_optimisees"] = normalized_experiences

    original_projects = candidate_cv.get("projets", []) or []
    normalized_projects = []
    for proj in normalized.get("projets_optimises") or []:
        if not isinstance(proj, dict):
            continue
        source = _match_by_title(original_projects, proj.get("titre"))
        technologies = proj.get("technologies")
        if isinstance(technologies, str):
            technologies = [part.strip() for part in re.split(r"[,;/|]", technologies) if part.strip()]
        source_technologies = _clean_list(
            (source or {}).get("technologies")
            or (source or {}).get("technologies_utilisees")
        )
        source_tech_keys = {tech.lower() for tech in source_technologies}
        clean_technologies = _clean_list(technologies)
        if source_tech_keys:
            clean_technologies = [tech for tech in clean_technologies if tech.lower() in source_tech_keys]
            if not clean_technologies:
                clean_technologies = source_technologies
        normalized_projects.append(
            {
                "titre": str(proj.get("titre") or (source or {}).get("titre") or "").strip(),
                "description_optimisee": str(
                    proj.get("description_optimisee")
                    or (source or {}).get("description")
                    or ""
                ).strip(),
                "technologies": clean_technologies,
                "taches_optimisees": _split_structured_bullets(
                    proj.get("taches_optimisees")
                    or (source or {}).get("taches")
                    or (source or {}).get("tasks")
                    or (source or {}).get("description")
                ),
                "mots_cles_cibles": _clean_list(proj.get("mots_cles_cibles")),
                "niveau_pertinence": str(proj.get("niveau_pertinence") or "medium").strip().lower() or "medium",
            }
        )
    normalized["projets_optimises"] = normalized_projects

    normalized["formations_optimisees"] = [
        {
            "diplome": str(form.get("diplome") or "").strip(),
            "etablissement": str(form.get("etablissement") or "").strip(),
        }
        for form in (normalized.get("formations_optimisees") or [])
        if isinstance(form, dict)
    ]
    normalized["certifications_optimisees"] = [
        {
            "nom": str(cert.get("nom") or "").strip(),
            "organisme": str(cert.get("organisme") or "").strip(),
        }
        for cert in (normalized.get("certifications_optimisees") or [])
        if isinstance(cert, dict)
    ]
    normalized["competences_reordonnees"] = _clean_list(normalized.get("competences_reordonnees"))
    normalized["competences_mises_en_avant"] = _clean_list(normalized.get("competences_mises_en_avant"))
    return normalized


def _split_structured_bullets(value: Any) -> list[str]:
    if isinstance(value, list):
        return [str(item).strip() for item in value if str(item).strip()]
    text = str(value or "").replace("\r", "\n").strip()
    if not text:
        return []
    bullets: list[str] = []
    for line in text.split("\n"):
        clean = line.strip()
        if not clean:
            continue
        bullets.append(clean if clean.startswith("-") else f"- {clean.lstrip('-').strip()}")
    return bullets


def _normalized_title(value: Any) -> str:
    return str(value or "").strip().lower()


def _extract_job_keywords(job_offer: dict, skill_gap_analysis: dict) -> set[str]:
    keywords: set[str] = set()
    skill_sources = []
    if isinstance(job_offer, dict):
        skill_sources.extend(job_offer.get("skills") or [])
        skill_sources.extend(job_offer.get("keywords") or [])
        skill_sources.extend(job_offer.get("required_skills") or [])
        skill_sources.extend(job_offer.get("preferred_skills") or [])
    if isinstance(skill_gap_analysis, dict):
        skill_sources.extend(skill_gap_analysis.get("missing_skills") or [])
        skill_sources.extend(skill_gap_analysis.get("partial_skills") or [])
        skill_sources.extend(skill_gap_analysis.get("matched_skills") or [])
        skill_sources.extend(skill_gap_analysis.get("keywords_manquants") or [])
        skill_sources.extend(skill_gap_analysis.get("revision_hints") or [])

    for item in skill_sources:
        if isinstance(item, dict):
            for candidate in (item.get("name"), item.get("skill"), item.get("keyword"), item.get("hint")):
                if candidate and str(candidate).strip():
                    keywords.add(str(candidate).strip().lower())
        elif item and str(item).strip():
            text = str(item).strip().lower()
            keywords.add(text)
            for part in re.split(r"[,;/|]", text):
                part = part.strip()
                if len(part) >= 3:
                    keywords.add(part)
    return {keyword for keyword in keywords if keyword}


def _normalize_keyword(value: Any) -> str:
    normalized = _strip_accents(str(value or "")).lower()
    normalized = re.sub(r"[^\w\s./+#-]", " ", normalized)
    return re.sub(r"\s+", " ", normalized).strip()


def _collect_gap_priorities(skill_gap_analysis: dict) -> list[str]:
    if not isinstance(skill_gap_analysis, dict):
        return []

    priorities: list[str] = []
    for key in ("missing_skills", "partial_skills", "keywords_manquants", "revision_hints", "matched_skills"):
        for item in skill_gap_analysis.get(key) or []:
            if isinstance(item, dict):
                values = (item.get("name"), item.get("skill"), item.get("keyword"), item.get("hint"))
            else:
                values = (item,)
            for value in values:
                normalized = _normalize_keyword(value)
                if normalized and len(normalized) >= 2:
                    priorities.append(normalized)
    return list(dict.fromkeys(priorities))


def _contains_priority(text: str, priority: str) -> bool:
    if not text or not priority:
        return False
    normalized_text = f" {_normalize_keyword(text)} "
    normalized_priority = _normalize_keyword(priority)
    if not normalized_priority:
        return False
    if f" {normalized_priority} " in normalized_text:
        return True
    parts = [part for part in re.split(r"[\s,;/|]+", normalized_priority) if len(part) >= 3]
    return bool(parts and all(f" {part} " in normalized_text for part in parts[:3]))


def _extract_source_technologies(project: dict) -> set[str]:
    values = project.get("technologies") or project.get("technologies_utilisees") or []
    if isinstance(values, str):
        values = re.split(r"[,;/|]", values)
    return {str(item).strip().lower() for item in values if str(item).strip()}


def _has_meaningful_source_content(item: dict) -> bool:
    tasks = item.get("taches") or item.get("tasks") or []
    if _split_structured_bullets(tasks):
        return True
    return bool(str(item.get("description") or "").strip())


def _starts_with_action_verb(bullet: str) -> bool:
    normalized = _strip_accents(str(bullet or "")).lower()
    words = re.findall(r"[A-Za-z]+", normalized)
    return bool(words and words[0] in ACTION_VERBS)


def _has_metric_impact_or_placeholder(bullet: str) -> bool:
    text = str(bullet or "").lower()
    if any(token in text for token in PLACEHOLDER_TOKENS):
        return True
    if re.search(r"\d", text):
        return True
    normalized = _strip_accents(text)
    return any(term in normalized for term in IMPACT_TERMS)


def _looks_like_giant_paragraph(bullet: str) -> bool:
    text = " ".join(str(bullet or "").split())
    word_count = len(text.split())
    return word_count > 35 or len(text) > 260


def _placeholder_heavy(bullets: list[str]) -> bool:
    if not bullets:
        return False
    placeholder_count = sum(
        1 for bullet in bullets if any(token in str(bullet).lower() for token in PLACEHOLDER_TOKENS)
    )
    return placeholder_count == len(bullets) and len(bullets) >= 2


def _project_tech_subset_valid(opt_project: dict, source_project: dict) -> bool:
    source_techs = _extract_source_technologies(source_project)
    if not source_techs:
        return True
    optimized_techs = {str(item).strip().lower() for item in opt_project.get("technologies") or [] if str(item).strip()}
    return optimized_techs.issubset(source_techs)


def _collect_candidate_skills(candidate_cv: dict) -> list[str]:
    skills = []
    for skill in candidate_cv.get("competences", []) or []:
        if isinstance(skill, dict):
            name = str(skill.get("nom") or "").strip()
        else:
            name = str(skill or "").strip()
        if name:
            skills.append(name)
    return skills


def _build_fallback_result(candidate_cv: dict) -> dict:
    experiences = []
    for exp in candidate_cv.get("experiences", []):
        experiences.append(
            {
                "titre": exp.get("titre", ""),
                "entreprise": exp.get("entreprise", ""),
                "description_optimisee": str(exp.get("description") or "").strip(),
                "taches_optimisees": _split_structured_bullets(
                    exp.get("taches") or exp.get("tasks") or exp.get("description", "")
                ),
                "mots_cles_cibles": [],
                "niveau_pertinence": "medium",
            }
        )

    projets = []
    for proj in candidate_cv.get("projets", []):
        technologies = proj.get("technologies") or proj.get("technologies_utilisees") or []
        if isinstance(technologies, str):
            technologies = [part.strip() for part in re.split(r"[,;/|]", technologies) if part.strip()]
        projets.append(
            {
                "titre": proj.get("titre", ""),
                "description_optimisee": str(proj.get("description") or "").strip(),
                "technologies": technologies,
                "taches_optimisees": _split_structured_bullets(
                    proj.get("taches") or proj.get("tasks") or proj.get("description", "")
                ),
                "mots_cles_cibles": [],
                "niveau_pertinence": "medium",
            }
        )

    formations = []
    for form in candidate_cv.get("formations", []):
        formations.append(
            {
                "diplome": form.get("diplome", ""),
                "etablissement": form.get("etablissement", ""),
            }
        )

    certifications = []
    for cert in candidate_cv.get("certifications", []):
        certifications.append(
            {
                "nom": cert.get("nom", ""),
                "organisme": cert.get("organisme", ""),
            }
        )

    ordered_skills = _collect_candidate_skills(candidate_cv)
    highlighted_skills = ordered_skills[: min(8, len(ordered_skills))]

    return OptimizedCVOutput(
        resume_optimise={"contenu": str(candidate_cv.get("resume") or "Non fourni").strip() or "Non fourni"},
        experiences_optimisees=experiences,
        projets_optimises=projets,
        formations_optimisees=formations,
        certifications_optimisees=certifications,
        competences_reordonnees=ordered_skills,
        competences_mises_en_avant=highlighted_skills,
    ).model_dump()


async def cv_optimizer_node(state: CVOptimizerState) -> dict:
    candidate_cv = state.get("candidate_cv")
    job_offer = state.get("job_offer")
    current_count = state.get("iteration_count", 0)
    prev_errors = state.get("errors", [])

    if not candidate_cv or not job_offer:
        logger.warning("Donnees manquantes pour l'optimisation de CV.")
        return {"errors": ["CV ou Offre d'emploi manquants."], "iteration_count": current_count + 1}

    logger.info("CV Optimizer Agent - tentative %s", current_count + 1)

    llm = get_llm(temperature=0.0, agent_name="cv_optimizer")
    if hasattr(llm, "bind"):
        llm = llm.bind(response_format={"type": "json_object"})

    prompt_content = _CV_OPTIMIZER_PROMPT
    if prev_errors and current_count > 0:
        feedback = "\n\nIMPORTANT : La tentative precedente a echoue. Corrige ces erreurs :\n"
        feedback += "\n".join([f"- {err}" for err in prev_errors[-3:]])
        prompt_content += feedback

    prompt = ChatPromptTemplate.from_template(prompt_content)
    chain = prompt | llm

    try:
        skill_gap_analysis = state.get("skill_gap_analysis") or state.get("match_result") or {}
        response = await chain.ainvoke(
            {
                "candidate_cv": json.dumps(candidate_cv, indent=2, ensure_ascii=False),
                "job_offer": json.dumps(job_offer, indent=2, ensure_ascii=False),
                "skill_gap_analysis": json.dumps(skill_gap_analysis, indent=2, ensure_ascii=False),
            }
        )

        output_dict = parse_json_markdown(response.content if hasattr(response, "content") else str(response))
        normalized_output = _normalize_optimized_output(output_dict, candidate_cv)
        optimized_result = OptimizedCVOutput(**normalized_output).model_dump()
    except Exception as e:
        logger.error("Erreur lors de l'optimisation du CV: %s", e)
        optimized_result = _build_fallback_result(candidate_cv)

    return {
        "optimized_cv": optimized_result,
        "iteration_count": current_count + 1,
        "messages": [AIMessage(content=f"Optimisation terminee (Tentative {current_count + 1})", name="cv_optimizer")],
    }


def cv_validator_node(state: CVOptimizerState) -> dict:
    logger.info("Validation algorithmique CV Optimizer - START")
    original_cv = state.get("candidate_cv", {})
    optimized_cv = state.get("optimized_cv", {})
    job_offer = state.get("job_offer", {})
    skill_gap_analysis = state.get("skill_gap_analysis") or state.get("match_result") or {}
    current_errors = []

    if not optimized_cv:
        return {"errors": ["Validator: Aucune donnee optimisee."]}

    orig_exps = original_cv.get("experiences", [])
    opt_exps = optimized_cv.get("experiences_optimisees", [])
    orig_exp_titles = [_normalized_title(e.get("titre", "")) for e in orig_exps]
    opt_exp_titles = [_normalized_title(e.get("titre", "")) for e in opt_exps]

    if len(opt_exps) < len(orig_exps):
        current_errors.append("Tu as supprime des experiences. Tu dois toutes les garder.")
    for opt_title in opt_exp_titles:
        if opt_title not in orig_exp_titles:
            current_errors.append(
                f"L'experience '{opt_title}' n'existe pas dans le profil original. Interdiction d'inventer ou de changer le titre original."
            )

    orig_projs = original_cv.get("projets", [])
    opt_projs = optimized_cv.get("projets_optimises", [])
    orig_proj_titles = [_normalized_title(p.get("titre", "")) for p in orig_projs]
    opt_proj_titles = [_normalized_title(p.get("titre", "")) for p in opt_projs]

    if len(opt_projs) < len(orig_projs):
        current_errors.append("Tu as supprime des projets. Tu dois tous les garder.")
    for opt_title in opt_proj_titles:
        if opt_title not in orig_proj_titles:
            current_errors.append(
                f"Le projet '{opt_title}' n'existe pas dans le profil original. Interdiction d'inventer ou de changer le titre original."
            )

    orig_forms = original_cv.get("formations", [])
    opt_forms = optimized_cv.get("formations_optimisees", [])
    if len(opt_forms) < len(orig_forms):
        current_errors.append("Tu as supprime des formations. Tu dois toutes les garder.")

    orig_certs = original_cv.get("certifications", [])
    opt_certs = optimized_cv.get("certifications_optimisees", [])
    if len(opt_certs) < len(orig_certs):
        current_errors.append("Tu as supprime des certifications. Tu dois toutes les garder.")

    for opt_exp in opt_exps:
        matching_source = next(
            (exp for exp in orig_exps if _normalized_title(exp.get("titre", "")) == _normalized_title(opt_exp.get("titre", ""))),
            None,
        )
        bullets = _split_structured_bullets(opt_exp.get("taches_optimisees"))
        if matching_source and _has_meaningful_source_content(matching_source) and not bullets:
            current_errors.append(f"L'experience '{opt_exp.get('titre', '')}' doit contenir des taches optimisees.")
        if len(bullets) > 5:
            current_errors.append(f"L'experience '{opt_exp.get('titre', '')}' contient trop de puces.")
        for bullet in bullets:
            if _looks_like_giant_paragraph(bullet):
                current_errors.append(f"L'experience '{opt_exp.get('titre', '')}' contient une puce trop longue pour un CV.")
                break
        if bullets and not any(_starts_with_action_verb(bullet) for bullet in bullets):
            current_errors.append(f"L'experience '{opt_exp.get('titre', '')}' doit commencer ses puces par des verbes d'action.")
        if bullets and not any(_has_metric_impact_or_placeholder(bullet) for bullet in bullets):
            current_errors.append(f"L'experience '{opt_exp.get('titre', '')}' devrait montrer au moins un resultat, impact ou chiffre.")
        if _placeholder_heavy(bullets):
            current_errors.append(f"L'experience '{opt_exp.get('titre', '')}' abuse des placeholders de metriques.")
        desc = str(opt_exp.get("description_optimisee") or "").strip()
        if desc and ("\n" in desc or len(re.findall(r"[.!?]", desc)) > 3):
            current_errors.append(f"L'experience '{opt_exp.get('titre', '')}' doit garder une description courte et naturelle.")

    for opt_proj in opt_projs:
        matching_source = next(
            (proj for proj in orig_projs if _normalized_title(proj.get("titre", "")) == _normalized_title(opt_proj.get("titre", ""))),
            None,
        )
        bullets = _split_structured_bullets(opt_proj.get("taches_optimisees"))
        if matching_source and _has_meaningful_source_content(matching_source) and not bullets:
            current_errors.append(f"Le projet '{opt_proj.get('titre', '')}' doit contenir des taches optimisees.")
        if len(bullets) > 5:
            current_errors.append(f"Le projet '{opt_proj.get('titre', '')}' contient trop de puces.")
        for bullet in bullets:
            if _looks_like_giant_paragraph(bullet):
                current_errors.append(f"Le projet '{opt_proj.get('titre', '')}' contient une puce trop longue pour un CV.")
                break
        if bullets and not any(_starts_with_action_verb(bullet) for bullet in bullets):
            current_errors.append(f"Le projet '{opt_proj.get('titre', '')}' doit commencer ses puces par des verbes d'action.")
        if bullets and not any(_has_metric_impact_or_placeholder(bullet) for bullet in bullets):
            current_errors.append(f"Le projet '{opt_proj.get('titre', '')}' devrait montrer au moins un resultat, impact ou chiffre.")
        if _placeholder_heavy(bullets):
            current_errors.append(f"Le projet '{opt_proj.get('titre', '')}' abuse des placeholders de metriques.")
        if matching_source and not _project_tech_subset_valid(opt_proj, matching_source):
            current_errors.append(f"Le projet '{opt_proj.get('titre', '')}' contient des technologies absentes de la source.")
        desc = str(opt_proj.get("description_optimisee") or "").strip()
        if desc and ("\n" in desc or len(re.findall(r"[.!?]", desc)) > 3):
            current_errors.append(f"Le projet '{opt_proj.get('titre', '')}' doit garder une description courte et naturelle.")

    job_keywords = _extract_job_keywords(job_offer, skill_gap_analysis)
    combined_text_parts = []
    combined_text_parts.append(str((optimized_cv.get("resume_optimise") or {}).get("contenu") or ""))
    combined_text_parts.extend(optimized_cv.get("competences_reordonnees") or [])
    for exp in opt_exps:
        combined_text_parts.append(str(exp.get("description_optimisee") or ""))
        combined_text_parts.extend(_split_structured_bullets(exp.get("taches_optimisees")))
        combined_text_parts.extend(_clean_list(exp.get("mots_cles_cibles")))
    for proj in opt_projs:
        combined_text_parts.append(str(proj.get("description_optimisee") or ""))
        combined_text_parts.extend(_split_structured_bullets(proj.get("taches_optimisees")))
        combined_text_parts.extend(_clean_list(proj.get("technologies")))
        combined_text_parts.extend(_clean_list(proj.get("mots_cles_cibles")))

    combined_text = " ".join(part.lower() for part in combined_text_parts if str(part).strip())
    matched_keywords = [keyword for keyword in job_keywords if keyword and keyword in combined_text]
    if job_keywords and not matched_keywords:
        current_errors.append("Le CV optimise ne reprend aucun mot-cle significatif de l'offre.")

    highlighted_skills = optimized_cv.get("competences_mises_en_avant", []) or []
    ordered_skills = optimized_cv.get("competences_reordonnees", []) or []
    if highlighted_skills:
        ordered_lower = {str(skill).strip().lower() for skill in ordered_skills}
        for skill in highlighted_skills:
            if str(skill).strip().lower() not in ordered_lower:
                current_errors.append("Les competences mises en avant doivent provenir de competences_reordonnees.")
                break

    gap_priorities = _collect_gap_priorities(skill_gap_analysis)
    if gap_priorities:
        highlighted_text = " ".join(str(skill) for skill in highlighted_skills)
        ordered_text = " ".join(str(skill) for skill in ordered_skills[: max(8, len(highlighted_skills))])
        targeted_text = " ".join(
            [
                *(str(keyword) for exp in opt_exps for keyword in _clean_list(exp.get("mots_cles_cibles"))),
                *(str(keyword) for proj in opt_projs for keyword in _clean_list(proj.get("mots_cles_cibles"))),
            ]
        )
        optimized_priority_text = " ".join([combined_text, highlighted_text, ordered_text, targeted_text])
        if not any(_contains_priority(optimized_priority_text, priority) for priority in gap_priorities[:10]):
            current_errors.append(
                "Le CV optimise ignore skill_gap_analysis: aucune priorite d'ecart n'apparait dans les competences ou contenus cibles."
            )

        if highlighted_skills and not any(_contains_priority(highlighted_text, priority) for priority in gap_priorities[:10]):
            current_errors.append(
                "Les competences mises en avant doivent chevaucher les priorites de skill_gap_analysis quand elles existent dans la source."
            )

    return {
        "errors": current_errors,
        "messages": [AIMessage(content=f"Validation terminee. {len(current_errors)} erreurs.", name="validator")],
    }


def cv_optimizer_router(state: CVOptimizerState) -> str:
    count = state.get("iteration_count", 0)
    errors = state.get("errors", [])

    if errors and count < 3:
        logger.warning("Retry CV Optimizer (Tentative %s/3). Raison: %s", count, errors[-1:])
        return "retry"

    if errors:
        logger.warning("Max retries atteint. Fin avec erreurs.")
    else:
        logger.info("CV optimise valide avec succes.")

    return "end"
