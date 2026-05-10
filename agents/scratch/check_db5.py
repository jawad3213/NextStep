import asyncio
from sqlalchemy import text
from app.core.database import AsyncSessionFactory

async def main():
    async with AsyncSessionFactory() as db:
        res = await db.execute(text("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'"))
        tables = [r[0] for r in res]
        for t in tables:
            try:
                cnt = await db.execute(text(f"SELECT count(*) FROM {t}"))
                print(f"{t}: {cnt.scalar()}")
            except Exception as e:
                print(f"Error on {t}: {e}")

asyncio.run(main())
