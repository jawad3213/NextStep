# ============================================================
# app/domain/company/tools/glassdoor_tool.py
# ============================================================
import logging
import asyncio
import re
from app.domain.company.tools.web_tool import smart_search, high_precision_scrape

logger = logging.getLogger(__name__)

async def glassdoor_search(company_name: str) -> dict:
    """Récupère les notes, avis, et questions d'entretien Glassdoor/RH de l'entreprise."""
    logger.info(f"🌱 Glassdoor Tool — Recherche d'avis et culture pour {company_name}")
    try:
        q1 = f"{company_name} Glassdoor avis"
        q2 = f"{company_name} Glassdoor entretien"
        q3 = f"{company_name} questions entretien rekrute indeed"
        
        r1, r2, r3 = await asyncio.gather(smart_search(q1), smart_search(q2), smart_search(q3))
        all_results = r1 + r2 + r3
        
        # 1. Extraction sélective des URLs
        # On ne veut que des sites RH ou pro connus
        trusted_domains = ["glassdoor", "indeed", "rekrute", "linkedin", "viadeo", "hellowork", "malt"]
        
        glassdoor_urls = [r["url"] for r in all_results if "glassdoor" in r["url"].lower()]
        
        interview_urls = [
            r["url"] for r in all_results 
            if any(domain in r["url"].lower() for domain in trusted_domains) 
            and any(k in r["url"].lower() for k in ["interview", "entretien", "question", "avis", "salaire"])
        ]
        
        scraped_text = ""
        interview_scraped_text = ""
        
        # 2. Scraping uniquement si on a des URLs de confiance
        scraping_tasks = []
        target_urls = []
        
        if glassdoor_urls:
            logger.info(f"🎯 URL Glassdoor identifiée : {glassdoor_urls[0]}")
            scraping_tasks.append(high_precision_scrape(glassdoor_urls[0]))
            target_urls.append("reviews")
            
        if interview_urls and (not glassdoor_urls or interview_urls[0] != glassdoor_urls[0]):
            logger.info(f"🎯 URL RH Entretien identifiée : {interview_urls[0]}")
            scraping_tasks.append(high_precision_scrape(interview_urls[0]))
            target_urls.append("interviews")
            
        if scraping_tasks:
            scraped_results = await asyncio.gather(*scraping_tasks)
            for role, content in zip(target_urls, scraped_results):
                if "just a moment" in content.lower() or "forbidden" in content.lower():
                    continue
                if role == "reviews":
                    scraped_text = content
                elif role == "interviews":
                    interview_scraped_text = content
                    
        # 3. Fallback sur les snippets si le scraping a échoué (très fréquent sur Glassdoor)
        return {
            "type": "glassdoor",
            "company": company_name,
            "url": glassdoor_urls[0] if glassdoor_urls else (interview_urls[0] if interview_urls else None),
            "raw_text": scraped_text if len(scraped_text) > 200 else "Analyse basée sur les extraits web.",
            "interview_raw_text": interview_scraped_text if len(interview_scraped_text) > 200 else "Analyse basée sur les extraits web.",
            "search_snippets": [r.get("snippet", "") for r in r1[:5]],
            "interview_snippets": [r.get("snippet", "") for r in r2[:5]] + [r.get("snippet", "") for r in r3[:5]]
        }
    except Exception as e:
        logger.error(f"❌ Erreur recherche Glassdoor : {e}")
        return {"type": "glassdoor", "company": company_name, "raw_text": "Non disponible", "search_snippets": []}
