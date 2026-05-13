import logging
import json
import re
from langchain_core.messages import AIMessage
from langchain_core.prompts import ChatPromptTemplate
from app.core.config import get_llm
from app.domain.skill_gap.schemas.state import SkillGapState
from app.domain.skill_gap.schemas.models import SkillGapResult, GapFlag
from app.domain.skill_gap.agents.prompts import _SKILL_GAP_PROMPT
from app.domain.skill_gap.logic.scoring import calculate_mathematical_score, determine_flag

logger = logging.getLogger(__name__)

def parse_json_markdown(text: str) -> dict:
    """Extrait et décode le bloc JSON d'une chaîne brute retournée par le LLM."""
    if not text:
        return {}
    text_clean = re.sub(r'(?<!:)\/\/.*$', '', text, flags=re.MULTILINE)
    match = re.search(r'```(?:json)?\s*([\s\S]*?)\s*```', text_clean, re.IGNORECASE)
    if match:
        json_str = match.group(1).strip()
    else:
        start = text_clean.find("{")
        end = text_clean.rfind("}")
        if start != -1 and end != -1:
            json_str = text_clean[start:end+1].strip()
        else:
            json_str = text_clean.strip()
            
    try:
        return json.loads(json_str, strict=False)
    except Exception as e:
        logger.warning(f"⚠️ Échec du décodage JSON direct, tentative de nettoyage: {e}")
        json_str_repaired = re.sub(r',\s*([\]}])', r'\1', json_str)
        try:
            return json.loads(json_str_repaired, strict=False)
        except Exception as e2:
            logger.error(f"❌ Échec de la réparation JSON : {e2}")
            raise e2

def _compress_profile(profile: dict) -> dict:
    """Compresse un profil pour réduire la consommation de tokens LLM."""
    compressed = dict(profile)

    # Compétences : garder les 15 plus pertinentes (tri par niveau desc)
    skills = compressed.get("competences") or compressed.get("skills") or []
    if isinstance(skills, list) and len(skills) > 15:
        skills.sort(key=lambda s: s.get("niveau", 0) if isinstance(s, dict) else 0, reverse=True)
        compressed["competences"] = skills[:15]

    # Résumé : tronquer à 200 caractères
    for field in ("resume", "summary", "description"):
        val = compressed.get(field)
        if isinstance(val, str) and len(val) > 200:
            compressed[field] = val[:200] + "..."

    # Expériences : tronquer les descriptions
    exps = compressed.get("experiences") or compressed.get("experiences_professionnelles") or []
    for exp in exps:
        desc = exp.get("description")
        if isinstance(desc, str) and len(desc) > 150:
            exp["description"] = desc[:150] + "..."

    return compressed


async def skill_gap_node(state: SkillGapState) -> dict:
    """
    Nœud 1 : Analyse des écarts de compétences (Skill Gap).
    Prend en compte les erreurs des tentatives précédentes pour s'auto-corriger.
    """
    candidate_cv_raw = state.get("candidate_cv")
    job_offer = state.get("job_offer")
    current_count = state.get("iteration_count", 0)
    prev_errors = state.get("errors", [])
    
    if candidate_cv_raw is None or job_offer is None:
        logger.warning("⚠️ Données manquantes pour l'analyse Skill Gap.")
        return {
            "errors": ["CV ou Offre d'emploi manquants."],
            "iteration_count": current_count + 1
        }
    if not candidate_cv_raw:
        candidate_cv_raw = {"name": "Candidat", "skills": []}
    if not job_offer:
        job_offer = {"title": "Offre sans titre", "skills": []}
        
    # Compression du profil pour respecter les limites tokens des LLM gratuits
    candidate_cv = _compress_profile(candidate_cv_raw)
        
    logger.info(f"🎯 Skill Gap Agent — Tentative {current_count + 1} pour {job_offer.get('job_title')}")
    
    llm = get_llm(agent_name="skill_gap")
    if hasattr(llm, "bind"):
        llm = llm.bind(response_format={"type": "json_object"})
        
    # Injection du feedback si erreurs précédentes
    prompt_content = _SKILL_GAP_PROMPT
    if prev_errors and current_count > 0:
        feedback = "\n\n⚠️ IMPORTANT : Ta tentative précédente a échoué. Voici les erreurs à corriger impérativement :\n"
        feedback += "\n".join([f"- {err}" for err in prev_errors[-3:]]) # On prend les 3 dernières
        prompt_content += feedback

    skill_gap_prompt = ChatPromptTemplate.from_template(prompt_content)
    skill_gap_chain = skill_gap_prompt | llm
    
    try:
        response = await skill_gap_chain.ainvoke({
            "candidate_cv": json.dumps(candidate_cv, indent=2, ensure_ascii=False),
            "job_offer": json.dumps(job_offer, indent=2, ensure_ascii=False)
        })
        
        skill_gap_dict = parse_json_markdown(response.content if hasattr(response, "content") else str(response))
        
        # --- CALCUL MATHÉMATIQUE DU SCORE (DÉTERMINISTE) ---
        m_skills = len(skill_gap_dict.get("matched_skills", []))
        miss_skills = len(skill_gap_dict.get("missing_skills", []))
        exp_c = float(skill_gap_dict.get("experience_years", 0.0))
        exp_r = float(skill_gap_dict.get("required_years", 0.0))
        c_match = bool(skill_gap_dict.get("cert_match", False))
        
        # Calcul de la note finale par fonction Python (pas par l'agent)
        math_score = calculate_mathematical_score(m_skills, miss_skills, exp_c, exp_r, c_match)
        
        # Injection du score calculé
        skill_gap_dict["relevance_score"] = math_score
        
        # Recalcul de l'écart d'années et du flag
        gap_years = max(0.0, exp_r - exp_c)
        skill_gap_dict["experience_gap_years"] = round(gap_years, 1)
        skill_gap_dict["flag"] = determine_flag(math_score, gap_years)

        # Validation Pydantic
        skill_gap_result = SkillGapResult(**skill_gap_dict).model_dump()
    except Exception as e:
        logger.error(f"❌ Erreur lors de l'analyse Skill Gap: {e}")
        # Fallback par défaut via le modèle
        skill_gap_result = SkillGapResult(
            candidate_name=candidate_cv.get("name", "Candidat"),
            job_title=job_offer.get("job_title", "Poste"),
            relevance_score=0.0,
            flag=GapFlag.CRITICAL,
            recommendations=[
                {
                    "type": "cv_content",
                    "title": "Analyse échouée",
                    "description": "Une erreur technique a empêché l'analyse détaillée. Veuillez réessayer.",
                    "priority": "high"
                },
                {
                    "type": "cv_content",
                    "title": "Vérification manuelle",
                    "description": "Comparez manuellement vos compétences avec l'offre en attendant la résolution.",
                    "priority": "medium"
                },
                {
                    "type": "cv_content",
                    "title": "Contact support",
                    "description": "Si le problème persiste, contactez l'assistance technique.",
                    "priority": "low"
                }
            ],
            revision_hints=[
                "Une erreur technique est survenue lors de l'analyse.",
                "Veuillez réessayer plus tard ou vérifier les logs."
            ]
        ).model_dump()
        
    return {
        "skill_gap": skill_gap_result,
        "iteration_count": current_count + 1,
        "messages": [AIMessage(content=f"Analyse terminée (Tentative {current_count + 1})", name="skill_gap_analyzer")]
    }

def skill_validator_node(state: SkillGapState) -> dict:
    """
    Nœud 2 (Algorithmique) : Vérifie la qualité et la cohérence de l'analyse produite.
    """
    logger.info("🔧 Validation Algorithmique Skill Gap — START")
    data = state.get("skill_gap")
    current_errors = []

    if not data:
        return {"errors": ["Validator: Aucune donnée à valider."]}

    # 1. Vérification de la présence de compétences
    if not data.get("matched_skills") and not data.get("missing_skills"):
        current_errors.append("L'analyse doit identifier au moins une compétence (matchée ou manquante).")

    # 2. Vérification des scores suspects
    score = data.get("relevance_score", 0.0)
    if score > 0.95 and data.get("missing_skills"):
        # Trop beau pour être vrai s'il manque des skills
        if len(data.get("missing_skills", [])) > 3:
            current_errors.append("Le score est trop élevé ( > 0.95) alors que plus de 3 compétences clés manquent.")

    # 3. Nettoyage des listes (Suppression des placeholders type "N/A", "Skill")
    for field in ["matched_skills", "missing_skills", "revision_hints"]:
        original = data.get(field, [])
        cleaned = [s.strip() for s in original if s and s.lower() not in ["n/a", "string", "none", "skill"]]
        data[field] = cleaned

    if len(data.get("revision_hints", [])) < 2:
        current_errors.append("L'analyse doit fournir au moins 2 conseils de révision concrets.")

    return {
        "skill_gap": data,
        "errors": current_errors,
        "messages": [AIMessage(content=f"Validation terminée. {len(current_errors)} erreurs détectées.", name="validator")]
    }

def skill_analyzer_router(state: SkillGapState) -> str:
    """
    Routeur : Décide si on boucle (retry) ou si on termine (end).
    """
    count = state.get("iteration_count", 0)
    errors = state.get("errors", [])
    
    # On retente si erreurs ET count < 3
    if errors and count < 3:
        logger.warning(f"🔄 Retry Skill Gap (Tentative {count}/3). Raison: {errors[-1:]}")
        return "retry"
    
    if errors:
        logger.warning("⚠️ Max retries atteint pour Skill Gap. On garde le dernier résultat malgré les erreurs.")
    else:
        logger.info("✅ Analyse Skill Gap validée avec succès.")
        
    return "end"
