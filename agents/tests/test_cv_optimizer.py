"""CV Optimizer: the LLM output keeps its wording but never its invented facts."""
import pytest
from app.domain.cv_optimizer.agents.agent import (
    MAX_ATTEMPTS,
    _build_fallback_result,
    _cv_content_for_llm,
    _normalize_optimized_output,
    cv_optimizer_router,
    cv_validator_node,
)
from app.domain.cv_optimizer.agents.prompts import output_language_name

PROFILE = {
    "user_id": "u1",
    "nom": "Alami",
    "prenom": "Sara",
    "email": "sara@example.com",
    "telephone": "+212600000000",
    "photo_url": "https://example.com/photo.jpg",
    "linkedin": "https://linkedin.com/in/sara",
    "github": "https://github.com/sara",
    "portfolio": "https://sara.dev",
    "ville": "Casablanca",
    "resume": "Développeuse full stack.",
    "experiences": [
        {"titre": "Stagiaire Développeuse", "entreprise": "Acme", "description": "API REST", "taches": ["Conçu des API"]},
        {"titre": "Développeuse", "entreprise": "Globex", "description": "Front Angular", "taches": ["Développé le front"]},
    ],
    "projets": [{"titre": "NextStep", "description": "Plateforme", "technologies": ["Angular", ".NET"], "taches": []}],
    "formations": [{"diplome": "Cycle Ingénieur", "etablissement": "ENSA"}],
    "certifications": [{"nom": "AWS CCP", "organisme": "AWS"}],
    "competences": [{"nom": "Angular"}, {"nom": "Node.js"}, {"nom": ".NET"}],
}


def _llm_output(**overrides):
    output = {
        "resume_optimise": {"contenu": "Développeuse full stack orientée produit."},
        "experiences_optimisees": [
            {"titre": "Stagiaire Développeuse", "entreprise": "Acme", "description_optimisee": "APIs",
             "taches_optimisees": ["Conçu 5 API REST", "Réduit le temps de réponse de [X]%"], "niveau_pertinence": "high"},
            {"titre": "CTO", "entreprise": "Inventée SA", "taches_optimisees": ["Dirigé 50 personnes"]},
        ],
        "projets_optimises": [
            {"titre": "NextStep", "description_optimisee": "Plateforme IA", "technologies": ["Angular", "Kubernetes"],
             "taches_optimisees": ["Intégré .NET"]},
        ],
        "formations_optimisees": [],
        "certifications_optimisees": [],
        "competences_reordonnees": ["NodeJS", "Rust", "Angular"],
        "competences_mises_en_avant": ["Angular", "Rust"],
    }
    output.update(overrides)
    return output


def test_invented_experience_is_dropped_and_missing_one_restored():
    result = _normalize_optimized_output(_llm_output(), PROFILE)
    companies = [e["entreprise"] for e in result["experiences_optimisees"]]
    assert companies == ["Acme", "Globex"]
    assert result["experiences_optimisees"][1]["niveau_pertinence"] == "low"


def test_placeholder_bullets_are_removed():
    bullets = _normalize_optimized_output(_llm_output(), PROFILE)["experiences_optimisees"][0]["taches_optimisees"]
    assert bullets == ["Conçu 5 API REST"]


def test_project_technologies_stay_within_the_project():
    project = _normalize_optimized_output(_llm_output(), PROFILE)["projets_optimises"][0]
    assert project["technologies"] == ["Angular"]


def test_skills_come_from_the_profile_only():
    result = _normalize_optimized_output(_llm_output(), PROFILE)
    assert result["competences_reordonnees"] == ["Node.js", "Angular", ".NET"]
    assert result["competences_mises_en_avant"] == ["Angular"]


def test_education_and_certifications_copied_from_the_profile():
    result = _normalize_optimized_output(_llm_output(), PROFILE)
    assert result["formations_optimisees"] == [{"diplome": "Cycle Ingénieur", "etablissement": "ENSA"}]
    assert result["certifications_optimisees"] == [{"nom": "AWS CCP", "organisme": "AWS"}]


def test_placeholder_summary_falls_back_to_the_profile_summary():
    result = _normalize_optimized_output(_llm_output(resume_optimise={"contenu": "Expert avec [X] ans"}), PROFILE)
    assert result["resume_optimise"]["contenu"] == "Développeuse full stack."


def test_fallback_without_llm_keeps_the_whole_profile():
    result = _build_fallback_result(PROFILE)
    assert [e["entreprise"] for e in result["experiences_optimisees"]] == ["Acme", "Globex"]
    assert result["competences_reordonnees"] == ["Angular", "Node.js", ".NET"]


def test_only_cv_content_is_sent_to_the_llm():
    content = _cv_content_for_llm(PROFILE)

    assert set(content) == {"resume", "experiences", "projets", "formations", "certifications", "competences"}
    for field in ("user_id", "nom", "prenom", "email", "telephone", "photo_url", "linkedin", "github", "portfolio", "ville"):
        assert field not in content
    assert content["experiences"] == PROFILE["experiences"]


def test_cv_content_skips_missing_fields():
    assert _cv_content_for_llm({"resume": "x", "email": "a@b.c"}) == {"resume": "x"}


def test_output_language_defaults_to_english():
    assert output_language_name(None) == output_language_name("en")
    assert "anglais" in output_language_name(None).lower() or "english" in output_language_name(None).lower()


def test_output_language_recognizes_french_case_and_space_insensitively():
    assert output_language_name(" FR ") == output_language_name("fr")
    assert "francais" in output_language_name("fr").lower() or "french" in output_language_name("fr").lower()


def test_unknown_language_falls_back_to_english():
    assert output_language_name("de") == output_language_name("en")


def test_router_retries_only_for_latest_fixable_problems():
    assert cv_optimizer_router({"iteration_count": 1, "validation_errors": ["x"]}) == "retry"
    assert cv_optimizer_router({"iteration_count": MAX_ATTEMPTS, "validation_errors": ["x"]}) == "end"
    assert cv_optimizer_router({"iteration_count": 1, "validation_errors": []}) == "end"
    # Errors accumulated from earlier attempts no longer force new attempts.
    assert cv_optimizer_router({"iteration_count": 1, "errors": ["old"], "validation_errors": []}) == "end"
    # After an LLM failure the profile is used as-is: no retry.
    assert cv_optimizer_router({"iteration_count": 1, "validation_errors": ["x"], "used_fallback": True}) == "end"


def _validate(optimized, offer=None):
    state = {"candidate_cv": PROFILE, "optimized_cv": optimized, "job_offer": offer or {"skills": ["Angular"]}}
    return cv_validator_node(state)["validation_errors"]


def test_style_advice_does_not_block():
    optimized = _normalize_optimized_output(_llm_output(), PROFILE)
    # No action verb / metric on some bullets, but the CV mentions the offer's keyword.
    assert _validate(optimized) == []


def test_giant_bullet_written_by_the_model_blocks():
    giant = "Développé " + " ".join(["une fonctionnalité"] * 30)
    optimized = _normalize_optimized_output(
        _llm_output(experiences_optimisees=[{"titre": "Stagiaire Développeuse", "entreprise": "Acme", "taches_optimisees": [giant]}]),
        PROFILE,
    )
    assert any("trop longue" in e for e in _validate(optimized))


def test_giant_bullet_copied_from_the_profile_does_not_block():
    giant = "Développé " + " ".join(["une fonctionnalité"] * 30)
    profile = {**PROFILE, "experiences": [{"titre": "Dev", "entreprise": "Acme", "taches": [giant]}]}
    optimized = _normalize_optimized_output(
        _llm_output(experiences_optimisees=[{"titre": "Dev", "entreprise": "Acme", "taches_optimisees": [giant]}]),
        profile,
    )
    state = {"candidate_cv": profile, "optimized_cv": optimized, "job_offer": {"skills": ["Angular"]}}
    assert cv_validator_node(state)["validation_errors"] == []


def test_cv_without_any_offer_keyword_blocks():
    optimized = _normalize_optimized_output(_llm_output(), PROFILE)
    assert _validate(optimized, {"skills": ["Kubernetes"]}) == ["Le CV optimise ne reprend aucun mot-cle significatif de l'offre."]


# --- CV summary: about the candidate, never the job -----------------------------------------
from app.domain.cv_optimizer.agents.agent import fallback_summary, summary_problem

OFFER = {
    "titre": "Stage PFA - Automatisation & IA",
    "type_contrat": "Stage",
    "description_poste": "Le stagiaire participera à la conception et à l'implémentation de workflows automatisés "
                         "et d'applications d'intelligence artificielle, en intégrant des API et en manipulant des données JSON.",
}
SCREENSHOT_SUMMARY = (
    "Le stagiaire participera à la conception et à l'implémentation de workflows automatisés et d'applications "
    "d'intelligence artificielle, en intégrant des API et en manipulant des données JSON. Il contribuera à des projets "
    "concrets tels que l'automatisation commerciale, le traitement de formulaires et le développement d'assistants IA, "
    "tout en documentant et testant les solutions."
)


def test_summary_describing_the_job_is_rejected():
    assert "decrit le poste" in summary_problem(SCREENSHOT_SUMMARY, OFFER)


@pytest.mark.parametrize("text", [
    "You will build automated workflows and AI assistants for our clients.",
    "Nous recherchons un profil curieux pour rejoindre notre équipe.",
    "The successful candidate will integrate APIs.",
])
def test_other_job_wording_is_rejected(text):
    assert summary_problem(text, OFFER)


def test_summary_copied_from_the_offer_is_rejected():
    copied = ("Conception et implémentation de workflows automatisés et d'applications d'intelligence "
              "artificielle, en intégrant des API et en manipulant des données JSON.")
    assert summary_problem(copied, OFFER) == "il recopie la description de l'offre"


def test_too_long_summary_is_rejected():
    assert "trop long" in summary_problem("Ingénieur " + "motivé " * 100, OFFER)


@pytest.mark.parametrize("text", [
    "Élève ingénieur en génie logiciel, je recherche un stage PFA en automatisation et IA. "
    "Je m'appuie sur Python, LangChain et l'intégration d'API pour livrer des assistants fiables.",
    "Engineering student passionate about AI, seeking a PFA internship to build automation workflows with Python and APIs.",
])
def test_first_person_candidate_summary_is_accepted(text):
    assert summary_problem(text, OFFER) is None


def test_fallback_keeps_the_candidates_own_summary():
    assert fallback_summary(PROFILE, OFFER, "fr") == PROFILE["resume"]


def test_fallback_is_built_from_the_profile_when_there_is_no_usable_one():
    profile = {**PROFILE, "resume": SCREENSHOT_SUMMARY}
    fr = fallback_summary(profile, OFFER, "fr")
    assert fr.startswith("Cycle Ingénieur, je recherche un stage PFA en tant que Stage PFA - Automatisation & IA.")
    assert "Angular, Node.js, .NET" in fr
    assert summary_problem(fr, OFFER) is None

    en = fallback_summary({**profile, "resume": ""}, OFFER, "en")
    assert en.startswith("Cycle Ingénieur, looking for a PFA internship as")
    assert summary_problem(en, OFFER) is None


def test_validator_asks_for_a_rewrite_when_the_summary_was_rejected():
    optimized = _normalize_optimized_output(_llm_output(), PROFILE)
    state = {"candidate_cv": PROFILE, "optimized_cv": optimized, "job_offer": {"skills": ["Angular"]},
             "summary_problem": "il decrit le poste au lieu de presenter le candidat"}
    errors = cv_validator_node(state)["validation_errors"]
    assert any("resume_optimise" in e and "premiere personne" in e for e in errors)


# --- The "seeking" part comes from the profile's target position ------------------------------
TARGET = "AI Engineer Intern"


def test_target_position_is_sent_to_the_llm():
    content = _cv_content_for_llm({**PROFILE, "titre": TARGET})
    assert content["poste_vise"] == TARGET


def test_no_target_position_sends_nothing_extra():
    assert "poste_vise" not in _cv_content_for_llm({**PROFILE, "titre": "  "})


def test_summary_must_name_the_profiles_target_position():
    about_offer_role = ("Engineering student at ENSA, seeking a PFA internship in automation. "
                        "Experienced with Python, REST APIs and n8n workflows.")
    assert "poste vise" in summary_problem(about_offer_role, OFFER, TARGET)

    about_target = ("Engineering student at ENSA, seeking a position as AI Engineer Intern. "
                    "Experienced with Python, LangChain and REST APIs.")
    assert summary_problem(about_target, OFFER, TARGET) is None


def test_fallback_seeks_the_target_position_not_the_offer_title():
    profile = {**PROFILE, "resume": "", "titre": TARGET}
    assert fallback_summary(profile, OFFER, "en").startswith("Cycle Ingénieur, looking for a PFA internship as AI Engineer Intern.")
    assert "en tant que AI Engineer Intern." in fallback_summary(profile, OFFER, "fr")


def test_own_summary_not_naming_the_target_is_not_reused():
    profile = {**PROFILE, "titre": TARGET}  # PROFILE["resume"] = "Développeuse full stack."
    assert fallback_summary(profile, OFFER, "en") != PROFILE["resume"]
