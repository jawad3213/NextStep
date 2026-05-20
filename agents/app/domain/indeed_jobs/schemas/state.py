import operator
from typing import Annotated, Optional, TypedDict

from langchain_core.messages import BaseMessage
from langgraph.graph.message import add_messages


class IndeedJobsState(TypedDict, total=False):
    keywords: str
    location: Optional[str]
    limit: int
    search_url: Optional[str]
    fetch_details: bool
    it_only: bool
    country_code: Optional[str]

    search_urls: list[str]
    raw_jobs: Annotated[list[dict], operator.add]
    enriched_jobs: Annotated[list[dict], operator.add]
    filtered_jobs: Annotated[list[dict], operator.add]
    result: Optional[dict]

    messages: Annotated[list[BaseMessage], add_messages]
    errors: Annotated[list[str], operator.add]
