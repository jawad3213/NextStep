# ============================================================
# app/domain/company/tools/web_tool.py
# ============================================================

import logging
import asyncio
import re
from duckduckgo_search import DDGS
import httpx
from curl_cffi import requests as curl_requests

logger = logging.getLogger(__name__)

def _clean_html(text: str) -> str:
    if not text: return ""
    # Nettoyage équilibré
    text = re.sub(r'<(script|style|nav|footer|header|aside)[^>]*>([\s\S]*?)<\/\1>', ' ', text, flags=re.IGNORECASE)
    text = re.sub(r'<[^>]+>', ' ', text)
    text = text.replace("&nbsp;", " ").replace("&quot;", '"').replace("&amp;", "&")
    return re.sub(r'\s+', ' ', text).strip()

async def smart_search(query: str, max_results: int = 8) -> list[dict]:
    """Recherche ultra-robuste utilisant DDGS (TLS fingerprinting)."""
    logger.info(f"🌐 DDGS Search: {query}")
    results = []
    try:
        # DDGS est un context manager, il ferme ses ressources à la fin du 'with'
        with DDGS() as ddgs:
            ddgs_gen = ddgs.text(query, region="fr-fr", safesearch="off", timelimit="y")
            for r in ddgs_gen:
                results.append({
                    "title": r.get("title", ""),
                    "url": r.get("href", ""),
                    "snippet": r.get("body", "")
                })
                if len(results) >= max_results:
                    break
        
        if results:
            logger.info(f"✅ Trouvé {len(results)} résultats pour: {query}")
            return results
        return []
            
    except Exception as e:
        logger.warning(f"⚠️ Échec DDGS: {e}")
        return []

async def high_precision_scrape(url: str) -> str:
    """Scraping haute précision avec contournement Cloudflare via curl_cffi."""
    if any(x in url.lower() for x in ["facebook.com", "twitter.com", "instagram.com", "youtube.com"]):
        return ""
    
    logger.info(f"🕷️ Scraping (Cloudflare Bypass): {url}")
    
    try:
        # 1. Tentative avec curl_cffi
        loop = asyncio.get_event_loop()
        # On utilise une fonction pour encapsuler l'appel synchrone
        def _fetch():
            return curl_requests.get(url, impersonate="chrome120", timeout=15)
            
        response = await loop.run_in_executor(None, _fetch)
        
        if response.status_code == 200:
            content = _clean_html(response.text)
            # On accepte même les contenus courts (certains sites sont très concis)
            if len(content) > 50: 
                return content[:6000]
            else:
                logger.warning(f"⚠️ Contenu trop court pour {url} ({len(content)} chars)")
        else:
            logger.warning(f"⚠️ curl_cffi Status: {response.status_code} pour {url}")
        
        # 2. Fallback Jina Reader
        async with httpx.AsyncClient(timeout=10.0, follow_redirects=True) as client:
            resp = await client.get(f"https://r.jina.ai/{url}")
            if resp.status_code == 200 and len(resp.text) > 100:
                return resp.text[:6000]
                
    except Exception as e:
        logger.error(f"❌ Erreur de scraping pour {url}: {e}")
        
    return ""
