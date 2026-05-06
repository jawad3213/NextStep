# ============================================================
# app/domain/offer/agents/normalizer/synonyms.py
# Dictionnaire de synonymes techniques
#
# Clé   = forme alternative rencontrée dans les CV/offres
# Valeur = forme canonique utilisée pour la comparaison
#
# Avantage : isolé du code → facile à enrichir sans toucher
# à la logique de normalisation.
# ============================================================

SYNONYMES: dict[str, str] = {
    # ── JavaScript / TypeScript ───────────────────────────────
    "js":            "javascript",
    "reactjs":       "react",
    "react.js":      "react",
    "vuejs":         "vue",
    "vue.js":        "vue",
    "angularjs":     "angular",
    "nodejs":        "node.js",
    "node":          "node.js",
    "ts":            "typescript",
    "nextjs":        "next.js",
    "next.js":       "next.js",
    "nuxtjs":        "nuxt.js",
    "expressjs":     "express",

    # ── Python ────────────────────────────────────────────────
    "py":                        "python",
    "drf":                       "django",
    "django rest framework":     "django",
    "flask-restful":             "flask",

    # ── .NET / C# ─────────────────────────────────────────────
    "c sharp":       "c#",
    "csharp":        "c#",
    "dotnet":        ".net",
    "asp.net core":  "asp.net",
    "aspnet":        "asp.net",
    "entity framework": "ef core",
    "ef":            "ef core",

    # ── Java ──────────────────────────────────────────────────
    "spring boot":   "spring",
    "jee":           "java ee",
    "j2ee":          "java ee",

    # ── Bases de données ──────────────────────────────────────
    "postgres":      "postgresql",
    "mongo":         "mongodb",
    "elastic":       "elasticsearch",
    "mssql":         "sql server",
    "mysql workbench": "mysql",

    # ── Cloud / DevOps ────────────────────────────────────────
    "amazon web services": "aws",
    "google cloud platform": "gcp",
    "microsoft azure": "azure",
    "k8s":           "kubernetes",
    "cicd":          "ci/cd",
    "ci cd":         "ci/cd",
    "github actions": "ci/cd",
    "gitlab ci":     "ci/cd",

    # ── ML / IA ───────────────────────────────────────────────
    "ml":            "machine learning",
    "dl":            "deep learning",
    "nlp":           "natural language processing",
    "llm":           "large language model",
    "gen ai":        "generative ai",

    # ── Méthodologies ─────────────────────────────────────────
    "agile scrum":   "scrum",
    "methode agile": "agile",
    "tdd":           "test driven development",
    "bdd":           "behavior driven development",
}
