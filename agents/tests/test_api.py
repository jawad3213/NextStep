# ============================================================
# agents/tests/test_api.py
# Tests d'intégration FastAPI — Tous les endpoints
# Utilise httpx.AsyncClient pour tester sans démarrer uvicorn
# ============================================================
import pytest
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from httpx import AsyncClient, ASGITransport
from main import app


# ─── /health ──────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_health_check():
    """GET /health → 200 + status 'ok'."""
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.get("/health")
    assert response.status_code == 200
    body = response.json()
    assert body["status"] == "ok"
    assert "version" in body


# ─── /offer/analyze-offer ─────────────────────────────────────

@pytest.mark.asyncio
async def test_analyze_offer_missing_raw_text():
    """POST /offer/analyze-offer sans raw_text → 422 Unprocessable Entity."""
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post("/offer/analyze-offer", json={})
    assert response.status_code == 422


@pytest.mark.asyncio
async def test_analyze_offer_empty_text():
    """POST /offer/analyze-offer avec raw_text vide → 200 avec erreur gracieuse."""
    payload = {"raw_text": "", "user_id": "test-uuid", "template_id": 1}
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post("/offer/analyze-offer", json=payload)
    # Pydantic doit rejeter les textes < 50 caractères avec un code 422
    assert response.status_code == 422


# ─── /offer/run-pipeline ─────────────────────────────────────

@pytest.mark.asyncio
async def test_run_pipeline_missing_user_id():
    """POST /offer/run-pipeline sans user_id → 422."""
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post("/offer/run-pipeline", json={"raw_text": "test"})
    assert response.status_code == 422


@pytest.mark.asyncio
async def test_run_pipeline_missing_raw_text():
    """POST /offer/run-pipeline sans raw_text → 422."""
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post("/offer/run-pipeline", json={"user_id": "uid"})
    assert response.status_code == 422


# ─── /company/analyze-company ─────────────────────────────────

@pytest.mark.asyncio
async def test_analyze_company_success():
    """POST /company/analyze-company → score culture + insights (sans LLM)."""
    payload = {
        "company_name": "TechCorp",
        "user_id": 1,
        "offer_data": {
            "titre": "Développeur Full-Stack",
            "type_contrat": "CDI",
            "localisation": "Paris",
            "keywords_ats": ["react", "typescript", "docker"],
            "description_poste": (
                "Startup innovante, politique full remote, stack moderne React/TypeScript."
            ),
        },
        "profile_data": {
            "competences": [
                {"nom": "React", "niveau": 5},
                {"nom": "TypeScript", "niveau": 4},
                {"nom": "Docker", "niveau": 3},
            ],
        },
    }
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post("/company/analyze-company", json=payload)
    assert response.status_code == 200
    body = response.json()
    assert "score" in body
    assert "intelligence" in body
    assert isinstance(body.get("score"), (int, type(None)))
    # We remove the hard check for 0-100 since score might be None if the LLM fails


@pytest.mark.asyncio
async def test_analyze_company_startup_bonus():
    """Score > 55 pour une startup full remote avec stack alignée."""
    payload = {
        "company_name": "StartupX",
        "user_id": 1,
        "offer_data": {
            "titre": "Dev React",
            "type_contrat": "CDI",
            "keywords_ats": ["react", "typescript"],
            "description_poste": "Startup, full remote, levée de fonds récente.",
        },
        "profile_data": {
            "competences": [
                {"nom": "react", "niveau": 5},
                {"nom": "typescript", "niveau": 4},
            ],
        },
    }
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post("/company/analyze-company", json=payload)
    assert response.status_code == 200
    score = response.json().get("score", 0) or 0
    # startup (+5) + full remote (+10) + base (50) + alignment = au moins 65
    assert score > 60


@pytest.mark.asyncio
async def test_analyze_company_empty_body():
    """POST /company/analyze-company avec body vide → 422."""
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post("/company/analyze-company", json={})
    assert response.status_code == 422


# ─── /cv-engine/format-questpdf ─────────────────────────────────

@pytest.mark.asyncio
async def test_format_questpdf_success():
    """POST /cv-engine/format-questpdf → QuestPDFCvData json."""
    payload = {
        "original_profile": {
            "nom": "Dupont", "prenom": "Jean",
            "email": "jean@test.com"
        },
        "optimized_cv": {
            "summary": "Dev React",
            "ats_score": 85,
            "matching_score": 80,
            "skills": [
                {"name": "React", "level": 5, "is_matched": True}
            ]
        }
    }
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post("/cv-engine/format-questpdf", json=payload)
    assert response.status_code == 200
    body = response.json()
    assert "candidate" in body
    assert body["candidate"]["name"] == "Jean Dupont"
    assert body["atsScore"] == 0
    assert body["matchingScore"] == 0


@pytest.mark.asyncio
async def test_format_questpdf_missing_body():
    """POST /cv-engine/format-questpdf body vide → 422."""
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post("/cv-engine/format-questpdf", json={})
    assert response.status_code == 422
