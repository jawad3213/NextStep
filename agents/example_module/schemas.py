"""
schemas.py — Modèles Pydantic

📌 Règle : Définit les types d'entrée (Request) et de sortie (Response).
Sert de validation automatique pour FastAPI.
"""
from pydantic import BaseModel, Field

class ExampleRequest(BaseModel):
    """Ce que le frontend (.NET) envoie."""
    user_id: str = Field(..., description="ID de l'utilisateur")
    message: str = Field(..., description="Le message à traiter")

class ExampleResponse(BaseModel):
    """Ce que l'agent (Python) retourne."""
    success: bool
    processed_message: str
    word_count: int
