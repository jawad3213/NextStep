# ============================================================
# app/domain/job/agents/cv_formatter/builder.py
# Fonctions de construction des sections CV
# Isolées pour faciliter les tests et les modifications
# ============================================================


def build_entete(profile: dict) -> dict:
    """Construit la section en-tête du CV."""
    return {
        "nom":       profile.get("nom", ""),
        "prenom":    profile.get("prenom", ""),
        "titre":     profile.get("titre", ""),
        "email":     profile.get("email", ""),
        "telephone": profile.get("telephone", ""),
        "ville":     profile.get("ville", ""),
        "linkedin":  profile.get("lien_linkedin", ""),
        "github":    profile.get("lien_github", ""),
        "portfolio": profile.get("lien_portfolio", ""),
    }


def build_competences(profile: dict, match_result: dict) -> list[dict]:
    """
    Trie les compétences : celles qui matchent l'offre en premier.
    Ajoute un flag `matched` pour que le template puisse les mettre en valeur.
    """
    matching = set(match_result.get("competences_matching", []))
    comps = [c for c in (profile.get("competences", []) or []) if isinstance(c, dict)]

    sorted_comps = sorted(
        comps,
        key=lambda c: (0 if c.get("nom", "").lower() in matching else 1),
    )
    return [
        {
            "nom":     c.get("nom", ""),
            "niveau":  c.get("niveau", 1),
            "matched": c.get("nom", "").lower() in matching,
        }
        for c in sorted_comps
        if isinstance(c, dict) and c.get("nom")
    ]


def build_experiences(profile: dict) -> list[dict]:
    """Construit la liste des expériences professionnelles."""
    return [
        {
            "titre":       e.get("titre", ""),
            "entreprise":  e.get("entreprise", ""),
            "date_debut":  e.get("date_debut", ""),
            "date_fin":    e.get("date_fin", ""),
            "description": e.get("description", ""),
        }
        for e in (profile.get("experiences", []) or [])
        if isinstance(e, dict)
    ]


def build_formations(profile: dict) -> list[dict]:
    """Construit la liste des formations."""
    return [
        {
            "diplome":       f.get("diplome", ""),
            "etablissement": f.get("etablissement", ""),
            "annee":         f.get("annee"),
        }
        for f in (profile.get("formations", []) or [])
        if isinstance(f, dict)
    ]


def build_projets(profile: dict) -> list[dict]:
    """Construit la liste des projets."""
    return [
        {
            "titre":        p.get("titre", ""),
            "description":  p.get("description", ""),
            "technologies": p.get("technologies", []) or [],
        }
        for p in (profile.get("projets", []) or [])
        if isinstance(p, dict)
    ]
