import requests
import json

url = "http://localhost:8000/offer/match"
payload = {
    "user_id": "35eb58d9-9b6f-4f1a-b92d-cc1e29758322",
    "analyzed_offer": {
        "titre": "Senior Frontend Developer",
        "entreprise": None,
        "type_contrat": None,
        "localisation": "Paris",
        "competences_requises": [
            "SignalR",
            "NgRx",
            "C#",
            "Angular",
            "Tailwind CSS"
        ],
        "competences_souhaitees": [],
        "keywords_ats": [
            "SignalR",
            "NgRx",
            "C#",
            "Angular",
            "Tailwind CSS",
            "Frontend Development"
        ],
        "annees_experience": "5",
        "niveau_etudes": None,
        "description_poste": "Développeur frontend senior spécialisé en Angular et Tailwind CSS avec au moins 5 ans d'expérience. Connaissance de SignalR, NgRx et C# fortement appréciée."
    }
}

headers = {
    "Content-Type": "application/json"
}

print("Sending POST request to:", url)
response = requests.post(url, json=payload, headers=headers)
print("Status Code:", response.status_code)
try:
    result = response.json()
    print("JSON Result:")
    print(json.dumps(result, indent=2, ensure_ascii=False))
except Exception as e:
    print("Raw Output:")
    print(response.text)
