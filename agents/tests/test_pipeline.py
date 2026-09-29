"""Offer pipeline (POST /offer/run-pipeline): which steps run, with services mocked (no LLM, no DB)."""
from unittest.mock import AsyncMock, patch

import pytest

from app.domain.cv_optimizer.schemas.models import OptimizedCVOutput
from app.domain.pipeline import workflow

OFFER = {
    "titre": "Développeuse Full Stack",
    "entreprise": "Acme",
    "competences_requises": ["Angular", "Java"],
    "competences_souhaitees": [],
    "keywords_ats": ["Angular"],
}
PROFILE = {
    "prenom": "Sara", "nom": "Alami", "email": "s@x.ma",
    "competences": [{"nom": "Angular"}],
    "experiences": [{"titre": "Stagiaire", "entreprise": "Globex", "description": "Front Angular", "taches": ["Développé le front"]}],
    "projets": [], "formations": [], "certifications": [], "activities": [],
}
OPTIMIZED = OptimizedCVOutput(
    resume_optimise={"contenu": "Développeuse Angular."},
    experiences_optimisees=[{"titre": "Stagiaire", "entreprise": "Globex", "description_optimisee": "Front",
                             "taches_optimisees": ["Développé le front Angular"], "mots_cles_cibles": [], "niveau_pertinence": "high"}],
    projets_optimises=[], formations_optimisees=[], certifications_optimisees=[],
    competences_reordonnees=["Angular"], competences_mises_en_avant=["Angular"],
)


def _no_database():
    raise RuntimeError("no database in tests")


@pytest.fixture
def mocks():
    with patch.object(workflow.offer_analyzer_service, "analyze", new=AsyncMock(return_value={"analyzed_offer": OFFER})) as analyze, \
         patch.object(workflow.profile_retriever_service, "get_profile", new=AsyncMock(return_value={"profile_data": PROFILE, "errors": []})) as profile, \
         patch.object(workflow.company_service, "get_company_intelligence", new=AsyncMock(return_value={"intelligence": {"nom": "Acme"}})) as company, \
         patch.object(workflow.cv_optimizer_service, "optimize_cv", new=AsyncMock(return_value=OPTIMIZED)) as optimize, \
         patch.object(workflow, "_email_composer_node", new=AsyncMock(return_value={"email_draft": {"subject": "s", "body": "b"}})) as email, \
         patch("app.core.database.AsyncSessionFactory", new=_no_database):
        yield {"analyze": analyze, "profile": profile, "company": company, "optimize": optimize, "email": email}


def _state(**overrides):
    state = {"raw_offer_text": "Offre Acme", "user_id": "u1", "template_id": 1, "offer_id": "not-a-uuid",
             "messages": [], "errors": [], "warnings": []}
    state.update(overrides)
    return state


@pytest.mark.asyncio
async def test_analysis_only_stops_before_the_cv(mocks):
    final = await workflow.build_offer_pipeline().ainvoke(_state(only_analysis=True))

    mocks["analyze"].assert_awaited_once()
    mocks["profile"].assert_awaited_once()
    mocks["optimize"].assert_not_awaited()
    gap = final["skill_gap_analysis"]
    assert gap["matched_skills"] == ["Angular"] and gap["missing_skills"] == ["Java"]
    assert gap["score_matching"] == 50


class _FakeSession:
    async def __aenter__(self):
        return self

    async def __aexit__(self, *exc):
        return False


@pytest.mark.asyncio
async def test_company_analysis_sent_by_the_backend_is_reused(mocks):
    given = {"intelligence": {"nom": "Acme", "summary": "déjà analysée"}}

    final = await workflow.build_offer_pipeline().ainvoke(
        _state(analyzed_offer=OFFER, profile_data=PROFILE, company_intelligence=given)
    )

    mocks["company"].assert_not_awaited()
    assert final["company_intelligence"] == given
    assert final["company_intelligence_source"] == "request"


@pytest.mark.asyncio
async def test_recent_company_analysis_is_taken_from_the_cache(mocks):
    cached = {"intelligence": {"nom": "Acme", "summary": "en cache"}}
    with patch("app.core.database.AsyncSessionFactory", new=_FakeSession), \
         patch.object(workflow.company_service, "find_cached_intelligence", new=AsyncMock(return_value=(cached, "2026-09-01"))):
        final = await workflow.build_offer_pipeline().ainvoke(_state(analyzed_offer=OFFER, profile_data=PROFILE))

    mocks["company"].assert_not_awaited()
    assert final["company_intelligence"] == cached
    assert final["company_intelligence_source"] == "cache"


@pytest.mark.asyncio
async def test_company_is_researched_when_nothing_is_known(mocks):
    with patch("app.core.database.AsyncSessionFactory", new=_FakeSession), \
         patch.object(workflow.company_service, "find_cached_intelligence", new=AsyncMock(return_value=None)):
        final = await workflow.build_offer_pipeline().ainvoke(_state(analyzed_offer=OFFER, profile_data=PROFILE))

    mocks["company"].assert_awaited_once()
    assert final["company_intelligence_source"] == "fresh"


@pytest.mark.asyncio
async def test_generation_reuses_the_given_analysis_and_builds_the_cv(mocks):
    final = await workflow.build_offer_pipeline().ainvoke(_state(analyzed_offer=OFFER, profile_data=PROFILE))

    mocks["analyze"].assert_not_awaited()
    mocks["profile"].assert_not_awaited()
    mocks["optimize"].assert_awaited_once()
    cv = final["cv_engine_result"]
    assert cv["candidate"]["name"] == "Sara Alami"
    assert [s["name"] for s in cv["skills"]] == ["Angular"] and cv["skills"][0]["isMatched"] is True
    assert final["email_draft"]["subject"] == "s"
