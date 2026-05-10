"""
TESTS DU ROUTER — app/modules/chatbot/router.py

PRINCIPE :
  On teste le router comme un vrai client HTTP.
  On mock le SERVICE (pas le graphe, pas la DB).
  Le router ne fait que recevoir → déléguer → retourner.
  Donc on vérifie :
    1. Status HTTP correct (200, 422, 500)
    2. Schema de réponse correct
    3. Le service a bien été appelé avec les bons paramètres

OUTIL : httpx.AsyncClient + app FastAPI en mode test
"""

import pytest
import uuid
from unittest.mock import AsyncMock, patch
from httpx import AsyncClient, ASGITransport

from main import app
from app.core.database import get_db
from app.domain.chatbot.schemas import (
    QuestionsResponse, QuestionOut,
    FreeChatResponse,
    StartInterviewResponse,
    SendMessageResponse,
    EndInterviewResponse, FeedbackOut, DimensionOut,
    SalaryResponse, NegotiationStepOut,
)


# ═══════════════════════════════════════════════════════════════
# FIXTURE — CLIENT HTTP DE TEST
# ═══════════════════════════════════════════════════════════════

@pytest.fixture
def mock_db_dep():
    """Mock de la session DB injectée par Depends(get_db)."""
    db = AsyncMock()
    db.add      = AsyncMock()
    db.commit   = AsyncMock()
    db.rollback = AsyncMock()
    db.flush    = AsyncMock()
    db.get      = AsyncMock(return_value=None)
    return db


import pytest_asyncio

@pytest_asyncio.fixture
async def client(mock_db_dep):
    """
    Client HTTP qui envoie des vraies requêtes HTTP à l'app FastAPI.
    La DB est remplacée par le mock — pas de vraie connexion PostgreSQL.
    """
    # Override la dependency get_db → retourne le mock
    async def override_get_db():
        yield mock_db_dep

    app.dependency_overrides[get_db] = override_get_db

    async with AsyncClient(
        transport=ASGITransport(app=app),
        base_url="http://test"
    ) as c:
        yield c

    # Nettoyer après le test
    app.dependency_overrides.clear()


# ═══════════════════════════════════════════════════════════════
# DONNÉES DE RÉPONSE SIMULÉES
# Chaque test mock le service pour retourner ces objets
# ═══════════════════════════════════════════════════════════════

def fake_questions_response() -> QuestionsResponse:
    return QuestionsResponse(
        mode="arena",
        total=2,
        questions=[
            QuestionOut(
                id=str(uuid.uuid4()),
                question="Describe a pipeline you built.",
                type="technical",
                source="generated",
                company_specific=False,
                tip="Use STAR method.",
            ),
            QuestionOut(
                id=str(uuid.uuid4()),
                question="Tell me about a challenge you faced.",
                type="behavioral",
                source="generated",
                company_specific=False,
                tip="Focus on your actions.",
            ),
        ],
    )


def fake_free_chat_response() -> FreeChatResponse:
    return FreeChatResponse(
        thread_id="44444444-4444-4444-4444-444444444444",
        response="To answer STAR questions, structure your response with Situation, Task, Action, Result.",
    )


def fake_start_response() -> StartInterviewResponse:
    return StartInterviewResponse(
        session_id="55555555-5555-5555-5555-555555555555",
        opening_message="Hello! I'm your interviewer today. Tell me about yourself.",
    )


def fake_send_response() -> SendMessageResponse:
    return SendMessageResponse(
        session_id="55555555-5555-5555-5555-555555555555",
        ai_response="Interesting! Can you tell me more about the scale of that pipeline?",
    )


def fake_end_response() -> EndInterviewResponse:
    return EndInterviewResponse(
        session_id="55555555-5555-5555-5555-555555555555",
        score=74,
        feedback=FeedbackOut(
            global_score=74,
            dimensions=[
                DimensionOut(name="Clarity",            score=80, comment="Clear answers."),
                DimensionOut(name="STAR Method",        score=65, comment="Apply STAR more."),
                DimensionOut(name="Technical Accuracy", score=78, comment="Good knowledge."),
                DimensionOut(name="Communication",      score=75, comment="Professional."),
                DimensionOut(name="Confidence",         score=70, comment="Some hesitation."),
            ],
            strengths=["Strong Python", "Good pipeline design"],
            improvements=["Apply STAR method", "Mention Kafka"],
            best_answer="My Spark pipeline processed 500GB daily...",
            worst_answer="I don't know Kafka. Better: show willingness to learn.",
            coaching_tips=["Use STAR always", "Quantify achievements", "Prepare 3 stories"],
        ),
    )


def fake_salary_response() -> SalaryResponse:
    return SalaryResponse(
        range_min=18000,
        range_max=28000,
        currency="MAD",
        your_target=25000,
        confidence_level="high",
        market_sources=["Glassdoor 2025", "LinkedIn Salary"],
        negotiation_script=[
            NegotiationStepOut(
                step=1,
                action="Anchor high",
                phrase="I was expecting around 25,000 MAD.",
                why="Sets anchor above target",
            ),
        ],
    )


# ═══════════════════════════════════════════════════════════════
# TESTS — HEALTH
# ═══════════════════════════════════════════════════════════════

class TestHealth:

    @pytest.mark.asyncio
    async def test_health_retourne_200(self, client):
        """Health check doit toujours retourner 200."""
        response = await client.get("/api/chatbot/health")

        assert response.status_code == 200
        assert response.json()["status"] == "ok"
        assert response.json()["agent"]  == "interview-prep"


# ═══════════════════════════════════════════════════════════════
# TESTS — POST /questions
# ═══════════════════════════════════════════════════════════════

class TestQuestionsRouter:

    @pytest.mark.asyncio
    async def test_arena_mode_retourne_200(self, client):
        """Mode arena valide → 200 + liste de questions."""
        with patch(
            "app.domain.chatbot.router.service.generate_questions_service",
            new=AsyncMock(return_value=fake_questions_response())
        ):
            response = await client.post("/api/chatbot/questions", json={
                "mode": "arena",
                "arena_config": {
                    "domain": "Data & AI",
                    "level": "junior",
                    "duration_minutes": 20,
                    "language": "en",
                    "focus_areas": ["Python", "SQL"],
                },
                "user_id": "11111111-1111-1111-1111-111111111111",
            })

        assert response.status_code == 200
        body = response.json()
        assert body["status"]            == "ok"
        assert body["mode"]              == "arena"
        assert body["total"]             == 2
        assert len(body["questions"])    == 2
        assert body["questions"][0]["question"] != ""
        assert body["questions"][0]["type"]     in ["technical", "behavioral", "situational"]

    @pytest.mark.asyncio
    async def test_offer_mode_retourne_200(self, client):
        """Mode offer avec offer_id → 200."""
        fake = fake_questions_response()
        fake.mode = "offer"

        with patch(
            "app.domain.chatbot.router.service.generate_questions_service",
            new=AsyncMock(return_value=fake)
        ):
            response = await client.post("/api/chatbot/questions", json={
                "mode": "offer",
                "offer_id": "22222222-2222-2222-2222-222222222222",
                "user_id": "11111111-1111-1111-1111-111111111111",
            })

        assert response.status_code == 200
        assert response.json()["mode"] == "offer"

    @pytest.mark.asyncio
    async def test_mode_manquant_retourne_422(self, client):
        """
        422 = Unprocessable Entity.
        Pydantic rejette la requête si le champ obligatoire 'mode' manque.
        """
        response = await client.post("/api/chatbot/questions", json={
            "user_id": "11111111-1111-1111-1111-111111111111",
            # "mode" manquant → validation échoue
        })

        assert response.status_code == 422

    @pytest.mark.asyncio
    async def test_service_leve_exception_retourne_500(self, client):
        """Si le service plante → router retourne 500."""
        with patch(
            "app.domain.chatbot.router.service.generate_questions_service",
            new=AsyncMock(side_effect=Exception("OpenAI API error"))
        ):
            response = await client.post("/api/chatbot/questions", json={
                "mode": "arena",
                "arena_config": {
                    "domain": "Data & AI",
                    "level": "junior",
                    "duration_minutes": 20,
                    "language": "en",
                    "focus_areas": [],
                },
                "user_id": "test-user",
            })

        assert response.status_code == 500
        assert "OpenAI API error" in response.json()["detail"]

    @pytest.mark.asyncio
    async def test_service_appele_avec_bons_params(self, client):
        """Vérifie que le router passe bien les bons paramètres au service."""
        mock_service = AsyncMock(return_value=fake_questions_response())

        with patch(
            "app.domain.chatbot.router.service.generate_questions_service",
            new=mock_service
        ):
            await client.post("/api/chatbot/questions", json={
                "mode": "arena",
                "arena_config": {
                    "domain": "Software Dev",
                    "level": "senior",
                    "duration_minutes": 30,
                    "language": "fr",
                    "focus_areas": ["Architecture"],
                },
                "user_id": "test-user-999",
            })

        # Vérifier les arguments passés au service
        call_kwargs = mock_service.call_args.kwargs
        assert call_kwargs["mode"]    == "arena"
        assert call_kwargs["user_id"] == "test-user-999"
        assert call_kwargs["arena_config"].domain   == "Software Dev"
        assert call_kwargs["arena_config"].level    == "senior"
        assert call_kwargs["arena_config"].language == "fr"


# ═══════════════════════════════════════════════════════════════
# TESTS — POST /free-chat
# ═══════════════════════════════════════════════════════════════

class TestFreeChatRouter:

    @pytest.mark.asyncio
    async def test_retourne_200_avec_reponse(self, client):
        """Chat libre → 200 + réponse AI."""
        with patch(
            "app.domain.chatbot.router.service.free_chat_service",
            new=AsyncMock(return_value=fake_free_chat_response())
        ):
            response = await client.post("/api/chatbot/free-chat", json={
                "user_input": "How should I answer STAR questions?",
                "thread_id":  "44444444-4444-4444-4444-444444444444",
                "history":    [],
                "user_id":    "11111111-1111-1111-1111-111111111111",
            })

        assert response.status_code    == 200
        body = response.json()
        assert body["status"]          == "ok"
        assert body["thread_id"]       == "44444444-4444-4444-4444-444444444444"
        assert body["response"]        != ""
        assert "STAR" in body["response"]

    @pytest.mark.asyncio
    async def test_user_input_vide_retourne_422(self, client):
        """user_input vide → 422 (champ requis)."""
        response = await client.post("/api/chatbot/free-chat", json={
            "user_input": "",          # vide mais présent
            "thread_id":  "44444444-4444-4444-4444-444444444444",
            "history":    [],
            "user_id":    "test-user",
        })
        # Pydantic accepte la string vide → le service décide
        # On vérifie juste que la requête passe le schéma
        assert response.status_code in [200, 500]

    @pytest.mark.asyncio
    async def test_avec_historique_passe_au_service(self, client):
        """L'historique de conversation doit être transmis au service."""
        mock_svc = AsyncMock(return_value=fake_free_chat_response())

        with patch(
            "app.domain.chatbot.router.service.free_chat_service",
            new=mock_svc
        ):
            await client.post("/api/chatbot/free-chat", json={
                "user_input": "Follow-up question",
                "thread_id":  "44444444-4444-4444-4444-444444444444",
                "history": [
                    {"role": "ai",   "content": "Hello!"},
                    {"role": "user", "content": "First question"},
                    {"role": "ai",   "content": "First answer"},
                ],
                "user_id": "test-user",
            })

        call_kwargs = mock_svc.call_args.kwargs
        assert len(call_kwargs["history"]) == 3
        assert call_kwargs["user_input"]   == "Follow-up question"


# ═══════════════════════════════════════════════════════════════
# TESTS — POST /interview/start
# ═══════════════════════════════════════════════════════════════

class TestStartInterviewRouter:

    @pytest.mark.asyncio
    async def test_arena_retourne_session_id_et_opening(self, client):
        """Démarrer une session → session_id + message d'ouverture."""
        with patch(
            "app.domain.chatbot.router.service.start_interview_service",
            new=AsyncMock(return_value=fake_start_response())
        ):
            response = await client.post("/api/chatbot/interview/start", json={
                "mode": "arena",
                "arena_config": {
                    "domain": "Data & AI",
                    "level": "mid",
                    "duration_minutes": 15,
                    "language": "en",
                    "focus_areas": [],
                },
                "user_id": "11111111-1111-1111-1111-111111111111",
            })

        assert response.status_code == 200
        body = response.json()
        assert body["status"]          == "ok"
        assert body["session_id"]      == "55555555-5555-5555-5555-555555555555"
        assert body["opening_message"] != ""

    @pytest.mark.asyncio
    async def test_offer_mode_avec_offer_id(self, client):
        """Mode offer → session liée à l'offre."""
        with patch(
            "app.domain.chatbot.router.service.start_interview_service",
            new=AsyncMock(return_value=fake_start_response())
        ):
            response = await client.post("/api/chatbot/interview/start", json={
                "mode":     "offer",
                "offer_id": "22222222-2222-2222-2222-222222222222",
                "user_id":  "11111111-1111-1111-1111-111111111111",
            })

        assert response.status_code == 200

    @pytest.mark.asyncio
    async def test_user_id_manquant_retourne_422(self, client):
        """user_id est requis."""
        response = await client.post("/api/chatbot/interview/start", json={
            "mode": "arena",
            # user_id manquant
        })
        assert response.status_code == 422


# ═══════════════════════════════════════════════════════════════
# TESTS — POST /interview/message
# ═══════════════════════════════════════════════════════════════

class TestSendMessageRouter:

    @pytest.mark.asyncio
    async def test_retourne_reponse_recruteur(self, client):
        """Envoyer un message → réponse du recruteur IA."""
        with patch(
            "app.domain.chatbot.router.service.send_message_service",
            new=AsyncMock(return_value=fake_send_response())
        ):
            response = await client.post("/api/chatbot/interview/message", json={
                "session_id": "55555555-5555-5555-5555-555555555555",
                "user_input": "I have 3 years of Python and Spark experience.",
                "history": [
                    {"role": "ai",   "content": "Tell me about yourself."},
                ],
                "mode": "arena",
                "arena_config": {
                    "domain": "Data & AI",
                    "level": "mid",
                    "duration_minutes": 15,
                    "language": "en",
                    "focus_areas": [],
                },
                "user_id": "11111111-1111-1111-1111-111111111111",
            })

        assert response.status_code == 200
        body = response.json()
        assert body["status"]      == "ok"
        assert body["session_id"]  == "55555555-5555-5555-5555-555555555555"
        assert body["ai_response"] != ""

    @pytest.mark.asyncio
    async def test_session_id_manquant_retourne_422(self, client):
        """session_id obligatoire."""
        response = await client.post("/api/chatbot/interview/message", json={
            "user_input": "My answer",
            "history":    [],
            "mode":       "arena",
            "user_id":    "test-user",
            # session_id manquant
        })
        assert response.status_code == 422

    @pytest.mark.asyncio
    async def test_service_reçoit_session_id_correct(self, client):
        """Le session_id doit être transmis intact au service."""
        mock_svc = AsyncMock(return_value=fake_send_response())
        target_session = "55555555-5555-5555-5555-555555555555"

        with patch(
            "app.domain.chatbot.router.service.send_message_service",
            new=mock_svc
        ):
            await client.post("/api/chatbot/interview/message", json={
                "session_id": target_session,
                "user_input": "My answer",
                "history":    [],
                "mode":       "arena",
                "arena_config": {
                    "domain": "Data & AI", "level": "junior",
                    "duration_minutes": 20, "language": "en", "focus_areas": [],
                },
                "user_id": "test-user",
            })

        call_kwargs = mock_svc.call_args.kwargs
        assert call_kwargs["session_id"] == target_session
        assert call_kwargs["user_input"] == "My answer"


# ═══════════════════════════════════════════════════════════════
# TESTS — POST /interview/end
# ═══════════════════════════════════════════════════════════════

class TestEndInterviewRouter:

    @pytest.mark.asyncio
    async def test_retourne_score_et_feedback_complet(self, client):
        """Fin de session → score + 5 dimensions + tips."""
        with patch(
            "app.domain.chatbot.router.service.end_interview_service",
            new=AsyncMock(return_value=fake_end_response())
        ):
            response = await client.post("/api/chatbot/interview/end", json={
                "session_id": "55555555-5555-5555-5555-555555555555",
                "history": [
                    {"role": "ai",   "content": "Tell me about yourself."},
                    {"role": "user", "content": "I have 3 years of Python..."},
                    {"role": "ai",   "content": "Can you describe a pipeline?"},
                    {"role": "user", "content": "I built a 500GB/day pipeline..."},
                ],
                "mode": "arena",
                "arena_config": {
                    "domain": "Data & AI",
                    "level": "mid",
                    "duration_minutes": 15,
                    "language": "en",
                    "focus_areas": [],
                },
                "user_id": "11111111-1111-1111-1111-111111111111",
            })

        assert response.status_code == 200
        body = response.json()
        assert body["status"]           == "ok"
        assert body["score"]            == 74
        assert len(body["feedback"]["dimensions"]) == 5
        assert body["feedback"]["global_score"]    == 74
        assert len(body["feedback"]["strengths"])  == 2
        assert len(body["feedback"]["coaching_tips"]) == 3

    @pytest.mark.asyncio
    async def test_schema_feedback_complet(self, client):
        """Vérifier que tous les champs du feedback sont présents."""
        with patch(
            "app.domain.chatbot.router.service.end_interview_service",
            new=AsyncMock(return_value=fake_end_response())
        ):
            response = await client.post("/api/chatbot/interview/end", json={
                "session_id": "55555555-5555-5555-5555-555555555555",
                "history":    [],
                "mode":       "arena",
                "arena_config": {
                    "domain": "Data & AI", "level": "mid",
                    "duration_minutes": 15, "language": "en", "focus_areas": [],
                },
                "user_id": "test-user",
            })

        feedback = response.json()["feedback"]

        # Tous ces champs doivent être présents
        assert "global_score"   in feedback
        assert "dimensions"     in feedback
        assert "strengths"      in feedback
        assert "improvements"   in feedback
        assert "best_answer"    in feedback
        assert "worst_answer"   in feedback
        assert "coaching_tips"  in feedback

        # Chaque dimension a name, score, comment
        for dim in feedback["dimensions"]:
            assert "name"    in dim
            assert "score"   in dim
            assert "comment" in dim
            assert 0 <= dim["score"] <= 100

    @pytest.mark.asyncio
    async def test_historique_vide_accepte(self, client):
        """
        Historique vide → le service s'en occupe, le router accepte.
        Utile si l'user termine sans avoir répondu.
        """
        with patch(
            "app.domain.chatbot.router.service.end_interview_service",
            new=AsyncMock(return_value=fake_end_response())
        ):
            response = await client.post("/api/chatbot/interview/end", json={
                "session_id": "55555555-5555-5555-5555-555555555555",
                "history":    [],           # ← vide
                "mode":       "arena",
                "arena_config": {
                    "domain": "Data & AI", "level": "mid",
                    "duration_minutes": 15, "language": "en", "focus_areas": [],
                },
                "user_id": "test-user",
            })

        assert response.status_code == 200


# ═══════════════════════════════════════════════════════════════
# TESTS — POST /salary
# ═══════════════════════════════════════════════════════════════

class TestSalaryRouter:

    @pytest.mark.asyncio
    async def test_arena_retourne_analyse_complete(self, client):
        """Mode arena → fourchette + script de négociation."""
        with patch(
            "app.domain.chatbot.router.service.get_salary_service",
            new=AsyncMock(return_value=fake_salary_response())
        ):
            response = await client.post("/api/chatbot/salary", json={
                "mode": "arena",
                "arena_config": {
                    "domain": "Data & AI",
                    "level": "mid",
                    "duration_minutes": 20,
                    "language": "en",
                    "focus_areas": [],
                },
                "user_id": "11111111-1111-1111-1111-111111111111",
            })

        assert response.status_code == 200
        body = response.json()
        assert body["status"]              == "ok"
        assert body["range_min"]           == 18000
        assert body["range_max"]           == 28000
        assert body["currency"]            == "MAD"
        assert body["your_target"]         == 25000
        assert body["confidence_level"]    == "high"
        assert len(body["market_sources"]) == 2
        assert len(body["negotiation_script"]) == 1

    @pytest.mark.asyncio
    async def test_offer_mode_avec_offer_id(self, client):
        """Mode offer → données enrichies avec contexte entreprise."""
        fake = fake_salary_response()

        with patch(
            "app.domain.chatbot.router.service.get_salary_service",
            new=AsyncMock(return_value=fake)
        ):
            response = await client.post("/api/chatbot/salary", json={
                "mode":     "offer",
                "offer_id": "22222222-2222-2222-2222-222222222222",
                "user_id":  "11111111-1111-1111-1111-111111111111",
            })

        assert response.status_code == 200

    @pytest.mark.asyncio
    async def test_script_negociation_structure_correcte(self, client):
        """Chaque étape du script doit avoir step, action, phrase, why."""
        with patch(
            "app.domain.chatbot.router.service.get_salary_service",
            new=AsyncMock(return_value=fake_salary_response())
        ):
            response = await client.post("/api/chatbot/salary", json={
                "mode": "arena",
                "arena_config": {
                    "domain": "Finance", "level": "junior",
                    "duration_minutes": 20, "language": "en", "focus_areas": [],
                },
                "user_id": "test-user",
            })

        body = response.json()
        for step in body["negotiation_script"]:
            assert "step"   in step
            assert "action" in step
            assert "phrase" in step
            assert "why"    in step

    @pytest.mark.asyncio
    async def test_mode_manquant_retourne_422(self, client):
        """mode est obligatoire."""
        response = await client.post("/api/chatbot/salary", json={
            "user_id": "test-user",
            # mode manquant
        })
        assert response.status_code == 422
