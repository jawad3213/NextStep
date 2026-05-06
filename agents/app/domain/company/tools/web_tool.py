# ============================================================
# app/domain/company/tools/web_tool.py
# ============================================================

import logging
import asyncio
from tavily import TavilyClient
from app.core.config import settings

logger = logging.getLogger(__name__)

# Initialisation du client Tavily
tavily = None
if settings.TAVILY_API_KEY:
    tavily = TavilyClient(api_key=settings.TAVILY_API_KEY)

async def smart_search(query: str, max_results: int = 5) -> list[dict]:
    """Recherche professionnelle via l'API Tavily (spécialisée pour les agents IA)."""
    if not tavily:
        logger.error("❌ TAVILY_API_KEY manquante dans le .env")
        return []

    logger.info(f"🚀 Tavily Search: {query}")
    try:
        # Exécution dans un thread séparé car le client Tavily est synchrone
        loop = asyncio.get_event_loop()
        response = await loop.run_in_executor(
            None, 
            lambda: tavily.search(
                query=query, 
                search_depth="advanced", 
                max_results=max_results,
                include_answer=True # Récupère aussi une réponse synthétisée
            )
        )
        
        results = []
        for r in response.get("results", []):
            results.append({
                "title": r.get("title", ""),
                "url": r.get("url", ""),
                "snippet": r.get("content", ""), # Tavily renvoie du contenu déjà extrait
                "raw_content": r.get("raw_content")
            })
            
        if response.get("answer"):
            # On ajoute la réponse synthétisée de Tavily comme un résultat spécial
            results.insert(0, {
                "title": "Tavily AI Synthesis",
                "url": "https://tavily.com",
                "snippet": response.get("answer")
            })

        logger.info(f"✅ Tavily a trouvé {len(results)} sources pertinentes.")
        return results
            
    except Exception as e:
        logger.error(f"❌ Erreur API Tavily: {e}")
        return []

async def high_precision_scrape(url: str) -> str:
    """Extraction de contenu via l'API Tavily (Extract)."""
    if not tavily: return ""
    
    logger.info(f"📄 Tavily Extract: {url}")
    try:
        loop = asyncio.get_event_loop()
        response = await loop.run_in_executor(
            None, 
            lambda: tavily.extract(urls=[url])
        )
        
        # Tavily renvoie le texte propre, sans pub ni scripts
        results = response.get("results", [])
        if results:
            return results[0].get("raw_content", "")[:10000]
            
    except Exception as e:
        logger.warning(f"⚠️ Erreur extraction Tavily pour {url}: {e}")
        
    return ""
