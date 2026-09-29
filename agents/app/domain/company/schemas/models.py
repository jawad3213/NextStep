# ============================================================
# app/domain/company/schemas/models.py
# Modèles Pydantic du domaine COMPANY
# ============================================================
from pydantic import BaseModel


class CompanyAnalyzeRequest(BaseModel):
    """Requête pour l'analyse d'une entreprise."""
    company_name: str
    offer_data: dict
    profile_data: dict
    user_id: str | int

