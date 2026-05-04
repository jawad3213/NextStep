import requests
import json
import sys

BASE_URL = "http://localhost:8000"

def test_health():
    print("\n--- Testing Health Check ---")
    try:
        response = requests.get(f"{BASE_URL}/health")
        print(f"Status: {response.status_code}")
        print(json.dumps(response.json(), indent=2))
    except Exception as e:
        print(f"Error: {e}")

def test_analyze_offer(raw_text, user_id):
    print("\n--- Testing Agent 1 (Analyze Offer) ---")
    payload = {
        "raw_text": raw_text,
        "user_id": user_id,
        "template_id": 1
    }
    try:
        response = requests.post(f"{BASE_URL}/analyze-offer", json=payload)
        print(f"Status: {response.status_code}")
        if response.status_code == 200:
            print("Success! Title found:", response.json().get('titre'))
            return response.json()
        else:
            print(response.text)
    except Exception as e:
        print(f"Error: {e}")
    return None

def test_pipeline(raw_text, user_id):
    print("\n--- Testing Full Pipeline (Agents 1-4) ---")
    payload = {
        "raw_text": raw_text,
        "user_id": user_id,
        "template_id": 1
    }
    try:
        response = requests.post(f"{BASE_URL}/run-pipeline", json=payload)
        if response.status_code == 200:
            result = response.json()
            match = result.get('match_result', {})
            
            print(f"✅ Status: {response.status_code}")
            
            # 🎬 Trace des actions
            if result.get('messages'):
                print("\n🎬 Actions des Agents (Trace) :")
                for m in result['messages']:
                    agent_name = m.get('agent', 'AI').replace('_', ' ').title()
                    print(f"   [{agent_name}] : {m.get('content')}")

            print(f"\n📊 Score Matching : {match.get('score_matching')}%")
            print(f"📊 Score ATS      : {match.get('score_ats')}%")
            
            # 💡 Recommandations
            if match.get('recommandations'):
                print("\n💡 Recommandations des agents :")
                for r in match['recommandations']:
                    print(f"   - {r}")
            
            return result
        else:
            print(f"❌ Error {response.status_code}: {response.text}")
    except Exception as e:
        print(f"Error: {e}")
    return None

if __name__ == "__main__":
    # Sample data
    SAMPLE_OFFER = """
    Nous recherchons un Développeur Fullstack Angular/Node.js.
    Compétences : TypeScript, SQL, Docker, AWS.
    Expérience : 3 ans minimum.
    """
    # TEST_USER_ID = "0eb0afcd-5ea2-4877-8e02-66395f172e1e" # Nichan Said (43 competences techniques)
    TEST_USER_ID = "a9709404-ae96-4070-91a3-21142ded4139" # Nichan Said (Langues + Parascolaire)

    if len(sys.argv) > 1:
        cmd = sys.argv[1]
        if cmd == "health":
            test_health()
        elif cmd == "analyze":
            test_analyze_offer(SAMPLE_OFFER, TEST_USER_ID)
        elif cmd == "pipeline":
            test_pipeline(SAMPLE_OFFER, TEST_USER_ID)
    else:
        print("Usage: python test_agents.py [health|analyze|pipeline]")
        test_health()
