import asyncio
import json

from app.domain.cv_engine.service import cv_engine_service

async def main():
    original_profile = {
        "user_id": 1,
        "first_name": "Jean",
        "last_name": "Dupont",
        "email": "jean.dupont@test.com",
        "phone": "+33600000000",
        "location": "Paris",
        "linkedin": "linkedin.com/in/jeandupont",
        "summary": "Développeur Backend",
        "experiences": [
            {
                "title": "Lead Backend Engineer",
                "company": "TechCorp",
                "start_date": "2020-01",
                "end_date": "Présent",
                "description": "Développement API"
            },
            {
                "title": "Développeur Fullstack",
                "company": "WebAgency",
                "start_date": "2018-05",
                "end_date": "2019-12",
                "description": "Création sites web"
            }
        ],
        "projects": [
            {
                "title": "SaaS Platform",
                "description": "Plateforme B2B",
                "technologies": ["Python", "FastAPI"]
            }
        ],
        "educations": [
            {
                "degree": "Master Informatique",
                "institution": "Université de Paris",
                "start_date": "2018",
                "end_date": "2020"
            }
        ],
        "languages": [
            {"language": "Français", "level": "Natif"},
            {"language": "Anglais", "level": "Courant"}
        ]
    }
    
    optimized_cv = {
        "resume_optimise": {
            "contenu": "Expert Backend avec un focus sur les performances des API SaaS.",
            "justification_rewrite": "Mise en avant SaaS."
        },
        "experiences_optimisees": [
            {
                "titre": "Lead Backend Engineer",
                "entreprise": "TechCorp",
                "description_optimisee": "- Création d'une API FastAPI réduisant le temps de réponse de 40%.\n- Mise en place d'un système de caching Redis.",
                "justification_reorder": "L'expérience la plus pertinente.",
                "justification_rewrite": "Ajout des mots clés FastAPI et Redis."
            },
            {
                "titre": "Développeur Fullstack",
                "entreprise": "WebAgency",
                "description_optimisee": "- Développement de sites B2C générant 1M de visites.",
                "justification_reorder": "Expérience secondaire.",
                "justification_rewrite": "Factuel."
            }
        ],
        "projets_optimises": [
            {
                "titre": "SaaS Platform",
                "description_optimisee": "- Conception from scratch d'un backend SaaS.\n- Gestion de plus de 1000 utilisateurs.",
                "technologies": ["Python", "FastAPI"],
                "justification_reorder": "Très pertinent pour l'offre.",
                "justification_rewrite": "Metrics ajoutées."
            }
        ],
        "formations_optimisees": [
            {
                "diplome": "Master Informatique",
                "etablissement": "Université de Paris",
                "justification_reorder": "Niveau requis.",
                "justification_rewrite": "-"
            }
        ],
        "certifications_optimisees": [],
        "competences_reordonnees": ["Python", "FastAPI", "Redis", "Architecture Backend"],
        "justification_competences": "Match avec l'offre.",
        "global_justification": "Profil optimisé pour un poste de Backend DevOps."
    }

    print("Fusion en cours...")
    result = await cv_engine_service.format_for_questpdf(original_profile, optimized_cv)
    
    print("\n=== RÉSULTAT QUESTPDF CV DATA ===")
    print(json.dumps(result, indent=2, ensure_ascii=False))
    
    print("\nVérification des champs clés :")
    assert result["candidate"]["name"] == "Jean Dupont", "Erreur nom"
    assert result["experience"][0]["start"] == "2020-01", "Erreur récupération date start"
    assert "FastAPI" in result["skills"][1]["name"], "Erreur skills"
    print("✅ Le mapping algorithmique fonctionne parfaitement et conserve les dates originelles !")

if __name__ == "__main__":
    asyncio.run(main())
