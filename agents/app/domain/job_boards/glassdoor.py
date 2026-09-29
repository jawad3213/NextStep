# Board-specific scraping for Glassdoor (URLs, selectors, pagination).
import asyncio
import json
import logging
import math
import re
from typing import Optional
from urllib.parse import parse_qsl, quote_plus, urlencode, urlparse, urlunparse

<<<<<<< HEAD:agents/app/domain/glassdoor_jobs/tools/scrapling_tool.py
from app.domain.linkedin_jobs.tools.scrapling_tool import (
    IT_TERMS,
    matches_contract_types,
=======
from app.domain.job_boards.common import (
    _classify_it_offer,
    _dedupe_jobs,
    _selector_attr,
    _selector_text,
    _selector_texts,
    fetch_page,
>>>>>>> chore/repo-cleanup:agents/app/domain/job_boards/glassdoor.py
    matches_posted_window,
    normalize_contract_type,
)


logger = logging.getLogger(__name__)

GLASSDOOR_PAGE_SIZE = 30
GLASSDOOR_BASE_URL = "https://www.glassdoor.com"
_LOCATION_LOOKUP_URL = (
    f"{GLASSDOOR_BASE_URL}/autocomplete/location?locationTypeFilters=CITY,STATE,COUNTRY&caller=jobs&term="
)

# Location name (lower-cased) -> (locT, locId), or None when Glassdoor doesn't know it.
# Without a location id Glassdoor ignores the place and searches the United States.
_LOCATION_CACHE: dict[str, Optional[tuple[str, int]]] = {}


def build_search_urls(
    keywords: Optional[str],
    location: Optional[str],
    limit: int,
    search_url: Optional[str] = None,
) -> list[str]:
    """Keyword-only search URLs; the location is added by fetch_listing_jobs once resolved."""
    if search_url:
        return [search_url]
    if not keywords:
        return []

    pages = max(1, math.ceil(limit / GLASSDOOR_PAGE_SIZE))
    base_params = {"sc.keyword": keywords}

    urls: list[str] = []
    for page_index in range(pages):
        params = dict(base_params)
        if page_index:
            params["p"] = page_index + 1
        urls.append(f"{GLASSDOOR_BASE_URL}/Job/jobs.htm?{urlencode(params, quote_via=quote_plus)}")
    return urls


def _parse_location_lookup(page) -> Optional[tuple[str, int]]:
    text = str(getattr(page, "html_content", "") or "")
    start, end = text.find("["), text.rfind("]")
    if start < 0 or end <= start:
        return None
    try:
        entries = json.loads(text[start:end + 1])
    except ValueError:
        return None
    for entry in entries:
        loc_type, loc_id = entry.get("locationType"), entry.get("locationId")
        if loc_type and isinstance(loc_id, int):
            return loc_type, loc_id
    return None


async def resolve_location(location: Optional[str]) -> Optional[tuple[str, int]]:
    """Glassdoor's (locT, locId) for a place name, cached. None if unknown or the lookup fails."""
    key = (location or "").strip().lower()
    if not key or key in {"remote", "worldwide", "anywhere"}:
        return None
    if key in _LOCATION_CACHE:
        return _LOCATION_CACHE[key]
    try:
        page = await fetch_page(_LOCATION_LOOKUP_URL + quote_plus(location.strip()))
        resolved = _parse_location_lookup(page) if getattr(page, "status", 200) < 400 else None
    except Exception as exc:
        logger.warning("Glassdoor location lookup failed for %r: %s", location, exc)
        return None  # not cached: a transient failure should be retried next search
    _LOCATION_CACHE[key] = resolved
    return resolved


def location_was_resolved(location: Optional[str]) -> bool:
    return _LOCATION_CACHE.get((location or "").strip().lower()) is not None


def _with_location(url: str, location: Optional[str], resolved: Optional[tuple[str, int]]) -> str:
    parsed = urlparse(url)
    params = dict(parse_qsl(parsed.query, keep_blank_values=True))
    if resolved:
        params["locT"], params["locId"] = resolved[0], str(resolved[1])
    elif location and location.strip().lower() not in params.get("sc.keyword", "").lower():
        # Best effort when Glassdoor doesn't know the place: put it in the keywords.
        params["sc.keyword"] = f"{params.get('sc.keyword', '')} {location.strip()}".strip()
    return urlunparse(parsed._replace(query=urlencode(params, quote_via=quote_plus)))


def _normalize_link(url: Optional[str]) -> Optional[str]:
    if not url:
        return None
    clean = url.strip()
    if clean.startswith("/"):
        return f"{GLASSDOOR_BASE_URL}{clean}"
    return clean


def _extract_job_id(url: Optional[str]) -> Optional[str]:
    if not url:
        return None
    match = re.search(r"[?&]jl=([0-9]+)", url)
    if match:
        return match.group(1)
    return None


def _merge_existing_query_params(search_url: str, page_number: int) -> str:
    parsed = urlparse(search_url)
    params = dict(parse_qsl(parsed.query, keep_blank_values=True))
    params["p"] = str(page_number)
    return urlunparse(parsed._replace(query=urlencode(params, quote_via=quote_plus)))


def _extract_listing_cards(page, search_url: str) -> list[dict]:
    jobs: list[dict] = []
    try:
        cards = page.css('li[data-test="jobListing"]')
    except Exception:
        cards = []

    for card in cards:
        title = _selector_text(card, [
            "a.JobCard_jobTitle__GLyJ1::text",
            'a[data-test="job-title"]::text',
            "a::text",
        ])
        url = _normalize_link(_selector_attr(card, [
            "a.JobCard_jobTitle__GLyJ1::attr(href)",
            'a[data-test="job-title"]::attr(href)',
            "a::attr(href)",
        ]))
        if not title or not url:
            continue

        company = _selector_text(card, [
            "span.EmployerProfile_compactEmployerName__9MGcV::text",
            'span[data-test="employer-name"]::text',
            '[data-test="employer-name"]::text',
        ])
        location = _selector_text(card, [
            "div.JobCard_location__Ds1fM::text",
            'div[data-test="emp-location"]::text',
            '[data-test="emp-location"]::text',
        ])
        snippets = _selector_texts(card, [
            "div.JobCard_jobDescriptionSnippet__l1tnl::text",
            "div.JobCard_salaryEstimate__QpbTW::text",
            "div::text",
        ])
        description = " ".join(snippets[:8]) if snippets else None

        jobs.append({
            "job_id": _extract_job_id(url),
            "title": title,
            "company": company,
            "location": location,
            "posted_at_text": None,
            "url": url,
            "description": description,
            "employment_type": None,
            "normalized_contract_type": None,
            "seniority_level": None,
            "job_function": None,
            "industries": [],
            "source": "glassdoor-search",
            "search_url": search_url,
        })
    return jobs


async def fetch_listing_jobs(search_urls: list[str], limit: int, location: Optional[str] = None) -> list[dict]:
    jobs: list[dict] = []

    async def _fetch_one(url: str) -> list[dict]:
        logger.info("Glassdoor jobs listing fetch: %s", url)
        page = await fetch_page(url)
        return _extract_listing_cards(page, url)

    expanded_urls: list[str]
    if len(search_urls) == 1 and "p=" not in search_urls[0] and limit > GLASSDOOR_PAGE_SIZE:
        pages = max(1, math.ceil(limit / GLASSDOOR_PAGE_SIZE))
        expanded_urls = [_merge_existing_query_params(search_urls[0], idx + 1) for idx in range(pages)]
    else:
        expanded_urls = search_urls

    if location and not any("locId=" in url for url in expanded_urls):
        resolved = await resolve_location(location)
        expanded_urls = [_with_location(url, location, resolved) for url in expanded_urls]

    results = await asyncio.gather(*[_fetch_one(url) for url in expanded_urls], return_exceptions=True)
    for result in results:
        if isinstance(result, Exception):
            raise result
        jobs.extend(result)

    return _dedupe_jobs(jobs, limit)


async def enrich_jobs_with_details(jobs: list[dict], enabled: bool = False) -> list[dict]:
    enriched_jobs: list[dict] = []
    for job in jobs:
        merged = dict(job)
        merged["normalized_contract_type"] = normalize_contract_type(merged)
        is_it_offer, matched_terms = _classify_it_offer(merged)
        merged["is_it_offer"] = is_it_offer
        merged["matched_it_terms"] = matched_terms
        enriched_jobs.append(merged)
    return enriched_jobs


def filter_jobs(
    jobs: list[dict],
    it_only: bool,
    limit: int,
    location: Optional[str] = None,
    posted_window: Optional[str] = None,
    contract_types: Optional[list[str]] = None,
) -> list[dict]:
    if it_only:
        jobs = [job for job in jobs if job.get("is_it_offer")]
    if location:
        loc = location.strip().lower()
        localized = [job for job in jobs if loc in (job.get("location") or "").lower() or loc in (job.get("description") or "").lower()]
        if localized:
            jobs = localized
    if posted_window and posted_window != "any":
        jobs = [job for job in jobs if matches_posted_window(job, posted_window)]
    normalized_contract_types = {value.strip().lower() for value in (contract_types or []) if value}
    if normalized_contract_types:
        jobs = [job for job in jobs if matches_contract_types(job, contract_types)]
    return jobs[:limit]
