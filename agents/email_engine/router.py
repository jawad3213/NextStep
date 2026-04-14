from fastapi import APIRouter
from .schemas import EmailGenerateRequest, EmailGenerateResponse
from .service import EmailService

router = APIRouter()
service = EmailService()


@router.post("/generate-email", response_model=EmailGenerateResponse)
async def generate_email(payload: EmailGenerateRequest) -> EmailGenerateResponse:
    return await service.generate_email(payload)