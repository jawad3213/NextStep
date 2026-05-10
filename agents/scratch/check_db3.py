import asyncio
from sqlalchemy import text
from app.core.database import AsyncSessionFactory

async def main():
    async with AsyncSessionFactory() as db:
        res = await db.execute(text("SELECT * FROM experience WHERE type_contrat ILIKE '%extra%' OR entreprise ILIKE '%extra%'"))
        print('Experiences:', [dict(r) for r in res.mappings().all()])
        
        res = await db.execute(text("SELECT * FROM projet"))
        for r in res.mappings().all():
            if 'extra' in str(r).lower() or 'asso' in str(r).lower():
                print('Projet:', dict(r))
                
        # What about checking table names again carefully?
        res = await db.execute(text("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'"))
        print('Tables:', [r[0] for r in res])

asyncio.run(main())
