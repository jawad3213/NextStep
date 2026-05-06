# ============================================================
# agents/tests/test_scorer.py
# Tests unitaires — Agent 4 (Scorer)
# Algorithmes purs : ats_score() et matching_score()
# ============================================================
import pytest
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from app.domain.offer.agents.scorer.scoring import (
    ats_score,
    matching_score,
    recommendations,
    POSITION_BONUS,
    MAX_BONUS_PAR_KW,
)


class TestAtsScore:
    """Tests de ats_score() — score positionnel ATS."""

    def test_perfect_score_all_positions(self):
        """Mot-clé présent dans toutes les zones → score élevé."""
        kw = ["python"]
        score, presents, manquants = ats_score(
            kw,
            profile_titre="python developer",
            profile_resume="expert python backend",
            profile_skills_text="python django fastapi",
            exp_text="développé en python"
        )
        assert score > 80
        assert "python" in presents
        assert manquants == []

    def test_empty_keywords(self):
        """Aucun mot-clé → score 0."""
        score, presents, manquants = ats_score([], "", "", "", "")
        assert score == 0
        assert presents == []
        assert manquants == []

    def test_missing_keyword(self):
        """Mot-clé absent partout → dans manquants."""
        score, presents, manquants = ats_score(
            ["kubernetes"],
            profile_titre="python developer",
            profile_resume="backend django",
            profile_skills_text="python django",
            exp_text="développé en python"
        )
        assert "kubernetes" in manquants
        assert "kubernetes" not in presents

    def test_partial_match(self):
        """Certains mots-clés présents, d'autres non."""
        keywords = ["react", "kubernetes", "docker"]
        score, presents, manquants = ats_score(
            keywords,
            profile_titre="développeur react",
            profile_resume="expérience react docker",
            profile_skills_text="react docker typescript",
            exp_text="react frontend"
        )
        assert "react" in presents
        assert "docker" in presents
        assert "kubernetes" in manquants
        assert 0 < score < 100

    def test_score_bounded(self):
        """Score toujours dans [0, 100]."""
        kw = ["python", "react", "docker", "typescript", "node.js"]
        full_text = "python react docker typescript node.js developer senior"
        score, _, _ = ats_score(kw, full_text, full_text, full_text, full_text)
        assert 0 <= score <= 100

    def test_title_bonus_higher(self):
        """Présence dans le titre donne plus de bonus que dans expériences uniquement."""
        kw = ["react"]
        # Cas 1 : mot-clé SEULEMENT dans les expériences
        score_only_exp, _, _ = ats_score(
            kw,
            profile_titre="python developer",
            profile_resume="backend django",
            profile_skills_text="python django",
            exp_text="développé avec react",
        )
        # Cas 2 : mot-clé dans le titre + exp
        score_title_plus_exp, _, _ = ats_score(
            kw,
            profile_titre="react developer",
            profile_resume="backend django",
            profile_skills_text="python django",
            exp_text="développé avec react",
        )
        # Titre + exp doit donner un score >= exp seule
        assert score_title_plus_exp >= score_only_exp

    def test_position_bonus_values(self):
        """Vérification des valeurs de pondération positionnelle."""
        assert POSITION_BONUS["titre"] == 5
        assert POSITION_BONUS["resume"] == 3
        assert POSITION_BONUS["competences"] == 2
        assert POSITION_BONUS["experiences"] == 1
        assert MAX_BONUS_PAR_KW == 11

    def test_no_false_positive_java_javascript(self):
        """'javascript' ne matche pas une ligne contenant seulement 'java'."""
        score, presents, manquants = ats_score(
            ["javascript"],
            profile_titre="java developer",
            profile_resume="expert java spring",
            profile_skills_text="java spring hibernate",
            exp_text="développé en java"
        )
        assert "javascript" in manquants, "java ne doit pas matcher javascript"


class TestMatchingScore:
    """Tests de matching_score() — score Jaccard pondéré."""

    def test_perfect_match(self):
        """Profil contient toutes les compétences requises et souhaitées."""
        score, matching, manquantes = matching_score(
            offer_required=["react", "typescript", "node.js"],
            offer_optional=["kubernetes"],
            profile_skills=["react", "typescript", "node.js", "kubernetes"],
        )
        assert score == 100
        assert manquantes == []
        assert set(matching) == {"react", "typescript", "node.js", "kubernetes"}

    def test_zero_match(self):
        """Profil ne contient aucune compétence de l'offre."""
        score, matching, manquantes = matching_score(
            offer_required=["kubernetes", "rust", "erlang"],
            offer_optional=["scala"],
            profile_skills=["react", "python", "django"],
        )
        assert score == 0
        assert matching == []
        assert set(manquantes) == {"kubernetes", "rust", "erlang"}

    def test_partial_match_70_30(self):
        """70% requis + 30% optionnel bien pondéré."""
        score, _, _ = matching_score(
            offer_required=["react", "typescript"],
            offer_optional=["kubernetes", "redis"],
            profile_skills=["react", "typescript"],  # 100% requis, 0% optionnel
        )
        # s_req=100 × 0.70 + s_opt=0 × 0.30 = 70
        assert score == 70

    def test_empty_required(self):
        """Aucune compétence requise → score requis = 100 (pas pénalisé)."""
        score, _, _ = matching_score(
            offer_required=[],
            offer_optional=["kubernetes"],
            profile_skills=["react"],
        )
        # s_req=100 × 0.70 + s_opt=0 × 0.30 = 70
        assert score == 70

    def test_textual_fallback(self):
        """Compétence présente dans le texte mais pas dans la liste → match contextuel."""
        score, matching, _ = matching_score(
            offer_required=["docker"],
            offer_optional=[],
            profile_skills=["react"],  # docker pas dans la liste
            profile_full_text="expérience avec docker et kubernetes",  # mais dans le texte
        )
        assert "docker" in matching
        assert score > 0

    def test_score_always_bounded(self):
        """Score toujours dans [0, 100]."""
        for _ in range(5):
            score, _, _ = matching_score(
                offer_required=["react", "node.js"],
                offer_optional=["kubernetes"],
                profile_skills=["react", "node.js", "kubernetes", "typescript"],
            )
            assert 0 <= score <= 100


class TestRecommendations:
    """Tests de recommendations()."""

    def test_no_recommendations_perfect_score(self):
        """Score parfait → seulement le message positif."""
        recs = recommendations([], [], score_ats=90, score_matching=80)
        assert any("Excellent" in r for r in recs)
        assert len(recs) >= 1

    def test_keywords_manquants_listed(self):
        """Mots-clés manquants → recommandation mentionnant les mots-clés."""
        recs = recommendations(
            kw_manquants=["kubernetes", "terraform", "ansible"],
            comp_manquantes=[],
            score_ats=60,
            score_matching=70,
        )
        assert any("kubernetes" in r for r in recs)

    def test_low_ats_recommendation(self):
        """Score ATS < 40 → recommandation sur l'enrichissement du résumé."""
        recs = recommendations([], [], score_ats=30, score_matching=70)
        assert any("très faible" in r for r in recs)

    def test_low_matching_recommendation(self):
        """Score matching < 50 → recommandation sur l'inadéquation du profil."""
        recs = recommendations([], [], score_ats=70, score_matching=40)
        assert any("faible" in r.lower() for r in recs)

    def test_max_keywords_shown(self):
        """Maximum 5 mots-clés affichés dans la recommandation."""
        many_kw = [f"kw{i}" for i in range(10)]
        recs = recommendations(many_kw, [], score_ats=60, score_matching=70)
        kw_rec = next((r for r in recs if "ATS" in r), "")
        # Maximum 5 mots-clés affichés
        assert kw_rec.count("kw") <= 5
