QUESTIONS_PROMPT_OFFER = """Generate 12 interview questions for {company} / role: {role}.
Required skills: {skills}. Missing skills: {missing}.
Real Glassdoor questions:\n{glassdoor_questions}

Mix 40% behavioral, 40% technical, 20% situational.
Glassdoor questions → company_specific=true.

Return ONLY JSON:
{{"questions":[{{"question":"...","type":"behavioral|technical|situational","source":"glassdoor|generated","company_specific":true,"tip":"..."}}]}}"""

QUESTIONS_PROMPT_ARENA = """Generate 15 interview questions for a {level} {domain} professional.
Language: {language}. Focus: {focus}.
Reference questions:\n{ref_questions}

Return ONLY JSON:
{{"questions":[{{"question":"...","type":"behavioral|technical|situational","source":"generated","company_specific":false,"tip":"..."}}]}}"""

RECRUITER_PROMPT = """You are a recruiter at {company} interviewing for {role}.
Culture: {culture}. Key skills: {skills}. Difficulty: {difficulty}.
Language: {language} — ALWAYS respond in this language.
Ask ONE question at a time. Stay in character. Be professional but human."""

EVALUATOR_PROMPT = """Evaluate this mock interview.
Context: {context}
Transcript:\n{transcript}

Return ONLY JSON:
{{"global_score":0,"dimensions":[{{"name":"Clarity","score":0,"comment":"..."}},{{"name":"STAR Method","score":0,"comment":"..."}},{{"name":"Technical Accuracy","score":0,"comment":"..."}},{{"name":"Communication","score":0,"comment":"..."}},{{"name":"Confidence","score":0,"comment":"..."}}],"strengths":["..."],"improvements":["..."],"best_answer":"...","worst_answer":"... Better: [...]","coaching_tips":["..."]}}"""

SALARY_PROMPT = """Salary negotiation expert — MENA region.
Job: {job_title}. Location: {location}.
{extra_context}
Market data:\n{market_raw}

Return ONLY JSON:
{{"range_min":0,"range_max":0,"currency":"MAD","your_target":0,"confidence_level":"medium","market_sources":["..."],"negotiation_script":[{{"step":1,"action":"...","phrase":"...","why":"..."}}]}}"""

FREE_CHAT_PROMPT = """You are an expert interview coach for NextStep.
{context}
Answer questions about interview prep, company culture, STAR method, technical topics. Be practical and encouraging."""
