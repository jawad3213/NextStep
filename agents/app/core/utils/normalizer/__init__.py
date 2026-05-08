# app/core/utils/normalizer/__init__.py
from app.core.utils.normalizer.text_utils import (
    normalize_skills,
    normalize_text,
    build_profile_full_text,
    _deduplicate,
)
from app.core.utils.normalizer.synonyms import SYNONYMES

__all__ = [
    "normalize_skills",
    "normalize_text",
    "build_profile_full_text",
    "_deduplicate",
    "SYNONYMES",
]
