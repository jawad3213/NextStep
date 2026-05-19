"""
FACADE - Rétrocompatibilité et point d'accès unifié.
Délègue toutes les opérations métier au sous-dossier modulaire `services/`.
"""

from .services.questions_service import generate_questions_service
from .services.chat_service import free_chat_service
from .services.interview_service import start_interview_service, send_message_service, end_interview_service
from .services.salary_service import get_salary_service
from .services.context_service import get_offer_context_from_db, get_internal_user_id
