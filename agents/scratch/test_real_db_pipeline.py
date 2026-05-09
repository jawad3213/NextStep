import asyncio
import sys
import os
import json

# Add root path
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from app.domain.profile_retriever.service import profile_retriever_service

async def test_db():
    user_id = "a9709404-ae96-4070-91a3-21142ded4139"
    print(f"Retrieving real profile for user: {user_id}...")
    res = await profile_retriever_service.get_profile(user_id)
    print("Database Retrieval Completed.")
    print(json.dumps(res, indent=2, ensure_ascii=False))

if __name__ == "__main__":
    asyncio.run(test_db())
