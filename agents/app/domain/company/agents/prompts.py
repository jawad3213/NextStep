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
Tu es un analyste en intelligence économique rigoureux et performant.
Ta tâche est de lire attentivement les données brutes récoltées sur l'entreprise "{company}" pour le poste "{job_title}" et de rédiger un rapport d'intelligence extrêmement complet et structuré, directement prêt à être présenté à un utilisateur et stocké en base de données.

Voici les données brutes collectées (extraits de recherche, LinkedIn, Indeed, Rekrute et Glassdoor) :
{raw_data}

⛔ RÈGLE ABSOLUE — AUCUNE INVENTION :
- Chaque fait et chaque chiffre (note, score, salaire, effectif, actualité, avis) doit provenir des données ci-dessus.
- Si une information n'y figure pas : null pour les nombres, [] pour les listes, "" pour les textes. N'estime rien,
  n'utilise pas tes connaissances générales, ne complète pas avec des valeurs "typiques du marché".
- Un rapport court et exact vaut mieux qu'un rapport long et inventé.

💡 DIRECTIVES DE SYNTHÈSE ET DE PARSING FACTUEL :
1. RÉSUMÉ DE L'ENTREPRISE (sera stocké dans `resume_entreprise` de la table PostgreSQL) :
   Rédige une présentation générale en français, dont la longueur est proportionnelle aux informations réellement disponibles (ne remplis pas). Elle couvre, seulement si les données le permettent :
   - Qui est l'entreprise (envergure, histoire, présence au Maroc, villes d'implantation comme Casablanca, Rabat, etc.).
   - Ce que fait exactement l'entreprise (ses secteurs d'activité, pôles technologiques, expertises en transformation numérique, ingénierie, R&D, intégration, etc.).
   - L'environnement de travail et le style de management décrits par les collaborateurs dans les avis scrapés.

2. NOTES ET CULTURE :
   - Extrais la note globale de l'entreprise sur Glassdoor/Indeed (ex: `3.6` ou `4.0` sur 5) SEULEMENT si elle est écrite dans les données, sinon null.
   - `culture_score` = note globale × 20 (ex: 3.6 * 20 = 72), ou null si la note est null.
   - `work_life_balance` : seulement si cette note est écrite dans les données, sinon null.

3. SALAIRES (RÈGLES STRICTES DE SÉNIORITÉ) :
   - Analyse les salaires et calcules-les au format mensuel net en dirhams (MAD).
   - Si les chiffres dans le texte brut sont annuels (ex: 129k MAD), divise-les par 12 (ex: 129000 / 12 = 10750 MAD/mois).
   - N'inclus QUE les salaires présents dans les données (avec leur source). S'il n'y a aucun salaire, "salaries" vaut [].
     N'estime jamais un salaire Junior ou Senior absent des données.

4. QUESTIONS D'ENTRETIEN (RÈGLE DES 6 QUESTIONS) :
   - **Tu DOIS impérativement lister EXACTEMENT 6 questions d'entretien d'embauche.**
   - Ces 6 questions doivent former un mix équilibré : **3 questions techniques/algorithmiques** (ex: SQL, Python, machine learning ou tests logiques spécifiques au poste) et **3 questions RH/motivationnelles/comportementales** (ex: "Pourquoi notre entreprise ?", "Présentez un projet complexe", "Comment gérez-vous le stress ?").
   - Utilise en priorité les vraies questions trouvées dans les textes scrapés de Glassdoor/Indeed/Rekrute. Si le texte n'en contient pas assez, complète en générant des questions de recrutement réelles et extrêmement réalistes pour le poste "{job_title}" chez "{company}".

⚠️ SCHÉMA DE SORTIE STRICT (JSON UNIQUEMENT) :
Réponds UNIQUEMENT avec un objet JSON valide structuré exactement comme ceci. Aucun texte avant ou après !

{{
  "intelligence": {{
    "nom": "{company}",
    "summary": "[Insère ici le long résumé hyper détaillé en 3 paragraphes rédigé en français avec émojis]",
    "sector": "[Secteur d'activité, ex: Services informatiques et conseil]",
    "hq_location": "[Siège social ou implantation principale au Maroc, ex: Casablanca, Maroc]",
    "linkedin_url": "https://www.linkedin.com/company/...",
    "culture": {{
      "culture_score": [Score de 0 à 100],
      "turnover_rate": "low",
      "work_life_balance": [Note de 0.0 à 5.0],
      "glassdoor_rating": [Note de 0.0 à 5.0],
      "key_values": ["[Valeur clé 1]", "[Valeur clé 2]"],
      "top_reviews": ["[Synthèse des points positifs marquants des avis]", "[Synthèse des points de vigilance ou négatifs]"]
    }},
    "salaries": [
      {{
        "job_title": "{job_title}",
        "seniority": "Junior",
        "location": "Casablanca, Maroc",
        "min_salary": [salaire min mensuel pour un profil junior en MAD],
        "max_salary": [salaire max mensuel pour un profil junior en MAD],
        "avg_salary": [salaire moyen mensuel pour un profil junior en MAD],
        "currency": "MAD",
        "period": "month",
        "source": "[Glassdoor, Levels.fyi ou Indeed]"
      }},
      {{
        "job_title": "{job_title}",
        "seniority": "Senior",
        "location": "Casablanca, Maroc",
        "min_salary": [salaire min mensuel pour un profil senior en MAD],
        "max_salary": [salaire max mensuel pour un profil senior en MAD],
        "avg_salary": [salaire moyen mensuel pour un profil senior en MAD],
        "currency": "MAD",
        "period": "month",
        "source": "[Glassdoor, Levels.fyi ou Indeed]"
      }}
    ],
    "actualites": ["[Actualité ou fait marquant 1]", "[Actualité ou fait marquant 2]"],
    "interview_difficulty": "[easy ou medium ou hard]",
    "interview_questions": [
      "[Vraie question technique 1]",
      "[Vraie question technique 2]",
      "[Vraie question technique 3]",
      "[Vraie question de fit/RH 1]",
      "[Vraie question de fit/RH 2]",
      "[Vraie question de fit/RH 3]"
    ],
    "pros": ["[Point fort 1]", "[Point fort 2]"],
    "cons": ["[Point faible 1]", "[Point faible 2]"],
    "career_opportunities": "[Synthèse des opportunités d'avancement professionnel]"
  }},
  "score": [Note globale de compatibilité de 0 à 100],
  "recommendations": ["[Conseil stratégique pour l'entretien 1]", "[Conseil stratégique pour l'entretien 2]"]
}}
"""

