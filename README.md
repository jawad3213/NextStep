# NextStep — AI Career Assistant Platform

> Architecture **Modular Monolith** extensible → migration **Microservices** par étapes.
> Stack : **Angular 17** · **.NET 8** · **Python FastAPI** · **Keycloak** · **PostgreSQL** · **Kafka** (Phase 2)

---

## 🎯 Fonctionnalités

| # | Fonctionnalité | Stack |
|---|---------------|-------|
| 1 | Authentification SSO | Keycloak · Angular · .NET |
| 2 | Profil candidat (compétences, expériences, projets, certifications) | .NET · Angular |
| 3 | Analyse offre d'emploi → JSON structuré | Python (LangChain) |
| 4 | Matching profil ↔ offre + score | Python (algorithme) |
| 5 | Score ATS algorithme (fonction déterministe) | Python |
| 6 | Génération CV PDF (templates pré-faits + injection JSON) | QuestPDF · .NET |
| 7 | Email de candidature personnalisé | Python (LangChain) · .NET (MailKit) |
| 8 | Suivi candidatures + monitoring IMAP | .NET · Python (imaplib) |
| 9 | **Relance automatique** si sans réponse après 7 jours | .NET (background job) |
| 10 | Chatbot préparation entretien (contextuel) | Python (LangChain) · WebSocket |
| 11 | **Système de Notification** (Temps réel & Email) | .NET (SignalR) · Angular |

---

## 👥 Équipe — Répartition des modules

| Membre | Module | Stack | Responsabilité |
|--------|--------|-------|----------------|
| **M1** | Auth + Profile | .NET + Angular | Keycloak, Utilisateur, Profil complet |
| **M2** | AI Agent | Python FastAPI | Analyse offre, matching, normalisation |
| **M3** | CV Engine | .NET (QuestPDF) + Python | Score ATS, génération PDF, templates |
| **M4** | Email Engine | .NET + Python | Génération email, SMTP, IMAP, relance 7j |
| **M5** | Chatbot + Frontend | Python + Angular | UI complète, chatbot entretien |

---

## 🗺️ Architecture — Modular Monolith (Phase 1)

```
┌───────────────────────────────────────────────────────────────────┐
│              Angular 17  —  Feature Modules                       │
│  auth │ profile │ offers │ cv │ email │ candidature │ chatbot     │
└────────────────────────┬──────────────────────────────────────────┘
                         │  HTTPS + SignalR (WebSocket)
                         ▼
┌───────────────────────────────────────────────────────────────────┐
│                  NGINX  —  Reverse Proxy                          │
│    /api/*  →  .NET :5000       /agents/*  →  Python :8000        │
└──────────┬──────────────────────────────┬─────────────────────────┘
           │                              │
           ▼                              ▼
┌──────────────────────┐      ┌──────────────────────────────────┐
│   .NET 8  Monolith   │      │   Python FastAPI  —  Agents IA   │
│                      │      │                                  │
│  ┌─────────────────┐ │ HTTP │  ┌────────────┐  Algo (pas LLM)  │
│  │ Modules métier  │◄├──────┤  │ AI Agent   │  ◄─ LangChain    │
│  │ (Clean Arch.)   │ │ JSON │  │ CV Engine  │  ◄─ Algorithme   │
│  └─────────────────┘ │      │  │ Email Eng. │  ◄─ LangChain    │
│  ┌─────────────────┐ │      │  │ Chatbot    │  ◄─ LangChain    │
│  │ IEventBus       │ │      │  └────────────┘                  │
│  │ (in-process)    │ │      │  shared/ (config, db, event_bus) │
│  └─────────────────┘ │      └──────────────────────────────────┘
│  ┌─────────────────┐ │
│  │ EF Core  + PgSQL│ │
│  └─────────────────┘ │
└──────────────────────┘
```

---

## 🗺️ Architecture — Microservices (Phase 2)

```
┌───────────────────────────────────────────────────────────────────┐
│              Angular 17  —  Feature Modules                       │
└─────────────────────────┬─────────────────────────────────────────┘
                          │
                          ▼
┌───────────────────────────────────────────────────────────────────┐
│                    API Gateway  —  NGINX                          │
└──┬──────────┬──────────┬──────────┬──────────┬────────────────────┘
   │          │          │          │          │
   ▼          ▼          ▼          ▼          ▼
[auth]   [profile]  [cv-svc]  [email-svc] [chatbot]
.NET      .NET      .NET+Py    .NET+Py      Python
:5001     :5002      :5003      :5004       :8004
   │          │    ai-agent     │
   │          │    Python       │
   │          │    :8001        │
   └──────────┴────────┬────────┘
                       │ Apache Kafka (événements async)
              ┌────────▼────────┐
              │   TOPICS KAFKA  │
              └─────────────────┘
```

---

## 📐 Pattern Clean Architecture — Identique pour chaque module

> **Règle** : Chaque module `.NET` et chaque service `Python` suit **exactement** le même pattern interne. Un nouveau membre peut comprendre n'importe quel module sans lire de documentation supplémentaire.

### .NET — Pattern par module

```
Module/
├── Controllers/    ← Reçoit HTTP, valide, délègue au Service
├── Services/       ← Logique métier (ne connaît pas EF Core)
├── Repositories/   ← Accès base de données (EF Core uniquement ici)
├── DTOs/           ← Contrats JSON entrée / sortie (Pydantic-équivalent)
└── Models/         ← Entités EF Core (tables PostgreSQL)
```

### Python — Pattern par service

```
service/
├── router.py       ← Endpoint FastAPI (HTTP ou WebSocket)
├── service.py      ← Logique métier
├── schemas.py      ← Modèles Pydantic (contrats JSON)
└── events.py       ← Émission / réception événements (→ Kafka en Phase 2)
```

---

## 🏗️ Structure Complète — Monolithique

```
nextstep/
├── docker-compose.yml
├── .env.example
│
├── frontend/                                ← Angular 19 (Feature-based)
│   └── src/
│       └── app/
│           ├── core/
│           │   ├── auth/
│           │   │   ├── keycloak.service.ts
│           │   │   ├── auth.guard.ts
│           │   │   └── auth.interceptor.ts      # Attache JWT sur chaque requête
│           │   └── models/                      # Interfaces TypeScript = contrats JSON
│           │       ├── user.model.ts
│           │       ├── profile.model.ts
│           │       ├── offer.model.ts
│           │       ├── cv.model.ts
│           │       ├── email.model.ts
│           │       └── candidature.model.ts
│           │
│           ├── shared/
│           │   └── components/
│           │       ├── navbar/
│           │       ├── sidebar/
│           │       ├── card/
│           │       └── loader/
│           │
│           └── features/
│               ├── auth/                    ── M1 ──
│               │   ├── login/
│               │   └── callback/
│               │
│               ├── profile/                 ── M1 ──
│               │   ├── profile.service.ts
│               │   └── components/
│               │       ├── personal-info/
│               │       ├── skills-section/
│               │       ├── experience-section/
│               │       ├── formation-section/
│               │       ├── certification-section/
│               │       └── projects-section/
│               │
│               ├── offers/                  ── M2 ──
│               │   ├── offer-paste/             # Coller texte offre
│               │   └── analysis-result/         # Afficher JSON analyse
│               │
│               ├── cv/                      ── M3 ──
│               │   ├── template-selector/       # Choisir template pré-fait
│               │   ├── cv-preview/              # Aperçu PDF
│               │   └── ats-score/               # Badge score ATS
│               │
│               ├── email/                   ── M4 ──
│               │   ├── email-preview/           # Aperçu email généré
│               │   └── inbox-monitor/           # Tableau suivi réponses
│               │
│               ├── candidature/             ── M4 ──
│               │   ├── candidature-tracker/     # Tableau Kanban statuts
│               │   └── followup-alert/          # Alerte relance 7j
│               │
│               └── chatbot/                 ── M5 ──
│                   ├── chat-interface/
│                   └── chat-bubble/
│
├── backend/                                 ← .NET 8  ASP.NET Core
│   └── NextStep.API/
│       ├── Program.cs
│       ├── appsettings.json
│       │
│       │   ┌─────────────────────────────────────────────────────┐
│       │   │  Pattern Clean Architecture — même structure partout  │
│       │   │  Controllers/ · Services/ · Repositories/ · DTOs/ · Models/ │
│       │   └─────────────────────────────────────────────────────┘
│       │
│       ├── Modules/
│       │   │
│       │   ├── Auth/                        ── M1 ──
│       │   │   ├── Controllers/
│       │   │   │   └── AuthController.cs
│       │   │   ├── Services/
│       │   │   │   └── KeycloakService.cs       # Validation token JWT
│       │   │   ├── Middleware/
│       │   │   │   └── JwtMiddleware.cs
│       │   │   ├── DTOs/
│       │   │   │   └── TokenResponseDto.cs
│       │   │   └── Models/
│       │   │       └── UserSession.cs
│       │   │
│       │   ├── Profile/                     ── M1 ──
│       │   │   ├── Controllers/
│       │   │   │   └── ProfileController.cs
│       │   │   ├── Services/
│       │   │   │   └── ProfileService.cs
│       │   │   ├── Repositories/
│       │   │   │   └── ProfileRepository.cs
│       │   │   ├── DTOs/
│       │   │   │   ├── ProfileDto.cs
│       │   │   │   ├── ExperienceDto.cs
│       │   │   │   ├── FormationDto.cs
│       │   │   │   ├── CertificationDto.cs
│       │   │   │   ├── CompetenceDto.cs
│       │   │   │   └── ProjetDto.cs
│       │   │   └── Models/
│       │   │       ├── Utilisateur.cs
│       │   │       ├── Profil.cs
│       │   │       ├── Experience.cs
│       │   │       ├── Formation.cs
│       │   │       ├── Certification.cs
│       │   │       ├── Competence.cs
│       │   │       └── Projet.cs
│       │   │
│       │   ├── Offer/                       ── M2 ──
│       │   │   ├── Controllers/
│       │   │   │   └── OfferController.cs
│       │   │   ├── Services/
│       │   │   │   └── OfferService.cs          # POST texte → FastAPI → JSON
│       │   │   ├── Repositories/
│       │   │   │   └── OfferRepository.cs
│       │   │   ├── DTOs/
│       │   │   │   ├── OfferSubmitDto.cs
│       │   │   │   └── OfferAnalysisDto.cs
│       │   │   └── Models/
│       │   │       └── OffreEmploi.cs
│       │   │
│       │   ├── CV/                          ── M3 ──
│       │   │   ├── Controllers/
│       │   │   │   └── CVController.cs
│       │   │   ├── Services/
│       │   │   │   ├── CVService.cs
│       │   │   │   └── CVGeneratorService.cs    # QuestPDF : injecte JSON → template
│       │   │   ├── Repositories/
│       │   │   │   └── CVRepository.cs
│       │   │   ├── DTOs/
│       │   │   │   ├── CVGenerateRequestDto.cs
│       │   │   │   └── CVResultDto.cs           # chemin PDF + score ATS
│       │   │   ├── Templates/                   # Templates QuestPDF pré-conçus
│       │   │   │   ├── ModernTemplate.cs
│       │   │   │   ├── ClassicTemplate.cs
│       │   │   │   └── CreativeTemplate.cs
│       │   │   └── Models/
│       │   │       ├── CV_Genere.cs
│       │   │       └── TemplateCV.cs
│       │   │
│       │   ├── Email/                       ── M4 ──
│       │   │   ├── Controllers/
│       │   │   │   └── EmailController.cs
│       │   │   ├── Services/
│       │   │   │   ├── EmailService.cs          # MailKit — envoi SMTP
│       │   │   │   └── EmailMonitorService.cs   # IMAP polling → màj statut
│       │   │   ├── Repositories/
│       │   │   │   └── EmailRepository.cs
│       │   │   ├── DTOs/
│       │   │   │   ├── EmailSendDto.cs
│       │   │   │   └── EmailStatusDto.cs
│       │   │   └── Models/
│       │   │       └── EmailAccompagnement.cs
│       │   │
│       │   ├── Candidature/                 ── M4 ──   (partagé)
│       │   │   ├── Controllers/
│       │   │   │   └── CandidatureController.cs
│       │   │   ├── Services/
│       │   │   │   ├── CandidatureService.cs
│       │   │   │   └── FollowUpService.cs       # ★ Relance auto si > 7j sans réponse
│       │   │   ├── Jobs/
│       │   │   │   └── FollowUpJob.cs           # Background job (Hangfire / Quartz)
│       │   │   ├── Repositories/
│       │   │   │   └── CandidatureRepository.cs
│       │   │   ├── DTOs/
│       │   │   │   ├── CandidatureDto.cs
│       │   │   │   └── StatutUpdateDto.cs
│       │   │   └── Models/
│       │   │       ├── Candidature.cs
│       │   │       └── HistoriqueStatut.cs
│       │   │
│       │   └── Chatbot/                     ── M5 ──
│       │       ├── Hubs/
│       │       │   └── ChatHub.cs               # SignalR WebSocket
│       │       ├── Services/
│       │       │   └── ChatbotService.cs         # Appelle FastAPI /ws/chat
│       │       ├── DTOs/
│       │       │   ├── ChatMessageDto.cs
│       │       │   └── QuestionEntretienDto.cs
│       │       └── Models/
│       │           └── QuestionEntretien.cs
│       │
│       ├── Infrastructure/
│       │   ├── Data/
│       │   │   ├── AppDbContext.cs
│       │   │   └── Migrations/
│       │   ├── Events/                          # PAS Kafka en monolithe
│       │   │   ├── IEventBus.cs                 # Interface commune Phase 1 + Phase 2
│       │   │   └── InMemoryEventBus.cs          # Impl in-process → remplacé par KafkaEventBus en Phase 2
│       │   └── Http/
│       │       └── AgentHttpClient.cs           # Appels HTTP/JSON vers FastAPI
│       │
│       └── Common/
│           ├── Middleware/
│           │   └── ExceptionMiddleware.cs
│           └── Extensions/
│               └── ServiceCollectionExtensions.cs
│
└── agents/                                  ← Python 3.12 + FastAPI
    │
    │  ┌──────────────────────────────────────────────────────────────┐
    │  │  Chaque dossier = 1 service → futur microservice indépendant  │
    │  │  Pattern commun :                                             │
    │  │    router.py   endpoint HTTP / WebSocket                      │
    │  │    service.py  logique métier                                 │
    │  │    schemas.py  modèles Pydantic (contrats JSON)               │
    │  │    events.py   Phase 1: direct │ Phase 2: Kafka topic         │
    │  └──────────────────────────────────────────────────────────────┘
    │
    ├── main.py                              # Monte tous les routers
    ├── requirements.txt
    │
    ├── shared/                              # Partagé entre tous les services
    │   ├── config.py                        # Variables env (pydantic-settings)
    │   ├── database.py                      # SQLAlchemy async → PostgreSQL
    │   ├── http_client.py                   # Client HTTP vers .NET
    │   └── event_bus.py                     # IEventBus : in-process Phase 1
    │                                        #             Kafka       Phase 2
    │
    ├── ai_agent/                            ── M2 ──  → futur :8001
    │   ├── router.py                        # POST /analyze-offer · POST /match
    │   ├── service.py
    │   ├── schemas.py                       # OfferInput · ProfileJson · MatchResult
    │   ├── events.py                        # Émet: analysis.completed
    │   └── agents/
    │       ├── offer_analyzer.py            # Offre texte → JSON structuré (LangChain)
    │       ├── profile_retriever.py         # Profil BD → JSON normalisé
    │       ├── normalizer.py                # Harmonise offre + profil
    │       └── matcher.py                   # Score matching + gaps (algorithme)
    │
    ├── cv_engine/                           ── M3 ──  → futur :8002
    │   │  Note : génération PDF = QuestPDF côté .NET
    │   │         Python prépare uniquement le JSON structuré du CV
    │   ├── router.py                        # POST /prepare-cv-data
    │   ├── service.py
    │   ├── schemas.py                       # CVDataJson · ATSResult
    │   ├── events.py                        # Écoute: analysis.completed
    │   └── ats_scorer.py                    # ★ Score ATS — fonction algorithmique
    │                                        #   Entrée : keywords_offre[], texte_cv
    │                                        #   Sortie : { score: int, manquants: [] }
    │                                        #   PAS de LLM — calcul déterministe
    │
    ├── email_engine/                        ── M4 ──  → futur :8003
    │   ├── router.py                        # POST /generate-email · GET /inbox
    │   ├── service.py
    │   ├── schemas.py                       # EmailInput · EmailOutput · StatusUpdate
    │   ├── events.py                        # Écoute: cv.generated · Émet: email.sent
    │   ├── email_generator.py               # LangChain → email personnalisé (JSON)
    │   └── email_monitor.py                 # imaplib → polling IMAP → JSON statut
    │
    └── chatbot/                             ── M5 ──  → futur :8004
        ├── router.py                        # WebSocket /ws/chat
        ├── service.py                       # LangChain ConversationChain
        ├── schemas.py                       # ChatMessage · QuestionEntretien
        ├── events.py                        # Écoute: analysis.completed (contexte)
        └── prompts/
            ├── interview_prep.py
            └── contextual_qa.py
```

---

## ⭐ Fonctionnalité : Score ATS (algorithme déterministe)

> Le score ATS **n'est pas calculé par un LLM**. C'est une **fonction algorithmique** qui retourne un entier entre 0 et 100.

```
Entrées :
  - keywords_offre   : liste de mots-clés extraits du JSON analyse offre
  - texte_cv         : contenu textuel du CV généré

Algorithme :
  1. Normaliser les deux textes (minuscules, sans accents)
  2. Pour chaque keyword_offre → chercher présence dans texte_cv
  3. score = (nb_keywords_trouvés / nb_keywords_total) × 100
  4. Bonus pondéré si le keyword est dans le titre ou résumé (+poids)

Sorties :
  {
    "score_ats"         : 78,
    "keywords_presents" : ["Angular", "TypeScript", "Docker"],
    "keywords_manquants": [".NET Core"],
    "recommandations"   : ["Ajouter .NET Core dans compétences"]
  }
```

---

## ⭐ Fonctionnalité : Relance automatique candidature (7 jours)

> Si une candidature est à l'état `ENVOYE` depuis **plus de 7 jours** sans changement de statut, le système envoie automatiquement un email de relance.

```
.NET — Module Candidature
  └── Jobs/
      └── FollowUpJob.cs    ← Tâche planifiée (Hangfire / Quartz.NET)

Logique :
  1. Toutes les 24h : sélectionner candidatures où
       statut = ENVOYE
       AND date_envoi < aujourd'hui - 7 jours
       AND relance_envoyee = false
  2. Pour chaque candidature trouvée :
       a. Appeler FastAPI /generate-email avec contexte "relance"
       b. Envoyer email via MailKit
       c. Mettre relance_envoyee = true + créer HistoriqueStatut
       d. Notifier Angular via SignalR

HistoriqueStatut :
  { etat_statut: "RELANCE_ENVOYEE", date_mise_a_jour: ..., commentaire: "Relance auto J+7" }
```

---

## 🔄 Flux de données — étape par étape

```
1.  Angular  →  login Keycloak → JWT token
2.  Angular  →  données profil (JSON) → .NET → PostgreSQL
3.  Angular  →  **choix template** + **colle offre (JD)** → .NET OfferController
4.  **DEBUT PIPELINE AUTOMATISÉ** :
    - .NET  →  Analyse (Python)
    - Python → Matching & Score (Algo)
    - Python → ATS Score (Algo)
    - .NET  → Génération PDF (QuestPDF)
5.  **FIN PIPELINE** → Angular affiche immédiatement le **CV PDF généré**
6.  .NET     →  POST /generate-email → FastAPI email_engine
7.  Python   →  email_generator (LangChain) → email JSON
8.  .NET     →  EmailService (MailKit) → envoi SMTP
9.  Python   →  email_monitor (imaplib) → polling IMAP → JSON statut
10. .NET     →  màj Candidature + HistoriqueStatut
11. .NET     →  SignalR → Angular (notification temps réel)
12. .NET     →  FollowUpJob : si 7j sans réponse → relance auto
13. Angular  →  WebSocket /ws/chat → FastAPI chatbot → Q&A entretien
```

---

## 📡 Topics Kafka (Phase 2 — Microservices)

| Topic | Producteur | Consommateur | Payload JSON |
|-------|-----------|--------------|--------------|
| `offer.submitted` | .NET Offer | Python ai_agent | `{ userId, offer_text }` |
| `analysis.completed` | Python ai_agent | .NET CV · Python chatbot | `{ offre_json, profil_json, score_matching }` |
| `cv.ready` | .NET CV | Python email_engine | `{ cv_id, pdf_path, score_ats }` |
| `email.sent` | .NET Email | .NET Candidature | `{ candidature_id, date_envoi }` |
| `candidature.updated` | .NET Candidature | Angular (SignalR) | `{ candidature_id, statut }` |
| `followup.triggered` | .NET FollowUpJob | Python email_engine | `{ candidature_id, type: "relance" }` |

---

## 🚀 Plan de migration Mono → Microservices

```
Phase 1  (Monolith)
  .NET backend     — tous les modules dans une seule solution
  Python agents    — une seule app FastAPI (main.py monte tout)
  Communication    — IEventBus in-process + HTTP direct
  DB               — 1 PostgreSQL partagé

Phase 2  (Extract Python services)
  Extraire chaque dossier Python → Dockerfile indépendant
  Brancher events.py sur Kafka (remplacer event_bus.py)
  .NET reste monolith — communication via Kafka events

Phase 3  (Full Microservices)
  Extraire modules .NET → services indépendants
  Remplacer InMemoryEventBus par KafkaEventBus (1 fichier)
  Chaque service → sa propre base de données (DB per service)
```

> **Clé de la migration** : `IEventBus` et `events.py` sont les seuls fichiers à modifier. Le code métier des modules ne change pas.

---

## 🐳 Services Docker Compose

| Service | Image | Port | Phase |
|---------|-------|------|-------|
| PostgreSQL 15 | `postgres:15` | 5432 | 1, 2, 3 |
| Keycloak 24 | `quay.io/keycloak/keycloak:24.0` | 8080 | 1, 2, 3 |
| Redis 7 | `redis:7-alpine` | 6379 | 1, 2, 3 |
| Backend .NET | `./backend` | 5000 | 1, 2, 3 |
| Agents Python | `./agents` | 8000 | 1 |
| Frontend Angular | `./frontend` | 4200 | 1, 2, 3 |
| Zookeeper | `cp-zookeeper:7.6.0` | 2181 | 2, 3 |
| Kafka | `cp-kafka:7.6.0` | 9092 | 2, 3 |

---

## 🔐 Flux Keycloak

```
Angular  ──[PKCE Login]────────────▶  Keycloak :8080
          ◀──[JWT Access Token]──
          ──[requête + Bearer Token]──▶  .NET Backend
                                        ──[Introspect token]──▶  Keycloak
                                        ◀──[Claims utilisateur]──
                                        ──[Réponse JSON]──▶  Angular
```

- Realm : `nextstep` · Client Angular : `nextstep-frontend` (PKCE) · Client .NET : `nextstep-backend` (Confidential)
- Rôles : `USER` · `ADMIN`
- L'`AuthInterceptor` Angular attache le Bearer Token sur toutes les requêtes HTTP automatiquement

---

## 🔧 Démarrage rapide

```bash
# 1. Infrastructure
docker-compose up -d postgres keycloak redis

# 2. Backend .NET
cd backend/NextStep.API
dotnet ef database update
dotnet run

# 3. Agents Python
cd agents
pip install -r requirements.txt
uvicorn main:app --reload --port 8000

# 4. Frontend Angular
cd frontend
npm install
ng serve
```

---

*NextStep — v4.0 — Mars 2026 · Angular 17 · .NET 8 · Python FastAPI · QuestPDF · Keycloak · PostgreSQL*
