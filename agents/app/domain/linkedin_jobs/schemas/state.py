import operator
from typing import Annotated, Optional, TypedDict

from langchain_core.messages import BaseMessage
from langgraph.graph.message import add_messages


class LinkedInJobsState(TypedDict, total=False):
    keywords: str
    location: Optional[str]
    limit: int
    posted_since_seconds: Optional[int]
    posted_window: Optional[str]
    search_url: Optional[str]
    fetch_details: bool
    it_only: bool
    contract_types: list[str]
    fetch_limit: int

    search_urls: list[str]
    raw_jobs: Annotated[list[dict], operator.add]
    enriched_jobs: Annotated[list[dict], operator.add]
    filtered_jobs: Annotated[list[dict], operator.add]
    result: Optional[dict]

    messages: Annotated[list[BaseMessage], add_messages]
    errors: Annotated[list[str], operator.add]
