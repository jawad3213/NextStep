"""Email Composer: response parsing, reply classification rules and the generation call."""
from unittest.mock import patch

import pytest
from langchain_core.runnables import RunnableLambda

from app.domain.email_composer import service
from app.domain.email_composer.schemas.models import (
    CandidateInput,
    ClassifyResponseRequest,
    GenerateEmailRequest,
    JobOfferInput,
)


class FakeEmailLLM:
    """Stands in for the LLM: records the prompt and returns a fixed answer."""

    def __init__(self, answer):
        self.answer = answer
        self.prompts: list[str] = []

    def with_structured_output(self, schema, method=None):
        def run(prompt_value):
            self.prompts.append(prompt_value.to_string())
            return self.answer
        return RunnableLambda(run)


class TestResponseParsing:
    def test_llm_keys_in_french_are_mapped(self):
        result = service._coerce_email_response({"Objet": "Candidature", "corps": "Bonjour"}, "fr", "professionnel")
        assert (result.subject, result.body, result.language) == ("Candidature", "Bonjour", "fr")

    def test_unrecognisable_answer_is_an_error(self):
        with pytest.raises(ValueError):
            service._coerce_email_response({"foo": "bar"})

    def test_first_json_object_in_free_text(self):
        text = 'Voici : {"response_type": "REFUSE", "note": "a } in a string"} fin'
        assert service._extract_first_json_object(text) == {"response_type": "REFUSE", "note": "a } in a string"}

    def test_quoted_previous_email_is_removed(self):
        reply = "Merci, disponible mardi.\n\nLe lun. 3 mars, Sara a écrit :\n> ma candidature"
        assert service._strip_quoted_sections(reply) == "Merci, disponible mardi."


class TestReplyClassificationRules:
    @pytest.mark.parametrize("reply, expected", [
        ("Malheureusement, nous ne donnons pas suite à votre candidature.", "REFUSE"),
        ("Nous souhaitons organiser un entretien : quelles sont vos disponibilités ?", "ENTRETIEN_PROPOSE"),
        ("Pouvez-vous nous transmettre votre portfolio ?", "INFORMATIONS_DEMANDEES"),
        ("Je suis absent du bureau jusqu'au 10 mars.", "REPONSE_AUTOMATIQUE"),
        ("Bien reçu, merci.", None),
    ])
    def test_semantic_type(self, reply, expected):
        assert service._semantic_type_from_reply(reply) == expected

    def test_classify_type_aliases(self):
        assert service._normalize_classify_type("interview") == "ENTRETIEN_PROPOSE"
        assert service._normalize_classify_type("rejected") == "REFUSE"


class TestGeneration:
    @pytest.mark.asyncio
    async def test_generate_email_uses_skill_gap_and_company_context(self):
        llm = FakeEmailLLM({"subject": "Candidature Développeuse", "body": "Madame, Monsieur..."})
        request = GenerateEmailRequest(
            candidature_id="c1",
            candidate=CandidateInput(full_name="Sara Alami", skills=["Angular", ".NET"]),
            job_offer=JobOfferInput(job_title="Développeuse Full Stack", company_name="Acme"),
            skill_gap={"matched_skills": ["Angular"], "recommendations": ["Mettre en avant .NET"]},
            company_intelligence={"intelligence": {"summary": "Éditeur SaaS marocain", "culture": {"key_values": ["Innovation"]}}},
        )

        with patch.object(service, "get_email_llm", return_value=llm):
            result = await service.generate_email_with_llm(request)

        assert result.subject == "Candidature Développeuse"
        prompt = llm.prompts[0]
        assert "Sara Alami" in prompt and "Acme" in prompt
        assert "Éditeur SaaS marocain" in prompt and "Innovation" in prompt
        assert "Mettre en avant .NET" in prompt

    @pytest.mark.asyncio
    async def test_obvious_interview_invitation_overrides_a_wrong_llm_label(self):
        llm = FakeEmailLLM({
            "response_type": "REPONSE_GENERALE", "confidence": 0.5,
            "summary": "Réponse", "recommended_action": "Aucune",
        })
        request = ClassifyResponseRequest(
            candidature_id="c1",
            reply_snippet="Nous aimerions planifier un entretien, quelles sont vos disponibilités ?",
        )

        with patch.object(service, "get_email_llm", return_value=llm):
            result = await service.classify_recruiter_response_with_llm(request)

        assert result.response_type == "ENTRETIEN_PROPOSE"
