# ============================================================
# app/domain/company/tools/salary_tool.py
# ============================================================
import logging
import asyncio
from typing import Optional
from app.domain.company.tools.web_tool import smart_search, high_precision_scrape

logger = logging.getLogger(__name__)

async def salary_data_search(company_name: str, job_title: str, location: Optional[str] = None) -> dict:
    """
    Récupère les fourchettes de salaires pour un poste donné au sein d'une entreprise via recherche ciblée.
    """
    # Deux requêtes plus ciblées et simples en parallèle pour maximiser les chances de résultats
    q1 = f"{company_name} {job_title} salaire"
    if location:
        q1 += f" {location}"
    q2 = f"{company_name} levels.fyi"
    
    # Étape de "Cross-referencing" avec des sites locaux marocains (ReKrute, Stagiaires.ma)
    q3 = f"site:rekrute.com \"{company_name}\""
    q4 = f"site:stagiaires.ma \"{company_name}\""
    
    logger.info(f"💰 Salary Tool — Recherche de salaires avec q1: '{q1}', q2: '{q2}', q3: '{q3}', q4: '{q4}'")
    try:
        results_q1, results_q2, results_q3, results_q4 = await asyncio.gather(
            smart_search(q1), 
            smart_search(q2),
            smart_search(q3),
            smart_search(q4)
        )
        
        # Combiner les résultats
        all_results = results_q1 + results_q2 + results_q3 + results_q4
        
        # Classer par priorité de domaine pour éviter les blocages de Glassdoor
        # Levels.fyi, ReKrute, Indeed, Impelup en premier, Glassdoor en dernier
        priority_domains = ["levels.fyi", "rekrute", "indeed", "impelup", "glassdoor"]
        
        salary_urls = []
        for domain in priority_domains:
            for r in all_results:
                url = r["url"].lower()
                if domain in url and r["url"] not in salary_urls:
                    salary_urls.append(r["url"])
                    
        # Si aucun domaine prioritaire n'est trouvé, prendre le premier résultat disponible
        if not salary_urls and all_results:
            salary_urls = [all_results[0]["url"]]
            
        scraped_contents = []
        urls_scraped = salary_urls[:3] # Prendre les 3 meilleures URLs à scraper en parallèle
        
        scraping_tasks = [high_precision_scrape(url) for url in urls_scraped]
        if scraping_tasks:
            scraped_results = await asyncio.gather(*scraping_tasks)
            # Filtrer les pages d'erreur Cloudflare ou "Just a moment..." de Glassdoor
            for url, content in zip(urls_scraped, scraped_results):
                if "just a moment" in content.lower() or "humans only" in content.lower() or "forbidden" in content.lower():
                    logger.warning(f"⚠️ Scraping bloqué par Cloudflare pour {url}, ignoré.")
                    continue
                scraped_contents.append(content)
            
        return {
            "type": "salaries",
            "company": company_name,
            "job": job_title,
            "scraped_salary_pages": scraped_contents,
            "search_snippets": [r.get("snippet", "") for r in all_results[:6]]
        }
    except Exception as e:
        logger.error(f"❌ Erreur recherche salaires : {e}")
        return {"type": "salaries", "company": company_name, "job": job_title, "error": str(e)}

