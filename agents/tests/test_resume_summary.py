"""POST /generate-resume: what the summary writer may use from the profile."""
from app.domain.resume.summary import SYSTEM_PROMPT_EN, SYSTEM_PROMPT_FR, _select_system_prompt, has_content, profile_facts


def test_facts_keep_the_career_and_drop_contact_details():
    profile = {
        "personal": {"jobTitle": "Développeuse", "email": "s@x.ma", "phone": "0600000000"},
        "experience": [{"title": "Stagiaire", "company": "Acme", "current": True, "taches": ["API"]}],
        "skills": [{"name": "Angular"}],
    }

    facts = profile_facts(profile)

    assert facts["poste_vise"] == "Développeuse"
    assert facts["experiences"][0]["entreprise"] == "Acme"
    assert facts["experiences"][0]["fin"] == "en cours"
    assert facts["competences"] == ["Angular"]
    assert "s@x.ma" not in str(facts) and "0600000000" not in str(facts)


def test_an_empty_profile_has_nothing_to_summarise():
    assert not has_content(profile_facts({}))
    assert has_content(profile_facts({"skills": [{"name": "Python"}]}))


def test_defaults_to_the_english_prompt():
    assert _select_system_prompt(None) is SYSTEM_PROMPT_EN
    assert _select_system_prompt("en") is SYSTEM_PROMPT_EN
    assert _select_system_prompt("de") is SYSTEM_PROMPT_EN  # unsupported -> English default


def test_french_preference_selects_the_french_prompt_case_and_space_insensitively():
    assert _select_system_prompt("fr") is SYSTEM_PROMPT_FR
    assert _select_system_prompt(" FR ") is SYSTEM_PROMPT_FR
