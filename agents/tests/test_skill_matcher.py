"""Deterministic skill matching (POST /offer/match and the offer pipeline)."""
from app.domain.matching.skill_matcher import MATCH, PARTIAL, ProfileEvidence, build_match_result, compute_match


def _profile(skills=(), summary="", experiences=(), projects=()):
    return {
        "competences": [{"nom": s} for s in skills],
        "resume": summary,
        "experiences": list(experiences),
        "projets": list(projects),
    }


def _offer(required=(), preferred=(), keywords=()):
    return {
        "titre": "Développeur",
        "competences_requises": list(required),
        "competences_souhaitees": list(preferred),
        "keywords_ats": list(keywords),
    }


class TestNoSubstringFalsePositives:
    def test_java_is_not_javascript(self):
        evidence = ProfileEvidence(_profile(skills=["JavaScript"]))
        assert evidence.score("java") == 0.0

    def test_sql_is_not_nosql(self):
        assert ProfileEvidence(_profile(skills=["NoSQL"])).score("sql") == 0.0

    def test_one_word_of_a_phrase_is_only_partial(self):
        evidence = ProfileEvidence(_profile(summary="Passionné de data et de visualisation"))
        assert evidence.score("data engineering") == PARTIAL


class TestRealMatches:
    def test_skill_written_in_an_experience(self):
        profile = _profile(experiences=[{"titre": "Dev", "description": "Déploiement avec Docker et Kubernetes"}])
        assert compute_match(profile, _offer(required=["Docker", "Kubernetes"]))["score_matching"] == 100

    def test_multi_word_skill_found_as_a_phrase(self):
        profile = _profile(summary="Projets de machine learning en Python")
        assert ProfileEvidence(profile).score("machine learning") == MATCH

    def test_synonyms_and_aliases(self):
        evidence = ProfileEvidence(_profile(skills=["ReactJS", "Postgres", "GitHub Actions"]))
        assert evidence.score("react") == MATCH
        assert evidence.score("postgresql") == MATCH
        assert evidence.score("ci/cd") == MATCH

    def test_implied_skills(self):
        evidence = ProfileEvidence(_profile(skills=["PostgreSQL", "GitHub", "Django"]))
        assert evidence.score("sql") == MATCH
        assert evidence.score("git") == MATCH
        assert evidence.score("python") == MATCH

    def test_typo_in_a_long_word(self):
        assert ProfileEvidence(_profile(skills=["Kubernete"])).score("kubernetes") == MATCH


class TestResult:
    def test_scores_and_offer_labels(self):
        profile = _profile(skills=["Node.js", "PostgreSQL"])
        offer = _offer(required=["Node.js", "Java", "PostgreSQL"], keywords=["Node.js", "Java"])

        result = build_match_result(profile, offer)

        assert result["matched_skills"] == ["Node.js", "PostgreSQL"]
        assert result["missing_skills"] == ["Java"]
        assert result["score_matching"] == 67
        assert result["keywords_presents"] == ["Node.js"]
        assert result["keywords_manquants"] == ["Java"]
        assert result["score_ats"] == 50
        assert result["flag"] == "minor_gap"

    def test_ci_cd_is_not_split_in_two(self):
        result = build_match_result(_profile(skills=["CI/CD"]), _offer(required=["CI/CD"]))
        assert result["matched_skills"] == ["CI/CD"]

    def test_empty_profile_and_offer(self):
        result = build_match_result({}, {})
        assert result["score_matching"] == 0
        assert result["matched_skills"] == []


class TestTargets:
    def test_short_skill_lists_are_used_as_they_are(self):
        result = build_match_result(_profile(skills=["Angular"]), _offer(required=["Angular", "Java"], keywords=["Angular"]))
        assert result["missing_skills"] == ["Java"]

    def test_sentence_lists_fall_back_to_ats_keywords(self):
        offer = _offer(
            required=["Maîtrise des outils de développement web modernes et agiles", "Capacité à travailler en équipe dans un contexte international", "Docker"],
            keywords=["Docker", "React"],
        )
        result = build_match_result(_profile(skills=["Docker"]), offer)
        assert result["matched_skills"] == ["Docker"] and result["missing_skills"] == ["React"]
