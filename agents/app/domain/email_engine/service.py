# ============================================================
# email_engine/service.py — Real LLM email generation
# Uses LangChain ChatPromptTemplate + with_structured_output()
# ============================================================
import logging
from langchain_core.prompts import ChatPromptTemplate
from .llm import get_email_llm
from .models import (
    GenerateEmailRequest,
    GenerateEmailResponse,
    GenerateFollowUpEmailRequest,
    ClassifyResponseRequest,
    ClassifyResponseResult,
    GenerateReplyEmailRequest,
)

logger = logging.getLogger(__name__)

# ─── Application email prompts ───────────────────────────────────────────────

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

# ─── Follow-up / Relance prompts ─────────────────────────────────────────────

_FOLLOWUP_SYSTEM = """\
Tu es un agent expert en rédaction d'emails de relance professionnels.

Règles strictes :
- Génère un email de relance complet, prêt à envoyer, faisant suite à une candidature sans réponse.
- Sois poli, concis et professionnel. Ne sois PAS impatient, agressif, désespéré ou insistant.
- Mentionne poliment que tu fais suite à ta candidature précédente.
- Mentionne le poste et l'entreprise si disponibles.
- N'invente aucune compétence, diplôme, entreprise, certification ou expérience.
- Utilise UNIQUEMENT les données du candidat et de l'offre fournies.
- Respecte la langue demandée (fr = français, en = English, etc.).
- Respecte le ton demandé.
- N'utilise PAS de markdown (pas de **, *, #, listes à puces, etc.).
- Ne laisse PAS de placeholders comme [Nom], [Entreprise], [Poste], etc.
- Ne mentionne PAS que l'email a été généré par une IA.
- L'email doit inclure : formule d'appel polie, rappel bref de la candidature précédente, \
réaffirmation de l'intérêt pour le poste, disponibilité pour un entretien ou complément \
d'information, formule de politesse et signature.
"""

_FOLLOWUP_HUMAN = """\
Génère un email de relance pour une candidature n'ayant reçu aucune réponse.

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

=== EMAIL PRÉCÉDENT (sans réponse) ===
Objet             : {previous_subject}
Corps             : {previous_body}
Envoyé le (UTC)   : {sent_at_utc}
Jours écoulés     : {days_since_sent}

=== OPTIONS ===
Langue            : {language}
Ton               : {tone}
"""


# ─── Helpers ─────────────────────────────────────────────────────────────────

def _fmt(lst: list) -> str:
    """Format a list as a comma-separated string, or 'Non spécifié' if empty."""
    filtered = [str(x).strip() for x in lst if str(x).strip()]
    return ", ".join(filtered) if filtered else "Non spécifié"


def _truncate(text: str | None, max_chars: int = 2000) -> str:
    if not text:
        return "Non spécifié"
    return text[:max_chars] + ("…" if len(text) > max_chars else "")


# ─── Application email generation ────────────────────────────────────────────

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


# ─── Follow-up / Relance email generation ────────────────────────────────────

async def generate_follow_up_email_with_llm(
    request: GenerateFollowUpEmailRequest,
) -> GenerateEmailResponse:
    """
    Generate a professional follow-up (relance) email for a candidature
    that received no response to the previous email.

    The generated email is polite, concise, and ready to send.
    It does NOT auto-approve or auto-send — it is saved as a draft.
    """
    logger.info(
        "Follow-up agent — generating for candidature_id=%s | lang=%s | tone=%s | days=%s",
        request.candidature_id,
        request.options.language,
        request.options.tone,
        request.options.days_since_sent,
    )

    llm = get_email_llm()
    structured_llm = llm.with_structured_output(GenerateEmailResponse)

    prompt = ChatPromptTemplate.from_messages(
        [("system", _FOLLOWUP_SYSTEM), ("human", _FOLLOWUP_HUMAN)]
    )

    chain = prompt | structured_llm

    c = request.candidate
    j = request.job_offer
    p = request.previous_email
    o = request.options

    days_label = str(o.days_since_sent) if o.days_since_sent is not None else "Non spécifié"

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
            # Previous email context
            "previous_subject": p.subject or "Non spécifié",
            "previous_body": _truncate(p.body, 1500),
            "sent_at_utc": p.sent_at_utc or "Non spécifié",
            "days_since_sent": days_label,
            # Options
            "language": o.language,
            "tone": o.tone,
        }
    )

    logger.info(
        "Follow-up agent — generation succeeded for candidature_id=%s | subject=%s",
        request.candidature_id,
        result.subject[:60] if result.subject else "",
    )
    return result


# ─── Response classification ────────────────────────────────────────────────────────────

_CLASSIFY_SYSTEM = """\
Tu es un agent expert en analyse de réponses de recruteurs à des candidatures professionnelles.

Règles strictes de classification :
- Classe la réponse dans une SEULE catégorie parmi :
  * ENTRETIEN_PROPOSE     : le recruteur propose un entretien, appel téléphonique ou réunion.
  * INFORMATIONS_DEMANDEES: le recruteur demande des documents, disponibilités, portfolio, CV,
                            prétentions salariales ou autres informations complémentaires.
  * ACCEPTE               : le recruteur confirme EXPLICITEMENT la sélection du candidat
                            (offre d’emploi formelle, proposition contractuelle).
  * REFUSE                : le recruteur signale EXPLICITEMENT un refus ou que la candidature
                            n’est pas retenue.
  * REPONSE_AUTOMATIQUE   : réponse automatique (absence du bureau, accusé de réception
                            automatique, notification de livraison, confirmation générique).
  * REPONSE_GENERALE      : vraie réponse humaine qui ne correspond à aucune catégorie précise.
  * INCONNU               : impossible de déterminer la catégorie avec certitude.

Règles de prudence :
- Utilise REFUSE UNIQUEMENT pour un refus explicite et sans ambigüité.
- Utilise ACCEPTE UNIQUEMENT pour une acceptation explicite ou une offre d’emploi formelle.
- Utilise ENTRETIEN_PROPOSE UNIQUEMENT si une invitation à un entretien, appel ou réunion
  est clairement formulée.
- En cas de doute, préfère REPONSE_GENERALE ou INCONNU.
- N’invente aucun fait non présent dans l’extrait.
- Ne mentionne PAS que l’analyse est faite par une IA.
- Retourne UNIQUEMENT la structure demandée, sans explication supplémentaire.
- Les champs summary et recommended_action doivent être rédigés dans la langue demandée.
- Le champ confidence est un décimal entre 0 et 1 représentant ta certitude.
"""

_CLASSIFY_HUMAN = """\
Analyse la réponse suivante d’un recruteur à une candidature :

=== CONTEXTE ===
Poste visé  : {job_title}
Entreprise  : {company_name}
Objet email précédent : {previous_email_subject}

=== RÉPONSE REÇUE ===
De      : {reply_from}
Date    : {reply_date_utc}
Objet   : {reply_subject}
Extrait : {reply_snippet}

=== OPTIONS ===
Langue de sortie : {language}
"""


async def classify_recruiter_response_with_llm(
    request: ClassifyResponseRequest,
) -> ClassifyResponseResult:
    """
    Classify a recruiter reply using the LLM.

    Conservative classification rules (see system prompt).
    Returns a structured ClassifyResponseResult; never auto-sends or generates drafts.
    """
    logger.info(
        "Classify agent — candidature_id=%s | lang=%s",
        request.candidature_id,
        request.language,
    )

    llm = get_email_llm()
    structured_llm = llm.with_structured_output(ClassifyResponseResult)

    prompt = ChatPromptTemplate.from_messages(
        [("system", _CLASSIFY_SYSTEM), ("human", _CLASSIFY_HUMAN)]
    )

    chain = prompt | structured_llm

    result: ClassifyResponseResult = await chain.ainvoke(
        {
            "job_title":              request.job_title              or "Non spécifié",
            "company_name":           request.company_name           or "Non spécifié",
            "previous_email_subject": request.previous_email_subject or "Non spécifié",
            "reply_from":             request.reply_from             or "Non spécifié",
            "reply_date_utc":         request.reply_date_utc         or "Non spécifié",
            "reply_subject":          request.reply_subject          or "Non spécifié",
            "reply_snippet":          _truncate(request.reply_snippet, 800),
            "language":               request.language,
        }
    )

    logger.info(
        "Classify agent — result: type=%s, confidence=%s for candidature_id=%s",
        result.response_type,
        result.confidence,
        request.candidature_id,
    )
    return result


# ─── Reply draft generation ───────────────────────────────────────────────────────────

_REPLY_SYSTEM = """\
Tu es un agent expert en rédaction d'emails professionnels répondant à des recruteurs.

Règles strictes :
- Génère une réponse professionnelle, complète, prête à envoyer.
- Adapte le contenu au type de réponse du recruteur :
    ENTRETIEN_PROPOSE     : Remercie le recruteur et accepte poliment. Si l’utilisateur a fourni
                           des disponibilités, mentionne-les. Sinon, dis que tu restes disponible
                           pour convenir d’un créneau.
    INFORMATIONS_DEMANDEES: Remercie et indique que tu peux fournir les informations demandées.
                           N’invente aucun document, lien, pièce jointe, portfolio ou référence.
    ACCEPTE               : Exprime ta gratitude, confirme ton intérêt et demande les prochaines étapes.
    REFUSE                : Rédige un court message de remerciement poli et professionnel.
    REPONSE_AUTOMATIQUE   : Rédige un bref accusé de réception si pertinent, sinon reste minimal.
    REPONSE_GENERALE      : Rédige un accusé de réception professionnel et prudent.
    INCONNU               : Rédige un accusé de réception professionnel et prudent.
- Si l’utilisateur fournit des instructions supplémentaires (disponibilités, ton spécifique,
  informations à mentionner, etc.), respecte-les si elles sont compatibles avec le contexte
  connu et les règles de sécurité. Ignore les parties en contradiction avec les faits connus.
- N’invente PAS de compétences, expériences, diplômes, certifications, liens, pièces jointes,
  salaires, entreprises ou disponibilités que l’utilisateur n’a pas mentionnés.
- Ne prétends PAS qu’une pièce jointe est incluse sauf si le backend le confirme explicitement.
- Utilise UNIQUEMENT les informations du candidat, de l’offre et de la réponse du recruteur.
- Respecte la langue demandée (fr = français, en = English, etc.).
- Respecte le ton demandé (professionnel, décontracté, formel, etc.).
- N’utilise PAS de markdown (étoiles, dièses, listes à puces, etc.).
- Ne laisse PAS de placeholders comme [Nom], [Date], [Entreprise], [Disponibilité], etc.
- Ne mentionne PAS que l’email a été généré par une IA.
- Sois concis et professionnel. Évite les longueurs excessives.
- L’email doit être prêt à envoyer mais reste modifiable par l’utilisateur.
"""

_REPLY_HUMAN = """\
Génère une réponse professionnelle à la réponse du recruteur.

=== CANDIDAT ===
Nom complet    : {full_name}
Titre actuel   : {current_title}
Email          : {email}

=== OFFRE D’EMPLOI ===
Poste          : {job_title}
Entreprise     : {company_name}

=== EMAIL PRÉCÉDENT ENVOYÉ ===
Objet          : {previous_subject}
Date d’envoi   : {previous_sent_at}

=== RÉPONSE DU RECRUTEUR ===
De             : {reply_from}
Objet          : {reply_subject}
Date           : {reply_received_at}
Extrait        : {reply_snippet}

=== ANALYSE DE LA RÉPONSE ===
Type de réponse       : {response_type}
Résumé               : {response_summary}
Action recommandée   : {recommended_action}

=== INSTRUCTIONS DE L’UTILISATEUR ===
{user_instructions}

=== OPTIONS ===
Langue : {language}
Ton    : {tone}
"""


async def generate_reply_email_with_llm(
    request: GenerateReplyEmailRequest,
) -> GenerateEmailResponse:
    """
    Generate a professional reply draft to the recruiter's response.

    Never auto-sends. Never auto-approves.
    Respects user_instructions only when compatible with known facts.
    """
    logger.info(
        "Reply agent — candidature_id=%s | response_type=%s | lang=%s",
        request.candidature_id,
        request.response_type,
        request.language,
    )

    llm = get_email_llm()
    structured_llm = llm.with_structured_output(GenerateEmailResponse)

    prompt = ChatPromptTemplate.from_messages(
        [("system", _REPLY_SYSTEM), ("human", _REPLY_HUMAN)]
    )

    chain = prompt | structured_llm

    result: GenerateEmailResponse = await chain.ainvoke(
        {
            # Candidate
            "full_name":       request.candidate.full_name,
            "current_title":   request.candidate.current_title or "Non spécifié",
            "email":           request.candidate.email         or "Non spécifié",
            # Job offer
            "job_title":       request.job_offer.job_title,
            "company_name":    request.job_offer.company_name  or "Non spécifié",
            # Previous sent email
            "previous_subject":  request.previous_email.subject   if request.previous_email else "Non spécifié",
            "previous_sent_at": request.previous_email.sent_at_utc if request.previous_email else "Non spécifié",
            # Recruiter reply
            "reply_from":        request.recruiter_reply.from_email    or "Non spécifié",
            "reply_subject":     request.recruiter_reply.subject        or "Non spécifié",
            "reply_received_at": request.recruiter_reply.received_at_utc or "Non spécifié",
            "reply_snippet":     _truncate(request.recruiter_reply.snippet, 800),
            # Classification context
            "response_type":      request.response_type,
            "response_summary":   request.response_summary   or "Non spécifié",
            "recommended_action": request.recommended_action or "Non spécifié",
            # Options
            "user_instructions": request.user_instructions or "Aucune instruction supplémentaire.",
            "language":          request.language,
            "tone":              request.tone,
        }
    )

    logger.info(
        "Reply agent — generation succeeded for candidature_id=%s | subject=%s",
        request.candidature_id,
        result.subject[:60] if result.subject else "",
    )
    return result