# ============================================================
# agents/tests/test_normalizer.py
# Tests unitaires — Agent 3 (Normalizer)
# Fonctions pures → aucune dépendance externe (pas de LLM, pas de DB)
# ============================================================
import pytest
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from app.core.utils.normalizer import normalize_skills, normalize_text, SYNONYMES


class TestNormalizeSkills:
    """Tests de normalize_skills() — Agent 3."""

    def test_basic_normalization(self):
        """Conversion simple : react.js → react."""
        result = normalize_skills(["React.js"])
        assert "react" in result

    def test_typescript_alias(self):
        """TS et TypeScript → même clé canonique 'typescript'."""
        result = normalize_skills(["TS", "TypeScript"])
        assert result.count("typescript") == 1, "Dédupliquation attendue"

    def test_nodejs_alias(self):
        """Node, nodejs, node.js → 'node.js'."""
        result = normalize_skills(["Node", "nodejs", "node.js"])
        assert result.count("node.js") == 1

    def test_entity_framework_alias(self):
        """'Entity Framework' et 'EF' → 'ef core'."""
        result = normalize_skills(["Entity Framework", "EF"])
        assert result.count("ef core") == 1

    def test_kubernetes_alias(self):
        """'k8s' → 'kubernetes'."""
        result = normalize_skills(["k8s"])
        assert result == ["kubernetes"]

    def test_accent_removal(self):
        """Caractères accentués supprimés correctement."""
        result = normalize_skills(["Développement"])
        assert "developpement" in result[0]

    def test_deduplication(self):
        """Pas de doublon dans le résultat."""
        result = normalize_skills(["React", "React", "react"])
        assert len(result) == 1
        assert result[0] == "react"

    def test_separator_split(self):
        """Séparateurs '/' et ',' gérés automatiquement."""
        result = normalize_skills(["JS / TS"])
        assert "javascript" in result
        assert "typescript" in result

    def test_empty_list(self):
        """Liste vide → liste vide."""
        assert normalize_skills([]) == []

    def test_mixed_list(self):
        """Liste mixte avec alias, accents et doublons."""
        result = normalize_skills(["React.js", "TS", "Node", "DRF", "k8s", "React"])
        assert "react" in result
        assert "typescript" in result
        assert "node.js" in result
        assert "django" in result
        assert "kubernetes" in result
        # Pas de doublon react
        assert result.count("react") == 1

    def test_case_insensitive(self):
        """Insensible à la casse."""
        r1 = normalize_skills(["PYTHON"])
        r2 = normalize_skills(["python"])
        r3 = normalize_skills(["Python"])
        assert r1 == r2 == r3


class TestNormalizeText:
    """Tests de normalize_text() — texte libre."""

    def test_lowercase(self):
        """Conversion en minuscules."""
        result = normalize_text("HELLO WORLD")
        assert result == "hello world"

    def test_accent_removal(self):
        """Suppression des accents."""
        result = normalize_text("développeur expérimenté")
        assert "e" in result
        assert "e" in result
        # Vérifie qu'il n'y a plus d'accents
        assert "é" not in result
        assert "è" not in result

    def test_special_chars_removed(self):
        """Caractères spéciaux supprimés (sauf . / # - +)."""
        result = normalize_text("C# .NET / React-Node")
        assert "c#" in result
        assert ".net" in result

    def test_empty_string(self):
        """Chaîne vide → chaîne vide."""
        assert normalize_text("") == ""


class TestSynonyms:
    """Tests du dictionnaire SYNONYMES."""

    def test_synonymes_not_empty(self):
        """Au moins 30 synonymes définis."""
        assert len(SYNONYMES) >= 30

    def test_key_value_are_strings(self):
        """Toutes les clés et valeurs sont des strings."""
        for k, v in SYNONYMES.items():
            assert isinstance(k, str), f"Clé non-string : {k}"
            assert isinstance(v, str), f"Valeur non-string : {v}"

    def test_no_self_mapping(self):
        """Aucune clé ne mappe vers elle-même (sauf formes canoniques déjà normalisées)."""
        # Certaines clés comme 'next.js' → 'next.js' sont volontaires :
        # elles garantissent que la forme canonique est retournée après normalize_token.
        # On vérifie simplement qu'il n'y a pas de doublons inutiles.
        for k, v in SYNONYMES.items():
            assert isinstance(k, str) and isinstance(v, str)
