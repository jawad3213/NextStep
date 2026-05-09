import logging
from fastapi import APIRouter, HTTPException

from app.domain.cv_engine.schemas.models import CVEngineRequest, PrepareCvRequest
from app.domain.cv_engine.service import cv_engine_service
from app.domain.profile_retriever.service import profile_retriever_service
from app.domain.cv_optimizer.service import cv_optimizer_service

logger = logging.getLogger(__name__)
router = APIRouter(tags=["CV Engine — Préparation données CV"])

@router.post(
    "/cv-engine/format-questpdf",
    response_model=dict,
    summary="Fusionne le profil d'origine et le CV optimisé pour QuestPDF",
    description=(
        "Prend en entrée les données brutes (Profile Retriever) et le CV optimisé "
        "(CV Optimizer) pour produire un JSON déterministe respectant la structure "
        "CvData attendue par le backend .NET."
    ),
)
async def format_questpdf(payload: CVEngineRequest) -> dict:
    """POST /cv-engine/format-questpdf"""
    logger.info("POST /cv-engine/format-questpdf appelé.")
    try:
        return await cv_engine_service.format_for_questpdf(
            original_profile=payload.original_profile,
            optimized_cv=payload.optimized_cv
        )
    except Exception as e:
        logger.error("POST /cv-engine/format-questpdf ❌ — %s", str(e))
        raise HTTPException(status_code=500, detail=str(e))


@router.post(
    "/prepare-cv",
    response_model=dict,
    summary="Restored Orchestrator: Charge, Optimise et Formate",
)
async def prepare_cv(payload: PrepareCvRequest) -> dict:
    """
    POST /prepare-cv
    Orchestre Profile Retriever -> CV Optimizer (si offre) -> CV Engine.
    Aligne sur le flux attendu par CvService.cs .NET
    """
    logger.info("POST /prepare-cv appele — user_id=%s", payload.user_id)
    try:
        # 1. Récupérer le profil
        resp = await profile_retriever_service.get_profile(payload.user_id)
        profile = resp.get("profile_data")
        if not profile:
            raise HTTPException(status_code=404, detail="Profil introuvable")

        # 2. Optimisation (optionnelle)
        optimized_cv = {}
        offer_skills = []
        matched_skills = []

        if payload.offer_data:
            logger.info("Offre detectee. Execution de CV Optimizer...")
            opt_output = await cv_optimizer_service.optimize_cv(
                candidate_cv=profile,
                job_offer=payload.offer_data
            )
            optimized_cv = opt_output.model_dump()
            offer_skills = payload.offer_data.get("competences_requises", [])
            matched_skills = optimized_cv.get("competences_reordonnees", [])
        else:
            logger.info("Aucune offre fournie. Utilisation du profil brut.")
            optimized_cv = {
                "resume_optimise": {"contenu": profile.get("resume", "") or "", "justification_rewrite": ""},
                "experiences_optimisees": [
                    {
                        "titre": exp.get("titre") or "",
                        "entreprise": exp.get("entreprise") or "",
                        "description_optimisee": exp.get("description") or "",
                        "justification_reorder": "", "justification_rewrite": ""
                    } for exp in profile.get("experiences", [])
                ],
                "projets_optimises": [
                    {
                        "titre": p.get("titre") or "",
                        "description_optimisee": p.get("description") or "",
                        "technologies": [],
                        "justification_reorder": "", "justification_rewrite": ""
                    } for p in profile.get("projets", [])
                ],
                "formations_optimisees": [
                    {
                        "diplome": f.get("diplome") or "",
                        "etablissement": f.get("etablissement") or "",
                        "justification_reorder": "", "justification_rewrite": ""
                    } for f in profile.get("formations", [])
                ],
                "certifications_optimisees": [],
                "competences_reordonnees": [
                    c.get("nom") for c in profile.get("competences", []) if c.get("nom")
                ],
                "justification_competences": "",
                "global_justification": "Previsualisation standard sans optimisation."
            }

        # 3. Formatage pour QuestPDF
        cv_json = await cv_engine_service.format_for_questpdf(
            original_profile=profile,
            optimized_cv=optimized_cv,
            matched_skills=matched_skills,
            offer_skills=offer_skills
        )

        # Encapsuler dans cv_json pour correspondre au C# CvEngineResult
        return {
            "cv_json": cv_json
        }

    except HTTPException:
        raise
    except Exception as e:
        logger.error("Erreur dans POST /prepare-cv: %s", str(e), exc_info=True)
        raise HTTPException(status_code=500, detail=str(e))
