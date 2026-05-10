import asyncio
from sqlalchemy import text
from app.core.database import AsyncSessionFactory

async def main():
    async with AsyncSessionFactory() as db:
        res = await db.execute(text("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'"))
        print([r[0] for r in res])

asyncio.run(main())
