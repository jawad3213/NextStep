import asyncio
import base64
import logging
import unicodedata
from typing import Optional
from urllib.parse import parse_qs, quote_plus, unquote, urlparse

import httpx
from bs4 import BeautifulSoup

logger = logging.getLogger(__name__)

_search_semaphore = asyncio.Semaphore(3)
_scrape_semaphore = asyncio.Semaphore(2)

_DDG_URL = "https://html.duckduckgo.com/html/"
_SEARCH_HEADERS = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36", "Accept": "text/html"}
_SCRAPE_HEADERS = {
    "User-Agent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
    "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
    "Accept-Language": "fr-FR,fr;q=0.9,en;q=0.8",
}

# One client for all searches and scrapes (connection reuse). TLS certificates are verified.
_client: httpx.AsyncClient | None = None
_client_loop: asyncio.AbstractEventLoop | None = None


def _http() -> httpx.AsyncClient:
    """One client per event loop (a client cannot be used from another loop)."""
    global _client, _client_loop
    loop = asyncio.get_running_loop()
    if _client is None or _client.is_closed or _client_loop is not loop:
        _client = httpx.AsyncClient(follow_redirects=True)
        _client_loop = loop
    return _client


def _result_url(href: str) -> str:
    """DuckDuckGo wraps result links (…?uddg=<encoded url>&…)."""
    if "uddg=" in href:
        try:
            return unquote(href.split("uddg=")[1].split("&")[0])
        except Exception:
            return href
    return href


async def duckduckgo_search(query: str, max_results: int = 5) -> list[dict]:
    """DuckDuckGo HTML search. The query is sent as an encoded parameter, so company
    names with '&', quotes or brackets search correctly."""
    logger.info(f"Search: {query}")
    async with _search_semaphore:
        try:
            r = await _http().get(_DDG_URL, params={"q": query}, headers=_SEARCH_HEADERS, timeout=8.0)
        except Exception as e:
            logger.info(f"Search failed for {query}: {e}")
            return []
        if r.status_code != 200 or not r.text:
            logger.info(f"Search returned HTTP {r.status_code} for {query}")
            return []

    results = []
    for block in BeautifulSoup(r.text, "html.parser").select(".result"):
        title_el = block.select_one(".result__title a")
        href = title_el.get("href") if title_el else None
        if not href:
            continue
        snippet_el = block.select_one(".result__snippet")
        results.append({
            "title": title_el.get_text(strip=True),
            "url": _result_url(href),
            "snippet": snippet_el.get_text(strip=True) if snippet_el else "",
        })
        if len(results) >= max_results:
            break
    logger.info(f"Résultats: {len(results)} pour: {query}")
    return results


async def tavily_search(query: str, max_results: int = 5) -> list[dict]:
    """Tavily search API (needs TAVILY_API_KEY). Same result shape as duckduckgo_search."""
    from app.domain.chatbot.tools import _search

    async with _search_semaphore:
        try:
            results = await _search(query, max_results=max_results)
        except Exception as e:
            logger.info(f"Tavily search failed for {query}: {e}")
            return []
    return [
        {"title": r.get("title", ""), "url": r.get("url", ""), "snippet": (r.get("content") or "")[:500]}
        for r in results
        if r.get("url")
    ]


_BING_URL = "https://www.bing.com/search"


def _bing_target(href: Optional[str]) -> Optional[str]:
    """Bing wraps result links in a click tracker (/ck/a?...&u=a1<base64url of the real URL>)."""
    if not href:
        return None
    if "bing.com/ck/a" not in href:
        return href if href.startswith("http") else None
    encoded = parse_qs(urlparse(href).query).get("u", [""])[0]
    if not encoded.startswith("a1"):
        return None
    try:
        payload = encoded[2:]
        url = base64.urlsafe_b64decode(payload + "=" * (-len(payload) % 4)).decode("utf-8")
    except (ValueError, UnicodeDecodeError):
        return None
    return url if url.startswith("http") else None


async def bing_search(query: str, max_results: int = 5) -> list[dict]:
    """Bing results page fetched with Scrapling (browser-like TLS fingerprint): unlike
    DuckDuckGo's HTML endpoint, Bing does not answer it with an anti-bot page."""
    from app.domain.job_boards.common import fetch_page

    async with _search_semaphore:
        try:
            page = await fetch_page(f"{_BING_URL}?q={quote_plus(query)}&setlang=en&count=10")
        except Exception as e:
            logger.info(f"Bing search failed for {query}: {e}")
            return []

    results = []
    for block in page.css("li.b_algo"):
        url = _bing_target(block.css("h2 a::attr(href)").get())
        title = " ".join(" ".join(block.css("h2 a ::text").getall()).split())
        if not url or not title:
            continue
        snippet = " ".join(" ".join(block.css(".b_caption p ::text").getall() or block.css("p ::text").getall()).split())
        results.append({"title": title, "url": url, "snippet": snippet[:500]})
        if len(results) >= max_results:
            break
    logger.info(f"Bing results: {len(results)} for: {query}")
    return results


_YAHOO_URL = "https://search.yahoo.com/search"


def _yahoo_target(href: Optional[str]) -> Optional[str]:
    """Yahoo wraps result links (r.search.yahoo.com/.../RU=<url-encoded target>/RK=...)."""
    if not href:
        return None
    if "/RU=" in href:
        url = unquote(href.split("/RU=", 1)[1].split("/RK=", 1)[0].split("/RS=", 1)[0])
        return url if url.startswith("http") else None
    if "search.yahoo.com" in href:
        return None
    return href if href.startswith("http") else None


async def yahoo_search(query: str, max_results: int = 5) -> list[dict]:
    """Yahoo results page fetched with Scrapling. Yahoo returns real results where DuckDuckGo
    answers with a CAPTCHA and Bing serves decoy results to automated clients."""
    from app.domain.job_boards.common import fetch_page

    async with _search_semaphore:
        try:
            page = await fetch_page(f"{_YAHOO_URL}?p={quote_plus(query)}")
        except Exception as e:
            logger.info(f"Yahoo search failed for {query}: {e}")
            return []

    results = []
    for block in page.css("div.algo"):
        url = _yahoo_target(block.css("h3 a::attr(href)").get() or block.css("a::attr(href)").get())
        title = " ".join(" ".join(block.css("h3 ::text").getall()).split())
        if not url or not title:
            continue
        snippet = " ".join(" ".join(block.css(".compText ::text").getall()).split())
        results.append({"title": title, "url": url, "snippet": snippet[:500]})
        if len(results) >= max_results:
            break
    logger.info(f"Yahoo results: {len(results)} for: {query}")
    return results


def _fold(text: str) -> str:
    """Case/accent/space-insensitive form: 'Société Générale' -> 'societegenerale'."""
    decomposed = unicodedata.normalize("NFKD", text or "")
    return "".join(c for c in decomposed if c.isalnum() and not unicodedata.combining(c)).lower()


def _mentions(result: dict, subject: str) -> bool:
    needle = _fold(subject)
    haystack = _fold(" ".join([result.get("title", ""), result.get("snippet", ""), result.get("url", "")]))
    return bool(needle) and needle in haystack


async def smart_search(query: str, max_results: int = 5, must_mention: Optional[str] = None) -> list[dict]:
    """Web search used by the company tools; the first provider with relevant results wins:
    Tavily (when TAVILY_API_KEY is set) -> Yahoo -> Bing -> DuckDuckGo.

    `must_mention` (e.g. the company name) drops results that do not mention it: search
    engines serve unrelated "decoy" results to automated clients, and those must never
    reach the report as if they were about the company."""
    from app.core.config import settings

    providers = ([tavily_search] if settings.TAVILY_API_KEY else []) + [yahoo_search, bing_search, duckduckgo_search]
    for provider in providers:
        results = await provider(query, max_results)
        if must_mention:
            dropped = len(results)
            results = [r for r in results if _mentions(r, must_mention)]
            dropped -= len(results)
            if dropped:
                logger.info(f"{provider.__name__}: dropped {dropped} result(s) not about {must_mention!r} for: {query}")
        if results:
            return results
    return []


async def lightweight_scrape(url: str) -> str:
    """Readable text of a page (httpx + BeautifulSoup), with retry/backoff."""
    async with _scrape_semaphore:
        logger.info(f"Lightweight scrape (httpx): {url}")
        for attempt in range(3):
            try:
                resp = await _http().get(url, headers=_SCRAPE_HEADERS, timeout=18.0)
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
    """Page text for the company analysis (same as lightweight_scrape)."""
    return await lightweight_scrape(url)
