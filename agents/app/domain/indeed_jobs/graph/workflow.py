from langgraph.graph import END, StateGraph

from app.domain.indeed_jobs.agents.agent import (
    build_search_urls_node,
    enrich_jobs_node,
    finalize_jobs_node,
    scrape_listing_jobs_node,
)
from app.domain.indeed_jobs.schemas.state import IndeedJobsState


def build_indeed_jobs_workflow():
    graph = StateGraph(IndeedJobsState)
    graph.add_node("build_search_urls", build_search_urls_node)
    graph.add_node("scrape_listing_jobs", scrape_listing_jobs_node)
    graph.add_node("enrich_jobs", enrich_jobs_node)
    graph.add_node("finalize_jobs", finalize_jobs_node)

    graph.set_entry_point("build_search_urls")
    graph.add_edge("build_search_urls", "scrape_listing_jobs")
    graph.add_edge("scrape_listing_jobs", "enrich_jobs")
    graph.add_edge("enrich_jobs", "finalize_jobs")
    graph.add_edge("finalize_jobs", END)

    return graph.compile()
