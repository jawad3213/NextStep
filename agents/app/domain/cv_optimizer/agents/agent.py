import json
import logging
import re
import unicodedata
from typing import Any

from langchain_core.messages import AIMessage
from langchain_core.prompts import ChatPromptTemplate
from langchain_core.utils.json import parse_json_markdown

from app.core.config import get_llm
from app.domain.cv_optimizer.agents.prompts import _CV_OPTIMIZER_PROMPT, output_language_name
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


def _squash(value: Any) -> str:
    """Accent/case/punctuation-insensitive key: 'Node.js' == 'NodeJS'."""
    return re.sub(r"[^a-z0-9#+]", "", _strip_accents(str(value or "")).lower())


# Placeholder metrics the model may leave in bullets: [X]%, [nombre], <x>, XX%, X%...
_PLACEHOLDER_RE = re.compile(r"\[[^\]]{0,20}\]|<[a-z]{1,3}>|\bX{1,3}\s?%|\bXX+\b", re.IGNORECASE)


def _real_bullets(bullets: list[str], fallback: Any) -> list[str]:
    """Drops bullets containing placeholders; falls back to the source tasks if none remain."""
    kept = [b for b in bullets if not _PLACEHOLDER_RE.search(b)]
    return kept or [b for b in _split_structured_bullets(fallback) if not _PLACEHOLDER_RE.search(b)]


def _take_source(items: list[dict], used: set[int], title: Any, company: Any = "") -> dict | None:
    """Finds the source entry an optimized entry comes from (same title, and same company if given)."""
    wanted_title, wanted_company = _normalized_title(title), _squash(company)
    if not wanted_title:
        return None
    for index, item in enumerate(items or []):
        if index in used or _normalized_title(item.get("titre", "")) != wanted_title:
            continue
        source_company = _squash(item.get("entreprise"))
        if wanted_company and source_company and wanted_company != source_company:
            continue
        used.add(index)
        return item
    return None


def _source_tasks(source: dict) -> Any:
    return source.get("taches") or source.get("tasks") or source.get("description") or source.get("missions")


def _normalize_optimized_output(output_dict: dict, candidate_cv: dict) -> dict:
    """
    Keeps the optimizer's wording but never its facts: experiences, projects, skills,
    education and certifications must exist in the candidate's profile. Invented entries
    are dropped, real entries the model dropped are restored, placeholders are removed.
    """
    if not isinstance(output_dict, dict):
        return output_dict

    normalized = dict(output_dict)
    normalized["resume_optimise"] = normalized.get("resume_optimise") or {}
    if not isinstance(normalized["resume_optimise"], dict):
        normalized["resume_optimise"] = {"contenu": str(normalized["resume_optimise"] or "").strip()}
    summary = str(normalized["resume_optimise"].get("contenu") or "").strip()
    if not summary or _PLACEHOLDER_RE.search(summary):
        summary = str(candidate_cv.get("resume") or "").strip()
    normalized["resume_optimise"]["contenu"] = summary

    # --- Experiences: only real ones (title + company from the profile) ---
    original_experiences = candidate_cv.get("experiences", []) or []
    used: set[int] = set()
    normalized_experiences = []
    for exp in normalized.get("experiences_optimisees") or []:
        if not isinstance(exp, dict):
            continue
        source = _take_source(original_experiences, used, exp.get("titre"), exp.get("entreprise"))
        if source is None:
            logger.warning("CV optimizer: dropped experience not in the profile: %r", exp.get("titre"))
            continue
        normalized_experiences.append({
            "titre": str(source.get("titre") or "").strip(),
            "entreprise": str(source.get("entreprise") or "").strip(),
            "description_optimisee": str(exp.get("description_optimisee") or source.get("description") or "").strip(),
            "taches_optimisees": _real_bullets(_split_structured_bullets(exp.get("taches_optimisees")), _source_tasks(source)),
            "mots_cles_cibles": _clean_list(exp.get("mots_cles_cibles")),
            "niveau_pertinence": str(exp.get("niveau_pertinence") or "medium").strip().lower() or "medium",
        })
    for index, source in enumerate(original_experiences):
        if index not in used:  # the model dropped or renamed it: restore the original
            normalized_experiences.append({
                "titre": str(source.get("titre") or "").strip(),
                "entreprise": str(source.get("entreprise") or "").strip(),
                "description_optimisee": str(source.get("description") or "").strip(),
                "taches_optimisees": _real_bullets([], _source_tasks(source)),
                "mots_cles_cibles": [],
                "niveau_pertinence": "low",
            })
    normalized["experiences_optimisees"] = normalized_experiences

    # --- Projects: only real ones; technologies limited to the project's own ---
    original_projects = candidate_cv.get("projets", []) or []
    used = set()
    normalized_projects = []

    def project_entry(proj: dict, source: dict) -> dict:
        source_technologies = _clean_list(source.get("technologies") or source.get("technologies_utilisees"))
        if not source_technologies and isinstance(source.get("technologies"), str):
            source_technologies = [t.strip() for t in re.split(r"[,;/|]", source["technologies"]) if t.strip()]
        technologies = proj.get("technologies")
        if isinstance(technologies, str):
            technologies = [part.strip() for part in re.split(r"[,;/|]", technologies) if part.strip()]
        allowed = {_squash(t) for t in source_technologies}
        clean_technologies = [t for t in _clean_list(technologies) if _squash(t) in allowed] or source_technologies
        return {
            "titre": str(source.get("titre") or "").strip(),
            "description_optimisee": str(proj.get("description_optimisee") or source.get("description") or "").strip(),
            "technologies": clean_technologies,
            "taches_optimisees": _real_bullets(_split_structured_bullets(proj.get("taches_optimisees")), _source_tasks(source)),
            "mots_cles_cibles": _clean_list(proj.get("mots_cles_cibles")),
            "niveau_pertinence": str(proj.get("niveau_pertinence") or "medium").strip().lower() or "medium",
        }

    for proj in normalized.get("projets_optimises") or []:
        if not isinstance(proj, dict):
            continue
        source = _take_source(original_projects, used, proj.get("titre"))
        if source is None:
            logger.warning("CV optimizer: dropped project not in the profile: %r", proj.get("titre"))
            continue
        normalized_projects.append(project_entry(proj, source))
    for index, source in enumerate(original_projects):
        if index not in used:
            normalized_projects.append({**project_entry({}, source), "niveau_pertinence": "low"})
    normalized["projets_optimises"] = normalized_projects

    # --- Education and certifications are facts: taken from the profile as-is ---
    normalized["formations_optimisees"] = [
        {"diplome": str(f.get("diplome") or "").strip(), "etablissement": str(f.get("etablissement") or "").strip()}
        for f in candidate_cv.get("formations", []) or [] if isinstance(f, dict)
    ]
    normalized["certifications_optimisees"] = [
        {"nom": str(c.get("nom") or "").strip(), "organisme": str(c.get("organisme") or "").strip()}
        for c in candidate_cv.get("certifications", []) or [] if isinstance(c, dict)
    ]

    # --- Skills: only skills the candidate actually has ---
    source_skills = [str(c.get("nom")).strip() for c in candidate_cv.get("competences", []) or []
                     if isinstance(c, dict) and c.get("nom")]
    by_key = {_squash(name): name for name in source_skills}

    def real_skills(values: Any) -> list[str]:
        result, seen = [], set()
        for value in _clean_list(values):
            key = _squash(value)
            if key in by_key and key not in seen:
                seen.add(key)
                result.append(by_key[key])
            elif key not in by_key:
                logger.warning("CV optimizer: dropped skill not in the profile: %r", value)
        return result

    reordered = real_skills(normalized.get("competences_reordonnees"))
    reordered += [name for key, name in by_key.items() if name not in reordered]  # keep every real skill
    normalized["competences_reordonnees"] = reordered
    normalized["competences_mises_en_avant"] = real_skills(normalized.get("competences_mises_en_avant"))
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


# What the model needs to rewrite the CV's wording. Name, email, phone, photo and social
# links are never sent to the LLM provider: it never writes them, so it doesn't need them.
_CV_CONTENT_FIELDS = ("resume", "experiences", "projets", "formations", "certifications", "competences")


def target_position(candidate_cv: dict) -> str:
    """The profile's "Target position / Current title": what the candidate is looking for."""
    return str((candidate_cv or {}).get("titre") or "").strip()


def _cv_content_for_llm(candidate_cv: dict) -> dict:
    content = {field: candidate_cv[field] for field in _CV_CONTENT_FIELDS if candidate_cv.get(field) is not None}
    if target_position(candidate_cv):
        content["poste_vise"] = target_position(candidate_cv)
    return content


# --- CV summary: it must present the candidate, never describe the job -------------------

SUMMARY_MAX_WORDS = 90
# Wording of a job description or of a text about someone else (accent-free, lower-case).
_JOB_DESCRIPTION_MARKERS = (
    "le stagiaire", "la stagiaire", "le candidat", "la candidate", "le titulaire du poste",
    "participera", "contribuera", "sera charge", "sera amene", "aura pour mission",
    "nous recherchons", "notre equipe", "rejoindre notre", "vous ",
    "the intern ", "the candidate", "the successful candidate", "you will", "we are looking",
    "our team", "join our", "the role will",
)


def _words(text: str) -> list[str]:
    return re.findall(r"[a-z0-9#+]+", _strip_accents(text).lower())


def _copied_share(summary: str, source: str) -> float:
    """Share of the summary's word trigrams that appear verbatim in the offer text."""
    s, o = _words(summary), _words(source)
    grams = {tuple(s[i:i + 3]) for i in range(len(s) - 2)}
    if not grams or len(o) < 3:
        return 0.0
    offer_grams = {tuple(o[i:i + 3]) for i in range(len(o) - 2)}
    return len(grams & offer_grams) / len(grams)


_TITLE_STOPWORDS = {"and", "the", "for", "with", "des", "les", "une", "pour", "avec", "stage", "internship"}


def _mentions_target(summary: str, target: str) -> bool:
    """The summary names the profile's target position (word order and accents ignored)."""
    wanted = [w for w in _words(target) if len(w) >= 2 and w not in _TITLE_STOPWORDS]
    if not wanted:
        return True
    present = set(_words(summary))
    return sum(w in present for w in wanted) >= max(1, round(len(wanted) * 0.6))


def summary_problem(summary: str, job_offer: dict | None, target: str = "") -> str | None:
    """Why a CV summary is not acceptable, or None. The summary sits at the top of the CV and
    must present the candidate: a summary of the offer (\"Le stagiaire participera a...\") is wrong,
    and when the profile has a target position, that is what the candidate is seeking."""
    text = str(summary or "").strip()
    if not text:
        return None
    folded = " " + " ".join(_words(text)) + " "
    marker = next((m for m in _JOB_DESCRIPTION_MARKERS if f" {' '.join(_words(m))} " in folded), None)
    if marker:
        return f"il decrit le poste au lieu de presenter le candidat (\"{marker.strip()}\")"
    offer_text = " ".join(str((job_offer or {}).get(k) or "") for k in ("description_poste", "descriptionPoste", "missions"))
    if _copied_share(text, offer_text) > 0.35:
        return "il recopie la description de l'offre"
    if len(_words(text)) > SUMMARY_MAX_WORDS:
        return f"il est trop long ({len(_words(text))} mots, maximum {SUMMARY_MAX_WORDS})"
    if target and not _mentions_target(text, target):
        return f"il ne reprend pas le poste vise du profil (\"{target}\")"
    return None


def _sought_position(job_offer: dict, language: str) -> str:
    """"un stage PFA" / "an internship (PFA)"... from the offer's contract type and wording."""
    offer = job_offer or {}
    contract = _strip_accents(str(offer.get("type_contrat") or offer.get("typeContrat") or "")).lower()
    wording = _strip_accents(" ".join(str(offer.get(k) or "") for k in ("titre", "description_poste", "descriptionPoste"))).lower()
    project = next((p for p in ("pfe", "pfa") if re.search(rf"\b{p}\b", wording)), None)
    french = language == "fr"
    if "altern" in contract or "apprenti" in contract:
        return "une alternance" if french else "an apprenticeship"
    if "stage" in contract or "intern" in contract or project or re.search(r"\b(stage|stagiaire|intern(ship)?)\b", wording):
        suffix = f" {project.upper()}" if project else ""
        return f"un stage{suffix}" if french else (f"a {project.upper()} internship" if project else "an internship")
    return "un poste" if french else "a position"


def fallback_summary(candidate_cv: dict, job_offer: dict | None, language: str = "en") -> str:
    """Summary used when the model's one is unusable: the candidate's own profile summary when
    it is fine, else a short factual one built from the profile and the offer."""
    target = target_position(candidate_cv)
    own = str(candidate_cv.get("resume") or "").strip()
    if own and not summary_problem(own, job_offer, target):
        return own

    french = (language or "en").lower() == "fr"
    formations = candidate_cv.get("formations") or []
    degree = next((str(f.get("diplome")).strip() for f in formations if isinstance(f, dict) and f.get("diplome")), "")
    identity = degree
    # What the candidate seeks: the profile's target position first, the offer's title otherwise.
    role = target or str((job_offer or {}).get("titre") or "").strip()
    skills = [str(c.get("nom")).strip() for c in (candidate_cv.get("competences") or []) if isinstance(c, dict) and c.get("nom")][:3]
    position = _sought_position(job_offer or {}, "fr" if french else "en")

    if french:
        first = f"{identity}, je recherche {position}" if identity else f"Je recherche {position}"
        first += f" en tant que {role}." if role else "."
        second = f" Je m'appuie sur mes compétences en {', '.join(skills)} pour contribuer rapidement aux projets de l'équipe." if skills else ""
    else:
        first = f"{identity}, looking for {position}" if identity else f"Looking for {position}"
        first += f" as {role}." if role else "."
        second = f" Skilled in {', '.join(skills)}, ready to contribute quickly to the team's projects." if skills else ""
    return first + second


async def cv_optimizer_node(state: CVOptimizerState) -> dict:
    candidate_cv = state.get("candidate_cv")
    job_offer = state.get("job_offer")
    current_count = state.get("iteration_count", 0)
    prev_errors = state.get("validation_errors") or []

    if not candidate_cv or not job_offer:
        logger.warning("Donnees manquantes pour l'optimisation de CV.")
        return {"errors": ["CV ou Offre d'emploi manquants."], "iteration_count": current_count + 1, "used_fallback": True}

    logger.info("CV Optimizer Agent - tentative %s", current_count + 1)

    llm = get_llm(temperature=0.0, agent_name="cv_optimizer")
    if hasattr(llm, "bind"):
        llm = llm.bind(response_format={"type": "json_object"})

    prompt_content = _CV_OPTIMIZER_PROMPT
    if prev_errors and current_count > 0:
        feedback = "\n\nIMPORTANT : La tentative precedente a echoue. Corrige ces erreurs :\n"
        feedback += "\n".join([f"- {err}" for err in prev_errors[:5]])
        prompt_content += feedback

    prompt = ChatPromptTemplate.from_template(prompt_content)
    chain = prompt | llm

    try:
        skill_gap_analysis = state.get("skill_gap_analysis") or state.get("match_result") or {}
        response = await chain.ainvoke(
            {
                "candidate_cv": json.dumps(_cv_content_for_llm(candidate_cv), indent=2, ensure_ascii=False),
                "job_offer": json.dumps(job_offer, indent=2, ensure_ascii=False),
                "skill_gap_analysis": json.dumps(skill_gap_analysis, indent=2, ensure_ascii=False),
                "output_language": output_language_name(state.get("language")),
            }
        )

        output_dict = parse_json_markdown(response.content if hasattr(response, "content") else str(response))
        # A summary describing the job instead of the candidate never reaches the CV: it is
        # replaced now, and the validator asks the model to rewrite it (one retry).
        problem = None
        if isinstance(output_dict, dict):
            raw_summary = output_dict.get("resume_optimise")
            raw_summary = raw_summary.get("contenu") if isinstance(raw_summary, dict) else raw_summary
            problem = summary_problem(str(raw_summary or ""), job_offer, target_position(candidate_cv))
            if problem:
                logger.warning("CV summary rejected: %s", problem)
                output_dict["resume_optimise"] = {"contenu": fallback_summary(candidate_cv, job_offer, state.get("language") or "en")}
        normalized_output = _normalize_optimized_output(output_dict, candidate_cv)
        optimized_result = OptimizedCVOutput(**normalized_output).model_dump()
    except Exception as e:
        logger.error("Erreur lors de l'optimisation du CV: %s", e)
        optimized_result = _build_fallback_result(candidate_cv)
        used_fallback = True
        problem = None
    else:
        used_fallback = False

    return {
        "optimized_cv": optimized_result,
        "summary_problem": problem,
        "used_fallback": used_fallback,
        "iteration_count": current_count + 1,
        "messages": [AIMessage(content=f"Optimisation terminee (Tentative {current_count + 1})", name="cv_optimizer")],
    }


# At most this many LLM calls per CV: a retry only happens for problems the model can fix.
MAX_ATTEMPTS = 2


def _entry_checks(kind: str, entry: dict, source: dict | None) -> tuple[list[str], list[str]]:
    """(blocking, advisory) problems of one optimized experience/project.

    The normalization already guarantees real entries, real technologies, no placeholder
    and at least the source tasks, so those are not re-checked here."""
    title = entry.get("titre", "")
    blocking, advisory = [], []
    bullets = _split_structured_bullets(entry.get("taches_optimisees"))
    source_bullets = {b.lstrip("- ").strip().lower() for b in _split_structured_bullets(_source_tasks(source or {}))}

    # A giant bullet written by the model (not copied from the profile) can be rewritten.
    if any(_looks_like_giant_paragraph(b) and b.lstrip("- ").strip().lower() not in source_bullets for b in bullets):
        blocking.append(f"{kind} '{title}' contient une puce trop longue pour un CV : fais des puces courtes.")

    if len(bullets) > 5:
        advisory.append(f"{kind} '{title}' contient plus de 5 puces.")
    if bullets and not any(_starts_with_action_verb(b) for b in bullets):
        advisory.append(f"{kind} '{title}' : puces sans verbe d'action.")
    if bullets and not any(_has_metric_impact_or_placeholder(b) for b in bullets):
        advisory.append(f"{kind} '{title}' : aucun résultat ou impact visible.")
    desc = str(entry.get("description_optimisee") or "").strip()
    if desc and ("\n" in desc or len(re.findall(r"[.!?]", desc)) > 3):
        advisory.append(f"{kind} '{title}' : description longue.")
    return blocking, advisory


def cv_validator_node(state: CVOptimizerState) -> dict:
    """Checks the (normalized) optimized CV. Only "blocking" problems, which the model can
    fix, trigger a new attempt; the others are logged."""
    logger.info("Validation algorithmique CV Optimizer - START")
    original_cv = state.get("candidate_cv", {})
    optimized_cv = state.get("optimized_cv", {})
    job_offer = state.get("job_offer", {})
    skill_gap_analysis = state.get("skill_gap_analysis") or state.get("match_result") or {}
    blocking: list[str] = []
    advisory: list[str] = []

    if not optimized_cv:
        return {"validation_errors": ["Aucune donnee optimisee."]}

    orig_exps = original_cv.get("experiences", [])
    opt_exps = optimized_cv.get("experiences_optimisees", [])
    orig_projs = original_cv.get("projets", [])
    opt_projs = optimized_cv.get("projets_optimises", [])

    for kind, entries, sources in (("L'experience", opt_exps, orig_exps), ("Le projet", opt_projs, orig_projs)):
        for entry in entries:
            source = next((s for s in sources if _normalized_title(s.get("titre", "")) == _normalized_title(entry.get("titre", ""))), None)
            b, a = _entry_checks(kind, entry, source)
            blocking += b
            advisory += a

    if state.get("summary_problem"):
        blocking.append(
            f"Le resume (resume_optimise.contenu) est refuse : {state['summary_problem']}. "
            "Reecris-le a la premiere personne sur le CANDIDAT, 45 a 70 mots : qui il est, "
            "le poste qu'il recherche (le \"poste_vise\" du profil tel quel, sinon d'apres l'offre), "
            "ses 2-3 atouts du profil."
        )

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
        blocking.append("Le CV optimise ne reprend aucun mot-cle significatif de l'offre.")

    highlighted_skills = optimized_cv.get("competences_mises_en_avant", []) or []
    ordered_skills = optimized_cv.get("competences_reordonnees", []) or []

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
            advisory.append("Aucune priorite de skill_gap_analysis n'apparait dans le CV optimise.")
        if highlighted_skills and not any(_contains_priority(highlighted_text, priority) for priority in gap_priorities[:10]):
            advisory.append("Les competences mises en avant ne recoupent pas les priorites de skill_gap_analysis.")

    if advisory:
        logger.info("CV Optimizer — remarques (sans nouvelle tentative) : %s", advisory[:5])
    return {
        # Replaced at each validation: the router looks at the latest attempt only.
        "validation_errors": blocking,
        "messages": [AIMessage(
            content=f"Validation terminee. {len(blocking)} probleme(s) bloquant(s), {len(advisory)} remarque(s).",
            name="validator",
        )],
    }


def cv_optimizer_router(state: CVOptimizerState) -> str:
    """New attempt only for problems of the latest attempt that the model can fix, at most
    MAX_ATTEMPTS calls, and never after an LLM failure (the fallback CV is final)."""
    count = state.get("iteration_count", 0)
    problems = state.get("validation_errors") or []

    if problems and count < MAX_ATTEMPTS and not state.get("used_fallback"):
        logger.warning("Retry CV Optimizer (tentative %s/%s). Raison: %s", count + 1, MAX_ATTEMPTS, problems[:2])
        return "retry"

    if problems:
        logger.warning("CV Optimizer : fin avec %s probleme(s) non corrige(s).", len(problems))
    else:
        logger.info("CV optimise valide avec succes.")
    return "end"
