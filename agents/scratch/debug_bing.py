import httpx
import asyncio

async def debug_bing():
    url = "https://www.bing.com/search?q=Docaposte+Maroc"
    headers = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"}
    async with httpx.AsyncClient(timeout=15.0, headers=headers, follow_redirects=True) as client:
        resp = await client.get(url)
        with open("bing_debug.html", "w", encoding="utf-8") as f:
            f.write(resp.text)
        print(f"Status: {resp.status_code}")
        print(f"Length: {len(resp.text)}")

if __name__ == "__main__":
    asyncio.run(debug_bing())
