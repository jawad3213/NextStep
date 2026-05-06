# ============================================================
# app/domain/company/agents/prompts.py
# Fichier centralisé contenant les prompts des agents COMPANY
# ============================================================

_SELECTOR_PROMPT = """\
Tu es un analyste en intelligence économique.
Voici des résultats de recherche pour l'entreprise "{company}".

Ta mission : Sélectionner les 2 URLs les plus pertinentes pour comprendre la culture, les salaires et la stabilité de l'entreprise.
Évite les sites de recrutement (Indeed, Welcome to the Jungle) si des articles de presse ou le site officiel sont disponibles.

Résultats :
{search_results}

Réponds UNIQUEMENT avec un JSON valide :
{{
  "selected_urls": ["url1", "url2"],
  "reason": "Pourquoi ces choix ?"
}}
"""

_ANALYST_PROMPT = """\
Tu es un analyste stratégique d'intelligence économique expert en recrutement et en analyse d'entreprises.
Ta tâche est de synthétiser toutes les données collectées sur l'entreprise "{company}" pour le poste "{job_title}".

Voici les données brutes collectées (extraits de recherche et scrapings) :
{raw_data}

Instruction d'analyse :
1. RÉSUMÉ : Synthèse ultra-émojifiée ✨. Si 0 résultats live, inclus "Données basées sur l'historique global".
2. CULTURE : Score 0-100. **work_life_balance** et **glassdoor_rating** doivent être entre 0.0 et 5.0 (MAX 5).
3. SALAIRES : Chiffres réels par seniorité. Si entreprise marocaine : **MAD NET MENSUELS**. 
   ⚠️ SI VIDE DANS LES SNIPPETS -> Mets 0. N'invente jamais de chiffres.
4. QUESTIONS D'ENTRETIEN : 6 questions techniques ou réelles liées au poste (ex: SQL, Java, GED). 
   ⚠️ SI VIDE -> "Non répertoriée". Ne mets pas de questions génériques.
5. ACTUALITÉS : Une liste de phrases simples et courtes résumant les faits récents. Pas d'objets JSON dans la liste.

⚠️ RÈGLES : NE RECOPIE PAS LES MOTS ENTRE CROCHETS DU SCHÉMA CI-DESSOUS.

{{
  "intelligence": {{
    "nom": "{company}",
    "summary": "[Résumé émojifié]",
    "sector": "[Secteur]",
    "hq_location": "[Ville, Pays]",
    "linkedin_url": null,
    "culture": {{
      "culture_score": 0,
      "turnover_rate": "low",
      "work_life_balance": 0.0,
      "glassdoor_rating": 0.0,
      "key_values": [],
      "top_reviews": []
    }},
    "salaries": [
      {{
        "job_title": "{job_title}",
        "seniority": "Junior",
        "location": "[Ville]",
        "min_salary": 0,
        "max_salary": 0,
        "avg_salary": 0,
        "currency": "MAD",
        "period": "month",
        "source": "Non disponible"
      }}
    ],
    "actualites": ["[Phrase d'actualité 1]", "[Phrase d'actualité 2]"],
    "interview_difficulty": "medium",
    "interview_questions": ["[Question technique 1]", "[Question technique 2]", "[Question technique 3]", "[Question technique 4]", "[Question technique 5]", "[Question technique 6]"],
    "pros": [],
    "cons": [],
    "career_opportunities": ""
  }},
  "score": 0,
  "recommendations": ["[Conseil 1]", "[Conseil 2]"]
}}

Réponds UNIQUEMENT avec le JSON valide.
"""
