"""Offer analyzer: the validator cleans the LLM's lists without reordering them."""
from app.domain.offer_analyzer.agents.agent import offer_validator_node


def test_lists_keep_the_offer_order_without_duplicates():
    offer = {
        "titre": "Développeuse Full Stack",
        "competences_requises": ["Angular", " .NET ", "SQL", "Angular", "string", ""],
        "competences_souhaitees": ["Docker", "Azure"],
        "keywords_ats": ["Angular", "api testing", "a very long sentence that is not a keyword at all", ".NET"],
    }

    for _ in range(3):  # a set would give a different order between runs
        cleaned = offer_validator_node({"analyzed_offer": {k: list(v) if isinstance(v, list) else v for k, v in offer.items()}})["analyzed_offer"]
        assert cleaned["competences_requises"] == ["Angular", ".NET", "SQL"]
        assert cleaned["competences_souhaitees"] == ["Docker", "Azure"]
        assert cleaned["keywords_ats"] == ["Angular", "api testing", ".NET"]
