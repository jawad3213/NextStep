"""CV Engine formatter: merge of the original profile and the optimized CV (QuestPDF payload)."""
from app.domain.cv_engine.agents.formatter import build_questpdf_payload


def _exp(titre, entreprise, debut, fin=None, type_=None, taches=None):
    return {"titre": titre, "entreprise": entreprise, "date_debut": debut, "date_fin": fin,
            "type": type_, "description": "", "taches": taches or []}


def _opt_exp(titre, entreprise):
    return {"titre": titre, "entreprise": entreprise, "description_optimisee": "",
            "taches_optimisees": ["Développé une API"], "niveau_pertinence": "medium"}


def _payload(profile, optimized, **kwargs):
    base_profile = {"prenom": "Sara", "nom": "Alami", "email": "s@x.ma", "competences": [], "experiences": [],
                    "formations": [], "projets": [], "certifications": [], "activities": []}
    base_opt = {"resume_optimise": {"contenu": "Résumé"}, "experiences_optimisees": [], "projets_optimises": [],
                "formations_optimisees": [], "certifications_optimisees": [], "competences_reordonnees": [],
                "competences_mises_en_avant": []}
    return build_questpdf_payload({**base_profile, **profile}, {**base_opt, **optimized}, **kwargs)


def test_two_jobs_with_the_same_title_keep_their_own_dates():
    profile = {"experiences": [
        _exp("Développeur Full Stack", "Acme", "2023-01-01", "2023-12-31"),
        _exp("Développeur Full Stack", "Globex", "2024-01-01", "2024-12-31"),
    ]}
    optimized = {"experiences_optimisees": [_opt_exp("Développeur Full Stack", "Globex"),
                                            _opt_exp("Développeur Full Stack", "Acme")]}

    result = _payload(profile, optimized)

    dates = {e.company: e.start for e in result.experience}
    assert dates == {"Acme": "2023-01-01", "Globex": "2024-01-01"}


def test_a_job_mentioning_trainings_or_events_stays_a_job():
    profile = {"experiences": [_exp("Formateur technique", "Kids Academy", "2023-01-01", type_="CDI")]}
    optimized = {"experiences_optimisees": [
        {**_opt_exp("Formateur technique", "Kids Academy"), "description_optimisee": "Formation, events et prix"}
    ]}

    result = _payload(profile, optimized)

    assert [e.company for e in result.experience] == ["Kids Academy"]
    assert result.activities == []


def test_club_entry_is_an_activity_listed_once():
    profile = {"experiences": [_exp("Présidente", "Club Robotique", "2022-09-01")],
               "activities": []}
    optimized = {"experiences_optimisees": [_opt_exp("Présidente", "Club Robotique")]}

    result = _payload(profile, optimized)

    assert result.experience == []
    assert [(a.title, a.role) for a in result.activities] == [("Présidente", "Club Robotique")]


def test_typed_extracurricular_listed_once():
    profile = {
        "experiences": [_exp("Membre", "Enactus", "2022-09-01", type_="Extracurricular")],
        "activities": [{"title": "Membre", "role": "Enactus", "description": None}],
    }
    optimized = {"experiences_optimisees": [_opt_exp("Membre", "Enactus")]}

    assert len(_payload(profile, optimized).activities) == 1


def test_education_shows_the_graduation_year():
    profile = {"formations": [{"diplome": "Cycle Ingénieur", "etablissement": "ENSA", "annee": 2022, "annee_fin": 2027}]}
    optimized = {"formations_optimisees": [{"diplome": "Cycle Ingénieur", "etablissement": "ENSA"}]}

    assert _payload(profile, optimized).education[0].year == "2027"


def test_only_real_matches_are_flagged_and_counted():
    profile = {"competences": [{"nom": "React"}, {"nom": "Figma"}]}
    optimized = {"competences_reordonnees": ["React", "Figma"]}

    result = _payload(profile, optimized, matched_skills=["React.js"], offer_skills=["React.js", "Java"])

    flags = {s.name: s.is_matched for s in result.skills}
    assert flags == {"React": True, "Figma": False}
    assert result.ats_score == 50
