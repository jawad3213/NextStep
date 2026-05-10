# ============================================================
# test_offer_pipeline.py
# Test du pipeline complet OFFER (Agents 1-4)
# ============================================================
import asyncio
import logging
import sys
import os

# Ajout du chemin racine pour les imports
sys.path.append(os.path.dirname(os.path.abspath(__file__)))

from app.domain.offer.service import offer_service
from app.domain.company.service import company_service

# Configuration du logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    stream=sys.stdout
)

RAW_OFFER = """
Nous recherchons un Développeur Full-Stack Senior Python/React.
Missions :
- Développer des APIs robustes avec Django et FastAPI.
- Concevoir des interfaces modernes avec React et TypeScript.
- Participer à l'architecture cloud sur AWS.
- Assurer la qualité du code (tests unitaires, CI/CD).

Profil :
- Bac+5 en informatique.
- 5 ans d'expérience minimum.
- Maîtrise de Python, React, PostgreSQL, Docker et Kubernetes.
- Expérience avec les méthodes Agiles (Scrum).
- Anglais technique courant.
"""

# ID d'un utilisateur de test (trouvé en DB pour saidnichan6@gmail.com)
TEST_USER_ID = "a9709404-ae96-4070-91a3-21142ded4139"

async def main():
    print("\n" + "="*70)
    print("STARTING OFFER PIPELINE TEST")
    print("="*70 + "\n")

    try:
        # Lancement du pipeline via le service
        result = await offer_service.run_pipeline(
            raw_text=RAW_OFFER,
            user_id=TEST_USER_ID,
            template_id=1
        )

        print("\n" + "PIPELINE RESULTS :")
        print(f"User ID : {result.user_id}")
        
        if result.analyzed_offer:
            print("\nANALYZED OFFER (Agent 1) :")
            print(f"   Titre : {result.analyzed_offer.get('titre')}")
            print(f"   Entreprise : {result.analyzed_offer.get('entreprise')}")
            print(f"   Compétences : {', '.join(result.analyzed_offer.get('competences_requises', []))}")

        if result.profile_data:
            import json
            print("\nDETAILED PROFILE DATA (Agent 2) :")
            print(json.dumps(result.profile_data, indent=4, ensure_ascii=False))

        if result.match_result:
            print("\nMATCHING SCORES (Agent 4) :")
            print(f"   Score Global : {result.match_result.get('score_matching')}%")
            print(f"   Score ATS : {result.match_result.get('score_ats')}%")
            print(f"   Compétences trouvées : {', '.join(result.match_result.get('competences_matching', []))}")
            print(f"   Compétences manquantes : {', '.join(result.match_result.get('competences_manquantes', []))}")
            
            print("\nRECOMMENDATIONS :")
            for rec in result.match_result.get('recommandations', []):
                print(f"   • {rec}")

        # ── Intégration Company Intelligence & Skill Gap ──
        if result.analyzed_offer and result.profile_data:
            print("\n" + "═" * 70)
            print(" RUNNING COMPANY INTELLIGENCE & SKILL GAP PIPELINE")
            print("═" * 70)
            
            company_name = result.analyzed_offer.get("entreprise") or "SQLI Maroc"
            job_title = result.analyzed_offer.get("titre") or "Développeur Full-Stack"
            
            # Formater le CV pour le Skill Gap Agent
            profile = result.profile_data or {}
            raw_competences = [c.get("nom", "") for c in profile.get("competences", []) if isinstance(c, dict) and c.get("nom")]
            
            # Normalisation et expansion robuste pour éviter les faux-négatifs sémantiques (ex: JavaScript/TypeScript, React.js)
            competences_list = []
            for skill in raw_competences:
                if skill not in competences_list:
                    competences_list.append(skill)
                # Gérer les slashes (ex: JavaScript/TypeScript -> ajoute JavaScript et TypeScript)
                if "/" in skill:
                    for part in skill.split("/"):
                        part_clean = part.strip()
                        if part_clean and part_clean not in competences_list:
                            competences_list.append(part_clean)
                # Gérer React.js -> ajoute React
                if skill.lower() == "react.js" and "React" not in competences_list:
                    competences_list.append("React")
                # Gérer JavaScript/TypeScript explicite
                if "typescript" in skill.lower() and "TypeScript" not in competences_list:
                    competences_list.append("TypeScript")

            certifications_list = [c.get("nom", "") for c in profile.get("certifications", []) if isinstance(c, dict) and c.get("nom")]
            
            # Détection ou estimation réaliste de l'expérience du candidat
            experience_years = 5.0
            candidate_cv = {
                "name": f"{profile.get('prenom', 'Said')} {profile.get('nom', 'Nichan')}",
                "skills": competences_list,
                "certifications": certifications_list,
                "experience_years": experience_years
            }
            
            # Exigences de l'offre
            job_offer = {
                "job_title": job_title,
                "required_skills": result.analyzed_offer.get("competences_requises", []),
                "required_certs": [],
                "required_years": float(result.analyzed_offer.get("annees_experience") or 5.0)
            }
            
            # Lancement de l'agent d'intelligence entreprise et du skill gap
            company_result = await company_service.get_company_intelligence(
                company_name=company_name,
                job_title=job_title,
                user_id=TEST_USER_ID,
                candidate_cv=candidate_cv,
                job_offer=job_offer
            )
            
            intel = company_result.get('intelligence', {})
            skill_gap = company_result.get('skill_gap', {})
            
            print(f"\n🏢 ENTREPRISE ANALYSÉE : {intel.get('nom', company_name).upper()}")
            print(f"📍 Siège Social         : {intel.get('hq_location', 'N/A')}")
            print(f"🌱 Score de Culture : {intel.get('culture', {}).get('culture_score', 'N/A')}/100")
            print(f"⭐ Note Glassdoor   : {intel.get('culture', {}).get('glassdoor_rating', 'N/A')}/5")
            
            print(f"\n💰 GRILLES SALARIALES ESTIMÉES :")
            salaries = intel.get('salaries', [])
            if salaries:
                for sal in salaries:
                    seniority = f" [{sal.get('seniority')}]" if sal.get('seniority') else ""
                    print(f"   • {sal.get('job_title')}{seniority} : {sal.get('min_salary')} - {sal.get('max_salary')} {sal.get('currency', 'EUR')} / mois")
            else:
                print("   Aucune donnée de salaire extraite.")
                
            print(f"\n🎯 SKILL GAP ANALYSIS ({candidate_cv.get('name')} vs {job_title}) :")
            if skill_gap:
                print(f"   • Score de pertinence sémantique : {skill_gap.get('relevance_score')}")
                print(f"   • Niveau d'écart (Alerte)        : {skill_gap.get('flag', '').upper()}")
                print(f"   • Compétences trouvées           : {', '.join(skill_gap.get('matched_skills', []))}")
                print(f"   • Compétences manquantes         : {', '.join(skill_gap.get('missing_skills', []))}")
                print(f"   • Suggestions d'amélioration du CV :")
                for hint in skill_gap.get('revision_hints', []):
                    print(f"     👉 {hint}")
            else:
                print("   Échec du calcul de l'écart de compétences.")
                
            print("═" * 70 + "\n")

        if result.errors:
            print("\n❌ ERREURS RENCONTRÉES :")
            for err in result.errors:
                print(f"   - {err}")

        print("\n" + "AGENTS HISTORY :")
        for msg in result.messages:
            print(f"   [{msg['agent']}] {msg['content'][:100]}...")

    except Exception as e:
        print(f"\nFATAL ERROR : {e}")

if __name__ == "__main__":
    if sys.platform.startswith("win"):
        import io
        try:
            sys.stdout.reconfigure(encoding='utf-8')
        except AttributeError:
            sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    asyncio.run(main())
