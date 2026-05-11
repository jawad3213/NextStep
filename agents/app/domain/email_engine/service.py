# ============================================================
# email_engine/service.py — Real LLM email generation
# Uses LangChain ChatPromptTemplate + with_structured_output()
# ============================================================
import logging
from langchain_core.prompts import ChatPromptTemplate
from .llm import get_email_llm
from .models import GenerateEmailRequest, GenerateEmailResponse

logger = logging.getLogger(__name__)

# ─── Prompts ────────────────────────────────────────────────

_SYSTEM = """\
Tu es un agent expert en rédaction d'emails de candidature professionnels.

Règles strictes :
- Génère un email de candidature complet, prêt à envoyer.
- N'invente aucune compétence, diplôme, entreprise, certification ou expérience.
- Utilise UNIQUEMENT les données du candidat et de l'offre fournies.
- Si le nom de l'entreprise est absent, utilise "votre entreprise".
- Si les données du candidat sont limitées, rédige un email concis et honnête.
- Respecte la langue demandée (fr = français, en = English, etc.).
- Respecte le ton demandé (professionnel, décontracté, formel, etc.).
- N'utilise PAS de markdown (pas de **, *, #, listes à puces, etc.).
- Ne laisse PAS de placeholders comme [Nom], [Entreprise], [Poste], etc.
- Ne mentionne PAS que l'email a été généré par une IA.
- N'exagère PAS le profil du candidat.
- Le corps doit inclure : formule d'appel, paragraphe de présentation, \
paragraphe de motivation, paragraphe sur les compétences clés, formule de politesse et signature.
"""

_HUMAN = """\
Génère un email de candidature avec les informations suivantes :

=== CANDIDAT ===
Nom complet       : {full_name}
Email             : {email}
Téléphone         : {phone}
Titre actuel      : {current_title}
Compétences       : {skills}
Expériences       : {experiences}
Formation         : {education}
Projets           : {projects}
Certifications    : {certifications}

=== OFFRE D'EMPLOI ===
Poste             : {job_title}
Entreprise        : {company_name}
Localisation      : {location}
Compétences req.  : {required_skills}
Compétences souh. : {preferred_skills}
Missions          : {missions}
Prérequis         : {requirements}
Texte brut offre  : {raw_text}

=== OPTIONS ===
Langue                       : {language}
Ton                          : {tone}
Inclure lettre de motivation : {include_motivation_letter}
"""


# ─── Helpers ────────────────────────────────────────────────

def _fmt(lst: list) -> str:
    """Format a list as a comma-separated string, or 'Non spécifié' if empty."""
    filtered = [str(x).strip() for x in lst if str(x).strip()]
    return ", ".join(filtered) if filtered else "Non spécifié"


def _truncate(text: str | None, max_chars: int = 2000) -> str:
    if not text:
        return "Non spécifié"
    return text[:max_chars] + ("…" if len(text) > max_chars else "")


# ─── Main function ───────────────────────────────────────────

async def generate_email_with_llm(
    request: GenerateEmailRequest,
) -> GenerateEmailResponse:
    """
    Generate a job-application email using a real LLM.

    Uses LangChain structured output to enforce the GenerateEmailResponse schema.
    Can be called as a standalone endpoint OR wired into the LangGraph AgentState
    as Agent 6 (M4_email_composer) in a future sprint.
    """
    logger.info(
        "Email agent — generating for candidature_id=%s | lang=%s | tone=%s",
        request.candidature_id,
        request.options.language,
        request.options.tone,
    )

    llm = get_email_llm()
    structured_llm = llm.with_structured_output(GenerateEmailResponse)

    prompt = ChatPromptTemplate.from_messages(
        [("system", _SYSTEM), ("human", _HUMAN)]
    )

    chain = prompt | structured_llm

    c = request.candidate
    j = request.job_offer
    o = request.options

    result: GenerateEmailResponse = await chain.ainvoke(
        {
            # Candidate
            "full_name": c.full_name or "Non spécifié",
            "email": c.email or "Non spécifié",
            "phone": c.phone or "Non spécifié",
            "current_title": c.current_title or "Non spécifié",
            "skills": _fmt(c.skills),
            "experiences": _fmt(c.experiences),
            "education": _fmt(c.education),
            "projects": _fmt(c.projects),
            "certifications": _fmt(c.certifications),
            # Offer
            "job_title": j.job_title,
            "company_name": j.company_name or "votre entreprise",
            "location": j.location or "Non spécifié",
            "required_skills": _fmt(j.required_skills),
            "preferred_skills": _fmt(j.preferred_skills),
            "missions": _fmt(j.missions),
            "requirements": _fmt(j.requirements),
            "raw_text": _truncate(j.raw_text),
            # Options
            "language": o.language,
            "tone": o.tone,
            "include_motivation_letter": "Oui" if o.include_motivation_letter else "Non",
        }
    )

    logger.info(
        "Email agent — generation succeeded for candidature_id=%s | subject=%s",
        request.candidature_id,
        result.subject[:60] if result.subject else "",
    )
    return result