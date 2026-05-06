# ============================================================
# app/core/utils/normalizer/text_utils.py
# Fonctions de normalisation partagées entre tous les domaines
#
# normalize_skills() — normalise une liste de compétences
# normalize_text()   — normalise un texte libre
# _deduplicate()     — déduplique en préservant l'ordre
# ============================================================
import re
from unidecode import unidecode
from app.core.utils.normalizer.synonyms import SYNONYMES


def _normalize_token(token: str) -> str:
    """
    Normalise un token individuel :
      1. unidecode (supprime accents : é→e, ç→c, ü→u…)
      2. lower()
      3. supprime caractères non significatifs (sauf . / # - +)
      4. remplace via le dictionnaire de synonymes
    """
    token = unidecode(token.strip().lower())
    token = re.sub(r"[^\w\s./#\-+]", "", token)
    return SYNONYMES.get(token, token)


def _deduplicate(items: list[str]) -> list[str]:
    """Déduplique une liste en préservant l'ordre d'insertion."""
    seen: set[str] = set()
    result = []
    for item in items:
        if item not in seen:
            seen.add(item)
            result.append(item)
    return result


def normalize_skills(skills: list[str]) -> list[str]:
    """
    Normalise et déduplique une liste de compétences.
    Gère le découpage automatique (ex: "JS / TS" -> ["javascript", "typescript"])
    """
    seen: set[str] = set()
    result = []

    # Séparateurs courants dans les CV/Offres
    separators = r"[,/&|]"

    for s in skills:
        tokens = re.split(separators, s)
        for token in tokens:
            n = _normalize_token(token)
            if n and n not in seen:
                seen.add(n)
                result.append(n)
    return result


def normalize_text(text: str) -> str:
    """
    Normalise un texte libre (résumé, description, titre…).
    Utilisé pour le calcul du score ATS positionnel.
    """
    text = unidecode(text.lower())
    return re.sub(r"[^\w\s./#\-+]", " ", text).strip()


def build_profile_full_text(profile: dict) -> str:
    """
    Construit le texte complet normalisé du profil candidat.
    Utilisé par le scorer pour le calcul du score ATS positionnel.
    """
    profile_titre  = normalize_text(profile.get("titre") or "")
    profile_resume = normalize_text(profile.get("resume") or "")

    profile_skills = normalize_skills([
        c["nom"] for c in profile.get("competences", [])
        if isinstance(c, dict) and c.get("nom")
    ])

    exp_text = normalize_text(" ".join(
        f"{e.get('titre', '')} {e.get('description', '')}"
        for e in profile.get("experiences", [])
        if isinstance(e, dict)
    ))
    proj_text = normalize_text(" ".join(
        f"{p.get('titre', '')} {p.get('description', '')} "
        f"{' '.join(p.get('technologies') or [])}"
        for p in profile.get("projets", [])
        if isinstance(p, dict)
    ))
    cert_text = normalize_text(" ".join(
        c.get("nom", "") for c in profile.get("certifications", [])
        if isinstance(c, dict)
    ))
    form_text = normalize_text(" ".join(
        f"{f.get('diplome', '')} {f.get('etablissement', '')}"
        for f in profile.get("formations", [])
        if isinstance(f, dict)
    ))

    return " ".join(filter(None, [
        profile_titre, profile_resume,
        " ".join(profile_skills),
        exp_text, proj_text, cert_text, form_text,
    ]))
