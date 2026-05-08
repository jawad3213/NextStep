import asyncio
import json
from pprint import pprint

from app.domain.offer_analyzer.service import offer_analyzer_service
from app.domain.profile_retriever.service import profile_retriever_service
from app.domain.skill_gap.service import skill_gap_service
from app.domain.cv_optimizer.service import cv_optimizer_service
from app.domain.cv_engine.service import cv_engine_service

async def run_master_test():
    print("="*50)
    print("DEMARRAGE DU TEST INTEGRAL DES AGENTS")
    print("="*50)
    
    # --- 1. Offer Analyzer ---
    print("\n[1/5] Agent: Offer Analyzer...")
    raw_offer = """
    Recherche Développeur Backend Python.
    Compétences requises: Python, FastAPI, PostgreSQL.
    Bonus: Redis, Docker.
    3 ans d'expérience. CDI.
    Mission: Construire des API robustes pour notre SaaS.
    """
    offer_res = await offer_analyzer_service.analyze(raw_offer)
    if offer_res.get("errors"):
        print("ERREUR Offer Analyzer:", offer_res["errors"])
        return
    analyzed_offer = offer_res["analyzed_offer"]
    print("Offer Analyzer OK !")
    print("Titre trouvé:", analyzed_offer.get("titre"))
    
    # --- 2. Profile Retriever ---
    print("\n[2/5] Agent: Profile Retriever (Utilisateur 1)...")
    profile_res = await profile_retriever_service.get_profile("1")
    if profile_res.get("errors"):
        print("ERREUR Profile Retriever:", profile_res["errors"])
        return
    profile_data = profile_res["profile_data"]
    print("Profile Retriever OK !")
    print("Nom:", profile_data.get("first_name"), profile_data.get("last_name"))
    
    # --- 3. Skill Gap ---
    print("\n[3/5] Agent: Skill Gap & Matching...")
    gap_res = await skill_gap_service.analyze_skill_gap(profile_data, analyzed_offer)
    if gap_res.errors:
        print("ERREUR Skill Gap:", gap_res.errors)
        return
    print("Skill Gap OK !")
    print("Score de pertinence:", gap_res.skill_gap.relevance_score)
    print("Compétences manquantes:", gap_res.skill_gap.missing_skills)
    
    # --- 4. CV Optimizer ---
    print("\n[4/5] Agent: CV Optimizer (Réécriture STAR)...")
    opt_res = await cv_optimizer_service.optimize_cv(profile_data, analyzed_offer)
    if not opt_res:
        print("ERREUR CV Optimizer")
        return
    print("CV Optimizer OK !")
    print("Justification globale:", opt_res.global_justification)
    
    # --- 5. CV Engine ---
    print("\n[5/5] Agent: CV Engine (JSON pour QuestPDF)...")
    engine_res = await cv_engine_service.format_for_questpdf(profile_data, opt_res.model_dump())
    print("CV Engine OK !")
    print("Structure finale générée (Clés) :", list(engine_res.keys()))
    
    print("\n" + "="*50)
    print("TOUT FONCTIONNE PARFAITEMENT ! Le pipeline est sain.")
    print("="*50)

if __name__ == "__main__":
    asyncio.run(run_master_test())
