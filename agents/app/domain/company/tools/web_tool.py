import logging
import asyncio
from urllib.parse import unquote

logger = logging.getLogger(__name__)

_search_semaphore = asyncio.Semaphore(3)
_scrape_semaphore = asyncio.Semaphore(2)

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
    """Recherche DuckDuckGo via httpx uniquement (léger et stable en container)."""
    from bs4 import BeautifulSoup
    logger.info(f"Search: {query}")
    url = f"https://html.duckduckgo.com/html/?q={query}"

    async with _search_semaphore:
        html = ""

        # httpx only
        try:
            import httpx
            headers = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36", "Accept": "text/html"}
            async with httpx.AsyncClient(timeout=8.0, follow_redirects=True) as c:
                r = await c.get(url, headers=headers)
            if r.status_code == 200:
                html = r.text
        except Exception as e:
            logger.info(f"httpx fail: {e}")

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
    """Recherche DuckDuckGo (httpx only)."""
    return await duckduckgo_search(query, max_results)

async def lightweight_scrape(url: str) -> str:
    """Extraction web légère: httpx + BeautifulSoup + retry/backoff."""
    async with _scrape_semaphore:
        logger.info(f"Lightweight scrape (httpx): {url}")
        import httpx
        from bs4 import BeautifulSoup
        for attempt in range(3):
            try:
                headers = {
                    "User-Agent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
                    "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
                    "Accept-Language": "fr-FR,fr;q=0.9,en;q=0.8",
                }
                async with httpx.AsyncClient(follow_redirects=True, timeout=18.0, verify=False) as client:
                    resp = await client.get(url, headers=headers)
                if resp.status_code == 200 and resp.text:
                    soup = BeautifulSoup(resp.text, "html.parser")
                    for tag in soup(["script", "style", "nav", "footer", "header", "aside", "noscript"]):
                        tag.extract()
                    texts = [
                        t.get_text(strip=True)
                        for t in soup.find_all(["p", "h1", "h2", "h3", "li"])
                        if len(t.get_text(strip=True)) > 20
                    ]
                    text = "\n".join(texts)
                    if len(text) > 100:
                        return text[:15000]
            except Exception as e:
                logger.warning(f"lightweight scrape attempt {attempt + 1} failed for {url}: {e}")
            await asyncio.sleep(0.8 * (attempt + 1))
    return ""

async def high_precision_scrape(url: str) -> str:
    """Extraction web légère (sans Scrapling)."""
    return await lightweight_scrape(url)
