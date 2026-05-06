# ============================================================
# test_company_agent.py
# Script de test pour le Company Intelligence Agent.
# ============================================================
import asyncio
import os
import logging
import sys
import io
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

async def test_run():
    """
    Lance une exécution de test pour une entreprise réelle.
    """
    company_name = "Docaposte Maroc"
    job_title = "Data Scientist"
    
    logger.info(f"🚀 Démarrage du test pour {company_name}...")
    
    try:
        result = await company_service.get_company_intelligence(
            company_name=company_name,
            job_title=job_title,
            user_id="test_user_123"
        )
        
        intel = result.get('intelligence', {})
        
        print("\n" + "═" * 70)
        print(f" 📑 FICHE DÉTAILLÉE D'INTELLIGENCE : {intel.get('nom', company_name).upper()}")
        print("═" * 70)
        
        print(f"🏢 Secteur             : {intel.get('sector', 'N/A')}")
        print(f"📍 Siège Social         : {intel.get('hq_location', 'N/A')}")
        print(f"🔗 LinkedIn URL        : {intel.get('linkedin_url', 'N/A')}")
        print(f"🎯 Score de Fit Global : {result.get('score', 'N/A')}%")
        
        print("\n📝 PRÉSENTATION GÉNÉRALE :")
        print("─" * 50)
        print(intel.get('summary', 'Aucun résumé disponible.'))
        
        culture = intel.get('culture', {})
        print("\n🌱 CULTURE D'ENTREPRISE :")
        print("─" * 50)
        print(f"  • Score de Culture : {culture.get('culture_score', 'N/A')}/100")
        print(f"  • Équilibre Vie Pro/Perso : {culture.get('work_life_balance', 'N/A')}/5")
        print(f"  • Note Glassdoor : {culture.get('glassdoor_rating', 'N/A')}/5")
        print(f"  • Taux de Turnover : {culture.get('turnover_rate', 'N/A')}")
        print(f"  • Valeurs Clés : {', '.join(culture.get('key_values', []))}")
        print("  • Avis marquants :")
        for review in culture.get('top_reviews', []):
            print(f"    - \"{review}\"")
            
        salaries = intel.get('salaries', [])
        print("\n💰 INTÉGRATION DES SALAIRES :")
        print("─" * 50)
        if salaries:
            for sal in salaries:
                seniority = f" [{sal.get('seniority')}]" if sal.get('seniority') else ""
                period_str = "mois" if sal.get('period') == "month" else "an"
                print(f"  • Poste : {sal.get('job_title')}{seniority}")
                print(f"    - Localisation : {sal.get('location', 'N/A')}")
                print(f"    - Salaire Moyen : {sal.get('avg_salary', 'N/A')} {sal.get('currency', 'EUR')} / {period_str}")
                print(f"    - Fourchette : {sal.get('min_salary', 'N/A')} - {sal.get('max_salary', 'N/A')} {sal.get('currency', 'EUR')} / {period_str}")
                print(f"    - Source : {sal.get('source', 'N/A')}")
        else:
            print("  Aucune donnée de salaire disponible.")
            
        print("\n📰 ACTUALITÉS ET FAITS MARQUANTS :")
        print("─" * 50)
        actualites = intel.get('actualites', [])
        if actualites:
            for act in actualites:
                print(f"  • {act}")
        else:
            print("  Aucun fait d'actualité disponible.")

        print("\n🤝 SÉCURISATION DE L'ENTRETIEN (GLASS DOOR & RETOURS) :")
        print("─" * 50)
        print(f"  • Difficulté estimée : {intel.get('interview_difficulty', 'N/A').upper()}")
        print("  • Questions d'entretien connues :")
        questions = intel.get('interview_questions', [])
        if questions:
            for q in questions:
                print(f"    - \"{q}\"")
        else:
            print("    Aucune question répertoriée.")

        print("\n⚖️ FORCES ET FAIBLESSES :")
        print("─" * 50)
        print("  🟢 Points forts (Pros) :")
        for pro in intel.get('pros', []):
            print(f"    + {pro}")
        print("  🔴 Points faibles (Cons) :")
        for con in intel.get('cons', []):
            print(f"    - {con}")
            
        print(f"\n📈 Perspectives d'Évolution de Carrière :")
        print("─" * 50)
        print(f"  {intel.get('career_opportunities', 'N/A')}")
            
        print("\n💡 RECOMMANDATIONS STRATÉGIQUES POUR L'ENTRETIEN :")
        print("─" * 50)
        for rec in result.get('recommendations', []):
            print(f"  👉 {rec}")
        print("═" * 70 + "\n")

        # Test de la sauvegarde optionnelle en base de données Postgres
        # print("💾 Tentative de sauvegarde en base de données Postgres...")
        # from app.core.database import AsyncSessionFactory
        # try:
        #     async with AsyncSessionFactory() as session:
        #         db_result = await company_service.save_company_intelligence(
        #             db=session,
        #             intelligence_data=result
        #         )
        #         if db_result:
        #             print(f"✅ SAUVEGARDE EN BASE DE DONNÉES REUSSIE !")
        #             print(f"  • ID de l'enregistrement : {db_result.get('id')}")
        #             print(f"  • Date de collecte : {db_result.get('date_collecte')}")
        #             print("═" * 70 + "\n")
        # except Exception as db_err:
        #     logger.warning(f"⚠️ Échec de la sauvegarde en base de données : {db_err}")
        #     print("═" * 70 + "\n")
        
    except Exception as e:
        logger.error(f"❌ Erreur pendant le test : {e}")

if __name__ == "__main__":
    # Assurez-vous d'avoir exporté votre clé API (OPENAI_API_KEY ou GROQ_API_KEY)
    asyncio.run(test_run())
