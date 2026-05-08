import logging
import asyncio
from urllib.parse import urlparse, unquote
from tavily import TavilyClient
from app.core.config import settings

logger = logging.getLogger(__name__)

# Initialisation du client Tavily
tavily = None
if settings.TAVILY_API_KEY:
    tavily = TavilyClient(api_key=settings.TAVILY_API_KEY)

async def duckduckgo_search(query: str, max_results: int = 5) -> list[dict]:
    """Recherche DuckDuckGo en direct via Scrapling (statique, ultra-rapide et robuste)."""
    logger.info(f"Stealth Search (Scrapling DuckDuckGo): {query}")
    try:
        from scrapling import Fetcher
        fetcher = Fetcher()
        
        # Version HTML simple de DuckDuckGo (sans Javascript, parfaite pour le scraping rapide)
        url = f"https://html.duckduckgo.com/html/?q={query}"
        
        # Exécuter dans un thread car Fetcher.get est synchrone
        loop = asyncio.get_event_loop()
        response = await loop.run_in_executor(
            None,
            lambda: fetcher.get(url)
        )
        
        results = []
        blocks = response.css(".result")
        for block in blocks:
            title_el = block.css(".result__title a")
            title = title_el.css("::text").get()
            url_raw = title_el.css("::attr(href)").get()
            snippet = block.css(".result__snippet::text").get()
            
            if title and url_raw:
                # Décoder l'URL de redirection de DuckDuckGo si présente
                if "uddg=" in url_raw:
                    try:
                        url_clean = unquote(url_raw.split("uddg=")[1].split("&")[0])
                    except Exception:
                        url_clean = url_raw
                else:
                    url_clean = url_raw
                    
                results.append({
                    "title": title.strip(),
                    "url": url_clean,
                    "snippet": snippet.strip() if snippet else "Pas de description."
                })
                if len(results) >= max_results:
                    break
                    
        logger.info(f"DuckDuckGo a trouvé {len(results)} résultats pour: {query}")
        return results
    except Exception as e:
        logger.warning(f"Erreur recherche DuckDuckGo pour {query}: {e}")
    return []

async def google_search_action_fallback(page, query):
    """Effectue la recherche de manière interactive sur Google."""
    search_box = page.locator('[name="q"]')
    await search_box.wait_for(state="visible", timeout=10000)
    await search_box.fill(query)
    await search_box.press("Enter")
    await page.wait_for_timeout(3000)

async def scrapling_search(query: str, max_results: int = 5) -> list[dict]:
    """Recherche Google en direct via Scrapling comme alternative interactive."""
    logger.info(f"Stealth Search (Scrapling Google): {query}")
    try:
        from scrapling import StealthyFetcher
        
        async def page_action(page):
            await google_search_action_fallback(page, query)
            
        response = await StealthyFetcher.async_fetch(
            "https://www.google.com",
            page_action=page_action,
            headless=True
        )
        
        results = []
        search_blocks = response.css("div.g")
        for block in search_blocks:
            title = block.css("h3::text").get()
            url = block.css("a::attr(href)").get()
            snippet = block.css(".VwiC3b::text, .VwiC3b span::text").get()
            if not snippet:
                spans = block.css("span::text").getall()
                snippet = " ".join([s.strip() for s in spans if len(s.strip()) > 10][:2])
                
            if url and title:
                if "google.com/" in url or "google.fr/" in url:
                    continue
                results.append({
                    "title": title.strip(),
                    "url": url,
                    "snippet": snippet.strip() if snippet else "Pas de description."
                })
                if len(results) >= max_results:
                    break
                    
        if not results:
            logger.info("Search blocks returned empty, using flat links fallback.")
            links = response.css("a::attr(href)").getall()
            for link in links:
                if link.startswith("http") and not any(k in link for k in ["google.com", "google.fr", "youtube.com", "maps.google"]):
                    results.append({
                        "title": "Résultat de recherche",
                        "url": link,
                        "snippet": "Extrait direct depuis la recherche web."
                    })
                    if len(results) >= max_results:
                        break
                        
        logger.info(f"Scrapling Google Search a trouvé {len(results)} résultats.")
        return results
    except Exception as e:
        logger.warning(f"Erreur recherche Scrapling Google: {e}")
    return []

async def smart_search(query: str, max_results: int = 5) -> list[dict]:
    """Recherche professionnelle. Utilise Tavily par défaut, DuckDuckGo en fallback principal, et Google en fallback secondaire."""
    if not tavily:
        logger.info("TAVILY_API_KEY manquante. Utilisation de Scrapling DuckDuckGo Search (Gratuit et Rapide).")
        return await duckduckgo_search(query, max_results)

    logger.info(f"Tavily Search: {query}")
    try:
        loop = asyncio.get_event_loop()
        response = await loop.run_in_executor(
            None, 
            lambda: tavily.search(
                query=query, 
                search_depth="advanced", 
                max_results=max_results,
                include_answer=True
            )
        )
        
        results = []
        for r in response.get("results", []):
            results.append({
                "title": r.get("title", ""),
                "url": r.get("url", ""),
                "snippet": r.get("content", ""),
                "raw_content": r.get("raw_content")
            })
            
        if response.get("answer"):
            results.insert(0, {
                "title": "Tavily AI Synthesis",
                "url": "https://tavily.com",
                "snippet": response.get("answer")
            })

        logger.info(f"Tavily a trouvé {len(results)} sources pertinentes.")
        return results
            
    except Exception as e:
        logger.error(f"Erreur API Tavily: {e}. Essai du fallback DuckDuckGo...")
        return await duckduckgo_search(query, max_results)

async def scrapling_scrape(url: str) -> str:
    """Extraction hautement furtive et résiliente via Scrapling (contourne Cloudflare uniquement si nécessaire)."""
    logger.info(f"Stealth Scrape (Scrapling): {url}")
    try:
        from scrapling import StealthyFetcher
        # Activer le solveur Cloudflare uniquement pour Glassdoor, car Indeed, Rekrute et Levels.fyi n'en ont pas besoin au premier abord
        is_glassdoor = "glassdoor" in url.lower()
        response = await StealthyFetcher.async_fetch(
            url,
            solve_cloudflare=is_glassdoor,
            headless=True,
            wait=1500 if not is_glassdoor else 2000
        )

        if response.status == 200:
            paragraphs = response.css("p::text, h1::text, h2::text, h3::text, li::text").getall()
            text_content = "\n".join([p.strip() for p in paragraphs if p.strip()])
            if len(text_content) > 100:
                logger.info("Scrapling successfully scraped page.")
                return text_content[:15000]
            
            text_content = response.get_all_text().clean()
            return text_content[:15000]
        else:
            logger.warning(f"Scrapling returned status {response.status} for {url}")
    except Exception as e:
        logger.warning(f"Error scraping with Scrapling for {url}: {e}")
    return ""

async def high_precision_scrape(url: str) -> str:
    """Extraction de contenu de haute précision.
    Utilise Scrapling pour les domaines hautement sécurisés (Glassdoor, etc.)
    ou comme fallback en cas de blocage, et Tavily Extract par défaut pour la rapidité.
    """
    domain = urlparse(url).netloc.lower() if url else ""
    use_scrapling = any(kd in domain for kd in ["glassdoor", "linkedin", "indeed", "rekrute"])
    
    if use_scrapling:
        logger.info(f"Using Scrapling for high-security domain: {url}")
        content = await scrapling_scrape(url)
        if content:
            return content

    # Default / Fallback to Tavily Extract
    if not tavily: 
        logger.warning("Tavily API Key is not set. Using Scrapling directly.")
        return await scrapling_scrape(url)
        
    logger.info(f"Tavily Extract: {url}")
    try:
        loop = asyncio.get_event_loop()
        response = await loop.run_in_executor(
            None, 
            lambda: tavily.extract(urls=[url])
        )
        
        results = response.get("results", [])
        if results:
            content = results[0].get("raw_content", "")[:10000]
            if "just a moment" in content.lower() or "forbidden" in content.lower():
                logger.info(f"Tavily blocked for {url}. Falling back to Scrapling...")
                return await scrapling_scrape(url)
            return content
            
    except Exception as e:
        logger.warning(f"Tavily extract error for {url}: {e}. Trying Scrapling fallback...")
        return await scrapling_scrape(url)
        
    return ""
