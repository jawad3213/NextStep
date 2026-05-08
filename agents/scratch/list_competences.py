import asyncio
import sys
import io
import os

sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from app.domain.profile_retriever.tools.db_tools import get_user_profile_from_db

async def main():
    user_id = "a9709404-ae96-4070-91a3-21142ded4139"
    profile = await get_user_profile_from_db.ainvoke({"user_id": user_id})
    print("=== COMPETENCES EN DB ===")
    for c in profile.get("competences", []):
        print(f"- {c.get('nom')} (Type: {c.get('type_competence')})")

if __name__ == "__main__":
    asyncio.run(main())
