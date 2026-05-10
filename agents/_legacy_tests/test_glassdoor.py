import asyncio
from scrapling import StealthyFetcher
from urllib.parse import urlparse

async def google_search_for_glassdoor(page):
    print("      [Google Search] Waiting for search input...")
    search_box = page.locator('[name="q"]')
    await search_box.wait_for(state="visible", timeout=10000)
    
    print("      [Google Search] Typing 'Docaposte questions entretien Glassdoor'...")
    await search_box.fill("Docaposte questions entretien Glassdoor")
    await search_box.press("Enter")
    
    print("      [Google Search] Waiting for search results...")
    await page.wait_for_timeout(4000)

async def test_glassdoor():
    print("\n" + "="*50)
    print("TEST SCRAPLING GLASSDOR -- RETRIEVING INTERVIEW QUESTIONS")
    print("="*50 + "\n")

    # Step 1: Find Glassdoor URL via Google Search using StealthyFetcher
    print("Step 1: Searching Google for Docaposte Glassdoor Interview page...")
    response = await StealthyFetcher.async_fetch(
        "https://www.google.com",
        page_action=google_search_for_glassdoor,
        headless=True
    )

    # Extract all Google search links
    links = response.css("a::attr(href)").getall()
    glassdoor_urls = []
    for link in links:
        if "glassdoor" in link and "url?q=" in link:
            # Parse Google redirect URL (e.g., https://google.com/url?q=https://www.glassdoor.fr/...)
            parts = link.split("url?q=")
            if len(parts) > 1:
                real_url = parts[1].split("&")[0]
                if real_url not in glassdoor_urls:
                    glassdoor_urls.append(real_url)
        elif "glassdoor" in link and link.startswith("http"):
            if link not in glassdoor_urls:
                glassdoor_urls.append(link)

    if not glassdoor_urls:
        print("WARNING: No direct Glassdoor link extracted from first page of search. Trying a fallback query.")
        # Fallback direct Glassdoor FR interview search URL
        target_url = "https://www.glassdoor.fr/Entretien/Docaposte-questions-entretien-E412214.htm"
    else:
        target_url = glassdoor_urls[0]
        print(f"SUCCESS: Found Glassdoor URL: {target_url}")

    # Step 2: Fetch Glassdoor page and extract questions
    print(f"\nStep 2: Fetching Glassdoor Page ({target_url}) with cloudflare solver enabled...")
    
    # Let's fetch Glassdoor using StealthyFetcher
    response_gd = await StealthyFetcher.async_fetch(
        target_url,
        solve_cloudflare=True, # Automatically solve Turnstile/Interstitial
        headless=True,
        wait=3000 # Wait 3 seconds to let content fully settle
    )
    
    print(f"Status from Glassdoor : {response_gd.status}")
    print(f"Title of Glassdoor Page: {response_gd.css('title::text').get()}")

    # Try various Glassdoor selectors for interview questions / feedback
    # Typical Glassdoor selectors for interview questions/experience text:
    # 1. Inside <span> or <p> with certain interview classes
    # 2. Let's dump all relevant texts on the page that could be questions
    questions = (
        response_gd.css(".interviewQuestion::text").getall() or 
        response_gd.css("p.interview-question::text").getall() or
        response_gd.css("span.interview-question::text").getall() or
        response_gd.css(".interview-details p::text").getall() or
        response_gd.css("span[data-test='interview-question']::text").getall()
    )

    if questions:
        print(f"\nSUCCESS: Extracted {len(questions)} interview snippets / questions:")
        for i, q in enumerate(questions[:10]):
            print(f"   {i+1}. {q.strip()}")
    else:
        print("\nWARNING: No specific interview questions matched using predefined selectors.")
        print("Dumping some generic text blocks on the page for review:")
        all_paragraphs = response_gd.css("p::text").getall()
        extracted_blocks = 0
        for p in all_paragraphs:
            cleaned = p.strip()
            # Glassdoor interview descriptions usually have some length and mentions questions/entretien
            if len(cleaned) > 20 and ("question" in cleaned.lower() or "entretien" in cleaned.lower() or "postulé" in cleaned.lower()):
                extracted_blocks += 1
                print(f"   Block {extracted_blocks}: {cleaned}")
                if extracted_blocks >= 5:
                    break
        if extracted_blocks == 0:
            print("No matching text blocks found. Glassdoor might require login or have loaded a simplified page.")

if __name__ == "__main__":
    asyncio.run(test_glassdoor())
