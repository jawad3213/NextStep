import os
import fitz  # PyMuPDF
import json
from langchain_groq import ChatGroq
from langchain_core.prompts import ChatPromptTemplate
from fastapi import HTTPException
from .prompts import SYSTEM_PROMPT

async def extract_text_from_pdf(pdf_content: bytes) -> str:
    """
    Extracts text from a PDF byte stream.
    """
    try:
        doc = fitz.open(stream=pdf_content, filetype="pdf")
        text = ""
        for page in doc:
            text += page.get_text()
        doc.close()
        return text
    except Exception as e:
        raise Exception(f"Error extracting text from PDF: {str(e)}")

async def parse_cv_with_ai(cv_text: str) -> dict:
    """
    Sends the CV text to the LLM and returns a structured JSON.
    """
    api_key = os.getenv("GROQ_API_KEY")
    if not api_key:
        raise HTTPException(status_code=500, detail="GROQ_API_KEY non configurée sur le serveur.")

    llm = ChatGroq(
        temperature=0,
        model_name="llama-3.3-70b-versatile",
        groq_api_key=api_key
    )

    prompt = ChatPromptTemplate.from_messages([
        ("system", SYSTEM_PROMPT),
        ("human", "Voici le texte du CV à parser :\n\n{cv_text}")
    ])

    chain = prompt | llm
    response = await chain.ainvoke({"cv_text": cv_text})
    
    return clean_and_parse_json(response.content)

def clean_and_parse_json(raw_content: str) -> dict:
    """
    Cleans the AI response and parses it as JSON.
    """
    raw_content = raw_content.strip()
    
    # Enlever les éventuels backticks markdown JSON
    if "```json" in raw_content:
        raw_content = raw_content.split("```json")[1].split("```")[0].strip()
    elif "```" in raw_content:
        raw_content = raw_content.split("```")[1].split("```")[0].strip()

    try:
        return json.loads(raw_content)
    except json.JSONDecodeError:
        # Tentative de récupération si l'IA a mis du texte avant/après
        start_idx = raw_content.find("{")
        end_idx = raw_content.rfind("}")
        if start_idx != -1 and end_idx != -1:
            try:
                return json.loads(raw_content[start_idx:end_idx+1])
            except json.JSONDecodeError:
                raise ValueError("Format JSON invalide même après extraction.")
        else:
            raise ValueError("Format JSON invalide reçu de l'IA.")
