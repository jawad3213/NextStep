"""SN Copilot: which candidature a command designates, and which statuses it may write."""
from app.domain.sn_copilot.tools import find_candidature, normalize_status

CANDIDATURES = [
    {"id": "a1", "entreprise": "", "role": "Stage data"},
    {"id": "b2", "entreprise": "Phi Partners", "role": "Quantitative Developer"},
    {"id": "c3", "entreprise": "BCG", "role": "AI Software Engineer"},
    {"id": "d4", "entreprise": "BCG X", "role": "Data Engineer"},
]


def test_empty_query_matches_nothing():
    assert find_candidature(CANDIDATURES, "  ")["status"] == "not_found"


def test_empty_company_does_not_match_every_query():
    found = find_candidature(CANDIDATURES, "phi partners")
    assert found["candidature"]["id"] == "b2"


def test_exact_company_wins_over_partial_ones():
    assert find_candidature(CANDIDATURES, "BCG")["candidature"]["id"] == "c3"


def test_several_partial_matches_ask_which_one():
    result = find_candidature(CANDIDATURES, "bc")
    assert result["status"] == "ambiguous"
    assert "BCG X" in result["message"]


def test_role_match():
    assert find_candidature(CANDIDATURES, "quantitative")["candidature"]["id"] == "b2"


def test_statuses():
    assert normalize_status("entretien") == "ENTRETIEN_PROPOSE"
    assert normalize_status("refusé") == "REFUSE"
    assert normalize_status("OFFRE_RECUE") == "OFFRE_RECUE"
    assert normalize_status("en cours examen") == "EN_COURS_EXAMEN"
    assert normalize_status("n'importe quoi") is None
