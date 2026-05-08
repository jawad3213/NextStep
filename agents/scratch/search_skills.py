import asyncio
import sys
import io
import os

sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from app.domain.profile_retriever.tools.db_tools import get_user_profile_from_db

async def main():
    user_id = "a9709404-ae96-4070-91a3-21142ded4139"
    profile = await get_user_profile_from_db.ainvoke({"user_id": user_id})
    print("=== RECHERCHE TYPESCRIPT ET DJANGO ===")
    
    # 1. Dans les compétences
    print("\nDans les compétences :")
    for c in profile.get("competences", []):
        name = c.get('nom', '')
        if "typescript" in name.lower() or "django" in name.lower() or "react" in name.lower():
            print(f"  - Competence: {name}")
            
    # 2. Dans les projets
    print("\nDans les projets :")
    for p in profile.get("projets", []):
        techs = p.get('technologies', '')
        desc = p.get('description', '')
        title = p.get('titre', '')
        if "typescript" in str(techs).lower() or "django" in str(techs).lower() or "react" in str(techs).lower() or "typescript" in desc.lower() or "django" in desc.lower() or "react" in desc.lower():
            print(f"  - Projet: {title} | Techs: {techs}")
            
    # 3. Dans les expériences
    print("\nDans les expériences :")
    for e in profile.get("experiences", []):
        desc = e.get('description', '')
        title = e.get('titre', '')
        if "typescript" in desc.lower() or "django" in desc.lower() or "react" in desc.lower():
            print(f"  - Exp: {title} | Desc: {desc[:100]}...")

if __name__ == "__main__":
    asyncio.run(main())
