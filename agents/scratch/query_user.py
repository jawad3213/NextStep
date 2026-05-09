import asyncio
import sys
import os
from sqlalchemy import text

# Add root path
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from app.core.database import AsyncSessionFactory

async def check():
    print("Connecting to database...")
    async with AsyncSessionFactory() as db:
        res = await db.execute(text("""
            SELECT id_utilisateur, keycloak_id, email, nom, prenom 
            FROM utilisateur
        """))
        rows = res.mappings().all()
        print(f"Total users found: {len(rows)}")
        for r in rows:
            print(dict(r))

if __name__ == "__main__":
    asyncio.run(check())
