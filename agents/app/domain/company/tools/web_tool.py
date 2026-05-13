import logging
import asyncio
import random
import time
from urllib.parse import urlparse, unquote
from concurrent.futures import ProcessPoolExecutor

logger = logging.getLogger(__name__)

_search_semaphore = asyncio.Semaphore(3)
_scrape_semaphore = asyncio.Semaphore(2)

def _scrapling_fetch_html(url: str) -> str:
    """S'exécute dans un processus séparé — utilise Scrapling StealthyFetcher avec timeout court."""
    from scrapling import StealthyFetcher
    try:
        StealthyFetcher.configure(navigation_timeout=8000)
    except Exception:
        pass
    fetcher = StealthyFetcher()
    try:
        response = fetcher.fetch(url, headless=True)
        if response and response.status in [200, 202]:
            raw = response.body if hasattr(response, 'body') else str(response.content) if hasattr(response, 'content') else response.text if hasattr(response, 'text') else ""
            if isinstance(raw, bytes):
                raw = raw.decode('utf-8', errors='replace')
            return str(raw)
    except Exception as e:
        logger.warning(f"Scrapling fetch error for {url}: {e}")
    return ""

async def _httpx_search(query: str, max_results: int = 5) -> list[dict]:
    """Recherche DuckDuckGo via httpx (fallback rapide)."""
    from bs4 import BeautifulSoup
    import httpx
    url = f"https://html.duckduckgo.com/html/?q={query}"
    headers = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36", "Accept": "text/html"}
    try:
        async with httpx.AsyncClient(timeout=8.0, follow_redirects=True) as c:
            r = await c.get(url, headers=headers)
        if r.status_code == 200:
            soup = BeautifulSoup(r.text, "html.parser")
            results = []
            for block in soup.select(".result"):
                title_el = block.select_one(".result__title a")
                if title_el:
                    url_raw = title_el.get("href")
                    if url_raw and "uddg=" in url_raw:
                        try:
                            url_raw = unquote(url_raw.split("uddg=")[1].split("&")[0])
                        except Exception:
                            pass
                    results.append({"title": title_el.get_text(strip=True), "url": url_raw, "snippet": block.select_one(".result__snippet").get_text(strip=True) if block.select_one(".result__snippet") else ""})
                    if len(results) >= max_results:
                        break
            if results:
                logger.info(f"httpx OK: {len(results)} résultats pour: {query}")
                return results
    except Exception as e:
        logger.info(f"httpx échoué pour {query}: {e}")
    return []

async def duckduckgo_search(query: str, max_results: int = 5) -> list[dict]:
    """Recherche DuckDuckGo : httpx (rapide) → Scrapling StealthyFetcher (anti-bot)."""
    from bs4 import BeautifulSoup
    logger.info(f"Search: {query}")
    url = f"https://html.duckduckgo.com/html/?q={query}"

    async with _search_semaphore:
        html = ""

        # 1. httpx d'abord (rapide)
        try:
            import httpx
            headers = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36", "Accept": "text/html"}
            async with httpx.AsyncClient(timeout=8.0, follow_redirects=True) as c:
                r = await c.get(url, headers=headers)
            if r.status_code == 200:
                html = r.text
        except Exception as e:
            logger.info(f"httpx fail: {e}")

        # 2. Fallback Scrapling si httpx vide
        if not html:
            try:
                loop = asyncio.get_event_loop()
                with ProcessPoolExecutor(max_workers=1) as pool:
                    html = await loop.run_in_executor(pool, _scrapling_fetch_html, url)
            except Exception as e:
                logger.warning(f"Scrapling fail: {e}")

        if not html:
            return []

        soup = BeautifulSoup(html, "html.parser")
        results = []
        for block in soup.select(".result"):
            title_el = block.select_one(".result__title a")
            snippet_el = block.select_one(".result__snippet")
            if title_el:
                title = title_el.get_text(strip=True)
                url_raw = title_el.get("href")
                snippet = snippet_el.get_text(strip=True) if snippet_el else ""
                if url_raw:
                    if "uddg=" in url_raw:
                        try:
                            url_clean = unquote(url_raw.split("uddg=")[1].split("&")[0])
                        except Exception:
                            url_clean = url_raw
                    else:
                        url_clean = url_raw
                    results.append({"title": title, "url": url_clean, "snippet": snippet})
                    if len(results) >= max_results:
                        break
        logger.info(f"Résultats: {len(results)} pour: {query}")
        return results

async def smart_search(query: str, max_results: int = 5) -> list[dict]:
    """Recherche DuckDuckGo (httpx puis Scrapling)."""
    return await duckduckgo_search(query, max_results)

async def scrapling_scrape(url: str) -> str:
    """Extraction résiliente : Scrapling StealthyFetcher (process isolé) + fallback Scrapling (même process)."""
    async with _scrape_semaphore:
        logger.info(f"Stealth Scrape (Scrapling): {url}")
        try:
            loop = asyncio.get_event_loop()
            with ProcessPoolExecutor(max_workers=1) as pool:
                html = await loop.run_in_executor(pool, _scrapling_fetch_html, url)

            from bs4 import BeautifulSoup
            if html:
                soup = BeautifulSoup(html, "html.parser")
                for tag in soup(["script", "style", "nav", "footer", "header", "aside"]):
                    tag.extract()
                texts = [t.get_text(strip=True) for t in soup.find_all(['p', 'h1', 'h2', 'h3', 'li']) if len(t.get_text(strip=True)) > 20]
                text_content = "\n".join(texts)
                if len(text_content) > 100:
                    logger.info(f"Extraction Scrapling réussie pour {url}")
                    return text_content[:15000]

            logger.warning(f"Contenu Scrapling vide pour {url}, tentative fallback...")
        except Exception as e:
            logger.warning(f"Scrapling error for {url}: {e}")

    # Fallback: httpx avec timeout long et retry
    logger.info(f"Fallback Scrape (httpx): {url}")
    import httpx
    from bs4 import BeautifulSoup
    for attempt in range(2):
        try:
            headers = {
                "User-Agent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
                "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
                "Accept-Language": "fr-FR,fr;q=0.9",
            }
            async with httpx.AsyncClient(follow_redirects=True, timeout=20.0, verify=False) as client:
                resp = await client.get(url, headers=headers)
            if resp.status_code == 200:
                soup = BeautifulSoup(resp.text, "html.parser")
                for tag in soup(["script", "style", "nav", "footer", "header", "aside"]):
                    tag.extract()
                texts = [t.get_text(strip=True) for t in soup.find_all(['p', 'h1', 'h2', 'h3', 'li']) if len(t.get_text(strip=True)) > 20]
                text = "\n".join(texts)
                if len(text) > 100:
                    return text[:15000]
        except Exception as e:
            logger.warning(f"Fallback attempt {attempt+1} failed for {url}: {e}")
        await asyncio.sleep(1)
    return ""

async def high_precision_scrape(url: str) -> str:
    """Extraction web — Scrapling puis httpx fallback."""
    return await scrapling_scrape(url)
