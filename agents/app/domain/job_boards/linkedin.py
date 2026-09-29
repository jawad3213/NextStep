# Board-specific scraping for Linkedin (URLs, selectors, pagination).
import asyncio
import logging
import math
import re
from typing import Optional
from urllib.parse import parse_qsl, quote_plus, urlencode, urlparse, urlunparse

from app.domain.job_boards.common import (
    _classify_it_offer,
    _dedupe_jobs,
    _selector_attr,
    _selector_block_text,
    _selector_text,
    fetch_page,
    matches_posted_window,
    normalize_contract_type,
    normalize_posted_window_to_seconds,
)

logger = logging.getLogger(__name__)

LINKEDIN_SEARCH_ENDPOINT = "https://www.linkedin.com/jobs-guest/jobs/api/seeMoreJobPostings/search"
LINKEDIN_JOB_ENDPOINT = "https://www.linkedin.com/jobs-guest/jobs/api/jobPosting/{job_id}"
SEARCH_PAGE_SIZE = 25
def build_search_urls(
    keywords: Optional[str],
    location: Optional[str],
    limit: int,
    posted_since_seconds: Optional[int] = None,
    posted_window: Optional[str] = None,
    search_url: Optional[str] = None,
) -> list[str]:
    if search_url:
        return [search_url]

    if not keywords:
        return []

    pages = max(1, math.ceil(limit / SEARCH_PAGE_SIZE))
    base_params = {"keywords": keywords}
    if location:
        base_params["location"] = location
    effective_posted_since_seconds = posted_since_seconds
    if effective_posted_since_seconds is None and posted_window:
        effective_posted_since_seconds = normalize_posted_window_to_seconds(posted_window)
    if effective_posted_since_seconds:
        base_params["f_TPR"] = f"r{effective_posted_since_seconds}"

    urls: list[str] = []
    for page_index in range(pages):
        params = dict(base_params)
        params["start"] = page_index * SEARCH_PAGE_SIZE
        urls.append(f"{LINKEDIN_SEARCH_ENDPOINT}?{urlencode(params, quote_via=quote_plus)}")
    return urls


def _normalize_link(url: Optional[str]) -> Optional[str]:
    if not url:
        return None
    clean = url.split("?")[0].strip()
    if clean.startswith("/"):
        clean = f"https://www.linkedin.com{clean}"
    return clean


def _extract_job_id(url: Optional[str]) -> Optional[str]:
    if not url:
        return None
    match = re.search(r"/view/([0-9]+)", url)
    if match:
        return match.group(1)
    match = re.search(r"-([0-9]{8,})/?$", url)
    if match:
        return match.group(1)
    match = re.search(r"currentJobId=([0-9]+)", url)
    if match:
        return match.group(1)
    return None


def _extract_listing_cards(page, search_url: str) -> list[dict]:
    jobs: list[dict] = []
    try:
        cards = page.css("li")
    except Exception:
        cards = []

    for card in cards:
        title = _selector_text(
            card,
            [
                "h3.base-search-card__title::text",
                "h3.base-search-card__title span::text",
                "h3::text",
            ],
        )
        url = _normalize_link(
            _selector_attr(
                card,
                [
                    "a.base-card__full-link::attr(href)",
                    "a.hidden-nested-link::attr(href)",
                    "a::attr(href)",
                ],
            )
        )
        if not title or not url:
            continue

        jobs.append(
            {
                "job_id": _extract_job_id(url),
                "title": title,
                "company": _selector_text(
                    card,
                    [
                        "h4.base-search-card__subtitle::text",
                        "h4.base-search-card__subtitle a::text",
                        "a.hidden-nested-link::text",
                    ],
                ),
                "location": _selector_text(
                    card,
                    [
                        "span.job-search-card__location::text",
                        "span.base-search-card__metadata::text",
                    ],
                ),
                "posted_at_text": _selector_text(
                    card,
                    [
                        "time.job-search-card__listdate--new::text",
                        "time.job-search-card__listdate::text",
                        "time::text",
                    ],
                ),
                "url": url,
                "search_url": search_url,
                "source": "linkedin-guest-api",
            }
        )
    return jobs


def _merge_existing_query_params(search_url: str, start: int) -> str:
    parsed = urlparse(search_url)
    params = dict(parse_qsl(parsed.query, keep_blank_values=True))
    params["start"] = str(start)
    return urlunparse(parsed._replace(query=urlencode(params, quote_via=quote_plus)))


async def fetch_listing_jobs(
    search_urls: list[str],
    limit: int,
) -> list[dict]:
    jobs: list[dict] = []

    async def _fetch_one(url: str) -> list[dict]:
        logger.info("LinkedIn jobs listing fetch: %s", url)
        page = await fetch_page(url)
        return _extract_listing_cards(page, url)

    expanded_urls: list[str] = []
    if len(search_urls) == 1 and "start=" not in search_urls[0] and limit > SEARCH_PAGE_SIZE:
        pages = max(1, math.ceil(limit / SEARCH_PAGE_SIZE))
        expanded_urls = [_merge_existing_query_params(search_urls[0], idx * SEARCH_PAGE_SIZE) for idx in range(pages)]
    else:
        expanded_urls = search_urls

    results = await asyncio.gather(*[_fetch_one(url) for url in expanded_urls], return_exceptions=True)
    for result in results:
        if isinstance(result, Exception):
            raise result
        jobs.extend(result)

    return _dedupe_jobs(jobs, limit)


async def enrich_jobs_with_details(jobs: list[dict], enabled: bool = True) -> list[dict]:
    if not enabled:
        enriched = []
        for job in jobs:
            is_it_offer, matched_terms = _classify_it_offer(job)
            enriched.append(
                {
                    **job,
                    "normalized_contract_type": normalize_contract_type(job),
                    "is_it_offer": is_it_offer,
                    "matched_it_terms": matched_terms,
                }
            )
        return enriched

    async def _enrich(job: dict) -> dict:
        job_id = job.get("job_id")
        if not job_id:
            is_it_offer, matched_terms = _classify_it_offer(job)
            return {
                **job,
                "normalized_contract_type": normalize_contract_type(job),
                "is_it_offer": is_it_offer,
                "matched_it_terms": matched_terms,
            }

        url = LINKEDIN_JOB_ENDPOINT.format(job_id=job_id)
        logger.info("LinkedIn job detail fetch: %s", url)
        page = await fetch_page(url)

        criteria_map: dict[str, str] = {}
        try:
            criteria_items = page.css("li.description__job-criteria-item")
        except Exception:
            criteria_items = []

        for item in criteria_items:
            label = _selector_text(item, ["h3::text", "span.description__job-criteria-subheader::text"]) or ""
            value = _selector_text(item, ["span.description__job-criteria-text::text", "span::text"]) or ""
            if label and value:
                criteria_map[label.lower()] = value

        merged = {
            **job,
            "description": _selector_block_text(
                page,
                [
                    "div.show-more-less-html__markup",
                    "div.description__text",
                    "section.show-more-less-html",
                ],
            ),
            "employment_type": criteria_map.get("employment type"),
            "seniority_level": criteria_map.get("seniority level"),
            "job_function": criteria_map.get("job function"),
            "industries": [criteria_map["industries"]] if criteria_map.get("industries") else [],
        }
        merged["normalized_contract_type"] = normalize_contract_type(merged)
        is_it_offer, matched_terms = _classify_it_offer(merged)
        merged["is_it_offer"] = is_it_offer
        merged["matched_it_terms"] = matched_terms
        return merged

    results = await asyncio.gather(*[_enrich(job) for job in jobs], return_exceptions=True)
    enriched_jobs: list[dict] = []
    for index, result in enumerate(results):
        if isinstance(result, Exception):
            logger.warning("LinkedIn job detail fetch failed for %s: %s", jobs[index].get("url"), result)
            fallback = dict(jobs[index])
            fallback["normalized_contract_type"] = normalize_contract_type(fallback)
            is_it_offer, matched_terms = _classify_it_offer(fallback)
            fallback["is_it_offer"] = is_it_offer
            fallback["matched_it_terms"] = matched_terms
            enriched_jobs.append(fallback)
        else:
            enriched_jobs.append(result)
    return enriched_jobs


def filter_jobs(
    jobs: list[dict],
    it_only: bool,
    limit: int,
    posted_window: Optional[str] = None,
    contract_types: Optional[list[str]] = None,
) -> list[dict]:
    if it_only:
        jobs = [job for job in jobs if job.get("is_it_offer")]
    if posted_window and posted_window != "any":
        jobs = [job for job in jobs if matches_posted_window(job, posted_window)]
    normalized_contract_types = {value.strip().lower() for value in (contract_types or []) if value}
    if normalized_contract_types:
        jobs = [
            job
            for job in jobs
            if (job.get("normalized_contract_type") or normalize_contract_type(job) or "").lower() in normalized_contract_types
        ]
    return jobs[:limit]
