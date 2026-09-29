# ============================================================
# app/domain/job_boards/common.py
# Helpers shared by the job-board scrapers (LinkedIn, Indeed, Glassdoor):
# HTML selectors, IT-offer classification, contract types, recency windows,
# de-duplication, and page fetching with a concurrency limit.
# ============================================================
import asyncio
import logging
import re
from contextvars import ContextVar
from typing import Optional

logger = logging.getLogger(__name__)

# Pages fetched at the same time across all searches (job boards block bursts).
FETCH_CONCURRENCY = 6
_fetch_semaphore: asyncio.Semaphore | None = None
_fetch_loop: asyncio.AbstractEventLoop | None = None

# Problems seen while fetching pages for the current search (HTTP errors, anti-bot pages).
# Lets a search that found nothing say *why* instead of looking like "no offers".
_fetch_issues: ContextVar[list[str] | None] = ContextVar("job_board_fetch_issues", default=None)

# Text only anti-bot / challenge pages contain (Cloudflare, PerimeterX, Akamai, Indeed, Glassdoor).
_BLOCK_MARKERS = (
    "captcha",
    "cf-challenge",
    "challenge-platform",
    "just a moment...",
    "verify you are human",
    "are you a robot",
    "pardon our interruption",
    "access denied",
    "request unsuccessful",
    "unusual traffic",
)
_MAX_CHALLENGE_PAGE_CHARS = 150_000


def start_fetch_tracking() -> list[str]:
    """Starts collecting fetch problems for the current search; returns the (live) list."""
    issues: list[str] = []
    _fetch_issues.set(issues)
    return issues


def _record_fetch_issue(issue: str) -> None:
    issues = _fetch_issues.get()
    if issues is not None and issue not in issues:
        issues.append(issue)


def detect_fetch_problem(page) -> Optional[str]:
    """Why a fetched page holds no usable listings (HTTP error or anti-bot page), or None."""
    status = getattr(page, "status", None)
    if isinstance(status, int) and status >= 400:
        if status in (403, 429):
            return f"blocked by the site (HTTP {status})"
        return f"HTTP {status}"
    try:
        html = str(getattr(page, "html_content", "") or "")
    except Exception:
        html = ""
    # Challenge pages are small; real result pages are large and can mention "captcha" in scripts.
    if len(html) > _MAX_CHALLENGE_PAGE_CHARS:
        return None
    html = html.lower()
    marker = next((m for m in _BLOCK_MARKERS if m in html), None)
    if marker:
        return f"anti-bot page returned ({marker})"
    return None


async def fetch_page(url: str):
    """GET a page with Scrapling's HTTP fetcher (in a thread), at most FETCH_CONCURRENCY at once.

    Failed or blocked fetches are logged and recorded for the current search."""
    global _fetch_semaphore, _fetch_loop
    loop = asyncio.get_running_loop()
    if _fetch_semaphore is None or _fetch_loop is not loop:
        _fetch_semaphore, _fetch_loop = asyncio.Semaphore(FETCH_CONCURRENCY), loop
    async with _fetch_semaphore:
        try:
            page = await asyncio.to_thread(_get_fetcher().get, url)
        except Exception as exc:
            logger.warning("Job board fetch failed: %s (%s: %s)", url, type(exc).__name__, exc)
            _record_fetch_issue(f"request failed ({type(exc).__name__})")
            raise
    problem = detect_fetch_problem(page)
    if problem:
        logger.warning("Job board fetch unusable: %s -> %s", url, problem)
        _record_fetch_issue(problem)
    return page


IT_TERMS = {
    "software",
    "developer",
    "engineer",
    "frontend",
    "backend",
    "full stack",
    "fullstack",
    "devops",
    "sre",
    "site reliability",
    "platform",
    "data",
    "analytics",
    "analyst",
    "machine learning",
    "ml",
    "artificial intelligence",
    "ai",
    "cloud",
    "security",
    "cybersecurity",
    "qa",
    "test automation",
    "mobile",
    "ios",
    "android",
    "python",
    "java",
    "javascript",
    "typescript",
    "angular",
    "react",
    "node",
    "node.js",
    "dotnet",
    ".net",
    "c#",
    "php",
    "golang",
    "go",
    "kubernetes",
    "docker",
    "database",
    "etl",
    "bi",
    "erp",
    "systems",
    "it support",
    "helpdesk",
    "network",
    "infrastructure",
    "product owner",
    "product manager",
    "scrum master",
}


CONTRACT_TYPE_TERMS = {
    "internship": {"internship", "intern", "stage", "stagiaire", "stage pfe", "stage pre-embauche"},
    "cdi": {"cdi", "permanent", "full-time permanent", "contrat a duree indeterminee"},
    "cdd": {"cdd", "contrat a duree determinee", "fixed term", "fixed-term", "contractuel"},
    "freelance": {"freelance", "freelancer", "contract", "contractor", "consultant indépendant"},
    "alternance": {"alternance", "apprenticeship", "apprenti", "work-study"},
    "part_time": {"part time", "part-time", "temps partiel"},
    "full_time": {"full time", "full-time", "temps plein"},
    "temporary": {"temporary", "temporaire", "interim"},
}


POSTED_WINDOW_TO_SECONDS = {
    "24h": 24 * 60 * 60,
    "3d": 3 * 24 * 60 * 60,
    "7d": 7 * 24 * 60 * 60,
    "14d": 14 * 24 * 60 * 60,
    "30d": 30 * 24 * 60 * 60,
}


def _get_fetcher():
    try:
        from scrapling.fetchers import Fetcher
    except ImportError as exc:
        raise RuntimeError(
            "Scrapling fetchers are unavailable. Install the agents dependencies so "
            "the scrapling packages from requirements.txt are installed before calling this agent."
        ) from exc
    return Fetcher


def _clean_text(value: Optional[str]) -> Optional[str]:
    if value is None:
        return None
    clean = re.sub(r"\s+", " ", value).strip()
    return clean or None


def _selector_text(node, selectors: list[str]) -> Optional[str]:
    for selector in selectors:
        try:
            value = node.css(selector).get()
        except Exception:
            value = None
        clean = _clean_text(value)
        if clean:
            return clean
    return None


def _selector_attr(node, selectors: list[str]) -> Optional[str]:
    for selector in selectors:
        try:
            value = node.css(selector).get()
        except Exception:
            value = None
        if value:
            return value.strip()
    return None


def _selector_block_text(node, selectors: list[str]) -> Optional[str]:
    for selector in selectors:
        try:
            matches = node.css(selector)
        except Exception:
            matches = []
        if not matches:
            continue
        try:
            texts = matches[0].css("::text").getall()
        except Exception:
            texts = []
        clean = _clean_text(" ".join(texts))
        if clean:
            return clean
    return None


def _has_term(corpus: str, term: str) -> bool:
    """Whole-word (or whole-phrase) match: "ai" must not match "email", "intern" not "international"."""
    return re.search(rf"(?<![a-z0-9]){re.escape(term)}(?![a-z0-9])", corpus) is not None


def _classify_it_offer(job: dict) -> tuple[bool, list[str]]:
    corpus = " ".join(
        [
            job.get("title") or "",
            job.get("description") or "",
            job.get("job_function") or "",
            job.get("employment_type") or "",
        ]
    ).lower()
    matched = sorted(term for term in IT_TERMS if _has_term(corpus, term))
    return bool(matched), matched


def _content_key(job: dict) -> Optional[str]:
    """Same title + company + location = same job, even under another id (boards repost sponsored jobs)."""
    title, company = _clean_text(job.get("title")), _clean_text(job.get("company"))
    if not title or not company:
        return None
    return "::".join(part.lower() for part in (title, company, _clean_text(job.get("location")) or ""))


def _dedupe_jobs(jobs: list[dict], limit: int) -> list[dict]:
    seen: set[str] = set()
    deduped: list[dict] = []
    for job in jobs:
        key = job.get("job_id") or job.get("url") or f"{job.get('title')}::{job.get('company')}"
        content_key = _content_key(job)
        if not key or key in seen or (content_key and content_key in seen):
            continue
        seen.add(key)
        if content_key:
            seen.add(content_key)
        deduped.append(job)
        if len(deduped) >= limit:
            break
    return deduped


def extract_relative_hours(posted_text: Optional[str]) -> Optional[int]:
    clean = (posted_text or "").strip().lower()
    if not clean:
        return None
    if any(token in clean for token in {"today", "just now", "aujourd", "maintenant"}):
        return 0

    match = re.search(r"(\d+)", clean)
    if not match:
        return None

    value = int(match.group(1))
    if any(token in clean for token in {"hour", "hours", "hr", "hrs", "heure", "heures"}):
        return value
    if any(token in clean for token in {"day", "days", "jour", "jours"}):
        return value * 24
    if any(token in clean for token in {"week", "weeks", "semaine", "semaines"}):
        return value * 24 * 7
    if any(token in clean for token in {"month", "months", "mois"}):
        return value * 24 * 30
    return None


def normalize_posted_window_to_seconds(posted_window: Optional[str]) -> Optional[int]:
    if not posted_window or posted_window == "any":
        return None
    return POSTED_WINDOW_TO_SECONDS.get(posted_window.strip().lower())


def matches_posted_window(job: dict, posted_window: Optional[str]) -> bool:
    if not posted_window or posted_window == "any":
        return True
    limit_seconds = normalize_posted_window_to_seconds(posted_window)
    if not limit_seconds:
        return True
    relative_hours = extract_relative_hours(job.get("posted_at_text"))
    if relative_hours is None:
        return True
    return relative_hours * 3600 <= limit_seconds


def normalize_contract_type(job: dict) -> Optional[str]:
    corpus = " ".join(
        [
            job.get("employment_type") or "",
            job.get("title") or "",
            job.get("description") or "",
            job.get("job_function") or "",
        ]
    ).lower()
    for contract_type, terms in CONTRACT_TYPE_TERMS.items():
        if any(_has_term(corpus, term) for term in terms):
            return contract_type
    return "other" if corpus.strip() else None


def _selector_texts(node, selectors: list[str]) -> list[str]:
    for selector in selectors:
        try:
            values = node.css(selector).getall()
        except Exception:
            values = []
        cleaned = [_clean_text(value) for value in values]
        filtered = [value for value in cleaned if value]
        if filtered:
            return filtered
    return []
