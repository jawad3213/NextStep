# ============================================================
# agents/tests/test_cv_engine.py
# Tests unitaires — CV Engine (Nodes 1, 2, 3 + Service)
#
# Node 1 — ProfileLoader  : DB mockée (unittest.mock.patch)
# Node 2 — SkillOptimizer : Algorithme pur — tests directs
# Node 3 — CvStructurer   : Algorithme pur — tests directs
# Service — CvEngineService : pipeline complet (DB mockée)
# ============================================================
import sys
import os
import pytest
import pytest_asyncio
from unittest.mock import patch, AsyncMock

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from app.domain.cv_engine.agents.skill_optimizer import skill_optimizer_node
from app.domain.cv_engine.agents.cv_structurer import cv_structurer_node
from app.domain.cv_engine.agents.profile_loader import profile_loader_node


# ─────────────────────────────────────────────────────────────────────────────
# Helpers — state factories
# ─────────────────────────────────────────────────────────────────────────────

def _make_optimizer_state(profile: dict, match_result: dict | None = None, offer_data: dict | None = None) -> dict:
    """Construit un state minimal pour skill_optimizer_node."""
    return {
        "user_id": "test-user",
        "raw_profile": profile,
        "match_result": match_result or {},
        "offer_data": offer_data or {},
        "messages": [],
        "errors": [],
    }


def _make_structurer_state(
    profile: dict,
    optimized_skills: list,
    ats_coverage: dict | None = None,
    match_result: dict | None = None,
    template_slug: str = "modern",
) -> dict:
    """Construit un state minimal pour cv_structurer_node."""
    return {
        "user_id": "test-user",
        "template_slug": template_slug,
        "raw_profile": profile,
        "optimized_skills": optimized_skills,
        "ats_coverage": ats_coverage or {"covered": 0, "total": 0, "percentage": 0.0, "keywords_present": [], "keywords_missing": []},
        "match_result": match_result or {},
        "offer_data": {},
        "messages": [],
        "errors": [],
    }


# ─────────────────────────────────────────────────────────────────────────────
# Class 1 — TestSkillOptimizer (Node 2)
# ─────────────────────────────────────────────────────────────────────────────

class TestSkillOptimizer:
    """Tests de skill_optimizer_node() — tri et couverture ATS."""

    @pytest.mark.asyncio
    async def test_matched_skills_appear_first(self, sample_profile, sample_match_result):
        """Les compétences présentes dans match_result arrivent en tête de liste."""
        state = _make_optimizer_state(sample_profile, match_result=sample_match_result)
        result = await skill_optimizer_node(state)

        skills = result["optimized_skills"]
        assert len(skills) > 0, "La liste de compétences ne doit pas être vide"

        matched = [s for s in skills if s["matched"]]
        not_matched = [s for s in skills if not s["matched"]]

        # Toutes les compétences matchées doivent précéder les non-matchées
        if matched and not_matched:
            last_matched_idx = max(skills.index(s) for s in matched)
            first_unmatched_idx = min(skills.index(s) for s in not_matched)
            assert last_matched_idx < first_unmatched_idx, (
                "Les compétences matchées doivent toutes précéder les non-matchées"
            )

    @pytest.mark.asyncio
    async def test_matched_flag_is_true_for_matching_skills(self, sample_profile, sample_match_result):
        """Les compétences listées dans competences_matching ont matched=True."""
        state = _make_optimizer_state(sample_profile, match_result=sample_match_result)
        result = await skill_optimizer_node(state)

        skills_by_name = {s["nom"].lower(): s for s in result["optimized_skills"]}
        for matched_name in sample_match_result["competences_matching"]:
            key = matched_name.lower()
            if key in skills_by_name:
                assert skills_by_name[key]["matched"] is True, (
                    f"La compétence '{matched_name}' devrait avoir matched=True"
                )

    @pytest.mark.asyncio
    async def test_unmatched_flag_is_false(self, sample_profile):
        """Sans match_result, toutes les compétences ont matched=False."""
        state = _make_optimizer_state(sample_profile, match_result={})
        result = await skill_optimizer_node(state)

        for skill in result["optimized_skills"]:
            assert skill["matched"] is False, (
                f"'{skill['nom']}' ne devrait pas être matchée sans match_result"
            )

    @pytest.mark.asyncio
    async def test_no_offer_sorts_by_level_descending(self):
        """Sans offre ni match_result, tri par niveau décroissant."""
        profile = {
            "competences": [
                {"nom": "Python",     "niveau": 2},
                {"nom": "React",      "niveau": 5},
                {"nom": "Docker",     "niveau": 3},
                {"nom": "TypeScript", "niveau": 4},
            ]
        }
        state = _make_optimizer_state(profile)
        result = await skill_optimizer_node(state)
        skills = result["optimized_skills"]

        levels = [s["niveau"] for s in skills]
        assert levels == sorted(levels, reverse=True), (
            "Sans offre, les compétences doivent être triées par niveau décroissant"
        )

    @pytest.mark.asyncio
    async def test_ats_coverage_calculated_with_offer_keywords(self, sample_profile, sample_analyzed_offer):
        """La couverture ATS est calculée quand offer_data.keywords_ats est fourni."""
        state = _make_optimizer_state(sample_profile, offer_data=sample_analyzed_offer)
        result = await skill_optimizer_node(state)

        coverage = result["ats_coverage"]
        assert coverage["total"] > 0,    "Le total ATS doit être > 0 si des keywords sont fournis"
        assert coverage["covered"] >= 0, "covered doit être >= 0"
        assert 0.0 <= coverage["percentage"] <= 100.0, "Le pourcentage doit être entre 0 et 100"
        assert isinstance(coverage["keywords_present"], list)
        assert isinstance(coverage["keywords_missing"], list)

    @pytest.mark.asyncio
    async def test_ats_coverage_is_zero_without_offer(self, sample_profile):
        """Sans offre ni match_result avec keywords, coverage retourne des zéros."""
        state = _make_optimizer_state(sample_profile, match_result={}, offer_data={})
        result = await skill_optimizer_node(state)

        coverage = result["ats_coverage"]
        assert coverage["total"] == 0
        assert coverage["covered"] == 0
        assert coverage["percentage"] == 0.0

    @pytest.mark.asyncio
    async def test_empty_profile_returns_empty_skills_list(self):
        """Un profil sans compétences retourne une liste vide."""
        state = _make_optimizer_state({"competences": []})
        result = await skill_optimizer_node(state)

        assert result["optimized_skills"] == []

    @pytest.mark.asyncio
    async def test_invalid_skill_entries_are_ignored(self):
        """Les entrées non-dict dans competences sont silencieusement ignorées."""
        profile = {
            "competences": [
                {"nom": "React", "niveau": 5},
                "invalid_string",
                None,
                42,
            ]
        }
        state = _make_optimizer_state(profile)
        result = await skill_optimizer_node(state)

        assert len(result["optimized_skills"]) == 1
        assert result["optimized_skills"][0]["nom"] == "React"

    @pytest.mark.asyncio
    async def test_output_skills_have_required_keys(self, sample_profile):
        """Chaque compétence dans la sortie a les clés nom, niveau, type_competence, matched."""
        state = _make_optimizer_state(sample_profile)
        result = await skill_optimizer_node(state)

        for skill in result["optimized_skills"]:
            assert "nom"             in skill, "Clé 'nom' manquante"
            assert "niveau"          in skill, "Clé 'niveau' manquante"
            assert "type_competence" in skill, "Clé 'type_competence' manquante"
            assert "matched"         in skill, "Clé 'matched' manquante"

    @pytest.mark.asyncio
    async def test_message_is_added_to_state(self, sample_profile):
        """Un AIMessage de skill_optimizer est ajouté aux messages."""
        state = _make_optimizer_state(sample_profile)
        result = await skill_optimizer_node(state)

        assert len(result["messages"]) == 1
        msg = result["messages"][0]
        assert getattr(msg, "name", None) == "skill_optimizer"


# ─────────────────────────────────────────────────────────────────────────────
# Class 2 — TestCvStructurer (Node 3)
# ─────────────────────────────────────────────────────────────────────────────

class TestCvStructurer:
    """Tests de cv_structurer_node() — transformation vers le schéma QuestPDF."""

    def _run_structurer(self, profile, skills=None, match_result=None, ats_coverage=None, template="modern"):
        """Helper pour appeler le node de manière synchrone dans les tests."""
        import asyncio
        skills = skills or []
        state = _make_structurer_state(profile, skills, ats_coverage=ats_coverage, match_result=match_result, template_slug=template)
        return asyncio.get_event_loop().run_until_complete(cv_structurer_node(state))

    def test_candidate_section_is_built(self, sample_profile):
        """La section Candidate contient les clés attendues par QuestPDF."""
        result = self._run_structurer(sample_profile)
        candidate = result["cv_json"]["Candidate"]

        assert "Name"      in candidate
        assert "Email"     in candidate
        assert "Phone"     in candidate
        assert "Location"  in candidate
        assert "LinkedIn"  in candidate
        assert "GitHub"    in candidate
        assert "Portfolio" in candidate

    def test_name_is_firstname_lastname_concatenated(self, sample_profile):
        """Name = prenom + espace + nom."""
        result = self._run_structurer(sample_profile)
        name = result["cv_json"]["Candidate"]["Name"]
        assert "Jean" in name
        assert "Dupont" in name

    def test_skills_schema_matches_csharp_model(self, sample_profile):
        """Les compétences ont les clés exactes du modèle C# : Name, Level, IsMatched."""
        optimized = [
            {"nom": "React",  "niveau": 5, "type_competence": "Technical", "matched": True},
            {"nom": "Docker", "niveau": 3, "type_competence": "Technical", "matched": False},
        ]
        result = self._run_structurer(sample_profile, skills=optimized)
        for skill in result["cv_json"]["Skills"]:
            assert "Name"      in skill, "Clé 'Name' manquante"
            assert "Level"     in skill, "Clé 'Level' manquante"
            assert "IsMatched" in skill, "Clé 'IsMatched' manquante"

    def test_matched_flag_propagated_to_cv_json(self, sample_profile):
        """IsMatched=True dans cv_json si la compétence était matchée."""
        optimized = [
            {"nom": "React", "niveau": 5, "type_competence": "Technical", "matched": True},
            {"nom": "Python","niveau": 2, "type_competence": "Technical", "matched": False},
        ]
        result = self._run_structurer(sample_profile, skills=optimized)
        skills = result["cv_json"]["Skills"]

        react  = next(s for s in skills if s["Name"] == "React")
        python = next(s for s in skills if s["Name"] == "Python")

        assert react["IsMatched"]  is True
        assert python["IsMatched"] is False

    def test_experience_description_split_into_bullets(self):
        """Description multi-ligne dans experiences est divisée en Bullets."""
        profile = {
            "experiences": [{
                "titre": "Dev Senior",
                "entreprise": "Corp",
                "date_debut": "2021-01",
                "date_fin": None,
                "description": "Développement React.\nDéploiement Docker.\nCI/CD GitHub Actions.",
            }]
        }
        result = self._run_structurer(profile)
        bullets = result["cv_json"]["Experience"][0]["Bullets"]
        assert len(bullets) == 3
        assert "Développement React." in bullets

    def test_education_year_is_string(self, sample_profile):
        """L'année de formation est convertie en string dans Education."""
        result = self._run_structurer(sample_profile)
        education = result["cv_json"]["Education"]
        assert len(education) > 0
        assert isinstance(education[0]["Year"], str)

    def test_certifications_formatted_as_strings(self, sample_profile):
        """Certifications = liste de chaînes 'nom — organisme'."""
        result = self._run_structurer(sample_profile)
        certs = result["cv_json"]["Certifications"]
        assert len(certs) > 0
        assert isinstance(certs[0], str)
        assert "—" in certs[0] or "AWS" in certs[0]

    def test_projects_have_required_keys(self, sample_profile):
        """Chaque projet a Title, Description, Bullets."""
        result = self._run_structurer(sample_profile)
        for project in result["cv_json"]["Projects"]:
            assert "Title"       in project
            assert "Description" in project
            assert "Bullets"     in project

    def test_ats_score_comes_from_match_result(self, sample_profile, sample_match_result):
        """AtsScore dans cv_json = score_ats du match_result."""
        result = self._run_structurer(sample_profile, match_result=sample_match_result)
        assert result["cv_json"]["AtsScore"] == sample_match_result["score_ats"]

    def test_matching_score_comes_from_match_result(self, sample_profile, sample_match_result):
        """MatchingScore dans cv_json = score_matching du match_result."""
        result = self._run_structurer(sample_profile, match_result=sample_match_result)
        assert result["cv_json"]["MatchingScore"] == sample_match_result["score_matching"]

    def test_empty_profile_returns_valid_structure(self):
        """Même avec un profil vide, cv_json a toutes les clés de niveau supérieur."""
        result = self._run_structurer({})
        cv = result["cv_json"]
        required_keys = ["Candidate", "Summary", "Skills", "Experience", "Education",
                         "Certifications", "Projects", "Languages", "Activities",
                         "AtsScore", "MatchingScore", "AtsCoveragePct"]
        for key in required_keys:
            assert key in cv, f"Clé '{key}' manquante dans cv_json"

    def test_message_added_with_correct_agent_name(self, sample_profile):
        """Un AIMessage nommé 'cv_structurer' est ajouté aux messages."""
        result = self._run_structurer(sample_profile)
        assert len(result["messages"]) == 1
        assert getattr(result["messages"][0], "name", None) == "cv_structurer"


# ─────────────────────────────────────────────────────────────────────────────
# Class 3 — TestProfileLoader (Node 1, DB mockée)
# ─────────────────────────────────────────────────────────────────────────────

class TestProfileLoader:
    """Tests de profile_loader_node() — DB mocké via unittest.mock."""

    @pytest.mark.asyncio
    async def test_loads_profile_and_returns_raw_profile(self, sample_profile):
        """Quand la DB retourne un profil, raw_profile est correctement rempli."""
        with patch(
            "app.domain.cv_engine.agents.profile_loader.load_full_profile",
            new_callable=lambda: type("T", (), {"ainvoke": AsyncMock(return_value=sample_profile)})
        ):
            state = {"user_id": "test-user-uuid-1234", "messages": [], "errors": []}
            result = await profile_loader_node(state)

            assert result["raw_profile"] is not None
            assert result["raw_profile"]["nom"] == "Dupont"
            assert result["raw_profile"]["prenom"] == "Jean"
            assert len(result["raw_profile"]["competences"]) > 0

    @pytest.mark.asyncio
    async def test_message_contains_profile_summary(self, sample_profile):
        """Un AIMessage résumant le profil est ajouté aux messages."""
        with patch(
            "app.domain.cv_engine.agents.profile_loader.load_full_profile",
            new_callable=lambda: type("T", (), {"ainvoke": AsyncMock(return_value=sample_profile)})
        ):
            state = {"user_id": "test-user-uuid-1234", "messages": [], "errors": []}
            result = await profile_loader_node(state)

            assert len(result["messages"]) == 1
            msg = result["messages"][0]
            assert getattr(msg, "name", None) == "profile_loader"
            assert "Dupont" in str(msg.content) or "compétences" in str(msg.content)

    @pytest.mark.asyncio
    async def test_missing_user_id_returns_empty_profile(self):
        """user_id vide → raw_profile vide et un message d'erreur."""
        state = {"user_id": "", "messages": [], "errors": []}
        result = await profile_loader_node(state)

        assert result["raw_profile"]["competences"] == []
        assert result["raw_profile"]["experiences"] == []
        assert len(result["errors"]) > 0
        assert "user_id" in result["errors"][0].lower() or "manquant" in result["errors"][0].lower()

    @pytest.mark.asyncio
    async def test_db_exception_returns_fallback_not_crash(self):
        """Si la DB lève une exception, le node retourne un fallback (pas de crash)."""
        with patch(
            "app.domain.cv_engine.agents.profile_loader.load_full_profile",
            new_callable=lambda: type("T", (), {"ainvoke": AsyncMock(side_effect=ConnectionError("DB unreachable"))})
        ):
            state = {"user_id": "test-user-uuid-1234", "messages": [], "errors": []}
            result = await profile_loader_node(state)

            # Ne doit pas lever une exception
            assert "raw_profile" in result
            assert result["raw_profile"]["competences"] == []
            assert len(result["errors"]) > 0
            assert "DB unreachable" in result["errors"][0] or "ProfileLoader" in result["errors"][0]


# ─────────────────────────────────────────────────────────────────────────────
# Class 4 — TestCvEngineService (Pipeline complet, DB mockée)
# ─────────────────────────────────────────────────────────────────────────────

class TestCvEngineService:
    """Tests d'intégration du service — pipeline Nodes 1→2→3 complet (DB mockée)."""

    @pytest.mark.asyncio
    async def test_full_pipeline_returns_cv_json(self, sample_profile, sample_match_result):
        """prepare_cv_data() retourne un cv_json avec toutes les sections."""
        from app.domain.cv_engine.service import CvEngineService

        with patch(
            "app.domain.cv_engine.agents.profile_loader.load_full_profile",
            new_callable=lambda: type("T", (), {"ainvoke": AsyncMock(return_value=sample_profile)})
        ):
            service = CvEngineService()
            result = await service.prepare_cv_data(
                user_id="test-user-uuid-1234",
                template_slug="modern",
                match_result=sample_match_result,
            )

        assert "cv_json" in result
        cv = result["cv_json"]
        assert "Candidate"   in cv
        assert "Skills"      in cv
        assert "Experience"  in cv
        assert "Education"   in cv

    @pytest.mark.asyncio
    async def test_pipeline_with_offer_puts_matched_skills_first(self, sample_profile, sample_match_result, sample_analyzed_offer):
        """Avec match_result fourni, les compétences matchées sont en tête dans cv_json."""
        from app.domain.cv_engine.service import CvEngineService

        with patch(
            "app.domain.cv_engine.agents.profile_loader.load_full_profile",
            new_callable=lambda: type("T", (), {"ainvoke": AsyncMock(return_value=sample_profile)})
        ):
            service = CvEngineService()
            result = await service.prepare_cv_data(
                user_id="test-user-uuid-1234",
                template_slug="modern",
                offer_data=sample_analyzed_offer,
                match_result=sample_match_result,
            )

        skills = result["cv_json"]["Skills"]
        matched   = [s for s in skills if s["IsMatched"]]
        unmatched = [s for s in skills if not s["IsMatched"]]

        if matched and unmatched:
            last_matched   = max(skills.index(s) for s in matched)
            first_unmatched = min(skills.index(s) for s in unmatched)
            assert last_matched < first_unmatched

    @pytest.mark.asyncio
    async def test_pipeline_without_offer_completes_successfully(self, sample_profile):
        """Sans offre ni match_result, le pipeline se termine sans erreur."""
        from app.domain.cv_engine.service import CvEngineService

        with patch(
            "app.domain.cv_engine.agents.profile_loader.load_full_profile",
            new_callable=lambda: type("T", (), {"ainvoke": AsyncMock(return_value=sample_profile)})
        ):
            service = CvEngineService()
            result = await service.prepare_cv_data(
                user_id="test-user-uuid-1234",
                template_slug="classic",
            )

        assert "cv_json" in result
        assert result["cv_json"] is not None

    @pytest.mark.asyncio
    async def test_ats_coverage_included_in_service_result(self, sample_profile, sample_analyzed_offer):
        """Le résultat du service contient ats_coverage avec percentage et keywords."""
        from app.domain.cv_engine.service import CvEngineService

        with patch(
            "app.domain.cv_engine.agents.profile_loader.load_full_profile",
            new_callable=lambda: type("T", (), {"ainvoke": AsyncMock(return_value=sample_profile)})
        ):
            service = CvEngineService()
            result = await service.prepare_cv_data(
                user_id="test-user-uuid-1234",
                template_slug="modern",
                offer_data=sample_analyzed_offer,
            )

        assert "ats_coverage" in result
        cov = result["ats_coverage"]
        assert "percentage"        in cov
        assert "keywords_present"  in cov
        assert "keywords_missing"  in cov

    @pytest.mark.asyncio
    async def test_errors_list_is_empty_on_success(self, sample_profile):
        """Quand tout se passe bien, errors est une liste vide."""
        from app.domain.cv_engine.service import CvEngineService

        with patch(
            "app.domain.cv_engine.agents.profile_loader.load_full_profile",
            new_callable=lambda: type("T", (), {"ainvoke": AsyncMock(return_value=sample_profile)})
        ):
            service = CvEngineService()
            result = await service.prepare_cv_data(
                user_id="test-user-uuid-1234",
                template_slug="modern",
            )

        assert result["errors"] == [], f"Des erreurs inattendues : {result['errors']}"
