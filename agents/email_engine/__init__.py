# ============================================================
# email_engine/__init__.py — Compatibility shim
#
# The email_engine package has been refactored into:
#   app/domain/email_composer/
#
# This shim re-exports the public API so any existing import
# that targets email_engine.* continues to work unchanged.
# ============================================================
from app.domain.email_composer.schemas.models import (  # noqa: F401
    CandidateInput,
    JobOfferInput,
    EmailOptions,
    GenerateEmailRequest,
    GenerateEmailResponse,
    GenerateFollowUpEmailRequest,
    PreviousEmailInput,
    FollowUpOptions,
    ClassifyResponseRequest,
    ClassifyResponseResult,
    RecruiterReplyInput,
    GenerateReplyEmailRequest,
    SkillGapInput,
    CompanyIntelligenceInput,
)
from app.domain.email_composer.service import (  # noqa: F401
    generate_email_with_llm,
    generate_follow_up_email_with_llm,
    classify_recruiter_response_with_llm,
    generate_reply_email_with_llm,
)
