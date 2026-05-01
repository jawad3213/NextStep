# ============================================================
# email_engine/schemas.py — DEPRECATED
#
# This file is kept only for backward compatibility.
# The canonical models are now in email_engine/models.py.
#
# DO NOT add new schemas here.
# ============================================================
from .models import (  # noqa: F401
    CandidateInput,
    JobOfferInput,
    EmailOptions,
    GenerateEmailRequest,
    GenerateEmailResponse,
)