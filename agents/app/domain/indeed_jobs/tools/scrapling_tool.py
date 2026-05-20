import asyncio
import logging
import math
import re
from typing import Optional
from urllib.parse import parse_qsl, quote_plus, urlencode, urlparse, urlunparse

from app.domain.linkedin_jobs.tools.scrapling_tool import IT_TERMS

logger = logging.getLogger(__name__)

INDEED_PAGE_SIZE = 10


def _get_fetcher():
    try:
        from scrapling.fetchers import Fetcher
    except ImportError as exc:
        raise RuntimeError(
            "Scrapling fetchers are unavailable. Install the agents dependencies so "
            "`scrapling[fetchers]==0.4.8` is present before calling this agent."
        ) from exc
    return Fetcher


def _normalize_country_code(country_code: Optional[str]) -> str:
    code = (country_code or "ma").strip().lower()
    return code or "ma"


def _indeed_base_url(country_code: Optional[str]) -> str:
    code = _normalize_country_code(country_code)
    subdomain = "www" if code in {"www", "com"} else code
    return f"https://{subdomain}.indeed.com"


def build_search_urls(
    keywords: Optional[str],
    location: Optional[str],
    limit: int,
    search_url: Optional[str] = None,
    country_code: Optional[str] = "ma",
) -> list[str]:
    if search_url:
        return [search_url]
    if not keywords:
        return []

    pages = max(1, math.ceil(limit / INDEED_PAGE_SIZE))
    base_url = _indeed_base_url(country_code)
    base_params = {"q": keywords}
    if location:
        base_params["l"] = location

    urls: list[str] = []
    for page_index in range(pages):
        params = dict(base_params)
        if page_index:
            params["start"] = page_index * INDEED_PAGE_SIZE
        urls.append(f"{base_url}/jobs?{urlencode(params, quote_via=quote_plus)}")
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


def _normalize_link(base_url: str, url: Optional[str]) -> Optional[str]:
    if not url:
        return None
    clean = url.strip()
    if clean.startswith("/"):
        return f"{base_url}{clean}"
    return clean


def _extract_job_id(url: Optional[str]) -> Optional[str]:
    if not url:
        return None
    match = re.search(r"[?&]jk=([a-zA-Z0-9]+)", url)
    if match:
        return match.group(1)
    match = re.search(r"viewjob\?jk=([a-zA-Z0-9]+)", url)
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


def _merge_existing_query_params(search_url: str, start: int) -> str:
    parsed = urlparse(search_url)
    params = dict(parse_qsl(parsed.query, keep_blank_values=True))
    params["start"] = str(start)
    return urlunparse(parsed._replace(query=urlencode(params, quote_via=quote_plus)))


def _extract_listing_cards(page, search_url: str, base_url: str) -> list[dict]:
    jobs: list[dict] = []
    try:
        cards = page.css('div[data-testid="slider_item"]')
    except Exception:
        cards = []

    for card in cards:
        title = _selector_text(card, ["h2.jobTitle span::text", "a.jcs-JobTitle span::text"])
        url = _normalize_link(
            base_url,
            _selector_attr(card, ["a.jcs-JobTitle::attr(href)", "h2.jobTitle a::attr(href)"]),
        )
        if not title or not url:
            continue

        metadata_bits = _selector_texts(card, [
            "div.jobMetaDataGroup li span::text",
            "div.jobMetaDataGroup li::text",
        ])

        jobs.append(
            {
                "job_id": _extract_job_id(url),
                "title": title,
                "company": _selector_text(card, ['[data-testid="company-name"]::text']),
                "location": _selector_text(card, ['[data-testid="text-location"]::text']),
                "posted_at_text": _selector_text(card, ["span.date::text", '[data-testid="myJobsStateDate"]::text']),
                "url": url,
                "search_url": search_url,
                "source": "indeed-search",
                "industries": [],
                "job_function": None,
                "employment_type": None,
                "seniority_level": None,
                "metadata_bits": metadata_bits,
            }
        )
    return jobs


async def fetch_listing_jobs(search_urls: list[str], limit: int, country_code: Optional[str] = "ma") -> list[dict]:
    Fetcher = _get_fetcher()
    base_url = _indeed_base_url(country_code)
    jobs: list[dict] = []

    async def _fetch_one(url: str) -> list[dict]:
        logger.info("Indeed jobs listing fetch: %s", url)
        page = await asyncio.to_thread(Fetcher.get, url)
        return _extract_listing_cards(page, url, base_url)

    expanded_urls: list[str]
    if len(search_urls) == 1 and "start=" not in search_urls[0] and limit > INDEED_PAGE_SIZE:
        pages = max(1, math.ceil(limit / INDEED_PAGE_SIZE))
        expanded_urls = [_merge_existing_query_params(search_urls[0], idx * INDEED_PAGE_SIZE) for idx in range(pages)]
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
            merged = dict(job)
            merged.pop("metadata_bits", None)
            merged["is_it_offer"] = is_it_offer
            merged["matched_it_terms"] = matched_terms
            enriched.append(merged)
        return enriched

    Fetcher = _get_fetcher()

    async def _enrich(job: dict) -> dict:
        url = job.get("url")
        if not url:
            is_it_offer, matched_terms = _classify_it_offer(job)
            merged = dict(job)
            merged.pop("metadata_bits", None)
            merged["is_it_offer"] = is_it_offer
            merged["matched_it_terms"] = matched_terms
            return merged

        logger.info("Indeed job detail fetch: %s", url)
        page = await asyncio.to_thread(Fetcher.get, url)
        metadata_bits = list(job.get("metadata_bits") or [])

        merged = {
            **job,
            "title": _selector_text(page, [".jobsearch-JobInfoHeader-title span::text"]) or job.get("title"),
            "location": _selector_text(page, ['[data-testid="inlineHeader-companyLocation"] div::text']) or job.get("location"),
            "description": _selector_block_text(page, ["#jobDescriptionText"]),
            "employment_type": next((bit for bit in metadata_bits if bit and any(token in bit.lower() for token in ["full-time", "part-time", "contract", "internship", "temporary"])), None),
            "seniority_level": next((bit for bit in metadata_bits if bit and any(token in bit.lower() for token in ["junior", "senior", "lead", "principal", "mid"])), None),
            "industries": [],
            "job_function": "Information Technology" if metadata_bits else None,
        }
        merged.pop("metadata_bits", None)
        is_it_offer, matched_terms = _classify_it_offer(merged)
        merged["is_it_offer"] = is_it_offer
        merged["matched_it_terms"] = matched_terms
        return merged

    results = await asyncio.gather(*[_enrich(job) for job in jobs], return_exceptions=True)
    enriched_jobs: list[dict] = []
    for index, result in enumerate(results):
        if isinstance(result, Exception):
            logger.warning("Indeed job detail fetch failed for %s: %s", jobs[index].get("url"), result)
            fallback = dict(jobs[index])
            fallback.pop("metadata_bits", None)
            is_it_offer, matched_terms = _classify_it_offer(fallback)
            fallback["is_it_offer"] = is_it_offer
            fallback["matched_it_terms"] = matched_terms
            enriched_jobs.append(fallback)
        else:
            enriched_jobs.append(result)
    return enriched_jobs


def filter_jobs(jobs: list[dict], it_only: bool, limit: int) -> list[dict]:
    if it_only:
        jobs = [job for job in jobs if job.get("is_it_offer")]
    return jobs[:limit]
