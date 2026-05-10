# ============================================================
# scratch/test_real_db_pipeline.py
# Test E2E du pipeline complet avec un vrai user_id en base
# ============================================================
import asyncio
import sys
import os
import json
import time
import logging

# ─── Fix Windows console encoding ─────────────────────────────
if sys.platform == "win32":
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")

# ─── Setup path ───────────────────────────────────────────────
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s | %(levelname)-8s | %(name)s — %(message)s",
)
logger = logging.getLogger("pipeline_test")

# ─── Imports des services ─────────────────────────────────────
from app.domain.pipeline.workflow import get_offer_pipeline

# ─── Configuration du test ────────────────────────────────────
USER_ID = "a9709404-ae96-4070-91a3-21142ded4139"

# Offre d'emploi réaliste pour tester le pipeline
RAW_OFFER_TEXT = """
Développeur Full Stack - CDI - Paris

Entreprise : TechVision SAS
Lieu : Paris 9e (Hybride - 3j bureau / 2j remote)
Salaire : 45K - 55K € brut annuel

Description du poste :
Nous recherchons un Développeur Full Stack passionné pour rejoindre notre équipe produit.
Vous participerez au développement de notre plateforme SaaS de gestion RH utilisée par
plus de 500 entreprises en France.

Missions principales :
- Développer de nouvelles fonctionnalités frontend en Angular 17+ et TypeScript
- Concevoir et implémenter des API REST avec .NET 8 / C#
- Participer à la conception de la base de données PostgreSQL
- Mettre en place des tests unitaires et d'intégration
- Collaborer avec l'équipe UX/UI pour implémenter les maquettes Figma
- Participer aux code reviews et aux cérémonies agiles (Scrum)
- Contribuer à l'amélioration continue de la CI/CD (GitHub Actions, Docker)

Compétences requises :
- 3+ ans d'expérience en développement web
- Maîtrise de Angular (v14+) et TypeScript
- Expérience avec .NET / C# (ASP.NET Core)
- Bonne connaissance de PostgreSQL et/ou SQL Server
- Connaissance de Docker et des pratiques DevOps
- Expérience avec Git et les workflows CI/CD
- Capacité à travailler en équipe agile

Compétences appréciées :
- Expérience avec les architectures microservices
- Connaissance de RabbitMQ ou Kafka
- Familiarité avec le cloud Azure ou AWS
- Expérience avec Entity Framework Core
- Notions de machine learning / IA

Avantages :
- Tickets restaurant (Swile)
- Mutuelle Alan
- RTT
- Budget formation annuel 1500€
- Matériel dernière génération (MacBook Pro M3)
"""


def print_section(title: str, char: str = "═", width: int = 70):
    """Helper to print a visual section separator."""
    print(f"\n{char * width}")
    print(f"  {title}")
    print(f"{char * width}")


def print_subsection(title: str):
    print(f"\n  ── {title} {'─' * (50 - len(title))}")


def truncate_json(data, max_depth=3, current_depth=0):
    """Truncate deep nested JSON for readable output."""
    if current_depth >= max_depth:
        if isinstance(data, dict):
            return f"{{...{len(data)} keys...}}"
        elif isinstance(data, list):
            return f"[...{len(data)} items...]"
        return data

    if isinstance(data, dict):
        return {k: truncate_json(v, max_depth, current_depth + 1) for k, v in data.items()}
    elif isinstance(data, list):
        if len(data) > 5:
            return [truncate_json(item, max_depth, current_depth + 1) for item in data[:3]] + [
                f"...+{len(data) - 3} more items..."
            ]
        return [truncate_json(item, max_depth, current_depth + 1) for item in data]
    return data


async def test_full_pipeline():
    """
    Test E2E : exécute le pipeline complet comme le ferait le endpoint
    POST /offer/run-pipeline avec un vrai user en base.
    """
    print_section("🚀 TEST PIPELINE E2E — REAL DATABASE", "═")
    print(f"  User ID   : {USER_ID}")
    print(f"  Offre     : {len(RAW_OFFER_TEXT)} caractères")
    print(f"  Timestamp : {time.strftime('%Y-%m-%d %H:%M:%S')}")

    # ─── 1. Construire l'état initial (identique à offer_routes.py) ───
    initial_state = {
        "raw_offer_text": RAW_OFFER_TEXT,
        "user_id": USER_ID,
        "template_id": 1,
        "messages": [],
        "errors": [],
        "normalized_offer_skills": [],
        "normalized_keywords": [],
        "normalized_profile_skills": [],
    }

    # ─── 2. Compiler et exécuter le pipeline ──────────────────────────
    print_section("⏳ Exécution du pipeline...", "─")
    pipeline = get_offer_pipeline()

    start_time = time.time()
    try:
        final_state = await pipeline.ainvoke(initial_state)
    except Exception as e:
        print(f"\n  ❌ PIPELINE CRASHED: {type(e).__name__}: {e}")
        import traceback
        traceback.print_exc()
        return
    elapsed = time.time() - start_time

    print(f"\n  ✅ Pipeline terminé en {elapsed:.2f}s")

    # ─── 3. Vérification des erreurs ──────────────────────────────────
    errors = final_state.get("errors", [])
    print_section(f"{'❌' if errors else '✅'} ERREURS ({len(errors)})", "─")
    if errors:
        for i, err in enumerate(errors, 1):
            print(f"  {i}. {err}")
    else:
        print("  Aucune erreur détectée.")

    # ─── 4. Résultats par nœud ────────────────────────────────────────

    # 4a. Offer Analyzer
    print_section("📋 NODE 1 — Offer Analyzer", "─")
    analyzed = final_state.get("analyzed_offer")
    if analyzed:
        print(f"  ✅ Offre analysée — {len(analyzed)} clés: {list(analyzed.keys())}")
        # Afficher quelques champs clés
        for key in ["title", "company", "location", "contract_type", "salary"]:
            val = analyzed.get(key)
            if val:
                print(f"     • {key}: {val}")
    else:
        print("  ❌ analyzed_offer est None/vide")

    skills = final_state.get("normalized_offer_skills", [])
    keywords = final_state.get("normalized_keywords", [])
    print(f"  Skills normalisés    : {len(skills)} — {skills[:8]}{'...' if len(skills) > 8 else ''}")
    print(f"  Keywords normalisés  : {len(keywords)} — {keywords[:8]}{'...' if len(keywords) > 8 else ''}")

    # 4b. Profile Retriever
    print_section("👤 NODE 2 — Profile Retriever", "─")
    profile = final_state.get("profile_data")
    if profile:
        print(f"  ✅ Profil récupéré — {len(profile)} clés: {list(profile.keys())}")
        # Résumé du profil
        if "personalInfo" in profile or "personal_info" in profile:
            pi = profile.get("personalInfo") or profile.get("personal_info") or {}
            name = f"{pi.get('firstName', '')} {pi.get('lastName', '')}".strip()
            print(f"     • Nom: {name or 'N/A'}")
        if "experiences" in profile:
            print(f"     • Expériences: {len(profile['experiences'])} entrées")
        if "skills" in profile:
            print(f"     • Compétences: {len(profile['skills'])} entrées")
        if "educations" in profile:
            print(f"     • Formations: {len(profile['educations'])} entrées")
    else:
        print("  ❌ profile_data est None/vide")

    # 4c. Skill Gap
    print_section("📊 NODE 3 — Skill Gap Analysis", "─")
    match_result = final_state.get("match_result")
    if match_result:
        print(f"  ✅ Résultat Skill Gap — {len(match_result)} clés: {list(match_result.keys())}")
        # Afficher le score si disponible
        for key in ["overall_score", "ats_score", "score", "match_score", "compatibility_score"]:
            if key in match_result:
                print(f"     • {key}: {match_result[key]}")
        # Afficher les gaps si disponibles
        for key in ["missing_skills", "gaps", "skill_gaps"]:
            if key in match_result:
                gaps = match_result[key]
                print(f"     • {key}: {len(gaps) if isinstance(gaps, list) else gaps}")
        print(f"\n  Aperçu (tronqué):")
        print(json.dumps(truncate_json(match_result, max_depth=2), indent=2, ensure_ascii=False))
    else:
        print("  ❌ match_result est None/vide")

    # 4d. CV Optimizer
    print_section("✏️ NODE 4 — CV Optimizer", "─")
    cv_optimized = final_state.get("cv_optimized_content")
    if cv_optimized:
        print(f"  ✅ CV optimisé — {len(cv_optimized)} clés: {list(cv_optimized.keys())}")
        print(f"\n  Aperçu (tronqué):")
        print(json.dumps(truncate_json(cv_optimized, max_depth=2), indent=2, ensure_ascii=False))
    else:
        print("  ❌ cv_optimized_content est None/vide")

    # 4e. CV Engine (QuestPDF)
    print_section("📄 NODE 5 — CV Engine (QuestPDF)", "─")
    cv_engine = final_state.get("cv_engine_result")
    if cv_engine:
        print(f"  ✅ Payload QuestPDF généré — {len(cv_engine)} clés: {list(cv_engine.keys())}")
        print(f"\n  Aperçu (tronqué):")
        print(json.dumps(truncate_json(cv_engine, max_depth=2), indent=2, ensure_ascii=False))
    else:
        print("  ❌ cv_engine_result est None/vide")

    # ─── 5. Messages du pipeline ──────────────────────────────────────
    print_section("💬 MESSAGES DU PIPELINE", "─")
    messages = final_state.get("messages", [])
    for msg in messages:
        content = msg.content if hasattr(msg, "content") else str(msg)
        name = getattr(msg, "name", "unknown")
        print(f"  [{name}] {content}")

    # ─── 6. Résumé Final ──────────────────────────────────────────────
    print_section("📝 RÉSUMÉ FINAL", "═")
    checks = {
        "Offer Analyzer":    bool(analyzed),
        "Profile Retriever": bool(profile),
        "Skill Gap":         bool(match_result),
        "CV Optimizer":      bool(cv_optimized),
        "CV Engine":         bool(cv_engine),
    }
    all_ok = all(checks.values())

    for node, ok in checks.items():
        status = "✅" if ok else "❌"
        print(f"  {status} {node}")

    print(f"\n  Temps total : {elapsed:.2f}s")
    print(f"  Erreurs     : {len(errors)}")
    print(f"  Statut      : {'✅ PIPELINE OK' if all_ok and not errors else '⚠️ PIPELINE PARTIEL' if any(checks.values()) else '❌ PIPELINE FAILED'}")
    print("═" * 70)

    # ─── 7. Sauvegarder le résultat complet en JSON ───────────────────
    output_path = os.path.join(os.path.dirname(__file__), "pipeline_result.json")
    serializable_state = {}
    for key in ["analyzed_offer", "profile_data", "match_result", "cv_optimized_content", "cv_engine_result", "errors",
                 "normalized_offer_skills", "normalized_keywords"]:
        serializable_state[key] = final_state.get(key)

    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(serializable_state, f, indent=2, ensure_ascii=False, default=str)
    print(f"\n  💾 Résultat complet sauvegardé → {output_path}")


async def test_individual_nodes():
    """
    Test individuel de chaque service séparément pour isoler les erreurs.
    """
    print_section("🔬 TEST INDIVIDUEL DES SERVICES", "═")

    # --- Test 1: Profile Retriever ---
    print_subsection("Test Profile Retriever")
    try:
        from app.domain.profile_retriever.service import profile_retriever_service
        t0 = time.time()
        profile_res = await profile_retriever_service.get_profile(USER_ID)
        t1 = time.time()
        profile_data = profile_res.get("profile_data")
        if profile_data:
            print(f"  ✅ Profil récupéré en {t1-t0:.2f}s — {len(profile_data)} clés")
        else:
            print(f"  ❌ Profil vide — errors: {profile_res.get('errors')}")
            return  # On ne peut pas continuer sans profil
    except Exception as e:
        print(f"  ❌ Erreur: {e}")
        import traceback; traceback.print_exc()
        return

    # --- Test 2: Offer Analyzer ---
    print_subsection("Test Offer Analyzer")
    try:
        from app.domain.offer_analyzer.service import offer_analyzer_service
        t0 = time.time()
        offer_res = await offer_analyzer_service.analyze(RAW_OFFER_TEXT)
        t1 = time.time()
        analyzed_offer = offer_res.get("analyzed_offer")
        if analyzed_offer:
            print(f"  ✅ Offre analysée en {t1-t0:.2f}s — {len(analyzed_offer)} clés")
        else:
            print(f"  ❌ Analyse vide — errors: {offer_res.get('errors')}")
            return
    except Exception as e:
        print(f"  ❌ Erreur: {e}")
        import traceback; traceback.print_exc()
        return

    # --- Test 3: Skill Gap ---
    print_subsection("Test Skill Gap")
    try:
        from app.domain.skill_gap.service import skill_gap_service
        t0 = time.time()
        gap_res = await skill_gap_service.analyze_skill_gap(
            candidate_cv=profile_data,
            job_offer=analyzed_offer
        )
        t1 = time.time()
        if gap_res and gap_res.skill_gap:
            print(f"  ✅ Skill Gap analysé en {t1-t0:.2f}s")
            print(f"     Résultat: {json.dumps(truncate_json(gap_res.skill_gap.model_dump(), 2), indent=2, ensure_ascii=False)}")
        else:
            print(f"  ❌ Skill Gap vide — errors: {gap_res.errors if gap_res else 'None'}")
    except Exception as e:
        print(f"  ❌ Erreur: {e}")
        import traceback; traceback.print_exc()

    # --- Test 4: CV Optimizer ---
    print_subsection("Test CV Optimizer")
    try:
        from app.domain.cv_optimizer.service import cv_optimizer_service
        t0 = time.time()
        opt_res = await cv_optimizer_service.optimize_cv(
            candidate_cv=profile_data,
            job_offer=analyzed_offer
        )
        t1 = time.time()
        if opt_res:
            opt_dict = opt_res.model_dump()
            print(f"  ✅ CV optimisé en {t1-t0:.2f}s — {len(opt_dict)} clés")
        else:
            print(f"  ❌ Optimisation vide")
    except Exception as e:
        print(f"  ❌ Erreur: {e}")
        import traceback; traceback.print_exc()
        opt_res = None

    # --- Test 5: CV Engine ---
    if opt_res:
        print_subsection("Test CV Engine")
        try:
            from app.domain.cv_engine.service import cv_engine_service
            t0 = time.time()
            engine_res = await cv_engine_service.format_for_questpdf(
                original_profile=profile_data,
                optimized_cv=opt_res.model_dump()
            )
            t1 = time.time()
            if engine_res:
                print(f"  ✅ Payload QuestPDF généré en {t1-t0:.2f}s — {len(engine_res)} clés")
            else:
                print(f"  ❌ Formatage vide")
        except Exception as e:
            print(f"  ❌ Erreur: {e}")
            import traceback; traceback.print_exc()

    print("\n" + "═" * 70)
    print("  🔬 Tests individuels terminés.")
    print("═" * 70)


async def main():
    """Point d'entrée principal — choix du mode de test."""
    print("\n" + "█" * 70)
    print("  NEXTSTEP — TEST PIPELINE E2E AVEC BASE DE DONNÉES RÉELLE")
    print("█" * 70)

    mode = os.environ.get("TEST_MODE", "full")

    if mode == "individual":
        await test_individual_nodes()
    elif mode == "full":
        await test_full_pipeline()
    elif mode == "both":
        await test_individual_nodes()
        print("\n\n")
        await test_full_pipeline()
    else:
        # Par défaut, run le pipeline complet
        await test_full_pipeline()


if __name__ == "__main__":
    asyncio.run(main())
