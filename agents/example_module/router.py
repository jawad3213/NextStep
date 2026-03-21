"""
╔══════════════════════════════════════════════════════════════╗
║  router.py — Endpoints FastAPI (Les contrôleurs HTTP)       ║
║                                                              ║
║  📌 Règle : Ce fichier NE DOIT PAS contenir de logique.      ║
║  Il fait juste : Recevoir requête -> Appeler service -> Retour║
╚══════════════════════════════════════════════════════════════╝
"""
from fastapi import APIRouter
from example_module.schemas import ExampleRequest, ExampleResponse
from example_module.service import ExampleService

router = APIRouter()

@router.post("/process", response_model=ExampleResponse)
async def process_data(payload: ExampleRequest):
    """
    Exemple de route POST.
    Reçoit un payload JSON (validé automatiquement par pydantic).
    """
    service = ExampleService()
    # Délègue la logique au service
    return await service.do_something(payload)
