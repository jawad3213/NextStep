# ============================================================
# app/domain/matching/skill_matcher.py
# Deterministic skill matching between an analysed offer and a candidate profile.
#
# Used by POST /offer/match (offer analysis) and by the offer pipeline (CV generation),
# so both always give the same score for the same offer and profile.
#
# An offer skill counts as present when the profile contains it as a whole word or
# phrase ("java" does not match "javascript", "sql" does not match "postgresql").
# Fuzzy matching only compares words of similar length (typos, plurals).
# ============================================================
import re
from difflib import SequenceMatcher

from app.core.utils.normalizer.synonyms import SYNONYMES
from app.core.utils.normalizer.text_utils import build_profile_full_text, normalize_skills, normalize_text

MATCH = 1.0
PARTIAL = 0.6
_FUZZY_MATCH = 0.9
_FUZZY_PARTIAL = 0.8
# Fuzzy comparison only between words of similar length ("java" vs "javascript" is 0.4).
_MIN_LENGTH_RATIO = 0.8
_MIN_FUZZY_LENGTH = 5
_NOISY_PHRASE_WORDS = 5

_ALIASES = {
    "chatbots": "chatbot",
    "retrieval augmented generation": "rag",
    "nodejs": "node.js",
    "expressjs": "express.js",
    "nextjs": "next.js",
    "postgres": "postgresql",
    "gh actions": "ci/cd",
    "github action": "ci/cd",
    "github actions": "ci/cd",
    "gitlab ci": "ci/cd",
    "ci cd": "ci/cd",
    "cicd": "ci/cd",
    "ai": "ia",
    "artificial intelligence": "ia",
    "intelligence artificielle": "ia",
    "integration ia": "ia",
    "integration ai": "ia",
}

# Concepts written as phrases in free text that should count as the canonical skill.
_TEXT_CONCEPTS = {
    "ia": ("ai", "artificial intelligence", "intelligence artificielle", "integration ia", "integration ai"),
    "rag": ("rag", "retrieval augmented generation"),
    "ci/cd": ("ci/cd", "ci cd", "cicd", "continuous integration", "continuous delivery", "github actions", "gitlab ci"),
    "chatbot": ("chatbot", "chatbots"),
}

# Having the left skill proves the right ones (GitHub implies Git, Django implies Python).
_IMPLIES = {
    "github": ("git",),
    "gitlab": ("git",),
    "postgresql": ("sql",),
    "mysql": ("sql",),
    "sqlite": ("sql",),
    "mariadb": ("sql",),
    "sql server": ("sql",),
    "oracle": ("sql",),
    "django": ("python",),
    "flask": ("python",),
    "fastapi": ("python",),
    "spring": ("java",),
    "react": ("javascript",),
    "angular": ("typescript", "javascript"),
    "vue": ("javascript",),
    "next.js": ("react", "javascript"),
    "node.js": ("javascript",),
    "express": ("node.js", "javascript"),
    "express.js": ("node.js", "javascript"),
    "asp.net": (".net", "c#"),
    "ef core": (".net",),
    "kubernetes": ("docker",),
}

_DISPLAY = {
    "ci/cd": "CI/CD",
    "ia": "IA",
    "rag": "RAG",
    "node.js": "Node.js",
    "express.js": "Express.js",
    "next.js": "Next.js",
    "postgresql": "PostgreSQL",
}

_STOPWORDS = {"de", "des", "du", "la", "le", "les", "et", "en", "of", "and", "the", "to", "a", "d", "l"}


def canon(value: str) -> str:
    """Lower-case, punctuation-free, alias-resolved form of a skill or word."""
    x = (value or "").strip().lower()
    x = re.sub(r"[^\w\s./+#-]", " ", x)
    x = re.sub(r"\s+", " ", x).strip()
    return _ALIASES.get(x, x)


def _normalize_list(values: list) -> list[str]:
    """normalize_skills() splits on "/": put "ci" + "cd" back together as "ci/cd"."""
    items = [canon(v) for v in normalize_skills([str(v) for v in values if v and str(v).strip()])]
    has_ci, has_cd = "ci" in items, "cd" in items
    items = [i for i in items if i and i not in {"ci", "cd"}]
    if has_ci and has_cd:
        items.append("ci/cd")
    return list(dict.fromkeys(items))


def canonical_skill(value: str) -> str:
    """One comparable form per skill: "React.js", "ReactJS" and "react" are all "react"."""
    items = _normalize_list([value])
    if len(items) == 1:
        return items[0]
    return canon(str(value or ""))


def _is_noisy_phrase(value: str) -> bool:
    c = canon(value)
    return not c or len(re.findall(r"\w+", c)) >= _NOISY_PHRASE_WORDS


def matching_targets(analyzed_offer: dict) -> list[str]:
    """Offer skills to look for: required + preferred skills, or the ATS keywords when the
    skill lists are mostly sentences rather than skills."""
    required = [str(s) for s in analyzed_offer.get("competences_requises") or [] if str(s).strip()]
    preferred = [str(s) for s in analyzed_offer.get("competences_souhaitees") or [] if str(s).strip()]
    keywords = [str(s) for s in analyzed_offer.get("keywords_ats") or [] if str(s).strip()]

    clean = [s for s in [*required, *preferred] if not _is_noisy_phrase(s)]
    normalized_keywords = _normalize_list(keywords)
    normalized_clean = _normalize_list(clean)

    # More than half of the listed skills are sentences: the ATS keywords are the better list.
    raw_count = len(required) + len(preferred)
    mostly_noisy = raw_count > 0 and len(clean) * 2 < raw_count
    if mostly_noisy and normalized_keywords:
        return normalized_keywords
    return normalized_clean or normalized_keywords


def _profile_text(profile: dict) -> str:
    """Every piece of text in the profile: summary, title, skills, experiences, projects..."""
    chunks: list[str] = [build_profile_full_text(profile)]
    chunks.append(str(profile.get("resume") or profile.get("resume_professionnel") or ""))

    personal = profile.get("personalInfo") or {}
    if isinstance(personal, dict):
        chunks += [str(personal.get("resumeProfessionnel") or ""), str(personal.get("titrePoste") or "")]

    for c in profile.get("competences") or []:
        chunks.append(str(c.get("nom") or c.get("name") or "") if isinstance(c, dict) else str(c))

    for e in profile.get("experiences") or []:
        if isinstance(e, dict):
            chunks += [str(e.get(k) or "") for k in ("titre", "poste", "description", "missions", "entreprise")]
            chunks += [str(t) for t in e.get("taches") or []]

    for p in profile.get("projets") or profile.get("projects") or []:
        if not isinstance(p, dict):
            continue
        chunks += [str(p.get("titre") or p.get("titreProjet") or ""), str(p.get("description") or "")]
        techs = p.get("technologies") or p.get("technologiesUtilisees") or []
        chunks += [t for t in re.split(r",", techs)] if isinstance(techs, str) else [str(t) for t in techs if t]
        chunks += [str(t) for t in p.get("taches") or []]

    return " ".join(c for c in chunks if c)


class ProfileEvidence:
    """What the profile says, in forms that can be compared with offer skills."""

    def __init__(self, profile: dict):
        profile = profile if isinstance(profile, dict) else {}
        skills: list[str] = []
        for c in profile.get("competences") or []:
            if isinstance(c, dict) and c.get("nom"):
                skills.append(c["nom"])
        for s in profile.get("skills") or []:
            skills.append(s if isinstance(s, str) else (s.get("nom") or s.get("name") or "") if isinstance(s, dict) else "")

        text = re.sub(r"\s+", " ", normalize_text(_profile_text(profile)))
        self.text = f" {text} "

        # Whole skills as the candidate wrote them ("machine learning", "node.js").
        self.phrases: set[str] = set(_normalize_list(skills))
        # Single words of the whole profile, plus the parts of hyphenated/dotted words.
        words: set[str] = set()
        for raw in re.split(r"[\s,;:(){}\[\]<>|]+", text):
            word = raw.strip(" .-_")
            if len(word) < 2:
                continue
            words.add(_word(word))
            for part in re.split(r"[-.]", word):
                if len(part) >= 2:
                    words.add(_word(part))
        for concept, forms in _TEXT_CONCEPTS.items():
            if any(f" {form} " in self.text for form in forms):
                words.add(concept)
        for known in list(self.phrases | words):
            for implied in _IMPLIES.get(known, ()):
                words.add(implied)
        self.words = words

    def score(self, target: str) -> float:
        """MATCH, PARTIAL or 0.0 for one canonical offer skill."""
        if not target:
            return 0.0
        if target in self.phrases or target in self.words or f" {target} " in self.text:
            return MATCH

        parts = [w for w in re.split(r"[\s/]+", target) if len(w) >= 2 and w not in _STOPWORDS]
        if len(parts) > 1:
            present = sum(1 for w in parts if canon(w) in self.words)
            if present == len(parts):
                return MATCH
            if present * 2 >= len(parts):
                return PARTIAL

        best = max((_fuzzy(target, candidate) for candidate in self.phrases | self.words), default=0.0)
        if best >= _FUZZY_MATCH:
            return MATCH
        if best >= _FUZZY_PARTIAL:
            return PARTIAL
        return 0.0


def _word(value: str) -> str:
    c = canon(value)
    return canon(SYNONYMES.get(c, c))


def _fuzzy(a: str, b: str) -> float:
    if min(len(a), len(b)) < _MIN_FUZZY_LENGTH:
        return 0.0
    if min(len(a), len(b)) / max(len(a), len(b)) < _MIN_LENGTH_RATIO:
        return 0.0
    return SequenceMatcher(None, a, b).ratio()


def compute_match(profile: dict, analyzed_offer: dict) -> dict:
    """Canonical matched/partial/missing skills and keywords, with the two scores (0–100)."""
    analyzed_offer = analyzed_offer or {}
    evidence = ProfileEvidence(profile or {})
    targets = matching_targets(analyzed_offer)
    keywords = _normalize_list(analyzed_offer.get("keywords_ats") or [])

    matched, partial, missing = [], [], []
    for target in targets:
        s = evidence.score(target)
        (matched if s == MATCH else partial if s == PARTIAL else missing).append(target)

    present = [k for k in keywords if evidence.score(k) == MATCH]
    absent = [k for k in keywords if k not in present]

    score_matching = round((len(matched) + 0.5 * len(partial)) / max(1, len(targets)) * 100)
    score_ats = round(len(present) / max(1, len(keywords)) * 100)
    return {
        "matched_skills": matched,
        "partial_skills": partial,
        "missing_skills": missing,
        "keywords_presents": present,
        "keywords_manquants": absent,
        "score_matching": max(0, min(100, score_matching)),
        "score_ats": max(0, min(100, score_ats)),
    }


def display_label(canonical: str, analyzed_offer: dict) -> str:
    """The skill as the offer wrote it ("Node.js" rather than "node.js")."""
    for label in [
        *(analyzed_offer.get("competences_requises") or []),
        *(analyzed_offer.get("competences_souhaitees") or []),
        *(analyzed_offer.get("keywords_ats") or []),
    ]:
        if canon(str(label)) == canonical:
            return str(label)
    return _DISPLAY.get(canonical, canonical)


def build_match_result(profile: dict, analyzed_offer: dict) -> dict:
    """The skill-gap result stored with the offer (labels as written in the offer)."""
    analyzed_offer = analyzed_offer or {}
    m = compute_match(profile or {}, analyzed_offer)

    def labels(values: list[str]) -> list[str]:
        return list(dict.fromkeys(display_label(v, analyzed_offer) for v in values))

    matched = labels(m["matched_skills"])
    partial = [s for s in labels(m["partial_skills"]) if s not in matched]
    missing = [s for s in labels(m["missing_skills"]) if s not in matched and s not in partial]

    return {
        "candidate_name": "Candidat",
        "job_title": analyzed_offer.get("titre") or analyzed_offer.get("job_title") or "Poste",
        "relevance_score": round(m["score_matching"] / 100, 2),
        "score_matching": m["score_matching"],
        "score_ats": m["score_ats"],
        "matched_skills": matched,
        "missing_skills": missing,
        "partial_skills": partial,
        "competences_matching": matched,
        "competences_manquantes": missing,
        "keywords_presents": labels(m["keywords_presents"]),
        "keywords_manquants": labels(m["keywords_manquants"]),
        "required_certs": [],
        "cert_match": False,
        "experience_years": 0.0,
        "required_years": 0.0,
        "experience_gap_years": 0.0,
        "flag": "minor_gap" if m["score_matching"] >= 60 else "critical_gap",
        "recommandations": [
            f"Ajouter une preuve concrete de '{skill}' dans une experience ou un projet."
            for skill in missing[:5]
        ],
        "revision_hints": [],
        "analysis_source": "deterministic_skill_gap_v3",
        "errors": [],
    }
