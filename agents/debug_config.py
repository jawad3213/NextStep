import os
from app.core.config import settings

print(f"Provider: {settings.LLM_PROVIDER}")
print(f"Groq Key exists: {bool(settings.GROQ_API_KEY)}")
if settings.GROQ_API_KEY:
    print(f"Groq Key starts with: {settings.GROQ_API_KEY[:10]}...")
else:
    print("GROQ_API_KEY is EMPTY in settings!")

print(f"Current Working Dir: {os.getcwd()}")
print(f"Files in current dir: {os.listdir('.')}")
