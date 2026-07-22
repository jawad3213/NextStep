import asyncio
import logging
import math
import re
from typing import Optional
from urllib.parse import parse_qsl, quote_plus, urlencode, urlparse, urlunparse

from app.domain.linkedin_jobs.tools.scrapling_tool import (
    IT_TERMS,
    matches_contract_types,
    matches_posted_window,
    normalize_contract_type,
)

logger = logging.getLogger(__name__)

GLASSDOOR_PAGE_SIZE = 30
GLASSDOOR_BASE_URL = "https://www.glassdoor.com"


def _get_fetcher():
    try:
        from scrapling.fetchers import Fetcher
    except ImportError as exc:
        raise RuntimeError(
            "Scrapling fetchers are unavailable. Install the agents dependencies so "
            "`scrapling[fetchers]==0.4.8` is present before calling this agent."
        ) from exc
    return Fetcher


def build_search_urls(
    keywords: Optional[str],
    location: Optional[str],
    limit: int,
    search_url: Optional[str] = None,
) -> list[str]:
    if search_url:
        return [search_url]
    if not keywords:
        return []

    pages = max(1, math.ceil(limit / GLASSDOOR_PAGE_SIZE))
    effective_keywords = keywords if not location else f"{keywords} {location}"
    base_params = {"sc.keyword": effective_keywords}

    urls: list[str] = []
    for page_index in range(pages):
        params = dict(base_params)
        if page_index:
            params["p"] = page_index + 1
        urls.append(f"{GLASSDOOR_BASE_URL}/Job/jobs.htm?{urlencode(params, quote_via=quote_plus)}")
    return urls


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


def _classify_it_offer(job: dict) -> tuple[bool, list[str]]:
    corpus = " ".join(
        [
            job.get("title") or "",
            job.get("description") or "",
            job.get("job_function") or "",
            job.get("employment_type") or "",
        ]
    ).lower()
    matched = sorted(term for term in IT_TERMS if term in corpus)
    return bool(matched), matched


def _dedupe_jobs(jobs: list[dict], limit: int) -> list[dict]:
    seen: set[str] = set()
    deduped: list[dict] = []
    for job in jobs:
        key = job.get("job_id") or job.get("url") or f"{job.get('title')}::{job.get('company')}"
        if not key or key in seen:
            continue
        seen.add(key)
        deduped.append(job)
        if len(deduped) >= limit:
            break
    return deduped


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


async def fetch_listing_jobs(search_urls: list[str], limit: int) -> list[dict]:
    Fetcher = _get_fetcher()
    jobs: list[dict] = []

    async def _fetch_one(url: str) -> list[dict]:
        logger.info("Glassdoor jobs listing fetch: %s", url)
        page = await asyncio.to_thread(Fetcher.get, url)
        return _extract_listing_cards(page, url)

    expanded_urls: list[str]
    if len(search_urls) == 1 and "p=" not in search_urls[0] and limit > GLASSDOOR_PAGE_SIZE:
        pages = max(1, math.ceil(limit / GLASSDOOR_PAGE_SIZE))
        expanded_urls = [_merge_existing_query_params(search_urls[0], idx + 1) for idx in range(pages)]
    else:
        expanded_urls = search_urls

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
