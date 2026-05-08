import asyncio
import sys
import io
import os
import json

# Ajouter le dossier agents au chemin de recherche de modules
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from app.domain.profile_retriever.tools.db_tools import get_user_profile_from_db

if sys.platform.startswith("win"):
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except AttributeError:
        sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

async def main():
    user_id = "a9709404-ae96-4070-91a3-21142ded4139"
    profile = await get_user_profile_from_db.ainvoke({"user_id": user_id})
    print(json.dumps(profile, indent=2, ensure_ascii=False))

if __name__ == "__main__":
    asyncio.run(main())
