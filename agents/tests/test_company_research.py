"""Company research: web search fallback (Tavily -> Bing -> DuckDuckGo) and the 7-day cache."""
import base64

import pytest

from app.domain.company import service as company_service_module
from app.domain.company.service import has_usable_data
from app.domain.company.tools import web_tool


def _bing_click_url(target: str) -> str:
    encoded = base64.urlsafe_b64encode(target.encode()).decode().rstrip("=")
    return f"https://www.bing.com/ck/a?!&&p=abc&ptn=3&ver=2&u=a1{encoded}&ntb=1"


class TestBingLinks:
    def test_click_tracker_is_decoded_to_the_real_url(self):
        assert web_tool._bing_target(_bing_click_url("https://www.capgemini.com/fr-fr/")) == "https://www.capgemini.com/fr-fr/"

    def test_direct_links_are_kept(self):
        assert web_tool._bing_target("https://fr.wikipedia.org/wiki/Capgemini") == "https://fr.wikipedia.org/wiki/Capgemini"

    @pytest.mark.parametrize("href", [None, "", "/search?q=x", "https://www.bing.com/ck/a?u=zz", "https://www.bing.com/ck/a?u=a1%%%"])
    def test_unusable_links_are_dropped(self, href):
        assert web_tool._bing_target(href) is None


class TestSearchFallback:
    @pytest.fixture
    def calls(self, monkeypatch):
        calls: list[str] = []

        def provider(name, results):
            async def search(query, max_results=5):
                calls.append(name)
                return results
            return search

        monkeypatch.setattr(web_tool, "tavily_search", provider("tavily", []))
        monkeypatch.setattr(web_tool, "yahoo_search", provider("yahoo", []))
        monkeypatch.setattr(web_tool, "bing_search", provider("bing", [{"title": "t", "url": "u", "snippet": ""}]))
        monkeypatch.setattr(web_tool, "duckduckgo_search", provider("ddg", []))
        return calls

    @pytest.mark.asyncio
    async def test_yahoo_then_bing_before_the_blocked_duckduckgo(self, calls, monkeypatch):
        from app.core.config import settings
        monkeypatch.setattr(settings, "TAVILY_API_KEY", None)
        assert await web_tool.smart_search("Capgemini") == [{"title": "t", "url": "u", "snippet": ""}]
        assert calls == ["yahoo", "bing"]

    @pytest.mark.asyncio
    async def test_tavily_first_when_configured(self, calls, monkeypatch):
        from app.core.config import settings
        monkeypatch.setattr(settings, "TAVILY_API_KEY", "configured")
        await web_tool.smart_search("Capgemini")
        assert calls == ["tavily", "yahoo", "bing"]

    @pytest.mark.asyncio
    async def test_duckduckgo_is_the_last_resort(self, calls, monkeypatch):
        from app.core.config import settings
        monkeypatch.setattr(settings, "TAVILY_API_KEY", None)

        async def empty(query, max_results=5):
            calls.append("bing")
            return []

        monkeypatch.setattr(web_tool, "bing_search", empty)
        assert await web_tool.smart_search("Capgemini") == []
        assert calls == ["yahoo", "bing", "ddg"]


class TestCache:
    @pytest.mark.parametrize("report, usable", [
        ({"intelligence": {"nom": "Acme", "sector": "IT services", "data_available": True}}, True),
        ({"intelligence": {"nom": "Acme", "sector": "IT services"}}, True),
        ({"intelligence": {"nom": "Acme", "data_available": True}}, False),
        ({"intelligence": {"nom": "Acme", "data_available": False}}, False),
        ({"intelligence": None}, False),
        (None, False),
    ])
    def test_empty_reports_are_not_usable(self, report, usable):
        assert has_usable_data(report) is usable

    @pytest.mark.asyncio
    async def test_cached_empty_report_is_treated_as_a_miss(self):
        from datetime import datetime

        class Result:
            def __init__(self, row):
                self._row = row

            def first(self):
                return self._row

        class Db:
            def __init__(self, row):
                self.row = row

            async def execute(self, stmt):
                return Result(self.row)

        empty = {"intelligence": {"nom": "Acme", "data_available": False}}
        full = {"intelligence": {"nom": "Acme", "sector": "IT services"}}
        now = datetime(2026, 9, 29)
        service = company_service_module.company_service

        assert await service.find_cached_intelligence(Db((empty, now)), "Acme") is None
        assert await service.find_cached_intelligence(Db((full, now)), "Acme") == (full, now)


class TestYahoo:
    def test_redirect_is_decoded_to_the_real_url(self):
        href = "https://r.search.yahoo.com/_ylt=A2R;_ylu=Y2/RV=2/RE=179/RO=10/RU=https%3a%2f%2fwww.zenika.com%2f/RK=2/RS=abc-"
        assert web_tool._yahoo_target(href) == "https://www.zenika.com/"

    @pytest.mark.parametrize("href", [None, "", "https://search.yahoo.com/search?p=x", "/relative"])
    def test_unusable_links_are_dropped(self, href):
        assert web_tool._yahoo_target(href) is None


class TestRelevance:
    @pytest.mark.parametrize("result, subject, expected", [
        ({"title": "Zenika", "url": "https://www.zenika.com/", "snippet": ""}, "zenika", True),
        ({"title": "Blog", "url": "https://blog.zenika.com/", "snippet": ""}, "Zenika", True),
        ({"title": "Société Générale", "url": "https://x.com", "snippet": ""}, "societe generale", True),
        ({"title": "Zhihu", "url": "https://www.zhihu.com/", "snippet": "问答社区"}, "zenika", False),
        ({"title": "Gmail Help", "url": "https://support.google.com/mail", "snippet": "entreprise"}, "Zenika", False),
    ])
    def test_result_must_mention_the_company(self, result, subject, expected):
        assert web_tool._mentions(result, subject) is expected

    @pytest.mark.asyncio
    async def test_decoy_results_fall_through_to_the_next_engine(self, monkeypatch):
        from app.core.config import settings
        monkeypatch.setattr(settings, "TAVILY_API_KEY", None)

        async def yahoo(query, max_results=5):
            return [{"title": "Zhihu", "url": "https://www.zhihu.com/", "snippet": ""}]

        async def bing(query, max_results=5):
            return [{"title": "Zenika", "url": "https://www.zenika.com/", "snippet": ""}]

        async def ddg(query, max_results=5):
            raise AssertionError("not reached")

        monkeypatch.setattr(web_tool, "yahoo_search", yahoo)
        monkeypatch.setattr(web_tool, "bing_search", bing)
        monkeypatch.setattr(web_tool, "duckduckgo_search", ddg)
        results = await web_tool.smart_search("zenika présentation", must_mention="zenika")
        assert [r["url"] for r in results] == ["https://www.zenika.com/"]


class TestHollowReports:
    def test_linkedin_link_and_generic_questions_are_not_a_report(self):
        hollow = {"intelligence": {"nom": "zenika", "summary": "", "sector": "", "linkedin_url": "https://linkedin.com/company/zenika",
                                   "interview_questions": ["Why us?"], "culture": {"key_values": []}, "data_available": True}}
        assert has_usable_data(hollow) is False

    def test_a_real_summary_is_a_report(self):
        report = {"intelligence": {"nom": "zenika", "summary": "Zenika est un cabinet de conseil IT fondé en 2006 qui accompagne les entreprises dans leur transformation."}}
        assert has_usable_data(report) is True
