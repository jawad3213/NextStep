# NextStep — AI Career Assistant Platform

> **NextStep** est une plateforme d'accompagnement IA pour les étudiants et jeunes diplômés
> à la recherche de stages (PFA/PFE) ou d'un premier emploi.
> Elle automatise le processus complet : **Analyse d'offre → Matching → Génération CV → Email → Suivi → Relance**.

> Architecture **Monolithe Modulaire** — Communication directe HTTP/JSON
> Stack : **Angular 19** · **.NET 8** · **Python FastAPI** · **Keycloak 24** · **PostgreSQL (pgvector)** · **Redis**

---

## 🎯 Fonctionnalités

| # | Fonctionnalité | Stack |
|---|---------------|-------|
| 1 | Authentification SSO (PKCE) | Keycloak 24 · keycloak-js · Angular · .NET JWT |
| 2 | Profil candidat (compétences, expériences, projets, certifications) | .NET (EF Core) · Angular |
| 3 | Analyse offre d'emploi → JSON structuré (LLM) | Python (LangChain · LangGraph) |
| 4 | Matching profil ↔ offre + score (algorithme pur) | Python |
| 5 | Score ATS (fonction déterministe, pas de LLM) | Python |
| 6 | Génération CV PDF (templates pré-faits + injection JSON) | QuestPDF (.NET) + Python |
| 7 | Email de candidature personnalisé (LLM) | Python (LangChain) · .NET · API Gmail |
| 8 | Suivi candidatures + surveillance Gmail (OAuth Just-in-Time) | .NET · Python · API Gmail (OAuth 2.0) |
| 9 | Relance automatique J+7 (validation obligatoire) | .NET (Hangfire) · API Gmail (OAuth 2.0) |
| 10 | Chatbot préparation entretien (contextuel) | Python (LangChain) · WebSocket |
| 11 | Notifications temps réel | .NET (SignalR) · Angular |

---

## 👥 Équipe — Répartition des modules

| Membre | Module | Stack | Responsabilité |
|--------|--------|-------|----------------|
| **M1** | Auth + Profile | .NET + Angular | Keycloak, Utilisateur, Profil complet |
| **M2** | AI Agent | Python FastAPI | Agents 1-4 : Analyse offre, RAG, normalisation, scoring |
| **M3** | CV Engine | .NET (QuestPDF) + Python | Agent 5 : Formatage template, génération PDF, 3 templates |
| **M4** | Email Engine | .NET + Python | Agent 6 : Email LangChain, API Gmail, relance 7j |
| **M5** | Chatbot + Frontend | Python + Angular | UI complète, chatbot LangChain, notifications SignalR |

---

## 🗺️ Architecture — Monolithe Modulaire

> **Pourquoi un Monolithe Modulaire ?**
> Simple à développer, un seul `docker-compose up`, pas de broker, idéal pour une équipe de 5.
> Communication directe : **Angular → .NET → Python** via HTTP/JSON. Le backend .NET est le **chef d'orchestre**.

```
                           ┌──────────────────────────────────────────────┐
                           │          🖥️  FRONTEND (Angular 19)           │
                           │                                              │
                           │  ┌──────┐ ┌───────┐ ┌────┐ ┌─────┐ ┌─────┐ │
                           │  │ Auth │ │Profile│ │ CV │ │Email│ │Chat │ │
                           │  └──────┘ └───────┘ └────┘ └─────┘ └─────┘ │
                           └─────────────────┬────────────────────────────┘
                                             │
                                   HTTP (REST) + SignalR (WebSocket)
                                             │
                           ┌─────────────────▼────────────────────────────┐
                           │          ⚙️  BACKEND .NET 8 (Monolithe)      │
                           │                                              │
                           │  ┌─────────────────────────────────────────┐ │
                           │  │          Modules Métier                  │ │
                           │  │                                         │ │
                           │  │  Auth ─ Profile ─ Offer ─ CV ─ Email   │ │
                           │  │  Candidature ─ Chatbot ─ Notification   │ │
                           │  └─────────────────────────────────────────┘ │
                           │                                              │
                           │  ┌──────────────┐  ┌────────────────┐       │
                           │  │ EF Core      │  │ SignalR Hub    │       │
                           │  │ (PostgreSQL) │  │ (Notifications)│       │
                           │  └──────┬───────┘  └────────────────┘       │
                           │         │                                    │
                           │  ┌──────▼───────┐  ┌────────────────┐       │
                           │  │ Hangfire     │  │ QuestPDF       │       │
                           │  │ (Jobs Redis) │  │ (PDF Engine)   │       │
                           │  └──────────────┘  └────────────────┘       │
                           └────────┬─────────────────────────────────────┘
                                    │
                          HTTP/JSON (appel synchrone)
                                    │
                           ┌────────▼─────────────────────────────────────┐
                           │       🤖  AGENTS IA — Python FastAPI         │
                           │                                              │
                           │  ┌─────────────────────────────────────────┐ │
                           │  │       LangGraph Pipeline (6 Agents)     │ │
                           │  │                                         │ │
                           │  │  Agent 1: Analyseur d'Offre (LLM)      │ │
                           │  │  Agent 2: Profile Retriever (RAG)      │ │
                           │  │  Agent 3: Normalisateur (Algo)         │ │
                           │  │  Agent 4: Scorer (Calcul)              │ │
                           │  │  Agent 5: Formatteur Template (JSON)   │ │
                           │  │  Agent 6: Email Composer (LLM)        │ │
                           │  └─────────────────────────────────────────┘ │
                           │                                              │
                           │  ┌──────────┐  ┌──────────┐  ┌───────────┐  │
                           │  │ Chatbot  │  │ Email    │  │ Inbox     │  │
                           │  │ Service  │  │ Monitor  │  │ Watcher   │  │
                           │  └──────────┘  └──────────┘  └───────────┘  │
                           └──────────────────────────────────────────────┘
                                    │
                           ┌────────▼─────────────────────────────────────┐
                           │   🗄️  PostgreSQL 15 (pgvector)              │
                           │                                              │
                           │  ┌────────────────┐  ┌────────────────────┐  │
                           │  │ Schema: public │  │ Schema: keycloak   │  │
                           │  │ (App data)     │  │ (Auth data)        │  │
                           │  └────────────────┘  └────────────────────┘  │
                           └──────────────────────────────────────────────┘
```

---

## 🤖 Pipeline Multi-Agent (LangGraph)

Le pipeline est orchéstré par **LangGraph** et exécuté en ~6 secondes :

```
    START (Offre brute + user_id + template_id)
      │
      ▼
  🤖 Agent 1 : Analyseur d'Offre (LLM)
  │  Offre brute → JSON structuré (titre, compétences, mots-clés ATS)
  ▼
  🤖 Agent 2 : Profile Retriever (RAG + pgvector)
  │  Mots-clés → Vector Search → profil filtré le plus pertinent
  ▼
  🤖 Agent 3 : Normalisateur (Algorithme pur, pas de LLM)
  │  Nettoyage accents, synonymes, mise en minuscules
  ▼
  🤖 Agent 4 : Scorer (Calcul mathématique, pas de LLM)
  │  Score matching + Score ATS avec pondération positionnelle
  ▼
  🤖 Agent 5 : Formatteur Template (JSON structuré)
  │  Organise les données dans le schéma attendu par QuestPDF
  ▼
  🤖 Agent 6 : Email Composer (LLM)
  │  Rédige l'email de candidature personnalisé
  ▼
 END → Retour JSON complet vers .NET
```

---

## 📐 Pattern Clean Architecture — Identique pour chaque module

> **Règle** : Chaque module `.NET` et chaque service `Python` suit **exactement** le même pattern interne.

### .NET — Pattern par module

```
Module/
├── Controllers/    ← Reçoit HTTP, valide, délègue au Service
├── Services/       ← Logique métier (ne connaît pas EF Core)
├── Repositories/   ← Accès base de données (EF Core uniquement ici)
├── DTOs/           ← Contrats JSON entrée / sortie
└── Models/         ← Entités EF Core (tables PostgreSQL)
```

### Python — Pattern par service

```
service/
├── router.py       ← Endpoint FastAPI (HTTP ou WebSocket)
├── service.py      ← Logique métier
└── schemas.py      ← Modèles Pydantic (contrats JSON)
```

---

## 🏗️ Structure des fichiers

```
nextstep/
├── docker-compose.yml
├── .env.example
│
├── frontend/                                ← Angular 19 (Standalone Components)
│   ├── Dockerfile
│   ├── nginx.conf                           # Routing SPA (Angular → index.html)
│   ├── .dockerignore
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
│           ├── shared/components/
│           │   ├── navbar/
│           │   ├── sidebar/
│           │   ├── card/
│           │   └── loader/
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
│   ├── Dockerfile
│   ├── .dockerignore
│   └── NextStep.API/
│       ├── Program.cs
│       ├── appsettings.json
│       │
│       ├── Modules/
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
│       │   │   │   └── EmailService.cs          # OAuth Gmail API — envoi emails
│       │   │   ├── Repositories/
│       │   │   │   └── EmailRepository.cs
│       │   │   ├── DTOs/
│       │   │   │   ├── EmailSendDto.cs
│       │   │   │   └── EmailStatusDto.cs
│       │   │   └── Models/
│       │   │       └── EmailAccompagnement.cs
│       │   │
│       │   ├── Candidature/                 ── M4 ──
│       │   │   ├── Controllers/
│       │   │   │   └── CandidatureController.cs
│       │   │   ├── Services/
│       │   │   │   ├── CandidatureService.cs
│       │   │   │   └── FollowUpService.cs       # ★ Relance auto si > 7j sans réponse
│       │   │   ├── Jobs/
│       │   │   │   └── FollowUpJob.cs           # Background job (Hangfire)
│       │   │   ├── Repositories/
│       │   │   │   └── CandidatureRepository.cs
│       │   │   ├── DTOs/
│       │   │   │   ├── CandidatureDto.cs
│       │   │   │   └── StatutUpdateDto.cs
│       │   │   └── Models/
│       │   │       ├── Candidature.cs
│       │   │       └── HistoriqueStatut.cs
│       │   │
│       │   ├── Chatbot/                     ── M5 ──
│       │   │   ├── Hubs/
│       │   │   │   └── ChatHub.cs               # SignalR WebSocket
│       │   │   ├── Services/
│       │   │   │   └── ChatbotService.cs         # Appelle FastAPI /ws/chat
│       │   │   ├── DTOs/
│       │   │   │   ├── ChatMessageDto.cs
│       │   │   │   └── QuestionEntretienDto.cs
│       │   │   └── Models/
│       │   │       └── QuestionEntretien.cs
│       │   │
│       │   └── Notification/
│       │       ├── Controllers/
│       │       │   └── NotificationController.cs
│       │       ├── Services/
│       │       │   └── NotificationService.cs
│       │       └── Hubs/
│       │           └── NotificationHub.cs
│       │
│       ├── Infrastructure/
│       │   ├── Data/
│       │   │   ├── AppDbContext.cs
│       │   │   └── Migrations/
│       │   └── Http/
│       │       └── AgentHttpClient.cs           # Appels HTTP/JSON vers FastAPI
│       │
│       └── Common/
│           ├── Middleware/
│           │   └── ExceptionMiddleware.cs
│           └── Extensions/
│               └── ServiceCollectionExtensions.cs
│
└── agents/                                  ← Python 3.12 + FastAPI + LangGraph
    ├── Dockerfile
    ├── .dockerignore
    ├── requirements.txt
    ├── main.py                              # Monte tous les routers
    │
    ├── shared/                              # Partagé entre tous les services
    │   ├── config.py                        # Variables env (pydantic-settings)
    │   ├── database.py                      # SQLAlchemy async → PostgreSQL + pgvector
    │   └── http_client.py                   # Client HTTP vers .NET (httpx)
    │
    ├── pipeline/
    │   ├── state.py                         # AgentState TypedDict
    │   ├── graph.py                         # LangGraph StateGraph
    │   └── nodes.py                         # 6 fonctions d'agents
    │
    ├── ai_agent/                            ── M2 ──
    │   ├── router.py                        # POST /analyze-offer · POST /match
    │   ├── service.py
    │   ├── schemas.py                       # OfferInput · ProfileJson · MatchResult
    │   └── agents/
    │       ├── offer_analyzer.py            # 🦜 Agent 1 — LLM
    │       ├── profile_retriever.py         # 📐 Agent 2 — RAG pgvector
    │       ├── normalizer.py                # 📐 Agent 3 — Algorithme
    │       └── scorer.py                    # 📐 Agent 4 — Calcul
    │
    ├── cv_engine/                           ── M3 ──
    │   ├── router.py                        # POST /prepare-cv-data
    │   ├── service.py
    │   ├── schemas.py                       # CVDataJson · ATSResult
    │   └── template_formatter.py            # 📐 Agent 5 — JSON
    │
    ├── email_engine/                        ── M4 ──
    │   ├── router.py                        # POST /generate-email · GET /inbox
    │   ├── service.py
    │   ├── schemas.py                       # EmailInput · EmailOutput
    │   ├── email_composer.py                # 🦜 Agent 6 — LLM
    │   └── email_monitor.py                 # 📐 API Gmail → polling OAuth
    │
    └── chatbot/                             ── M5 ──
        ├── router.py                        # WS /ws/chat
        ├── service.py                       # 🦜 LangChain ConversationChain
        ├── schemas.py                       # ChatMessage · QuestionEntretien
        └── prompts/
            ├── interview_prep.py
            └── contextual_qa.py
```

---

## ✉️ Flux OAuth Gmail — "Just-in-Time" (Suivi & Relance)

> L'envoi et la surveillance des emails utilisent **l'API Gmail avec OAuth 2.0**, isolé de l'authentification Keycloak.

```
Étape 0 — L'utilisateur clique sur "Activer le suivi email" dans l'onglet Candidature
              │
Étape 1 — Demande des scopes Gmail (gmail.readonly + gmail.send)
              │  Paramètres : access_type=offline + prompt=consent
              │
Étape 2 — Google renvoie un code → .NET l'échange contre Access + Refresh Token
              │  Stockage isolé dans PostgreSQL (table email_credentials), PAS dans Keycloak
              │
Étape 3 — Worker Python (email_monitor.py) tourne en boucle (toutes les 15 min)
              │  Lit le refresh_token depuis la base → requiert un Access Token frais
              │  Interroge l'API Gmail → détecte les réponses des entreprises
              │
Étape 4 — Relance J+7 : Hangfire détecte → Notification SignalR → Validation utilisateur
              │  L'email n'est JAMAIS envoyé automatiquement (validation manuelle obligatoire)
              └─ .NET envoie via l'API Gmail (scope gmail.send)
```

**Avantages de l'isolation** :

- ✅ **Confiance** : L'utilisateur comprend pourquoi il donne accès (contexte Candidature)
- ✅ **Flexibilité** : Changement de fournisseur (Gmail → Outlook) sans toucher Keycloak
- ✅ **Sécurité** : Le worker Python fonctionne même si la session Keycloak expire
- ✅ **Indépendance** : Auth applicative et auth email sont deux systèmes séparés

---

## ⭐ Score ATS (algorithme déterministe)

> Le score ATS **n'est pas calculé par un LLM**. C'est une **fonction algorithmique** qui retourne un entier entre 0 et 100.

```
Entrées :
  - keywords_offre   : liste de mots-clés extraits du JSON analyse offre
  - texte_cv         : contenu textuel du CV généré

Algorithme :
  1. Normaliser les deux textes (minuscules, sans accents via unidecode)
  2. Pour chaque keyword_offre → chercher présence dans texte_cv
  3. score = (nb_keywords_trouvés / nb_keywords_total) × 100
  4. Bonus pondéré :
       - Mot-clé dans le TITRE     → +5 pts
       - Mot-clé dans le RÉSUMÉ    → +3 pts
       - Mot-clé dans COMPÉTENCES  → +2 pts
       - Mot-clé dans EXPÉRIENCES  → +1 pt

Sorties :
  {
    "score_ats"         : 78,
    "keywords_presents" : ["Angular", "TypeScript", "Docker"],
    "keywords_manquants": [".NET Core"],
    "recommandations"   : ["Ajouter .NET Core dans compétences"]
  }
```

---

## ⭐ Relance automatique candidature (J+7)

> Si une candidature est à l'état `ENVOYE` depuis **plus de 7 jours** sans réponse, le système prépare une relance.

```
Hangfire (FollowUpJob.cs) — S'exécute toutes les 24 heures
    │
    ├── SELECT * FROM candidatures
    │   WHERE statut = 'ENVOYE'
    │   AND date_envoi < NOW() - INTERVAL '7 days'
    │   AND relance_envoyee = false
    │
    ├── Pour chaque candidature trouvée :
    │   ├── POST /generate-email (type = "relance")
    │   │   → Python LangChain prépare le texte de relance
    │   └── Notification SignalR → Angular 🔔
    │       "Relance prête pour l'entreprise X, voulez-vous l'envoyer ?"
    │
    └── Validation manuelle de l'utilisateur (Clic "Envoyer") :
        ├── .NET récupère le Refresh Token depuis la table EmailCredentials
        ├── .NET requiert un nouvel Access Token via l'API OAuth Google
        ├── .NET envoie l'email de relance via l'API Gmail (scope gmail.send)
        └── UPDATE candidatures SET relance_envoyee = true
```

> 🔒 L'email de relance n'est **JAMAIS envoyé automatiquement**. Validation manuelle obligatoire.

---

## 🔄 Flux de données — étape par étape

```
1.  Angular  →  login Keycloak → JWT token
2.  Angular  →  données profil (JSON) → .NET → PostgreSQL
3.  Angular  →  choix template + colle offre (JD) → .NET OfferController
4.  DEBUT PIPELINE AUTOMATISÉ :
    - .NET  →  POST /run-pipeline → Python (LangGraph)
    - Agent 1 : Analyse offre (LLM)
    - Agent 2 : Récupération profil (RAG pgvector)
    - Agent 3 : Normalisation (Algorithme)
    - Agent 4 : Score matching + ATS (Algorithme)
    - Agent 5 : Formatage template (JSON)
    - Agent 6 : Rédaction email (LLM)
5.  FIN PIPELINE → .NET génère le PDF via QuestPDF
6.  Angular affiche : CV PDF + Score ATS + Email rédigé
7.  L'utilisateur valide → .NET envoie via API Gmail (OAuth)
8.  Python (email_monitor.py) → surveille les réponses via API Gmail
9.  .NET  →  màj Candidature + HistoriqueStatut
10. .NET  →  SignalR → Angular (notification temps réel)
11. .NET  →  FollowUpJob : si J+7 sans réponse → notification + relance si validée
12. Angular  →  WebSocket /ws/chat → FastAPI chatbot → Q&A entretien
```

---

## 📡 Endpoints API

### Backend .NET (`/api/*`)

| Méthode | Endpoint | Module | Description |
|:--------|:---------|:-------|:------------|
| `POST` | `/api/auth/me` | Auth | Infos utilisateur connecté |
| `GET` | `/api/profile` | Profile | Lire le profil complet |
| `POST` | `/api/profile` | Profile | Créer le profil |
| `PUT` | `/api/profile` | Profile | Mettre à jour le profil |
| `POST` | `/api/offers/submit` | Offer | Soumettre offre + lancer pipeline |
| `GET` | `/api/offers/:id/analysis` | Offer | Résultat analyse IA |
| `POST` | `/api/cv/generate` | CV | Lancer génération PDF |
| `GET` | `/api/cv/:id/download` | CV | Télécharger le PDF |
| `POST` | `/api/emails/send` | Email | Envoyer email (après validation) |
| `GET` | `/api/candidatures` | Candidature | Liste des candidatures |
| `PUT` | `/api/candidatures/:id/status` | Candidature | MAJ statut |
| `GET` | `/api/notifications` | Notification | Liste notifications |
| `PUT` | `/api/notifications/:id/read` | Notification | Marquer comme lu |

### Agents Python (`http://agents-python:8000`)

| Méthode | Endpoint | Service | Description |
|:--------|:---------|:--------|:------------|
| `POST` | `/run-pipeline` | pipeline | Lance le pipeline multi-agent (6 agents) |
| `POST` | `/analyze-offer` | ai_agent | Analyse LLM de l'offre → JSON |
| `POST` | `/match` | ai_agent | Score matching profil ↔ offre |
| `POST` | `/prepare-cv-data` | cv_engine | Prépare JSON CV pour QuestPDF |
| `POST` | `/generate-email` | email_engine | Génère email candidature (ou relance) |
| `GET` | `/inbox` | email_engine | Statut boîte mail (API Gmail) |
| `WS` | `/ws/chat` | chatbot | WebSocket chatbot entretien |
| `GET` | `/health` | — | Health check |

---

## 🗄️ Base de données — Schéma

### Instance unique PostgreSQL (2 schémas)

```
nextstep_db (PostgreSQL 15 + pgvector)
├── Schema: public          → Données applicatives
└── Schema: keycloak_schema → Données Keycloak (géré automatiquement)
```

### Tables principales (Schema `public`)

| Table | Champs clés | Relations |
|:------|:------------|:----------|
| `utilisateurs` | id, keycloak_id, email, nom, prenom | 1:1 → profils |
| `profils` | id, utilisateur_id, titre, resume, telephone, ville | 1:N → competences, experiences, formations, certifications, projets |
| `competences` | id, profil_id, nom, niveau | N:1 → profils |
| `experiences` | id, profil_id, titre, entreprise, date_debut, date_fin, description | N:1 → profils |
| `formations` | id, profil_id, diplome, etablissement, annee | N:1 → profils |
| `certifications` | id, profil_id, nom, organisme, date | N:1 → profils |
| `projets` | id, profil_id, titre, description, technologies | N:1 → profils |
| `offres_emploi` | id, utilisateur_id, texte_brut, analyse_json (JSONB), date_creation | N:1 → utilisateurs |
| `cv_generes` | id, utilisateur_id, offre_id, template_id, pdf_path, score_ats, date_creation | N:1 → utilisateurs, offres |
| `candidatures` | id, utilisateur_id, offre_id, cv_id, statut, date_envoi, relance_envoyee | N:1 → utilisateurs |
| `email_credentials` | id, utilisateur_id, provider, access_token, refresh_token, token_expiry, scopes | 1:1 → utilisateurs |
| `historique_statuts` | id, candidature_id, etat_statut, date_mise_a_jour, commentaire | N:1 → candidatures |
| `emails` | id, candidature_id, objet, corps, signature, type, date_envoi | N:1 → candidatures |
| `notifications` | id, utilisateur_id, type, message, lu, date_creation | N:1 → utilisateurs |

---

## 🐳 Docker Compose

| Service | Image | Port | Rôle |
|:--------|:------|:-----|:-----|
| **postgres** | `pgvector/pgvector:pg15` | 5432 | BDD unique (app + Keycloak) + Vector Search |
| **keycloak** | `quay.io/keycloak/keycloak:24.0` | 8080 | Authentification SSO |
| **redis** | `redis:7-alpine` | 6379 | Cache + Hangfire background jobs |
| **backend** | `./backend/Dockerfile` | 5000 | API .NET 8 (Monolithe modulaire) |
| **agents-python** | `./agents/Dockerfile` | 8000 | Pipeline IA (LangGraph + FastAPI) |
| **frontend** | `./frontend/Dockerfile` | 4200 | Angular 19 SPA (Nginx) |

### Ordre de démarrage

```
postgres (healthcheck) → keycloak → redis → backend → agents-python → frontend
```

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

| Élément | Valeur |
|:--------|:-------|
| **Realm** | `nextstep` |
| **Client Angular** | `nextstep-frontend` (Public, PKCE) |
| **Client .NET** | `nextstep-backend` (Confidential) |
| **Rôles** | `USER`, `ADMIN` |
| **Token** | JWT RS256, 5 min access, 30 min refresh |
| **Interceptor** | `auth.interceptor.ts` → inject Bearer auto |

---

## 🔧 Démarrage rapide

```bash
# Tout démarrer avec Docker
docker-compose up -d --build

# Ou service par service :

# 1. Infrastructure
docker-compose up -d postgres keycloak redis

# 2. Backend .NET
cd backend
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

*NextStep — v7.0 — Mars 2026*
*Angular 19 · .NET 8 · Python 3.12 · FastAPI · LangChain · LangGraph · QuestPDF · Keycloak 24 · PostgreSQL (pgvector) · SignalR · Redis · Hangfire · Docker · API Gmail (OAuth 2.0)*
