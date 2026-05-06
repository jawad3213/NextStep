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
        "user_id": "test-uuid",
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
    assert "company_culture_score" in body
    assert "company_insights" in body
    assert isinstance(body["company_culture_score"], int)
    assert 0 <= body["company_culture_score"] <= 100


@pytest.mark.asyncio
async def test_analyze_company_startup_bonus():
    """Score > 55 pour une startup full remote avec stack alignée."""
    payload = {
        "company_name": "StartupX",
        "user_id": "test-uuid",
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
    score = response.json()["company_culture_score"]
    # startup (+5) + full remote (+10) + base (50) + alignment = au moins 65
    assert score > 60


@pytest.mark.asyncio
async def test_analyze_company_empty_body():
    """POST /company/analyze-company avec body vide → 422."""
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post("/company/analyze-company", json={})
    assert response.status_code == 422


# ─── /job/prepare-cv-data ─────────────────────────────────────

@pytest.mark.asyncio
async def test_prepare_cv_data_success():
    """POST /job/prepare-cv-data → cv_template_json complet."""
    payload = {
        "user_id": "test-uuid",
        "profile_data": {
            "nom": "Dupont", "prenom": "Jean",
            "titre": "Dev Full-Stack React",
            "resume": "4 ans d'expérience React Node.js",
            "email": "jean@test.com",
            "competences": [
                {"nom": "React", "niveau": 5},
                {"nom": "TypeScript", "niveau": 4},
                {"nom": "Docker", "niveau": 3},
            ],
            "experiences": [
                {"titre": "Dev Senior", "entreprise": "TechCorp",
                 "date_debut": "2020-01-01", "date_fin": None,
                 "description": "Développement React et Node.js"},
            ],
            "formations": [
                {"diplome": "Master Info", "etablissement": "Université Paris", "annee": 2019},
            ],
            "certifications": [
                {"nom": "AWS Developer", "organisme": "Amazon"},
            ],
            "projets": [
                {"titre": "Mon App", "description": "App React", "technologies": ["React", "Node"]},
            ],
        },
        "offer_data": {
            "titre": "Développeur Full-Stack",
            "entreprise": "StartupZ",
            "type_contrat": "CDI",
        },
        "match_result": {
            "score_matching": 75,
            "score_ats": 68,
            "competences_matching": ["React", "TypeScript"],
        },
        "template_id": 2,
    }
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post("/job/prepare-cv-data", json=payload)
    assert response.status_code == 200
    body = response.json()
    cv = body
    # Vérifier les sections
    assert "sections" in cv
    assert "metadata" in cv
    assert cv["sections"]["entete"]["nom"] == "Dupont"
    assert cv["metadata"]["template_id"] == 2
    assert cv["metadata"]["poste_vise"] == "Développeur Full-Stack"
    assert cv["metadata"]["score_matching"] == 75
    # Compétences
    comps = cv["sections"]["competences"]
    assert len(comps) == 3
    # Expériences
    assert len(cv["sections"]["experiences"]) == 1
    # Formations
    assert len(cv["sections"]["formations"]) == 1


@pytest.mark.asyncio
async def test_prepare_cv_data_matched_skills_first():
    """Les compétences matchées doivent apparaître en premier."""
    payload = {
        "user_id": "test-uuid",
        "profile_data": {
            "competences": [
                {"nom": "Python", "niveau": 3},    # non matchée
                {"nom": "React", "niveau": 5},     # matchée
                {"nom": "Docker", "niveau": 2},    # matchée
            ],
        },
        "offer_data": {},
        "match_result": {"competences_matching": ["react", "docker"]},
        "template_id": 1,
    }
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post("/job/prepare-cv-data", json=payload)
    assert response.status_code == 200
    comps = response.json()["sections"]["competences"]
    # Les 2 premiers doivent être matchés
    assert comps[0]["matched"] is True
    assert comps[1]["matched"] is True
    assert comps[2]["matched"] is False


@pytest.mark.asyncio
async def test_prepare_cv_data_missing_body():
    """POST /job/prepare-cv-data body vide → 422."""
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post("/job/prepare-cv-data", json={})
    assert response.status_code == 422
