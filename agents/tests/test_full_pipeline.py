"""
Test complet du pipeline — Exécute les 7 agents et affiche les résultats.

Usage:
    python -m tests.test_full_pipeline

Pour utiliser un ID utilisateur différent :
    python -m tests.test_full_pipeline --user-id "votre-uuid"

L'offre peut être modifiée directement dans OFFER_TEXT ci-dessous.
"""
import asyncio
import json
import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import logging
logging.basicConfig(level=logging.INFO, format="%(asctime)s | %(levelname)-8s | %(message)s")
logger = logging.getLogger("test_pipeline")

OFFER_TEXT = """\
Société : Inetum
Poste : Développeur Full Stack Senior
Type : CDI
Localisation : Rabat, Maroc (Hybride - 2 jours/semaine sur site)

Description du poste :
Nous recherchons un Développeur Full Stack Senior pour rejoindre notre équipe R&D au Maroc.

Missions principales :
- Concevoir et développer des applications web modernes avec Angular 19 et .NET 10
- Participer à l'architecture technique et aux choix technologiques
- Optimiser les performances des applications existantes
- Assurer la qualité du code via des tests unitaires et d'intégration
- Encadrer les développeurs juniors et participer aux code reviews
- Collaborer avec les équipes produit et design

Compétences requises :
- Angular 19, TypeScript, RxJS, Ngrx
- .NET 10, C# 13, ASP.NET Core Web API
- Entity Framework Core, PostgreSQL (pgvector est un plus)
- Docker, Kubernetes, CI/CD (GitHub Actions)
- SignalR, Redis, Keycloak/OIDC
- Expérience en architecture microservices
- Minimum 5 ans d'expérience en développement web
- Niveau d'études : Bac+5 (Master ou Ingénieur)

Compétences souhaitées :
- Python, FastAPI, LangChain
- Connaissances en IA/ML et systèmes multi-agents
- Expérience avec Azure DevOps
- Certification AWS ou Azure
"""

DEFAULT_USER_ID = "a9709404-ae96-4070-91a3-21142ded4139"

SEPARATOR = "=" * 72


def pretty(obj):
    return json.dumps(obj, indent=2, ensure_ascii=False, default=str) if obj else "None"


async def test_pipeline(user_id: str):
    from app.domain.pipeline.workflow import get_offer_pipeline

    pipeline = get_offer_pipeline()
    initial_state = {
        "raw_offer_text": OFFER_TEXT,
        "user_id": user_id,
        "template_id": 1,
        "offer_id": "00000000-0000-0000-0000-000000000001",
        "messages": [],
        "errors": [],
        "normalized_offer_skills": [],
        "normalized_keywords": [],
        "normalized_profile_skills": [],
    }

    print(f"\n{SEPARATOR}")
    print("  NEXTSTEP — TEST PIPELINE COMPLET (7 AGENTS)")
    print(f"  User ID   : {user_id}")
    print(f"  Entreprise: Inetum")
    print(f"  Poste     : Développeur Full Stack Senior")
    print(f"{SEPARATOR}\n")

    print(">>> Exécution du pipeline LangGraph...")
    final_state = await pipeline.ainvoke(initial_state)
    print(">>> Pipeline terminé.\n")

    # ── Agent 1 : Offer Analyzer ──────────────────────────────
    print(f"{SEPARATOR}")
    print("  AGENT 1 — OFFER ANALYZER")
    print(f"{SEPARATOR}")
    ao = final_state.get("analyzed_offer")
    if ao:
        print(f"  Titre       : {ao.get('titre', 'N/A')}")
        print(f"  Entreprise  : {ao.get('entreprise', 'N/A')}")
        print(f"  Contrat     : {ao.get('type_contrat', 'N/A')}")
        print(f"  Localisation: {ao.get('localisation', 'N/A')}")
        print(f"  Niveau      : {ao.get('niveau_etudes', 'N/A')}")
        print(f"  Expérience  : {ao.get('annees_experience', 'N/A')} ans")
        print(f"\n  Compétences requises ({len(ao.get('competences_requises', []))}):")
        for s in ao.get("competences_requises", []):
            print(f"    - {s}")
        print(f"\n  Compétences souhaitées ({len(ao.get('competences_souhaitees', []))}):")
        for s in ao.get("competences_souhaitees", []):
            print(f"    - {s}")
        print(f"\n  Keywords ATS ({len(ao.get('keywords_ats', []))}):")
        for k in ao.get("keywords_ats", []):
            print(f"    - {k}")
    else:
        print("  ⚠️ analyzed_offer = None")

    # ── Agent 2 : Profile Retriever ───────────────────────────
    print(f"\n{SEPARATOR}")
    print("  AGENT 2 — PROFILE RETRIEVER")
    print(f"{SEPARATOR}")
    pd = final_state.get("profile_data")
    if pd:
        print(f"  Nom      : {pd.get('prenom', 'N/A')} {pd.get('nom', 'N/A')}")
        print(f"  Email    : {pd.get('email', 'N/A')}")
        skills = pd.get("competences", pd.get("skills", []))
        print(f"  Skills   : {len(skills)} compétences")
        exps = pd.get("experiences", pd.get("experiences_professionnelles", []))
        print(f"  Exp.     : {len(exps)} expériences")
        formations = pd.get("formations", [])
        print(f"  Formations: {len(formations)} diplômes")
    else:
        print("  ⚠️ profile_data = None")

    # ── Agent 3 : Skill Gap ───────────────────────────────────
    print(f"\n{SEPARATOR}")
    print("  AGENT 3 — SKILL GAP (SCORING)")
    print(f"{SEPARATOR}")
    mr = final_state.get("match_result")
    if mr:
        print(f"  Relevance Score : {mr.get('relevance_score', 'N/A')}")
        print(f"  Score Matching  : {mr.get('score_matching', 'N/A')} / 100")
        print(f"  Flag            : {mr.get('flag', 'N/A')}")
        print(f"  Exp. requises   : {mr.get('required_years', 'N/A')} ans")
        print(f"  Exp. candidat   : {mr.get('experience_years', 'N/A')} ans")
        print(f"  Gap             : {mr.get('experience_gap_years', 'N/A')} ans")
        print(f"  Certif. match   : {mr.get('cert_match', 'N/A')}")
        print(f"\n  ✅ Compétences MATCHÉES ({len(mr.get('matched_skills', []))}):")
        for s in mr.get("matched_skills", []):
            print(f"    - {s}")
        print(f"\n  ❌ Compétences MANQUANTES ({len(mr.get('missing_skills', []))}):")
        for s in mr.get("missing_skills", []):
            print(f"    - {s}")
        recs = mr.get("recommendations", [])
        if recs:
            print(f"\n  💡 Recommandations ({len(recs)}):")
            for r in recs:
                t = r.get("title", r.get("type", ""))
                d = r.get("description", "")
                print(f"    - {t}: {d[:100]}...")
    else:
        print("  ⚠️ match_result = None")

    # ── Agent 4 : Company Intelligence ────────────────────────
    print(f"\n{SEPARATOR}")
    print("  AGENT 4 — COMPANY INTELLIGENCE")
    print(f"{SEPARATOR}")
    ci = final_state.get("company_intelligence")
    if ci:
        intel = ci.get("intelligence", {})
        print(f"  Nom        : {intel.get('nom', 'N/A')}")
        culture = intel.get("culture", {})
        print(f"  Glassdoor  : {culture.get('glassdoor_rating', 'N/A')} ★")
        print(f"  Culture    : {culture.get('culture_score', 'N/A')} / 100")
        salaries = intel.get("salaries", [])
        if salaries:
            s = salaries[0]
            print(f"  Salaire    : {s.get('min_salary', '?')} - {s.get('max_salary', '?')} {s.get('currency', 'MAD')}")
        print(f"  Summary    : {intel.get('summary', 'N/A')[:200]}")
        actualites = intel.get("actualites", [])
        if actualites:
            print(f"  Actualités :")
            for a in actualites[:3]:
                print(f"    - {a}")
        print(f"  Score comp.: {ci.get('score', 'N/A')}")
    else:
        print("  ⚠️ company_intelligence = None")

    # ── Agent 5 : CV Optimizer ────────────────────────────────
    print(f"\n{SEPARATOR}")
    print("  AGENT 5 — CV OPTIMIZER")
    print(f"{SEPARATOR}")
    cv_opt = final_state.get("cv_optimized_content")
    if cv_opt:
        sections = list(cv_opt.keys())[:8]
        print(f"  Sections produites ({len(sections)}):")
        for s in sections:
            val = cv_opt.get(s)
            if isinstance(val, str):
                print(f"    - {s}: {val[:80]}...")
            elif isinstance(val, list):
                print(f"    - {s}: [{len(val)} éléments]")
            else:
                print(f"    - {s}: {str(val)[:80]}")
    else:
        print("  ⚠️ cv_optimized_content = None")

    # ── Agent 6 : CV Engine (formatage QuestPDF) ─────────────
    print(f"\n{SEPARATOR}")
    print("  AGENT 6 — CV ENGINE (FORMATAGE QUESTPDF)")
    print(f"{SEPARATOR}")
    cv_eng = final_state.get("cv_engine_result")
    if cv_eng:
        if isinstance(cv_eng, dict):
            for k, v in cv_eng.items():
                if isinstance(v, str) and len(v) > 100:
                    print(f"  {k}: {v[:100]}...")
                elif isinstance(v, list):
                    print(f"  {k}: [{len(v)} éléments]")
                else:
                    print(f"  {k}: {v}")
        else:
            print(f"  {cv_eng}")
    else:
        print("  ⚠️ cv_engine_result = None")

    # ── Erreurs ───────────────────────────────────────────────
    errors = final_state.get("errors", [])
    if errors:
        print(f"\n{SEPARATOR}")
        print(f"  ⚠️ ERREURS ({len(errors)})")
        print(f"{SEPARATOR}")
        for e in errors:
            print(f"    - {e}")

    # ── Résumé ────────────────────────────────────────────────
    print(f"\n{SEPARATOR}")
    print("  RÉSUMÉ DU PIPELINE")
    print(f"{SEPARATOR}")
    score = mr.get("score_matching", 0) if mr else 0
    skills_matched = len(mr.get("matched_skills", [])) if mr else 0
    skills_missing = len(mr.get("missing_skills", [])) if mr else 0
    profile_ok = pd is not None
    company_ok = ci is not None
    cv_ok = cv_eng is not None
    print(f"  ✅ Score Matching : {score}%")
    print(f"  ✅ Skills Matchés : {skills_matched}")
    print(f"  ✅ Skills Manquants: {skills_missing}")
    print(f"  ✅ Profil trouvé  : {'Oui' if profile_ok else 'Non'}")
    print(f"  ✅ Company Intel  : {'Oui' if company_ok else 'Non'}")
    print(f"  ✅ CV Généré      : {'Oui' if cv_ok else 'Non'}")
    print(f"  ⚠️  Erreurs        : {len(errors)}")
    print(f"{SEPARATOR}\n")


if __name__ == "__main__":
    user_id = DEFAULT_USER_ID
    if "--user-id" in sys.argv:
        idx = sys.argv.index("--user-id")
        if idx + 1 < len(sys.argv):
            user_id = sys.argv[idx + 1]

    asyncio.run(test_pipeline(user_id))
