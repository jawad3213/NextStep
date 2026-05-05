# ============================================================
# app/domain/company/agents/company_analyzer/signals.py
# Fonctions de détection de signaux dans les offres
# Isolées pour enrichissement facile et tests unitaires
# ============================================================
from app.domain.offer.agents.normalizer import normalize_text

# ─── Signaux de détection ─────────────────────────────────────
_REMOTE_SIGNALS       = {"télétravail", "remote", "distanciel", "full remote", "hybrid", "hybride"}
_STARTUP_SIGNALS      = {"startup", "scale-up", "jeune pousse", "early stage", "seed", "levée de fonds"}
_GRAND_GROUPE_SIGNALS = {"groupe", "international", "multinational", "cac40", "fortune 500", "filiale"}


def detect_company_size(offer_text: str) -> str:
    """
    Déduit la taille de l'entreprise depuis le texte de l'offre.

    Returns:
        "startup" | "grand_groupe" | "pme"
    """
    text = normalize_text(offer_text)
    if any(s in text for s in _STARTUP_SIGNALS):
        return "startup"
    if any(s in text for s in _GRAND_GROUPE_SIGNALS):
        return "grand_groupe"
    return "pme"


def detect_remote_policy(offer_text: str) -> str | None:
    """
    Déduit la politique de télétravail depuis le texte de l'offre.

    Returns:
        "full_remote" | "hybride" | "presentiel" | None
    """
    text = normalize_text(offer_text)
    if any(p in text for p in ("full remote", "100% remote", "100  remote", "100  teletravail", "100 teletravail")):
        return "full_remote"
    if any(s in text for s in {"hybrid", "hybride", "teletravail partiel", "2 jours", "3 jours"}):
        return "hybride"
    if any(s in text for s in {"presentiel", "sur site", "en local", "bureau"}):
        return "presentiel"
    return None
