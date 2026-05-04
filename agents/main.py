from fastapi import FastAPI
from resume.router import router as resume_router

app = FastAPI(
    title="NextStep Agents IA",
    description="Backend Python pour les agents IA",
    version="1.0.0",
)

# Enregistrement des routes
app.include_router(resume_router, prefix="/resume", tags=["Resume Parsing"])

@app.get("/health", tags=["Health"])
async def health_check():
    return {"status": "ok", "service": "nextstep-agents"}
