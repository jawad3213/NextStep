import asyncio
from sqlalchemy import text
from app.core.database import AsyncSessionFactory

async def main():
    async with AsyncSessionFactory() as db:
        res = await db.execute(text("SELECT titre_projet, is_university FROM projet"))
        print([r for r in res])

asyncio.run(main())
