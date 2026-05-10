# app/modules/chatbot/graph.py
"""
GRAPHE LANGGRAPH — Interview Prep Agent

STRUCTURE :
                    START
                      │
                 router_node          ← lit request_type et décide
                /     |      \   \
   questions_node  interview  salary  free_chat
        │            _node    _node    _node
        │              │
        │         evaluator_node  ← seulement si session_complete=True
        │              │
                      END

CHAQUE NOEUD :
  - Reçoit le State complet
  - Retourne un dict avec SEULEMENT les champs modifiés
  - Ne modifie jamais directement le State
"""

import uuid
import json
import logging
from typing import Literal
from langchain_core.messages import SystemMessage, HumanMessage, AIMessage
from langgraph.graph import StateGraph, START, END

from app.core.config import get_llm, get_llm_precise
from app.domain.chatbot.state import (
    InterviewPrepState, MessageTurn, QuestionItem,
    FeedbackResult, DimensionScore, SalaryResult,
)
from app.domain.chatbot.tools import (
    search_interview_questions,
    search_arena_questions,
    search_salary_data,
)
from app.domain.chatbot.prompts import (
    QUESTIONS_PROMPT_OFFER,
    QUESTIONS_PROMPT_ARENA,
    RECRUITER_PROMPT,
    EVALUATOR_PROMPT,
    SALARY_PROMPT,
    FREE_CHAT_PROMPT,
)

logger = logging.getLogger(__name__)


# ═══════════════════════════════════════════════════════════════
# NOEUD 0 — ROUTER
# Décide vers quel noeud aller selon request_type
# ═══════════════════════════════════════════════════════════════

def router_node(state: InterviewPrepState) -> dict:
    """
    Noeud de routage — ne fait rien, juste lit le state.
    La décision est dans router_edge (conditional edge).
    """
    logger.info(f"[ROUTER] request_type={state.request_type} mode={state.mode}")
    return {}   # ne modifie rien


def router_edge(state: InterviewPrepState) -> str:
    """
    Conditional edge — retourne le nom du prochain noeud.
    """
    mapping = {
        "generate_questions":  "questions_node",
        "ask_free":            "free_chat_node",
        "start_interview":     "interview_node",
        "continue_interview":  "interview_node",
        "end_interview":       "evaluator_node",
        "get_salary":          "salary_node",
    }
    destination = mapping.get(state.request_type, "questions_node")
    logger.info(f"[ROUTER] → {destination}")
    return destination


# ═══════════════════════════════════════════════════════════════
# NOEUD 1 — QUESTIONS
# Tab 1 : génère la liste de questions
# ═══════════════════════════════════════════════════════════════

async def questions_node(state: InterviewPrepState) -> dict:
    """
    Génère les questions selon le mode.
    Mode offer  → cherche questions Glassdoor réelles + génère selon offre
    Mode arena  → génère selon domain/level/focus
    """
    logger.info(f"[QUESTIONS] mode={state.mode}")
    llm = get_llm_precise()

    if state.mode == "offer" and state.offer_context:
        offer   = state.offer_context.offer
        company = state.offer_context.company
        match   = state.offer_context.match

        # 1. Chercher les vraies questions Glassdoor
        real_questions = await search_interview_questions(
            offer.company_name, offer.job_title
        )

        # 2. Construire le prompt avec tout le contexte
        prompt = QUESTIONS_PROMPT_OFFER.format(
            company=offer.company_name,
            role=offer.job_title,
            skills=", ".join(offer.required_skills),
            missing=", ".join(match.missing_skills),
            glassdoor_questions="\n".join(real_questions) if real_questions else "Aucune trouvée",
        )

    else:
        # Mode arena
        cfg = state.arena_config
        domain = cfg.domain if cfg else "Software"
        level  = cfg.level  if cfg else "junior"
        focus  = cfg.focus_areas if cfg else []

        # 1. Chercher des questions de référence
        ref_questions = await search_arena_questions(domain, level, focus)

        # 2. Construire le prompt arena
        prompt = QUESTIONS_PROMPT_ARENA.format(
            domain=domain,
            level=level,
            language=cfg.language if cfg else "en",
            focus=", ".join(focus) if focus else "auto-balanced",
            ref_questions="\n".join(ref_questions) if ref_questions else "",
        )

    # 3. Appeler le LLM → reçoit JSON
    messages = [
        SystemMessage(content="You are an expert interview coach. Return ONLY valid JSON, no markdown."),
        HumanMessage(content=prompt),
    ]

    try:
        response = await llm.ainvoke(messages)
        raw = response.content.strip()

        # Nettoyer les backticks si présents
        if raw.startswith("```"):
            raw = raw.split("```")[1]
            if raw.startswith("json"):
                raw = raw[4:]

        data = json.loads(raw)
        questions = [
            QuestionItem(
                id=str(uuid.uuid4()),
                question=q.get("question", ""),
                type=q.get("type", "behavioral"),
                source=q.get("source", "generated"),
                company_specific=q.get("company_specific", False),
                tip=q.get("tip", ""),
            )
            for q in data.get("questions", [])
        ]
        logger.info(f"[QUESTIONS] Generated {len(questions)} questions")
        return {"questions": questions}

    except Exception as e:
        logger.error(f"[QUESTIONS] Error: {e}")
        return {"error": str(e), "questions": []}


# ═══════════════════════════════════════════════════════════════
# NOEUD 2 — FREE CHAT
# Tab 1 : chat libre (l'user pose des questions à l'agent)
# ═══════════════════════════════════════════════════════════════

async def free_chat_node(state: InterviewPrepState) -> dict:
    """
    Répond aux questions libres de l'utilisateur dans le tab Questions.
    Ex: 'Comment répondre à la Q2 ?' / 'Quelles questions sur Kafka ?'
    """
    logger.info("[FREE_CHAT] Answering free question")
    llm = get_llm(temperature=0.7)

    # Construire le contexte
    ctx = ""
    if state.offer_context:
        o = state.offer_context.offer
        c = state.offer_context.company
        ctx = (
            f"Company: {o.company_name}\n"
            f"Role: {o.job_title}\n"
            f"Summary: {c.company_summary}\n"
            f"Required skills: {', '.join(o.required_skills)}\n"
        )

    system = FREE_CHAT_PROMPT.format(context=ctx)

    # Historique des 6 derniers messages
    lc_messages = [SystemMessage(content=system)]
    for turn in state.messages[-6:]:
        if turn.role == "user":
            lc_messages.append(HumanMessage(content=turn.content))
        else:
            lc_messages.append(AIMessage(content=turn.content))

    lc_messages.append(HumanMessage(content=state.user_input))

    try:
        response = await llm.ainvoke(lc_messages)
        new_turn = MessageTurn(role="ai", content=response.content)

        # Ajouter le message user + la réponse AI à l'historique
        user_turn = MessageTurn(role="user", content=state.user_input)
        updated_messages = state.messages + [user_turn, new_turn]

        return {"messages": updated_messages}

    except Exception as e:
        logger.error(f"[FREE_CHAT] Error: {e}")
        return {"error": str(e)}


# ═══════════════════════════════════════════════════════════════
# NOEUD 3 — INTERVIEW (Recruteur IA)
# Tab 2 : mock interview — l'IA joue le recruteur
# ═══════════════════════════════════════════════════════════════

async def interview_node(state: InterviewPrepState) -> dict:
    """
    Gère la session mock interview.
    - start_interview → message d'ouverture du recruteur
    - continue_interview → réponse au message de l'user
    """
    logger.info(f"[INTERVIEW] request_type={state.request_type}")
    llm = get_llm(temperature=0.8)

    # Construire le prompt recruteur selon le mode
    if state.mode == "offer" and state.offer_context:
        o = state.offer_context.offer
        c = state.offer_context.company
        lang = state.offer_context.offer.job_title  # fallback
        if state.arena_config:
            lang = state.arena_config.language

        system = RECRUITER_PROMPT.format(
            company=o.company_name,
            role=o.job_title,
            culture=c.company_summary[:200] if c.company_summary else "professional",
            skills=", ".join(o.required_skills[:6]),
            difficulty=c.interview_difficulty,
            language="English",
        )
    else:
        cfg = state.arena_config
        system = RECRUITER_PROMPT.format(
            company="a leading company",
            role=f"{cfg.level if cfg else 'mid'} {cfg.domain if cfg else 'Software'} engineer",
            culture="innovative and collaborative",
            skills=", ".join(cfg.focus_areas[:6]) if cfg and cfg.focus_areas else "core skills",
            difficulty="medium",
            language=cfg.language if cfg else "English",
        )

    # Construire les messages LangChain
    lc_messages = [SystemMessage(content=system)]
    for turn in state.messages:
        if turn.role == "user":
            lc_messages.append(HumanMessage(content=turn.content))
        else:
            lc_messages.append(AIMessage(content=turn.content))

    # Si l'user a envoyé un message, l'ajouter
    if state.user_input and state.request_type == "continue_interview":
        lc_messages.append(HumanMessage(content=state.user_input))

    try:
        response = await llm.ainvoke(lc_messages)
        ai_turn = MessageTurn(role="ai", content=response.content)

        updated = list(state.messages)
        if state.user_input and state.request_type == "continue_interview":
            updated.append(MessageTurn(role="user", content=state.user_input))
        updated.append(ai_turn)

        return {"messages": updated}

    except Exception as e:
        logger.error(f"[INTERVIEW] Error: {e}")
        return {"error": str(e)}


# ═══════════════════════════════════════════════════════════════
# NOEUD 4 — EVALUATOR
# Tab 2 : appelé quand la session se termine
# ═══════════════════════════════════════════════════════════════

async def evaluator_node(state: InterviewPrepState) -> dict:
    """
    Analyse tout le transcript et génère le feedback + score.
    Appelé seulement quand request_type = 'end_interview'.
    """
    logger.info("[EVALUATOR] Evaluating session")
    llm = get_llm_precise()

    # Construire le transcript
    transcript = "\n\n".join([
        f"{'RECRUITER' if t.role == 'ai' else 'CANDIDATE'}: {t.content}"
        for t in state.messages
    ])

    ctx = ""
    if state.mode == "offer" and state.offer_context:
        o = state.offer_context.offer
        ctx = f"Role: {o.job_title} at {o.company_name}"
    elif state.arena_config:
        cfg = state.arena_config
        ctx = f"Domain: {cfg.domain}, Level: {cfg.level}"

    prompt = EVALUATOR_PROMPT.format(context=ctx, transcript=transcript)

    lc_messages = [
        SystemMessage(content="You are an expert interview evaluator. Return ONLY valid JSON."),
        HumanMessage(content=prompt),
    ]

    try:
        response = await llm.ainvoke(lc_messages)
        raw = response.content.strip()

        if raw.startswith("```"):
            raw = raw.split("```")[1]
            if raw.startswith("json"):
                raw = raw[4:]

        data = json.loads(raw)
        feedback = FeedbackResult(
            global_score=data.get("global_score", 0),
            dimensions=[DimensionScore(**d) for d in data.get("dimensions", [])],
            strengths=data.get("strengths", []),
            improvements=data.get("improvements", []),
            best_answer=data.get("best_answer", ""),
            worst_answer=data.get("worst_answer", ""),
            coaching_tips=data.get("coaching_tips", []),
        )
        logger.info(f"[EVALUATOR] Score: {feedback.global_score}")
        return {"feedback": feedback, "session_complete": True}

    except Exception as e:
        logger.error(f"[EVALUATOR] Error: {e}")
        return {"error": str(e), "session_complete": True}


# ═══════════════════════════════════════════════════════════════
# NOEUD 5 — SALARY
# Tab 3 : coach salaire + argumentaire négociation
# ═══════════════════════════════════════════════════════════════

async def salary_node(state: InterviewPrepState) -> dict:
    """
    Génère l'analyse salariale et le script de négociation.
    Fonctionne en mode offer (données entreprise) et arena (marché général).
    """
    logger.info(f"[SALARY] mode={state.mode}")
    llm = get_llm_precise()

    # Déterminer job_title + location
    if state.mode == "offer" and state.offer_context:
        o = state.offer_context.offer
        c = state.offer_context.company
        job_title = o.job_title
        location  = o.raw_text[:50] if o.raw_text else "Casablanca"
        extra = (
            f"Company: {o.company_name}\n"
            f"Known range from Glassdoor: {c.salary_min}–{c.salary_max} {c.currency}\n"
            f"Candidate strengths: {', '.join(state.offer_context.match.strengths)}\n"
        )
    else:
        cfg = state.arena_config
        job_title = cfg.domain if cfg else "Software Engineer"
        location  = "Casablanca, Morocco"
        extra = ""

    # Recherche données marché
    market_data = await search_salary_data(job_title, location)

    prompt = SALARY_PROMPT.format(
        job_title=job_title,
        location=location,
        extra_context=extra,
        market_raw="\n".join(market_data.get("raw_data", [])),
    )

    lc_messages = [
        SystemMessage(content="You are a salary negotiation expert. Return ONLY valid JSON."),
        HumanMessage(content=prompt),
    ]

    try:
        response = await llm.ainvoke(lc_messages)
        raw = response.content.strip()

        if raw.startswith("```"):
            raw = raw.split("```")[1]
            if raw.startswith("json"):
                raw = raw[4:]

        data = json.loads(raw)
        salary = SalaryResult(
            range_min=data.get("range_min", 0),
            range_max=data.get("range_max", 0),
            currency=data.get("currency", "MAD"),
            your_target=data.get("your_target", 0),
            confidence_level=data.get("confidence_level", "medium"),
            market_sources=data.get("market_sources", []),
            negotiation_script=data.get("negotiation_script", []),
        )
        logger.info(f"[SALARY] Range: {salary.range_min}–{salary.range_max}")
        return {"salary": salary}

    except Exception as e:
        logger.error(f"[SALARY] Error: {e}")
        return {"error": str(e)}


# ═══════════════════════════════════════════════════════════════
# COMPILATION DU GRAPHE
# ═══════════════════════════════════════════════════════════════

def build_graph():
    """
    Construit et compile le graphe LangGraph.
    Appelé une seule fois au démarrage de l'app.

    STRUCTURE DES EDGES :
      START → router_node
      router_node → [conditional] → questions/free_chat/interview/evaluator/salary
      Tous les noeuds → END
    """
    builder = StateGraph(InterviewPrepState)

    # ── Ajouter les noeuds ─────────────────────────────────
    builder.add_node("router_node",    router_node)
    builder.add_node("questions_node", questions_node)
    builder.add_node("free_chat_node", free_chat_node)
    builder.add_node("interview_node", interview_node)
    builder.add_node("evaluator_node", evaluator_node)
    builder.add_node("salary_node",    salary_node)

    # ── Edge de départ ─────────────────────────────────────
    builder.add_edge(START, "router_node")

    # ── Conditional edge depuis router ─────────────────────
    builder.add_conditional_edges(
        "router_node",
        router_edge,
        {
            "questions_node":  "questions_node",
            "free_chat_node":  "free_chat_node",
            "interview_node":  "interview_node",
            "evaluator_node":  "evaluator_node",
            "salary_node":     "salary_node",
        }
    )

    # ── Tous les noeuds terminaux → END ────────────────────
    builder.add_edge("questions_node", END)
    builder.add_edge("free_chat_node", END)
    builder.add_edge("interview_node", END)
    builder.add_edge("evaluator_node", END)
    builder.add_edge("salary_node",    END)

    graph = builder.compile()
    logger.info("✅ LangGraph compiled successfully")
    return graph


# Instance singleton — importée par le router FastAPI
interview_graph = build_graph()
