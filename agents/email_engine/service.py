from .schemas import EmailGenerateRequest, EmailGenerateResponse


class EmailService:
    async def generate_email(self, request: EmailGenerateRequest) -> EmailGenerateResponse:
        subject = f"Candidature au poste de {request.job_offer.job_title}"
        body = (
            f"Bonjour,\n\n"
            f"Je vous adresse ma candidature pour le poste de {request.job_offer.job_title} "
            f"chez {request.job_offer.company_name}.\n\n"
            f"Je suis {request.candidate.title} et je possède des compétences en "
            f"{', '.join(request.candidate.skills) if request.candidate.skills else 'développement logiciel'}.\n\n"
            f"Je reste à votre disposition pour tout échange complémentaire.\n\n"
            f"Cordialement,\n"
            f"{request.candidate.full_name}"
        )

        return EmailGenerateResponse(
            subject=subject,
            body=body,
            detected_language=request.language
        )