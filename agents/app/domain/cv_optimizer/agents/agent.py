import json
import logging
from langchain_core.messages import AIMessage
from langchain_core.prompts import ChatPromptTemplate
from app.core.config import get_llm
from langchain_core.utils.json import parse_json_markdown
from app.domain.cv_optimizer.schemas.state import CVOptimizerState
from app.domain.cv_optimizer.schemas.models import OptimizedCVOutput
from app.domain.cv_optimizer.agents.prompts import _CV_OPTIMIZER_PROMPT

logger = logging.getLogger(__name__)

async def cv_optimizer_node(state: CVOptimizerState) -> dict:
    candidate_cv = state.get("candidate_cv")
    job_offer = state.get("job_offer")
    current_count = state.get("iteration_count", 0)
    prev_errors = state.get("errors", [])
    
    if not candidate_cv or not job_offer:
        logger.warning("⚠️ Données manquantes pour l'optimisation de CV.")
        return {"errors": ["CV ou Offre d'emploi manquants."], "iteration_count": current_count + 1}
        
    logger.info(f"🎯 CV Optimizer Agent — Tentative {current_count + 1}")
    
    # 3. 🌡️ Température très basse pour éviter les hallucinations créatives
    llm = get_llm(temperature=0.0, agent_name="cv_optimizer")
    if hasattr(llm, "bind"):
        llm = llm.bind(response_format={"type": "json_object"})
        
    prompt_content = _CV_OPTIMIZER_PROMPT
    if prev_errors and current_count > 0:
        feedback = "\n\n⚠️ IMPORTANT : Ta tentative précédente a échoué. Corrige ces erreurs :\n"
        feedback += "\n".join([f"- {err}" for err in prev_errors[-3:]])
        prompt_content += feedback

    prompt = ChatPromptTemplate.from_template(prompt_content)
    chain = prompt | llm
    
    try:
        response = await chain.ainvoke({
            "candidate_cv": json.dumps(candidate_cv, indent=2, ensure_ascii=False),
            "job_offer": json.dumps(job_offer, indent=2, ensure_ascii=False),
            "match_result": json.dumps(state.get("match_result") or {}, indent=2, ensure_ascii=False)
        })
        
        output_dict = parse_json_markdown(response.content if hasattr(response, "content") else str(response))
        # Validation Pydantic
        optimized_result = OptimizedCVOutput(**output_dict).model_dump()
    except Exception as e:
        logger.error(f"❌ Erreur lors de l'optimisation du CV: {e}")
        # 1. 🚨 Fallback intelligent (Graceful Degradation)
        # On renvoie le CV original mappé dans la structure OptimizedCVOutput
        experiences = []
        for exp in candidate_cv.get("experiences", []):
            experiences.append({
                "titre": exp.get("titre", ""),
                "entreprise": exp.get("entreprise", ""),
                "description_optimisee": exp.get("description", ""),
                "justification_reorder": "Original conservé suite à une erreur technique.",
                "justification_rewrite": "Original conservé suite à une erreur technique."
            })
            
        projets = []
        for proj in candidate_cv.get("projets", []):
            projets.append({
                "titre": proj.get("titre", ""),
                "description_optimisee": proj.get("description", ""),
                "technologies": proj.get("technologies", []),
                "justification_reorder": "Original conservé suite à une erreur technique.",
                "justification_rewrite": "Original conservé suite à une erreur technique."
            })
            
        formations = []
        for form in candidate_cv.get("formations", []):
            formations.append({
                "diplome": form.get("diplome", ""),
                "etablissement": form.get("etablissement", ""),
                "justification_reorder": "Original conservé.",
                "justification_rewrite": "Original conservé."
            })
            
        certifications = []
        for cert in candidate_cv.get("certifications", []):
            certifications.append({
                "nom": cert.get("nom", ""),
                "organisme": cert.get("organisme", ""),
                "justification_reorder": "Original conservé.",
                "justification_rewrite": "Original conservé."
            })

        optimized_result = OptimizedCVOutput(
            resume_optimise={
                "contenu": candidate_cv.get("resume", "Non fourni"),
                "justification_rewrite": "Original conservé suite à une erreur technique."
            },
            experiences_optimisees=experiences,
            projets_optimises=projets,
            formations_optimisees=formations,
            certifications_optimisees=certifications,
            competences_reordonnees=[s.get("nom", "") for s in candidate_cv.get("competences", [])],
            justification_competences="Ordre original conservé.",
            global_justification="Une erreur technique est survenue, le CV original a été retourné pour éviter toute perte de données."
        ).model_dump()
        
    return {
        "optimized_cv": optimized_result,
        "iteration_count": current_count + 1,
        "messages": [AIMessage(content=f"Optimisation terminée (Tentative {current_count + 1})", name="cv_optimizer")]
    }

def cv_validator_node(state: CVOptimizerState) -> dict:
    logger.info("🔧 Validation Algorithmique CV Optimizer — START")
    original_cv = state.get("candidate_cv", {})
    optimized_cv = state.get("optimized_cv", {})
    current_errors = []

    if not optimized_cv:
        return {"errors": ["Validator: Aucune donnée optimisée."]}

    # 2. 🛡️ Validation Anti-Hallucination stricte (par Titre)
    orig_exps = original_cv.get("experiences", [])
    opt_exps = optimized_cv.get("experiences_optimisees", [])
    
    orig_exp_titles = [e.get("titre", "").lower().strip() for e in orig_exps]
    opt_exp_titles = [e.get("titre", "").lower().strip() for e in opt_exps]
    
    if len(opt_exps) < len(orig_exps):
        current_errors.append(f"Tu as supprimé des expériences. Tu dois toutes les garder.")
    for opt_title in opt_exp_titles:
        if opt_title not in orig_exp_titles:
            current_errors.append(f"L'expérience '{opt_title}' n'existe pas dans le profil original. Interdiction d'inventer ou de changer le titre original.")

    orig_projs = original_cv.get("projets", [])
    opt_projs = optimized_cv.get("projets_optimises", [])
    
    orig_proj_titles = [p.get("titre", "").lower().strip() for p in orig_projs]
    opt_proj_titles = [p.get("titre", "").lower().strip() for p in opt_projs]
    
    if len(opt_projs) < len(orig_projs):
        current_errors.append("Tu as supprimé des projets. Tu dois tous les garder.")
    for opt_title in opt_proj_titles:
        if opt_title not in orig_proj_titles:
            current_errors.append(f"Le projet '{opt_title}' n'existe pas dans le profil original. Interdiction d'inventer ou de changer le titre original.")

    orig_forms = original_cv.get("formations", [])
    opt_forms = optimized_cv.get("formations_optimisees", [])
    if len(opt_forms) < len(orig_forms):
        current_errors.append("Tu as supprimé des formations. Tu dois toutes les garder.")

    orig_certs = original_cv.get("certifications", [])
    opt_certs = optimized_cv.get("certifications_optimisees", [])
    if len(opt_certs) < len(orig_certs):
        current_errors.append("Tu as supprimé des certifications. Tu dois toutes les garder.")

    return {
        "errors": current_errors,
        "messages": [AIMessage(content=f"Validation terminée. {len(current_errors)} erreurs.", name="validator")]
    }

def cv_optimizer_router(state: CVOptimizerState) -> str:
    count = state.get("iteration_count", 0)
    errors = state.get("errors", [])
    
    if errors and count < 3:
        logger.warning(f"🔄 Retry CV Optimizer (Tentative {count}/3). Raison: {errors[-1:]}")
        return "retry"
    
    if errors:
        logger.warning("⚠️ Max retries atteint. Fin avec erreurs.")
    else:
        logger.info("✅ CV Optimisé validé avec succès.")
        
    return "end"
