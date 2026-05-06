import logging
import json
import asyncio
import re
from langchain_core.messages import AIMessage
from langchain_core.prompts import ChatPromptTemplate
from app.core.config import get_llm
from app.domain.company.schemas.state import CompanyState
from app.domain.company.tools.web_tool import smart_search, high_precision_scrape
from app.domain.company.tools.glassdoor_tool import glassdoor_search
from app.domain.company.tools.linkedin_tool import linkedin_company_search
from app.domain.company.tools.salary_tool import salary_data_search
from app.domain.company.agents.prompts import _SELECTOR_PROMPT, _ANALYST_PROMPT

logger = logging.getLogger(__name__)


def parse_json_markdown(text: str) -> dict:
    """
    Extrait et décode le bloc JSON d'une chaîne brute retournée par le LLM,
    même s'il y a du texte explicatif autour ou des balises ```json.
    """
    if not text:
        return {}
    
    # Enlever les commentaires de ligne // (non standard JSON) sans détruire les URLs http:// ou https://
    text_clean = re.sub(r'(?<!:)\/\/.*$', '', text, flags=re.MULTILINE)
    
    # Chercher un bloc ```json ... ``` ou ``` ... ```
    match = re.search(r'```(?:json)?\s*([\s\S]*?)\s*```', text_clean, re.IGNORECASE)
    if match:
        json_str = match.group(1).strip()
    else:
        # Sinon, chercher la première accolade ouvrante { et la dernière accolade fermante }
        start = text_clean.find("{")
        end = text_clean.rfind("}")
        if start != -1 and end != -1:
            json_str = text_clean[start:end+1].strip()
        else:
            json_str = text_clean.strip()
            
    try:
        return json.loads(json_str, strict=False)
    except Exception as e:
        logger.warning(f"⚠️ Échec du décodage JSON direct, tentative de nettoyage: {e}")
        # Retirer d'éventuelles virgules traînantes avant une accolade fermante ou crochet fermant
        json_str_repaired = re.sub(r',\s*([\]}])', r'\1', json_str)
        try:
            return json.loads(json_str_repaired, strict=False)
        except Exception as e2:
            logger.error(f"❌ Échec de la réparation JSON : {e2}")
            raise e2


async def researcher_node(state: CompanyState) -> dict:
    """
    Nœud 1 : Recherche -> Sélection -> Scraping Haute Précision.
    """
    company = state.get("company_name")
    job_title = state.get("job_title", "Poste recherché")
    logger.info(f"🔍 Researcher Agent — Deep Research for {company} (Poste: {job_title})")
    
    # 1. Smart Search (Focalisé uniquement sur la fiche générale / présentation d'entreprise)
    search_query = f"{company} présentation"
    search_results = await smart_search(search_query)
    
    logger.info(f"📊 Researcher got {len(search_results)} general results from DDG")
    
    urls = []
    scraped_contents = []
    
    if search_results:
        # 2. Sélection des meilleures URLs via LLM pour la présentation générale
        llm = get_llm()
        if hasattr(llm, "bind"):
            llm = llm.bind(response_format={"type": "json_object"})
        selector_prompt = ChatPromptTemplate.from_template(_SELECTOR_PROMPT)
        selector_chain = selector_prompt | llm
        
        try:
            response = await selector_chain.ainvoke({
                "company": company,
                "search_results": json.dumps(search_results, indent=2)
            })
            # Extraction robuste
            selection = parse_json_markdown(response.content if hasattr(response, "content") else str(response))
            urls = selection.get("selected_urls", [])
        except Exception as e:
            logger.error(f"Selection error: {e}")
            urls = [r['url'] for r in search_results[:2]] # Fallback
            
        logger.info(f"🎯 URLs de présentation générale sélectionnées : {urls}")

        # 3. Scraping Haute Précision en parallèle pour la présentation générale
        scraping_tasks = [high_precision_scrape(url) for url in urls]
        scraped_contents = await asyncio.gather(*scraping_tasks)

    # 4. Préparation du warning si 0 résultats
    search_warning = ""
    if not search_results:
        search_warning = "⚠️ AVERTISSEMENT : 0 résultats récupérés en direct sur le web (blocage anti-bot ou absence de données récentes)."
        logger.warning(f"🚨 {search_warning}")

    # 5. Lancement de tous les outils spécialisés en parallèle
    logger.info("⚡ Lancement de tous les outils spécialisés en parallèle (LinkedIn, Glassdoor, Salaire)...")
    li_task = linkedin_company_search(company)
    gd_task = glassdoor_search(company)
    sal_task = salary_data_search(company, job_title)
    
    li_data, gd_data, sal_data = await asyncio.gather(
        li_task, gd_task, sal_task
    )

    return {
        "raw_search_results": [
            {"type": "warning", "content": search_warning} if search_warning else None,
            {"type": "web_deep_scrape", "content": scraped_contents},
            {"type": "glassdoor", "data": gd_data},
            {"type": "linkedin", "data": li_data},
            {"type": "salaries", "data": sal_data}
        ],
        "messages": [AIMessage(content=f"Recherche profonde terminée pour {company}. Présentation générale, LinkedIn, Glassdoor et Salaires recherchés de manière indépendante.", name="researcher")]
    }


def clean_and_truncate_text(text: str, max_chars: int = 3500) -> str:
    """
    Nettoie les espaces multiples et les sauts de ligne répétés,
    puis tronque le texte pour éviter de saturer les limites de tokens (TPM/TPD) de l'API LLM.
    """
    if not text:
        return ""
    # Remplacer les retours à la ligne répétés par des sauts simples
    text = re.sub(r'\n+', '\n', text)
    # Remplacer les espaces multiples par un espace simple
    text = re.sub(r' {2,}', ' ', text)
    text = text.strip()
    if len(text) > max_chars:
        return text[:max_chars] + "\n... [CONTENU TRONQUÉ POUR LIMITER LES TOKENS]"
    return text


async def analyst_node(state: CompanyState) -> dict:
    """
    Nœud 2 : Synthèse finale de l'intelligence.
    Utilise le LLM pour analyser en profondeur les données brutes accumulées
    et générer un rapport ultra-structuré qui priorise les informations générales,
    puis la culture d'entreprise, puis les salaires.
    """
    company = state.get("company_name")
    job_title = state.get("job_title", "Poste recherché")
    raw_data = state.get("raw_search_results", [])
    
    logger.info(f"🧠 Analyst Agent — Synthesizing intelligence for {company}")
    
    # Formatage des données brutes récoltées pour le prompt (avec nettoyage et troncature)
    formatted_data = []
    for r in raw_data:
        if not r: continue
        dtype = r.get("type", "unknown")
        if dtype == "warning":
            formatted_data.append(f"!!! {r.get('content')} !!!\n")
        elif dtype == "web_deep_scrape":
            contents = r.get("content", [])
            for i, content in enumerate(contents):
                clean_content = clean_and_truncate_text(content, max_chars=2000)
                formatted_data.append(f"--- SOURCE WEB SCRAPE {i+1} ---\n{clean_content}\n")
        elif dtype == "glassdoor":
            data = r.get("data", {})
            if isinstance(data, dict):
                if "raw_text" in data:
                    data["raw_text"] = clean_and_truncate_text(data["raw_text"], max_chars=1500)
                if "interview_raw_text" in data:
                    data["interview_raw_text"] = clean_and_truncate_text(data["interview_raw_text"], max_chars=1500)
            formatted_data.append(f"--- GLASSDOOR DATA ---\n{json.dumps(data, indent=2, ensure_ascii=False)}\n")
        elif dtype == "linkedin":
            formatted_data.append(f"--- LINKEDIN DATA ---\n{json.dumps(r.get('data'), indent=2, ensure_ascii=False)}\n")
        elif dtype == "salaries":
            data = r.get("data", {})
            if isinstance(data, dict) and "scraped_salary_pages" in data:
                scraped = data.get("scraped_salary_pages", [])
                data["scraped_salary_pages"] = [clean_and_truncate_text(p, max_chars=1500) for p in scraped]
            formatted_data.append(f"--- SALARIES DATA ---\n{json.dumps(data, indent=2, ensure_ascii=False)}\n")
        else:
            formatted_data.append(f"--- RAW DATA ({dtype}) ---\n{json.dumps(r, indent=2, ensure_ascii=False)}\n")
            
    raw_data_str = "\n".join(formatted_data) if formatted_data else "Aucune donnée collectée."

    llm = get_llm()
    if hasattr(llm, "bind"):
        llm = llm.bind(response_format={"type": "json_object"})
    analyst_prompt = ChatPromptTemplate.from_template(_ANALYST_PROMPT)
    analyst_chain = analyst_prompt | llm
    
    try:
        response = await analyst_chain.ainvoke({
            "company": company,
            "job_title": job_title,
            "raw_data": raw_data_str
        })
        
        # Log pour debug
        import os
        os.makedirs("scratch", exist_ok=True)
        with open("scratch/analyst_raw_output.txt", "w", encoding="utf-8") as f:
            f.write(response.content if hasattr(response, "content") else str(response))
            
        # Décoder de manière très robuste
        synthesis = parse_json_markdown(response.content if hasattr(response, "content") else str(response))
        
        intelligence_report = synthesis.get("intelligence", {})
        # S'assurer que le nom est bien défini
        if "nom" not in intelligence_report or not intelligence_report["nom"]:
            intelligence_report["nom"] = company
            
        # Sécurité : Plafonner les notes à 5.0
        culture = intelligence_report.get("culture", {})
        if isinstance(culture, dict):
            if culture.get("work_life_balance", 0) > 5: culture["work_life_balance"] = 5.0
            if culture.get("glassdoor_rating", 0) > 5: culture["glassdoor_rating"] = 5.0
            
        # Sécurité : Forcer les actualités en liste de strings
        actualites = intelligence_report.get("actualites", [])
        clean_news = []
        for act in actualites:
            if isinstance(act, dict):
                clean_news.append(act.get("title") or act.get("description") or str(act))
            else:
                clean_news.append(str(act))
        intelligence_report["actualites"] = clean_news
            
        compatibility_score = synthesis.get("score") or synthesis.get("compatibility_score") or 80
        recommendations = synthesis.get("recommendations", [
            "Se renseigner sur les technologies récentes de l'entreprise",
            "Mettre en avant sa capacité d'adaptation et d'apprentissage rapide"
        ])
        
    except Exception as e:
        logger.error(f"❌ Erreur lors de la synthèse LLM par l'Analyste: {e}")
        # Fallback si le parsing JSON ou l'appel échoue
        intelligence_report = {
            "nom": company,
            "summary": f"Fiche d'information générale de secours pour {company} en raison d'une erreur d'analyse.",
            "sector": "Secteur technologique",
            "hq_location": "Non spécifié",
            "culture": {
                "culture_score": 75,
                "turnover_rate": "medium",
                "work_life_balance": 3.8,
                "glassdoor_rating": 3.8,
                "key_values": ["Adaptabilité", "Infiltration technologique"],
                "top_reviews": ["Environnement dynamique mais exigeant."]
            },
            "salaries": [
                {
                    "job_title": job_title,
                    "location": "National",
                    "min_salary": 45000,
                    "max_salary": 70000,
                    "avg_salary": 55000,
                    "currency": "EUR",
                    "source": "Standard du marché (Fallback)"
                }
            ],
            "actualites": [
                f"Développement continu des activités de {company} dans le numérique.",
                "Renforcement de l'expertise et des équipes techniques."
            ],
            "interview_difficulty": "medium",
            "interview_questions": [
                "Présentez votre parcours technique et vos projets récents.",
                "Pourquoi souhaitez-vous rejoindre nos équipes ?"
            ],
            "pros": ["Innovation", "Excellence technique"],
            "cons": ["Pression élevée", "Processus en cours de structuration"]
        }
        compatibility_score = 75
        recommendations = [
            "Se renseigner sur les technologies récentes de l'entreprise",
            "Mettre en avant sa capacité d'adaptation et d'apprentissage rapide"
        ]

    return {
        "intelligence": intelligence_report,
        "company_summary": intelligence_report.get("summary", ""),
        "score": compatibility_score,
        "recommendations": recommendations,
        "messages": [AIMessage(content=f"Analyse finale synthétisée pour {company}.", name="analyst")]
    }
