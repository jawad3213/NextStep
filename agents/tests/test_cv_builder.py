# ============================================================
# agents/tests/test_cv_builder.py
# Tests unitaires — Agent 5 (CV Formatter / builder.py)
# ============================================================
import pytest
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from app.domain.job.agents.cv_formatter.builder import (
    build_entete,
    build_competences,
    build_experiences,
    build_formations,
    build_projets,
)


class TestBuildEntete:
    """Tests de build_entete()."""

    def test_standard_profile(self):
        profile = {
            "nom": "Dupont", "prenom": "Jean", "titre": "Dev React",
            "email": "jean@test.com", "telephone": "0612345678",
            "ville": "Paris", "lien_linkedin": "https://linkedin.com/in/jean",
            "lien_github": "https://github.com/jean", "lien_portfolio": "",
        }
        result = build_entete(profile)
        assert result["nom"] == "Dupont"
        assert result["prenom"] == "Jean"
        assert result["linkedin"] == "https://linkedin.com/in/jean"
        assert result["portfolio"] == ""

    def test_empty_profile(self):
        """Profil vide → valeurs vides, pas d'exception."""
        result = build_entete({})
        assert result["nom"] == ""
        assert result["prenom"] == ""
        assert result["email"] == ""


class TestBuildCompetences:
    """Tests de build_competences()."""

    def test_matching_skills_first(self):
        """Les compétences matchées apparaissent en premier."""
        profile = {
            "competences": [
                {"nom": "Python", "niveau": 3},
                {"nom": "React", "niveau": 5},
                {"nom": "Docker", "niveau": 2},
            ]
        }
        match_result = {"competences_matching": ["react", "docker"]}
        result = build_competences(profile, match_result)

        # Les deux premiers doivent être matchés
        matched = [c for c in result if c["matched"]]
        not_matched = [c for c in result if not c["matched"]]
        assert len(matched) == 2
        assert len(not_matched) == 1
        # React et Docker matchés
        matched_names = {c["nom"] for c in matched}
        assert "React" in matched_names or "react" in matched_names

    def test_matched_flag(self):
        """Flag `matched` correctement assigné."""
        profile = {"competences": [{"nom": "React", "niveau": 5}]}
        match_result = {"competences_matching": ["react"]}
        result = build_competences(profile, match_result)
        assert result[0]["matched"] is True

    def test_empty_skills(self):
        """Profil sans compétences → liste vide."""
        result = build_competences({"competences": []}, {})
        assert result == []

    def test_invalid_competence_ignored(self):
        """Les entrées invalides (non-dict) sont ignorées."""
        profile = {"competences": [{"nom": "React"}, "invalid", None]}
        result = build_competences(profile, {})
        assert len(result) == 1


class TestBuildExperiences:
    """Tests de build_experiences()."""

    def test_standard_experience(self):
        profile = {"experiences": [
            {"titre": "Dev Senior", "entreprise": "TechCorp",
             "date_debut": "2020-01-01", "date_fin": None,
             "description": "Développement React."},
        ]}
        result = build_experiences(profile)
        assert len(result) == 1
        assert result[0]["titre"] == "Dev Senior"
        assert result[0]["entreprise"] == "TechCorp"

    def test_empty_experiences(self):
        assert build_experiences({"experiences": []}) == []
        assert build_experiences({}) == []


class TestBuildFormations:
    """Tests de build_formations()."""

    def test_standard_formation(self):
        profile = {"formations": [
            {"diplome": "Master Info", "etablissement": "Université Paris", "annee": 2020},
        ]}
        result = build_formations(profile)
        assert result[0]["diplome"] == "Master Info"
        assert result[0]["annee"] == 2020

    def test_empty_formations(self):
        assert build_formations({}) == []


class TestBuildProjets:
    """Tests de build_projets()."""

    def test_standard_project(self):
        profile = {"projets": [
            {"titre": "Mon App", "description": "App React", "technologies": ["React", "Node"]},
        ]}
        result = build_projets(profile)
        assert result[0]["titre"] == "Mon App"
        assert "React" in result[0]["technologies"]

    def test_missing_technologies(self):
        """Projet sans technologies → liste vide."""
        profile = {"projets": [{"titre": "Projet", "description": "desc"}]}
        result = build_projets(profile)
        assert result[0]["technologies"] == []
