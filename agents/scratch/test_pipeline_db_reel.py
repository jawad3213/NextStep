import asyncio
import uuid
import sys
import os
from unittest.mock import patch

# Ajoute le dossier racine au path
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from app.domain.pipeline.workflow import get_offer_pipeline

SAMPLE_OFFER = """
Titre : Développeur Full Stack Python / React
Entreprise : TechNova Solutions
Localisation : Tunis (100% Remote possible)
Type de contrat : CDI

Mission :
- Concevoir et développer des API RESTful avec Python (FastAPI)
- Développer l'interface utilisateur avec React.js et TypeScript
- Optimiser les performances des requêtes PostgreSQL

Compétences requises :
- Python (FastAPI)
- React.js, TypeScript
- PostgreSQL
"""

MOCK_PROFILE = {
    "prenom": "Jean",
    "nom": "Dupont",
    "titre": "Développeur Python & JavaScript",
    "email": "jean.dupont@email.com",
    "competences": [
        {"nom": "Python", "type_competence": "hard"},
        {"nom": "React", "type_competence": "hard"},
        {"nom": "SQL", "type_competence": "hard"}
    ],
    "experiences": [
        {
            "titre": "Développeur Web",
            "entreprise": "Ancienne Entreprise",
            "date_debut": "2022-01-01",
            "date_fin": "2023-12-31",
            "description": "Développement d'applications en Python."
        }
    ]
}

async def run_test():
    pipeline = get_offer_pipeline()
    
    fake_offer_id = str(uuid.uuid4())
    fake_user_id = "a9709404-ae96-4070-91a3-21142ded4139"
    
    initial_state = {
        "raw_offer_text": SAMPLE_OFFER,
        "user_id": fake_user_id,
        "template_id": 1,
        "offer_id": fake_offer_id,
        "messages": [],
        "errors": [],
        "normalized_offer_skills": [],
        "normalized_keywords": [],
        "normalized_profile_skills": []
    }
    
    print("🚀 Lancement du pipeline complet via LangGraph...")
    
    # On mocke le ProfileRetrieverService pour éviter l'erreur de table manquante
    with patch("app.domain.profile_retriever.service.profile_retriever_service.get_profile") as mock_get:
        mock_get.return_value = {
            "profile_data": MOCK_PROFILE,
            "errors": []
        }
        
        final_state = await pipeline.ainvoke(initial_state)
    
    print("\n" + "="*50)
    print("📊 RÉSULTATS DU PIPELINE :")
    print("="*50)
    print(f"❌ Erreurs : {final_state.get('errors')}")
    print(f"✅ Analyse offre générée : {final_state.get('analyzed_offer') is not None}")
    print(f"✅ Matching généré : {final_state.get('match_result') is not None}")
    print(f"✅ CV optimisé : {final_state.get('cv_engine_result') is not None}")
    
    # On affiche les résultats si générés
    if final_state.get('match_result'):
        print(f"\n🎯 Score Matching : {final_state['match_result'].get('score_matching')}/100")
        
    print("\n💬 Messages du système :")
    for msg in final_state.get("messages", []):
        print(f" - {msg.content}")

if __name__ == "__main__":
    asyncio.run(run_test())
