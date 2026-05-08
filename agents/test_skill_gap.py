# ============================================================
# test_skill_gap.py
# Script de test de bout en bout intégrant le Skill Gap Agent.
# ============================================================
import asyncio
import os
import sys
import io
import logging
from app.domain.company.service import company_service

# Forcer sys.stdout en UTF-8 pour Windows
if sys.platform.startswith("win"):
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except AttributeError:
        sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

# Configuration du logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

async def run_skill_gap_test():
    """
    Exécute le pipeline complet (Fiche Entreprise + Skill Gap).
    """
    company_name = "Capgemini Maroc"
    job_title = "Data Engineer"
    
    # 📄 CV fictif de la candidate (identique à l'exemple graphique)
    candidate_cv = {
        "name": "Alice Martin",
        "skills": ["SQL", "Python", "Docker", "Java", "PostgreSQL"],
        "certifications": [],
        "experience_years": 3.5
    }
    
    # 🎯 Exigences de l'offre d'emploi
    job_offer = {
        "job_title": "Data Engineer",
        "required_skills": ["SQL", "Python", "Apache Airflow", "dbt", "Docker", "Spark"],
        "required_certs": ["AWS Certified Data Analytics"],
        "required_years": 4.0
    }
    
    logger.info("🚀 Démarrage du test combiné Intelligence + Skill Gap...")
    
    try:
        result = await company_service.get_company_intelligence(
            company_name=company_name,
            job_title=job_title,
            user_id="user_test_999",
            candidate_cv=candidate_cv,
            job_offer=job_offer
        )
        
        intel = result.get('intelligence', {})
        skill_gap = result.get('skill_gap', {})
        
        # 🏢 SECTION 1 : RÉSULTAT COMPANY INTEL
        print("\n" + "═" * 70)
        print(f" 📑 FICHE DÉTAILLÉE D'INTELLIGENCE : {intel.get('nom', company_name).upper()}")
        print("═" * 70)
        print(f"🏢 Secteur             : {intel.get('sector', 'N/A')}")
        print(f"📍 Siège Social         : {intel.get('hq_location', 'N/A')}")
        print(f"🎯 Score de Fit Global : {result.get('score', 'N/A')}%")
        
        # 🎯 SECTION 2 : RÉSULTAT SKILL GAP
        print("\n" + "═" * 70)
        print(" 🎯 ÉVALUATION DES COMPÉTENCES (SKILL GAP)")
        print("═" * 70)
        print(f"👤 Candidate          : {skill_gap.get('candidate_name')}")
        print(f"💼 Job Title          : {skill_gap.get('job_title')}")
        print(f"📈 Relevance Score    : {skill_gap.get('relevance_score')}")
        print(f"🚨 Flag               : {skill_gap.get('flag', '').upper()}")
        
        print("\n🛠️ COMPÉTENCES MAÎTRISÉES (MATCHED SKILLS) :")
        print("─" * 50)
        for skill in skill_gap.get('matched_skills', []):
            print(f"  ✅ {skill}")
            
        print("\n❌ COMPÉTENCES MANQUANTES (MISSING SKILLS) :")
        print("─" * 50)
        for skill in skill_gap.get('missing_skills', []):
            print(f"  🔴 {skill}")
            
        print("\n🏅 CERTIFICATIONS :")
        print("─" * 50)
        print(f"  • Certif requises  : {', '.join(skill_gap.get('required_certs', []))}")
        print(f"  • Certif Match     : {'OUI ✅' if skill_gap.get('cert_match') else 'NON ❌'}")
        
        print("\n⏳ EXPÉRIENCE :")
        print("─" * 50)
        print(f"  • Expérience du candidat  : {skill_gap.get('experience_years')} ans")
        print(f"  • Expérience requise      : {skill_gap.get('required_years')} ans")
        print(f"  • Écart d'expérience       : {skill_gap.get('experience_gap_years')} ans")
        
        print("\n💡 SUGGESTIONS D'AMÉLIORATION DU CV (REVISION HINTS) :")
        print("─" * 50)
        for hint in skill_gap.get('revision_hints', []):
            print(f"  👉 {hint}")
            
        print("\n" + "═" * 70)
        
    except Exception as e:
        logger.error(f"❌ Erreur critique lors de l'exécution du test: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    asyncio.run(run_skill_gap_test())
