# ============================================================
# agents/tests/conftest.py
# Configuration pytest partagée entre tous les tests
# ============================================================
import sys
import os
import pytest
import pytest_asyncio

# ── Ajoute le dossier agents/ au PYTHONPATH ────────────────────
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))


# ── Données de test partagées (fixtures) ──────────────────────

@pytest.fixture
def sample_offer_text():
    """Texte brut d'offre pour les tests d'intégration."""
    return (
        "Nous recherchons un Développeur Fullstack Senior (CDI) à Paris. "
        "Compétences requises : React.js, Node.js, TypeScript, PostgreSQL, Docker. "
        "Compétences souhaitées : Kubernetes, Redis, GraphQL. "
        "Mots-clés ATS : React, Node, TypeScript, Docker, PostgreSQL, REST API, CI/CD. "
        "Expérience : 3 ans minimum. Niveau : Bac+5. "
        "Rémunération : 50-65k€. Télétravail hybride possible."
    )


@pytest.fixture
def sample_analyzed_offer():
    """Offre déjà analysée (résultat Agent 1 simulé)."""
    return {
        "titre": "Développeur Fullstack Senior",
        "entreprise": "TechCorp",
        "type_contrat": "CDI",
        "localisation": "Paris",
        "competences_requises": ["React", "Node.js", "TypeScript", "PostgreSQL", "Docker"],
        "competences_souhaitees": ["Kubernetes", "Redis", "GraphQL"],
        "keywords_ats": ["react", "node.js", "typescript", "docker", "postgresql", "rest api", "ci/cd"],
        "annees_experience": 3,
        "niveau_etudes": "Bac+5",
        "description_poste": "Développement d'une plateforme SaaS en full remote partiel.",
    }


@pytest.fixture
def sample_profile():
    """Profil candidat simulé (résultat Agent 2)."""
    return {
        "user_id": "test-user-uuid-1234",
        "nom": "Dupont",
        "prenom": "Jean",
        "titre": "Développeur Full-Stack React Node.js",
        "resume": (
            "Développeur passionné avec 4 ans d'expérience en React, TypeScript et Node.js. "
            "Expert Docker et CI/CD, habitué aux environnements cloud (AWS)."
        ),
        "telephone": "+33612345678",
        "ville": "Paris",
        "competences": [
            {"nom": "React", "niveau": 5},
            {"nom": "TypeScript", "niveau": 4},
            {"nom": "Node.js", "niveau": 4},
            {"nom": "PostgreSQL", "niveau": 3},
            {"nom": "Docker", "niveau": 3},
            {"nom": "Python", "niveau": 2},
            {"nom": "AWS", "niveau": 2},
        ],
        "experiences": [
            {
                "titre": "Développeur Full-Stack",
                "entreprise": "StartupX",
                "date_debut": "2021-01-01",
                "date_fin": None,
                "description": "Développement React et Node.js, déploiement Docker.",
            },
            {
                "titre": "Développeur Frontend",
                "entreprise": "AgenceY",
                "date_debut": "2019-06-01",
                "date_fin": "2020-12-31",
                "description": "Interfaces React et TypeScript.",
            },
        ],
        "formations": [
            {"diplome": "Master Informatique", "etablissement": "Université Paris-Saclay", "annee": 2019},
        ],
        "certifications": [
            {"nom": "AWS Certified Developer", "organisme": "Amazon"},
        ],
        "projets": [
            {
                "titre": "Portfolio Personnel",
                "description": "Site Next.js avec TypeScript et Vercel.",
                "technologies": ["Next.js", "TypeScript", "Vercel"],
            }
        ],
    }


@pytest.fixture
def sample_match_result():
    """Résultat de scoring simulé (résultat Agent 4)."""
    return {
        "score_matching": 75,
        "score_ats": 68,
        "keywords_presents": ["react", "node.js", "typescript", "docker"],
        "keywords_manquants": ["kubernetes", "redis"],
        "recommandations": ["Ajouter Kubernetes dans votre profil"],
        "competences_matching": ["React", "TypeScript", "Node.js", "Docker"],
        "competences_manquantes": ["Kubernetes", "Redis"],
    }
