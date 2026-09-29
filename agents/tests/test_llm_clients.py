"""Chat models are built once per settings and reused (no new HTTP client per call)."""
import asyncio
import time

import pytest
from pydantic import BaseModel

from app.core import config


class Answer(BaseModel):
    text: str


@pytest.fixture(autouse=True)
def groq_only(monkeypatch):
    monkeypatch.setattr(config.settings, "GROQ_API_KEY", "test-key")
    config._LLM_CLIENTS.clear()
    config._PROVIDER_COOLDOWNS.clear()
    yield
    config._LLM_CLIENTS.clear()
    config._PROVIDER_COOLDOWNS.clear()


def _get(**overrides):
    args = {"provider": "groq", "agent_name": "offer_analyzer", "temperature": 0.0,
            "bound_kwargs": None, "structured_output": None, "structured_method": None, "loop": None}
    args.update(overrides)
    return config._get_provider_llm(**args)


def test_same_settings_reuse_the_same_client():
    assert _get() is _get()
    assert _get(structured_output=Answer) is _get(structured_output=Answer)
    assert _get(bound_kwargs={"response_format": {"type": "json_object"}}) is _get(bound_kwargs={"response_format": {"type": "json_object"}})


def test_different_settings_get_their_own_client():
    base = _get()
    assert _get(temperature=0.7) is not base
    assert _get(agent_name="default") is not base  # other model
    assert _get(structured_output=Answer) is not base
    assert _get(structured_output=Answer, structured_method="json_mode") is not _get(structured_output=Answer)


def test_each_event_loop_gets_its_own_client():
    first = asyncio.new_event_loop()
    second = asyncio.new_event_loop()
    try:
        assert _get(loop=first) is _get(loop=first)
        assert _get(loop=first) is not _get(loop=second)
    finally:
        first.close()
        second.close()


def test_unconfigured_provider_is_not_cached(monkeypatch):
    monkeypatch.setattr(config.settings, "OPENAI_API_KEY", "")
    assert _get(provider="openai") is None
    assert not any(key[0] == "openai" for key in config._LLM_CLIENTS)


@pytest.mark.asyncio
async def test_wrapper_reuses_the_client_between_calls(monkeypatch):
    calls = []
    original = config._create_provider_llm

    def counting(*args, **kwargs):
        calls.append(args[0])
        return original(*args, **kwargs)

    class FakeAnswer:
        content = "ok"

    async def fake_ainvoke(self, *args, **kwargs):
        return FakeAnswer()

    monkeypatch.setattr(config, "_create_provider_llm", counting)
    monkeypatch.setattr(config.settings, "LLM_PROVIDER_PRIORITY", "groq")
    monkeypatch.setattr("langchain_groq.ChatGroq.ainvoke", fake_ainvoke)

    llm = config.get_llm(agent_name="offer_analyzer", temperature=0.0)
    await llm.ainvoke("a")
    await llm.ainvoke("b")

    assert calls == ["groq"]


class _FakeAnswer:
    def __init__(self, text: str):
        self.content = text


@pytest.mark.asyncio
async def test_a_provider_stuck_past_the_attempt_timeout_is_abandoned_for_the_next_one(monkeypatch):
    """The scenario that caused a 64s CV rewrite: a provider's SDK keeps retrying a
    rate-limit error internally, far longer than any timeout passed to its constructor.
    LLM_PROVIDER_ATTEMPT_TIMEOUT must cut that attempt off and move to the next provider."""
    monkeypatch.setattr(config.settings, "OPENAI_API_KEY", "test-key")
    monkeypatch.setattr(config.settings, "LLM_PROVIDER_PRIORITY", "groq,openai")
    monkeypatch.setattr(config.settings, "LLM_PROVIDER_ATTEMPT_TIMEOUT", 0.05)

    async def stuck_ainvoke(self, *args, **kwargs):
        await asyncio.sleep(2)
        return _FakeAnswer("groq-too-late")

    async def fast_ainvoke(self, *args, **kwargs):
        return _FakeAnswer("openai-ok")

    monkeypatch.setattr("langchain_groq.ChatGroq.ainvoke", stuck_ainvoke)
    monkeypatch.setattr("langchain_openai.ChatOpenAI.ainvoke", fast_ainvoke)

    llm = config.get_llm(agent_name="offer_analyzer", temperature=0.0)
    # Warm up both clients first: constructing a provider's SDK client for the first time has
    # its own one-off cost, which isn't what this test measures (in the real fallback loop, a
    # provider used earlier in the same request is already cached — see _LLM_CLIENTS).
    config._get_provider_llm("openai", "offer_analyzer", 0.0, None, None, None)

    started = time.perf_counter()
    result = await llm.ainvoke("hi")
    elapsed = time.perf_counter() - started

    assert result.content == "openai-ok"
    assert elapsed < 1.0, f"took {elapsed}s: the stuck provider's 2s sleep was not abandoned"
    assert config._provider_on_cooldown("groq", "offer_analyzer") is True


@pytest.mark.asyncio
async def test_every_provider_stuck_fails_fast_instead_of_hanging(monkeypatch):
    monkeypatch.setattr(config.settings, "LLM_PROVIDER_PRIORITY", "groq")
    monkeypatch.setattr(config.settings, "LLM_PROVIDER_ATTEMPT_TIMEOUT", 0.05)

    async def stuck_ainvoke(self, *args, **kwargs):
        await asyncio.sleep(2)

    monkeypatch.setattr("langchain_groq.ChatGroq.ainvoke", stuck_ainvoke)

    llm = config.get_llm(agent_name="offer_analyzer", temperature=0.0)
    started = time.perf_counter()
    with pytest.raises(asyncio.TimeoutError):
        await llm.ainvoke("hi")
    elapsed = time.perf_counter() - started

    assert elapsed < 1.0, f"took {elapsed}s: should fail fast, not wait out the 2s sleep"


def test_sync_invoke_also_abandons_a_stuck_provider(monkeypatch):
    monkeypatch.setattr(config.settings, "OPENAI_API_KEY", "test-key")
    monkeypatch.setattr(config.settings, "LLM_PROVIDER_PRIORITY", "groq,openai")
    monkeypatch.setattr(config.settings, "LLM_PROVIDER_ATTEMPT_TIMEOUT", 0.1)

    def stuck_invoke(self, *args, **kwargs):
        time.sleep(0.5)
        return _FakeAnswer("groq-too-late")

    def fast_invoke(self, *args, **kwargs):
        return _FakeAnswer("openai-ok")

    monkeypatch.setattr("langchain_groq.ChatGroq.invoke", stuck_invoke)
    monkeypatch.setattr("langchain_openai.ChatOpenAI.invoke", fast_invoke)

    llm = config.get_llm(agent_name="offer_analyzer", temperature=0.0)
    started = time.perf_counter()
    result = llm.invoke("hi")
    elapsed = time.perf_counter() - started

    assert result.content == "openai-ok"
    assert elapsed < 0.4, f"took {elapsed}s: the stuck provider's 0.5s call was not abandoned"


def test_max_retries_is_disabled_on_every_provider_client(monkeypatch):
    """Belt and suspenders: each SDK's own retry loop is also turned off, though
    LLM_PROVIDER_ATTEMPT_TIMEOUT is what actually bounds a stuck/misbehaving client."""
    monkeypatch.setattr(config.settings, "OPENAI_API_KEY", "test-key")
    monkeypatch.setattr(config.settings, "GEMINI_API_KEY", "test-key")

    assert config._get_provider_llm("groq", None, None, None, None, None).max_retries == 0
    assert config._get_provider_llm("openai", None, None, None, None, None).max_retries == 0
    assert config._get_provider_llm("gemini", None, None, None, None, None).max_retries == 0
