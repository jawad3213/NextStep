import asyncio
import json
import sys
import os
from pprint import pprint

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from app.domain.offer_analyzer.service import offer_analyzer_service
from app.domain.profile_retriever.service import profile_retriever_service
from app.domain.skill_gap.service import skill_gap_service
from app.domain.cv_optimizer.service import cv_optimizer_service
from app.domain.cv_engine.service import cv_engine_service

USER_KEYCLOAK_ID = "a9709404-ae96-4070-91a3-21142ded4139"

SAMPLE_OFFER = """
Titre : Développeur Full Stack Python / React

Entreprise : TechNova Solutions
Localisation : Tunis (100% Remote possible)
Type de contrat : CDI

Mission :
Nous recherchons un Développeur Full Stack talentueux pour rejoindre notre équipe produit.
Vous serez responsable de :
- Concevoir et développer des API RESTful avec Python (FastAPI / Django REST)
- Développer l'interface utilisateur avec React.js et TypeScript
- Optimiser les performances des requêtes PostgreSQL
- Participer à la conception architecturale (microservices)
- Mettre en place des pipelines CI/CD avec Docker et GitHub Actions
- Rédiger et maintenir les tests unitaires et d'intégration

Compétences requises :
- Python (FastAPI, Django REST)
- React.js, TypeScript, HTML/CSS
- PostgreSQL, Redis
- Docker, Git, CI/CD
- Connaissances en DevOps (Kubernetes est un plus)

Compétences bonus :
- GraphQL, RabbitMQ, Celery
- Next.js, TailwindCSS
- AWS ou Azure

Expérience : 3+ ans
Salaire : 4000-6000 TND/mois
"""

async def run_full_pipeline():
    print("="*60)
    print("TEST PIPELINE COMPLET — Agent 1 à 5")
    print(f"Utilisateur: saidnichan6@gmail.com (keycloak_id: {USER_KEYCLOAK_ID})")
    print("="*60)

    # ─── Agent 1: Offer Analyzer ───
    print("\n[1/5] Offer Analyzer — Analyse de l'offre...")
    offer_res = await offer_analyzer_service.analyze(SAMPLE_OFFER)
    if offer_res.get("errors"):
        print("ERREUR:", offer_res["errors"])
        return
    analyzed_offer = offer_res["analyzed_offer"]
    print("  [OK] Titre:", analyzed_offer.get("titre"))
    print("  [OK] Contrat:", analyzed_offer.get("type_contrat"))
    print("  [OK] Competences requises:", analyzed_offer.get("competences_requises", [])[:5])
    normalized_skills = offer_res.get("normalized_offer_skills", [])
    print(f"  [OK] Competences normalisees ({len(normalized_skills)}):", normalized_skills[:8])

    # ─── Agent 2: Profile Retriever ───
    print(f"\n[2/5] Profile Retriever — Recherche du profil (ID: {USER_KEYCLOAK_ID})...")
    profile_res = await profile_retriever_service.get_profile(USER_KEYCLOAK_ID)
    if profile_res.get("errors"):
        print("ERREUR:", profile_res["errors"])
        return
    profile_data = profile_res["profile_data"]
    print(f"  [OK] Utilisateur: {profile_data.get('prenom')} {profile_data.get('nom')}")
    print(f"  [OK] Titre: {profile_data.get('titre')}")
    comps = profile_data.get("competences", [])
    print(f"  [OK] Competences ({len(comps)}): {[c.get('nom') for c in comps[:6]]}")
    exps = profile_data.get("experiences", [])
    print(f"  [OK] Experiences ({len(exps)}): {[e.get('titre') for e in exps[:3]]}")

    # ─── Agent 3: Skill Gap ───
    print("\n[3/5] Skill Gap — Matching profil vs offre...")
    gap_res = await skill_gap_service.analyze_skill_gap(profile_data, analyzed_offer)
    if gap_res.errors:
        print("ERREUR:", gap_res.errors)
        return
    print(f"  [OK] Score: {gap_res.skill_gap.relevance_score}/100")
    print(f"  [OK] Flag: {gap_res.skill_gap.flag}")
    print(f"  [OK] Competences matchées ({len(gap_res.skill_gap.matched_skills)}): {gap_res.skill_gap.matched_skills[:8]}")
    print(f"  [OK] Competences manquantes ({len(gap_res.skill_gap.missing_skills)}): {gap_res.skill_gap.missing_skills[:5]}")
    if gap_res.skill_gap.recommendations:
        print(f"  [OK] Recommandations ({len(gap_res.skill_gap.recommendations)}):")
        for rec in gap_res.skill_gap.recommendations[:3]:
            print(f"     - [{rec.type}] {rec.title}: {rec.description[:120]}...")

    # ─── Agent 4: CV Optimizer ───
    print("\n[4/5] CV Optimizer — Réécriture STAR...")
    opt_res = await cv_optimizer_service.optimize_cv(profile_data, analyzed_offer)
    if not opt_res:
        print("ERREUR: CV Optimizer a retourné None")
        return
    print(f"  [OK] Justification: {opt_res.global_justification[:150]}...")
    opt_exps = opt_res.experiences_optimisees
    print(f"  [OK] Experiences optimisees ({len(opt_exps)}):")
    for exp in opt_exps[:2]:
        print(f"     - {exp.titre}: {exp.description_optimisee[:100]}...")
    opt_projs = opt_res.projets_optimises
    print(f"  [OK] Projets optimises ({len(opt_projs)}):")
    for proj in opt_projs[:2]:
        print(f"     - {proj.titre}")

    # ─── Agent 5: CV Engine ───
    print("\n[5/5] CV Engine — Génération payload QuestPDF...")
    offer_skills = analyzed_offer.get("competences_requises", [])
    matched_skills = gap_res.skill_gap.matched_skills if gap_res.skill_gap else []
    engine_res = await cv_engine_service.format_for_questpdf(
        profile_data,
        opt_res.model_dump(),
        matched_skills=matched_skills,
        offer_skills=offer_skills,
    )
    print(f"  [OK] Cles generees: {list(engine_res.keys())}")

    cv_json = json.dumps(engine_res, indent=2, ensure_ascii=False)
    output_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "cv_generated.json")
    with open(output_path, "w", encoding="utf-8") as f:
        f.write(cv_json)

    print("\n" + "="*60)
    print("PIPELINE COMPLET -- SUCCES")
    print("="*60)
    print(f"\nCV complet genere dans : {output_path}")
    print(f"  [INFO] Score ATS: {engine_res.get('atsScore')}/100")
    print(f"  [INFO] Competences matchées: {sum(1 for s in engine_res.get('skills', []) if s.get('isMatched'))}/{len(engine_res.get('skills', []))}")
    print(f"  [INFO] Langues detectees: {engine_res.get('languages', [])}")
    print(f"  [INFO] Activites: {len(engine_res.get('activities', []))}")
    print("\n--- Apercu du CV ---")
    print(cv_json[:2500])
    print("..." if len(cv_json) > 2500 else "")

if __name__ == "__main__":
    asyncio.run(run_full_pipeline())
