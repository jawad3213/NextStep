import argparse
import json
import sys
from typing import Any

import requests


def _pretty(data: Any) -> str:
    return json.dumps(data, indent=2, ensure_ascii=False)


def build_payload(keywords: str, location: str | None, limit: int) -> dict:
    return {
        "keywords": keywords,
        "location": location,
        "limit": limit,
        "fetch_details": False,
        "it_only": True,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Manual test for the Glassdoor jobs LangGraph agent")
    parser.add_argument("--base-url", default="http://localhost:8000", help="Agents API base url")
    parser.add_argument("--keywords", default="software engineer", help="Glassdoor job keywords")
    parser.add_argument("--location", default="Morocco", help="Glassdoor location hint")
    parser.add_argument("--limit", type=int, default=10, help="Max number of jobs to return")
    parser.add_argument("--timeout", type=int, default=180, help="HTTP timeout in seconds")
    args = parser.parse_args()

    url = f"{args.base_url.rstrip('/')}/glassdoor-jobs/search"
    payload = build_payload(args.keywords, args.location, args.limit)

    print(f"[INFO] POST {url}")
    print(f"[INFO] payload={_pretty(payload)}")
    try:
        response = requests.post(url, json=payload, timeout=args.timeout)
    except Exception as exc:
        print(f"[ERROR] Request failed: {exc}")
        return 2

    print(f"[INFO] status={response.status_code}")
    try:
        body = response.json()
    except Exception:
        print(response.text)
        return 3

    print(_pretty(body))
    return 0 if response.ok else 1


if __name__ == "__main__":
    sys.exit(main())
