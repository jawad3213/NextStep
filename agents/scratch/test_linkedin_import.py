import asyncio
import sys
import os

# Ajouter le chemin racine des agents pour les imports
sys.path.append(os.path.join(os.getcwd(), 'agents'))

from app.domain.resume.service import parse_linkedin_with_ai

async def test_linkedin():
    test_url = "https://www.linkedin.com/in/williamhgates"
    print(f"Testing LinkedIn import for: {test_url}")
    try:
        result = await parse_linkedin_with_ai(url=test_url)
        print("SUCCESS!")
        print(result)
    except Exception as e:
        print(f"FAILED with error: {type(e).__name__}: {str(e)}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    asyncio.run(test_linkedin())
