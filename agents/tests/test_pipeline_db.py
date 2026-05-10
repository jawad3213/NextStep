import pytest
import uuid
from unittest.mock import AsyncMock, MagicMock, patch
from app.domain.pipeline.workflow import db_persist_node
from app.domain.pipeline.state import PipelineState
from langchain_core.messages import AIMessage

@pytest.mark.asyncio
async def test_db_persist_node_saves_data():
    """Test that db_persist_node saves data to the database using mocks."""
    
    # 1. Prepare mock state
    offer_id = str(uuid.uuid4())
    user_id = str(uuid.uuid4())
    
    state: PipelineState = {
        "offer_id": offer_id,
        "user_id": user_id,
        "raw_offer_text": "Need a Python dev.",
        "analyzed_offer": {
            "titre": "Python Developer",
            "entreprise": "Tech Inc",
            "competences_requises": ["Python", "SQL"],
        },
        "match_result": {
            "score_matching": 85,
            "competences_matching": ["Python"],
            "competences_manquantes": ["SQL"],
        }
    }
    
    # 2. Mock DB session
    mock_db = AsyncMock()
    mock_db.add = MagicMock()
    mock_db.commit = AsyncMock()
    
    # Mock the context manager
    mock_session_factory = MagicMock()
    mock_session_factory.return_value.__aenter__.return_value = mock_db
    
    # 3. Patch AsyncSessionFactory
    with patch("app.core.database.AsyncSessionFactory", mock_session_factory):
        result = await db_persist_node(state)
        
    # 4. Assertions
    assert "messages" in result
    assert "[Pipeline] Résultats sauvegardés en DB" in result["messages"][0].content
    
    # Check that db.add was called for both models
    assert mock_db.add.call_count == 2
    
    calls = mock_db.add.call_args_list
    first_obj = calls[0][0][0]
    second_obj = calls[1][0][0]
    
    from app.domain.chatbot.models import OffreAnalysee, ResultatMatching
    assert isinstance(first_obj, OffreAnalysee)
    assert isinstance(second_obj, ResultatMatching)
    
    assert str(first_obj.id_offre) == offer_id
    assert first_obj.titre_poste == "Python Developer"
    assert first_obj.entreprise == "Tech Inc"
    
    assert str(second_obj.id_offre) == offer_id
    assert str(second_obj.id_utilisateur) == user_id
    assert second_obj.score_global == 85

@pytest.mark.asyncio
async def test_db_persist_node_skips_if_no_offer_id():
    """Test that db_persist_node skips saving if offer_id is missing."""
    state: PipelineState = {
        "analyzed_offer": {"titre": "Python Developer"}
    }
    
    result = await db_persist_node(state)
    
    assert "messages" in result
    assert "DB save skipped" in result["messages"][0].content
