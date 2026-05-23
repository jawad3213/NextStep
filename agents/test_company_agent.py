import argparse
import json
import sys
from typing import Any

import requests


def _pretty(data: Any) -> str:
    return json.dumps(data, indent=2, ensure_ascii=False)


def _quality_checks(result: dict) -> list[str]:
    checks: list[str] = []
    intel = result.get("intelligence") or {}
    score = result.get("score")
    recs = result.get("recommendations") or []
    summary = result.get("summary")
    skill_gap = result.get("skill_gap")

    if intel:
        checks.append("OK: intelligence payload present")
    else:
        checks.append("KO: intelligence payload is empty")

    if isinstance(score, int) and 0 <= score <= 100:
        checks.append(f"OK: score in range [0..100] -> {score}")
    else:
        checks.append(f"KO: invalid score -> {score}")

    if isinstance(summary, str) and summary.strip():
        checks.append("OK: summary generated")
    else:
        checks.append("KO: summary missing")

    if isinstance(recs, list):
        checks.append(f"OK: recommendations list size = {len(recs)}")
    else:
        checks.append("KO: recommendations is not a list")

    if skill_gap is not None:
        checks.append("OK: skill_gap present")
    else:
        checks.append("WARN: skill_gap missing/null")

    culture = (intel or {}).get("culture")
    if isinstance(culture, dict) and culture:
        checks.append("OK: culture data present")
    else:
        checks.append("WARN: culture data missing/empty")

    salaries = (intel or {}).get("salaries")
    if isinstance(salaries, list):
        checks.append(f"OK: salaries entries = {len(salaries)}")
    else:
        checks.append("WARN: salaries missing/not a list")

    return checks


def build_payload(user_id: str, company_name: str, job_title: str) -> dict:
    return {
        "company_name": company_name,
        "user_id": user_id,
        "profile_data": {
            "competences": [
                {"nom": "Python", "type_competence": "hard_skill", "niveau": 4},
                {"nom": "Docker", "type_competence": "hard_skill", "niveau": 3},
                {"nom": "AWS", "type_competence": "hard_skill", "niveau": 3},
            ],
            "experiences": [
                {
                    "titre": "Software Engineer",
                    "entreprise": "TechCorp",
                    "description": "Backend APIs, cloud deployment, data pipelines",
                }
            ],
            "projets": [
                {
                    "titre": "RAG Platform",
                    "description": "Vector DB + embeddings + APIs",
                    "technologies": ["Python", "FastAPI", "Docker", "AWS"],
                }
            ],
            "resume": "Ingénieur logiciel orienté IA, backend et cloud.",
        },
        "offer_data": {
            "titre": job_title,
            "entreprise": company_name,
            "typeContrat": "CDI",
            "localisation": "Lyon",
            "competencesRequises": [
                "Infrastructure as Code (Terraform)",
                "Vector Databases",
                "Node.js",
                "Golang",
                "AWS",
            ],
            "competencesSouhaitees": ["LangChain", "LlamaIndex"],
            "keywordsAts": [
                "Terraform",
                "Vector Databases",
                "AWS",
                "Node.js",
                "Golang",
                "LangChain",
                "LlamaIndex",
                "MongoDB",
                "DynamoDB",
            ],
            "descriptionPoste": "Construire des systèmes RAG robustes à grande échelle.",
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Test standalone de l'agent company via HTTP API")
    parser.add_argument("--base-url", default="http://localhost:8000", help="Base URL API agents")
    parser.add_argument("--user-id", required=True, help="UUID utilisateur")
    parser.add_argument("--company", default="Lumina AI", help="Nom de l'entreprise")
    parser.add_argument("--job-title", default="Ingénieur Software - AI Systems & Data", help="Intitulé du poste")
    parser.add_argument("--timeout", type=int, default=180, help="Timeout HTTP en secondes")
    parser.add_argument("--raw", action="store_true", help="Afficher seulement le JSON brut")
    args = parser.parse_args()

    url = f"{args.base_url.rstrip('/')}/company/analyze-company"
    payload = build_payload(args.user_id, args.company, args.job_title)

    print(f"[INFO] POST {url}")
    print(f"[INFO] company={args.company} job_title={args.job_title} user_id={args.user_id}")

    try:
        resp = requests.post(url, json=payload, timeout=args.timeout)
    except Exception as e:
        print(f"[ERROR] Request failed: {e}")
        return 2

    print(f"[INFO] status={resp.status_code}")
    try:
        data = resp.json()
    except Exception:
        print("[ERROR] Non-JSON response:")
        print(resp.text)
        return 3

    if args.raw:
        print(_pretty(data))
        return 0 if resp.ok else 1

    print("\n=== RESPONSE JSON ===")
    print(_pretty(data))

    print("\n=== QUALITY CHECKS ===")
    for line in _quality_checks(data):
        print(f"- {line}")

    if not resp.ok:
        print("\n[RESULT] FAIL (HTTP not OK)")
        return 1

    print("\n[RESULT] DONE")
    return 0


if __name__ == "__main__":
    sys.exit(main())
