import asyncio
import httpx
import sys
import io
from urllib.parse import quote

# Force UTF-8 encoding for Windows
if sys.platform.startswith("win"):
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except AttributeError:
        sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

async def qwant_search(query: str) -> list[dict]:
    print(f"Searching Qwant for: {query}")
    encoded_query = quote(query)
    # Use Qwant's web search API
    url = f"https://api.qwant.com/v3/search/web?q={encoded_query}&count=5&locale=fr_FR"
    
    headers = {
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Accept": "application/json",
        "Referer": "https://www.qwant.com/"
    }
    
    try:
        async with httpx.AsyncClient(timeout=10.0, headers=headers, follow_redirects=True) as client:
            response = await client.get(url)
            print("QWANT STATUS:", response.status_code)
            
            if response.status_code == 200:
                data = response.json()
                results = []
                # Qwant API response format: data -> result -> items
                items = data.get("data", {}).get("result", {}).get("items", [])
                print(f"Found {len(items)} items in Qwant.")
                for item in items:
                    results.append({
                        "title": item.get("title") or "Sans titre",
                        "snippet": item.get("desc") or "",
                        "url": item.get("url") or ""
                    })
                return results
            else:
                print("Raw Qwant Response:", response.text[:500])
                return []
    except Exception as e:
        print("QWANT ERROR:", e)
        return []

async def main():
    res = await qwant_search("SQLI Maroc")
    print(f"\nFound {len(res)} results from Qwant:")
    for idx, r in enumerate(res[:3]):
        print(f"[{idx}] {r['title']}\nURL: {r['url']}\nSnippet: {r['snippet']}\n")

if __name__ == "__main__":
    asyncio.run(main())
