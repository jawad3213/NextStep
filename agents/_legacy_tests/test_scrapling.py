import asyncio
from scrapling import Fetcher, StealthyFetcher

# 1. Page action for StealthyFetcher on Google
async def google_search_action(page):
    print("      [page_action] Page loaded. Title before action:", await page.title())
    
    # Locate the search input. Google modern uses textarea[name="q"] or input[name="q"]
    search_box = page.locator('[name="q"]')
    await search_box.wait_for(state="visible", timeout=10000)
    
    print("      [page_action] Typing 'Docaposte Maroc'...")
    await search_box.fill("Docaposte Maroc")
    
    print("      [page_action] Pressing Enter...")
    await search_box.press("Enter")
    
    # Wait for results to load
    print("      [page_action] Waiting for results to load...")
    await page.wait_for_timeout(3000)
    print("      [page_action] Title after action:", await page.title())

async def test_scrapling():
    print("\n" + "="*50)
    print("TEST SCRAPLING -- FETCH, STEALTH & INTERACTION")
    print("="*50 + "\n")

    # --- 1. Static Fetcher ---
    print("1. Test Fetcher (Simple GET via curl-cffi)...")
    fetcher = Fetcher()
    response = fetcher.get("https://www.google.com")
    print(f"   Status : {response.status}")
    print(f"   Title  : {response.css('title::text').get()}")
    
    # --- 2. StealthyFetcher (Async, direct GET) ---
    print("\n2. Test StealthyFetcher (Async GET)...")
    response = await StealthyFetcher.async_fetch("https://www.google.com")
    print(f"   Status : {response.status}")
    print(f"   Title  : {response.css('title::text').get()}")
    
    # --- 3. Interactive StealthyFetcher with page_action on Google ---
    print("\n3. Test Interactive StealthyFetcher with page_action on Google...")
    response = await StealthyFetcher.async_fetch(
        "https://www.google.com",
        page_action=google_search_action,
        headless=True  # Run browser headless
    )
    print(f"   Status : {response.status}")
    print(f"   Title  : {response.css('title::text').get()}")
    
    # Extract results after the interactive search action has completed
    # Google result titles are inside <h3> tags
    results = response.css("h3::text").getall()
    if results:
        print(f"   SUCCESS - Found {len(results)} search results on Google:")
        for i, r in enumerate(results[:5]):
            print(f"      {i+1}. {r.strip()}")
    else:
        print("   WARNING - No search results found (Selector might have changed or Google blocked us)")
        # Let's dump some text from the body to see what is returned
        text_snippets = response.css("p::text").getall()
        if text_snippets:
            print("   Page text snippets:")
            for snippet in text_snippets[:5]:
                print(f"      - {snippet.strip()}")

if __name__ == "__main__":
    asyncio.run(test_scrapling())
