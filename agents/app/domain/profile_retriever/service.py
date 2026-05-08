import logging
import time
from functools import wraps
from typing import Dict, Any, Tuple
from app.domain.profile_retriever.graph.workflow import build_profile_retriever_workflow
from app.domain.profile_retriever.schemas.state import ProfileRetrieverState

logger = logging.getLogger(__name__)

def async_ttl_cache(ttl_seconds: int = 300):
    """Décorateur simple pour cacher les résultats de fonctions async avec un TTL."""
    cache: Dict[str, Tuple[float, Any]] = {}

    def decorator(func):
        @wraps(func)
        async def wrapper(self, user_id: str, *args, **kwargs):
            now = time.time()
            
            # Vérifier si on a un hit en cache et s'il est encore valide
            if user_id in cache:
                timestamp, result = cache[user_id]
                if now - timestamp < ttl_seconds:
                    logger.info(f"[Cache] Hit pour user_id={user_id}")
                    return result
            
            # Sinon, exécuter la fonction et mettre en cache
            logger.info(f"[Cache] Miss pour user_id={user_id}. Récupération DB...")
            result = await func(self, user_id, *args, **kwargs)
            cache[user_id] = (now, result)
            return result
        return wrapper
    return decorator

class ProfileRetrieverService:
    """Service pour la récupération du profil candidat."""

    def __init__(self):
        # On compile le workflow une seule fois au démarrage (Singleton)
        self._workflow = build_profile_retriever_workflow()

    @async_ttl_cache(ttl_seconds=300) # Cache de 5 minutes
    async def get_profile(self, user_id: str) -> dict:
        """Récupère le profil complet depuis la DB avec un mécanisme de cache TTL."""
        initial_state: ProfileRetrieverState = {
            "user_id": user_id,
            "messages": [],
            "errors": []
        }
        
        final_state = await self._workflow.ainvoke(initial_state)
        
        return {
            "profile_data": final_state.get("profile_data"),
            "errors":       final_state.get("errors")
        }

profile_retriever_service = ProfileRetrieverService()
