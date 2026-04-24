# ============================================================
# app/agents/normalizer.py
# Agent 3 — Normalisateur (Algorithme pur, pas de LLM)
#
# Nettoie accents, synonymes, mise en minuscules.
# Prépare les données pour la comparaison equitable (Agent 4).
# ============================================================
import re
import logging
from unidecode import unidecode
from langchain_core.messages import AIMessage
from app.schemas.state import AgentState

logger = logging.getLogger(__name__)

# ─── Dictionnaire de synonymes techniques ───
SYNONYMES: dict[str, str] = {
    # JavaScript
    "js": "javascript", "reactjs": "react", "react.js": "react",
    "vuejs": "vue", "vue.js": "vue", "angularjs": "angular",
    "nodejs": "node.js", "node": "node.js",
    "typescript": "typescript", "ts": "typescript",
    # Python
    "py": "python", "drf": "django", "django rest framework": "django",
    # .NET
    "c sharp": "c#", "csharp": "c#", "dotnet": ".net",
    "asp.net core": "asp.net", "aspnet": "asp.net",
    "entity framework": "ef core", "ef": "ef core",
    # Bases de données
    "postgres": "postgresql", "mongo": "mongodb",
    # Cloud / DevOps
    "amazon web services": "aws", "google cloud platform": "gcp",
    "microsoft azure": "azure", "k8s": "kubernetes",
    "cicd": "ci/cd",
    # ML / IA
    "ml": "machine learning", "dl": "deep learning",
}


def _normalize_token(token: str) -> str:
    """Normalise un token : unidecode + lower + synonymes."""
    token = unidecode(token.strip().lower())
    token = re.sub(r"[^\w\s./#\-+]", "", token)
    return SYNONYMES.get(token, token)


def normalize_skills(skills: list[str]) -> list[str]:
    """Normalise et déduplique une liste de compétences."""
    seen: set[str] = set()
    result = []
    for s in skills:
        n = _normalize_token(s)
        if n and n not in seen:
            seen.add(n)
            result.append(n)
    return result


def normalize_text(text: str) -> str:
    """Normalise un texte libre."""
    text = unidecode(text.lower())
    return re.sub(r"[^\w\s./#\-+]", " ", text).strip()


async def normalizer_node(state: AgentState) -> dict:
    """
    Nœud LangGraph — Agent 3 : Normalisation des compétences et textes.

    Entrées depuis state :
        - analyzed_offer : dict de l'offre analysée
        - profile_data : dict du profil candidat

    Mise à jour de state :
        - normalized_offer_skills : list[str] (accumulatif via operator.add)
        - normalized_profile_skills : list[str] (accumulatif)
        - normalized_keywords : list[str] (accumulatif)
        - profile_full_text : str (texte complet normalisé du profil)
        - messages : message AI
    """
    logger.info("🔧 Agent 3 [Normalizer] — Démarrage")

    offer = state.get("analyzed_offer") or {}
    profile = state.get("profile_data") or {}

    # ─── Normalisation des compétences offre ───
    offer_skills = normalize_skills(offer.get("competences_requises", []))
    offer_optional = normalize_skills(offer.get("competences_souhaitees", []))
    keywords = normalize_skills(offer.get("keywords_ats", []))

    # ─── Normalisation du profil ───
    profile_skills = normalize_skills([
        c["nom"] for c in profile.get("competences", [])
        if isinstance(c, dict) and c.get("nom")
    ])

    profile_titre = normalize_text(profile.get("titre") or "")
    profile_resume = normalize_text(profile.get("resume") or "")
    exp_text = normalize_text(" ".join(
        f"{e.get('titre', '')} {e.get('description', '')}"
        for e in profile.get("experiences", [])
        if isinstance(e, dict)
    ))
    proj_text = normalize_text(" ".join(
        f"{p.get('titre', '')} {p.get('description', '')} {' '.join(p.get('technologies') or [])}"
        for p in profile.get("projets", [])
        if isinstance(p, dict)
    ))
    profile_full_text = " ".join([
        profile_titre, profile_resume,
        " ".join(profile_skills), exp_text, proj_text,
    ])

    logger.info(
        "Agent 3 ✅ — %d comp. offre | %d comp. profil | %d keywords ATS normalisés",
        len(offer_skills), len(profile_skills), len(keywords),
    )

    summary = (
        f"[Agent 3] Normalisation terminée : {len(offer_skills)} compétences offre, "
        f"{len(profile_skills)} compétences profil, {len(keywords)} keywords ATS"
    )
    return {
        "normalized_offer_skills": offer_skills,
        "normalized_profile_skills": profile_skills,
        "normalized_keywords": keywords,
        "profile_full_text": profile_full_text,
        "messages": [AIMessage(content=summary, name="normalizer")],
    }
