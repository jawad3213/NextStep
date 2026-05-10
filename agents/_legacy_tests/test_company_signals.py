# ============================================================
# agents/tests/test_company_signals.py
# Tests unitaires — Agent Company (signals.py)
# ============================================================
import pytest
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from app.domain.company.agents.company_analyzer.signals import (
    detect_company_size,
    detect_remote_policy,
)


class TestDetectCompanySize:
    """Tests de detect_company_size()."""

    def test_startup_signal(self):
        """Texte contenant 'startup' → 'startup'."""
        assert detect_company_size("Rejoignez notre startup en pleine croissance") == "startup"

    def test_scale_up_signal(self):
        """'scale-up' → 'startup'."""
        assert detect_company_size("Nous sommes une scale-up tech parisienne") == "startup"

    def test_grand_groupe_signal(self):
        """'international' ou 'groupe' → 'grand_groupe'."""
        assert detect_company_size("Groupe international avec 10 000 employés") == "grand_groupe"

    def test_multinational_signal(self):
        """'multinational' → 'grand_groupe'."""
        assert detect_company_size("Entreprise multinationale CAC40") == "grand_groupe"

    def test_pme_default(self):
        """Texte sans signal → 'pme' par défaut."""
        assert detect_company_size("Nous développons des applications web") == "pme"

    def test_empty_text(self):
        """Texte vide → 'pme' par défaut."""
        assert detect_company_size("") == "pme"

    def test_case_insensitive(self):
        """Insensible à la casse."""
        assert detect_company_size("STARTUP") == "startup"


class TestDetectRemotePolicy:
    """Tests de detect_remote_policy()."""

    def test_full_remote(self):
        """'full remote' → 'full_remote'."""
        assert detect_remote_policy("Poste 100% full remote, depuis chez vous") == "full_remote"

    def test_100_teletravail(self):
        """'100% teletravail' → 'full_remote'."""
        assert detect_remote_policy("100% teletravail possible") == "full_remote"

    def test_hybride(self):
        """'hybride' → 'hybride'."""
        assert detect_remote_policy("Politique hybride : 3 jours télétravail") == "hybride"

    def test_teletravail_partiel(self):
        """'teletravail partiel' → 'hybride'."""
        assert detect_remote_policy("teletravail partiel envisageable") == "hybride"

    def test_presentiel(self):
        """'présentiel' / 'bureau' → 'presentiel'."""
        result = detect_remote_policy("Poste 100% au bureau, en presentiel")
        assert result == "presentiel"

    def test_no_policy(self):
        """Texte sans information remote → None."""
        result = detect_remote_policy("Poste de développeur avec belles missions")
        assert result is None

    def test_empty_text(self):
        """Texte vide → None."""
        assert detect_remote_policy("") is None
