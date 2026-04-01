"""
service.py — Logique Métier

📌 Règle : TOUTE la logique complexe se trouve ici.
- Appels à la DB (SQLAlchemy)
- Appels aux LLMs (LangChain)
- Traitements algorithmiques
"""
from example_module.schemas import ExampleRequest, ExampleResponse

class ExampleService:
 def do_something(self, payload: ExampleRequest) -> ExampleResponse:
        """
        Traite la requête.
        Dans un vrai agent, on ferait des appels RAG, LLM, etc.
        """
        # --- Logique métier ici ---
        processed = payload.message.upper() + " (TRAITÉ PAR PYTHON)"
        count = len(payload.message.split())
       
        # Retourne les résultats selon le format défini dans schemas.py
        return ExampleResponse(
            success=True,
            processed_message=processed,
            word_count=count
        )
