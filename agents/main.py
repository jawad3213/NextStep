from fastapi import FastAPI
from example_module.router import router as example_router

app = FastAPI(
    title="NextStep Agents IA",
    description="Backend Python pour les agents IA",
    version="1.0.0",
)

# Enregistrement des routes du module exemple
app.include_router(example_router, prefix="/example", tags=["Example Module"])

@app.get("/health", tags=["Health"])
async def health_check():
    return {"status": "ok", "service": "nextstep-agents"}
