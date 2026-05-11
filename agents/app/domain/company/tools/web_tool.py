import logging
import asyncio
from urllib.parse import urlparse, unquote
from concurrent.futures import ProcessPoolExecutor

logger = logging.getLogger(__name__)

async def duckduckgo_search(query: str, max_results: int = 5) -> list[dict]:
    """Recherche DuckDuckGo en direct via httpx et BeautifulSoup."""
    logger.info(f"Stealth Search (DuckDuckGo BS4): {query}")
    try:
        import httpx
        from bs4 import BeautifulSoup
        
        url = f"https://html.duckduckgo.com/html/?q={query}"
        headers = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"}
        
        async with httpx.AsyncClient() as client:
            response = await client.get(url, headers=headers)
            
        soup = BeautifulSoup(response.text, "html.parser")
        results = []
        
        for block in soup.select(".result"):
            title_el = block.select_one(".result__title a")
            snippet_el = block.select_one(".result__snippet")
            
            if title_el:
                title = title_el.get_text(strip=True)
                url_raw = title_el.get("href")
                snippet = snippet_el.get_text(strip=True) if snippet_el else "Pas de description."
                
                if url_raw:
                    if "uddg=" in url_raw:
                        try:
                            url_clean = unquote(url_raw.split("uddg=")[1].split("&")[0])
                        except Exception:
                            url_clean = url_raw
                    else:
                        url_clean = url_raw
                        
                    results.append({
                        "title": title,
                        "url": url_clean,
                        "snippet": snippet
                    })
                    if len(results) >= max_results:
                        break
                        
        logger.info(f"DuckDuckGo a trouvé {len(results)} résultats pour: {query}")
        return results
    except Exception as e:
        logger.warning(f"Erreur recherche DuckDuckGo pour {query}: {e}")
    return []

async def smart_search(query: str, max_results: int = 5) -> list[dict]:
    """Recherche professionnelle utilisant exclusivement DuckDuckGo comme demandé."""
    return await duckduckgo_search(query, max_results)

def _isolated_scrapling_process(url: str) -> str:
    """S'exécute dans un processus séparé pour isoler Playwright de la boucle asyncio principale."""
    from scrapling import StealthyFetcher
    fetcher = StealthyFetcher()
    response = fetcher.fetch(url, headless=True)
    
    if response.status in [200, 201, 202, 203]:
        paragraphs = response.css("p::text, h1::text, h2::text, h3::text, li::text")
        if isinstance(paragraphs, list):
            return "\n".join([str(p).strip() for p in paragraphs if str(p).strip()])
    return ""

async def scrapling_scrape(url: str) -> str:
    """Extraction résiliente : Utilise Scrapling isolé dans un Process, avec fallback httpx+BS4."""
    logger.info(f"Stealth Scrape (Scrapling): {url}")
    text_content = ""
    
    # 1. Tentative avec Scrapling StealthyFetcher (Process Isolé)
    try:
        loop = asyncio.get_event_loop()
        # Le ProcessPoolExecutor empêche Playwright de détecter la boucle asyncio du thread principal
        with ProcessPoolExecutor(max_workers=1) as pool:
            text_content = await loop.run_in_executor(pool, _isolated_scrapling_process, url)
            
        if text_content and len(text_content) > 100:
            logger.info(f"Extraction Scrapling réussie pour {url}")
            return text_content[:15000]
        else:
            logger.warning(f"Contenu Scrapling vide ou trop court pour {url}, passage au fallback.")
    except Exception as e:
        logger.warning(f"Error scraping with Scrapling for {url}: {e}. Passage au fallback httpx.")

    # 2. Fallback avec httpx et BeautifulSoup
    logger.info(f"Fallback Light Scrape (httpx+BS4): {url}")
    try:
        import httpx
        from bs4 import BeautifulSoup
        
        headers = {
            "User-Agent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
            "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8",
            "Accept-Language": "fr-FR,fr;q=0.9,en-US;q=0.8,en;q=0.7",
            "Accept-Encoding": "gzip, deflate, br",
            "Connection": "keep-alive",
            "Upgrade-Insecure-Requests": "1"
        }
        
        async with httpx.AsyncClient(follow_redirects=True, timeout=12.0, verify=False) as client:
            fallback_resp = await client.get(url, headers=headers)
            
        if fallback_resp.status_code == 200:
            soup = BeautifulSoup(fallback_resp.text, "html.parser")
            
            for script in soup(["script", "style", "nav", "footer", "header", "aside"]):
                script.extract()
                
            paragraphs = soup.find_all(['p', 'h1', 'h2', 'h3', 'li'])
            fallback_text = "\n".join([p.get_text(strip=True) for p in paragraphs if len(p.get_text(strip=True)) > 20])
            
            if len(fallback_text) > 100:
                logger.info(f"Extraction Fallback (httpx+BS4) réussie pour {url}")
                return fallback_text[:15000]
    except Exception as fallback_e:
        logger.warning(f"Error in fallback scraping for {url}: {fallback_e}")
        
    return ""

async def high_precision_scrape(url: str) -> str:
    """Extraction web utilisant Scrapling exclusivement, comme demandé."""
    logger.info(f"Scraping requested for domain: {url}")
    return await scrapling_scrape(url)
