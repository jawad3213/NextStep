# 🚀 NextStep

> **Accompagner l'utilisateur dans l'optimisation de ses candidatures académiques et professionnelles (stages PFA/PFE, premiers emplois).**

NextStep est une application intelligente et modulaire destinée à propulser la carrière des étudiants et jeunes diplômés. Grâce à une architecture robuste et à l'aide de l'Intelligence Artificielle multi-agents, l'application offre une assistance complète allant de l'analyse des offres d'emploi à la préparation des entretiens, le tout dans un environnement sécurisé et respectueux de la vie privée.

---

## 🌟 Capacités Principales

### ① Analyse d'Offre
- **Reconnaissance Intelligente** : Identifier les mots-clés et compétences requises à partir d'une description.
- **Classification** : Déterminer le type de poste et le niveau de séniorité attendu.
- **Extraction** : Extraire précisément les exigences techniques et comportementales recherchées.

### ② Génération de CV (Sur-mesure)
- **Adaptation Stratégique** : Adapter le résumé professionnel pour qu'il réponde directement à l'offre ciblée.
- **Organisation Pertinente** : Réordonner les compétences par ordre de pertinence décroissante.
- **Maximisation d'Impact** : Reformuler les expériences passées pour capter l'attention des recruteurs.
- **Design Cohérent** : Respecter les contraintes visuelles et structurelles du template sélectionné.

### ③ Suivi des Candidatures
- **Communication** : Rédiger des emails d'accompagnement personnalisés (soumis à validation).
- **Gestion de Pipeline** : Mettre à jour et suivre les statuts de candidature (En attente, Retenu, Refusé, Entretien).
- **Insights** : Générer des statistiques détaillées et des recommandations d'amélioration.

### ④ Préparation aux Entretiens
- **Simulations Personnalisées** : Générer des questions d'entretien contextualisées (basées sur l'offre, le profil et l'entreprise).
- **Réponses Structurées** : Proposer des suggestions de réponses basées sur la méthode STAR (Situation, Task, Action, Result).
- **Niveau Adaptatif** : Ajuster dynamiquement la difficulté des questions en fonction de l'ancienneté requise pour le poste.

---

## 🏗️ Architecture du Système

Le projet repose sur 3 grandes composantes, permettant une scalabilité, une forte isolation des responsabilités et des performances analytiques de pointe.

### 🗂️ Arborescence Globale

```text
📦 NextStep
├── 📂 frontend/                  # Interface Utilisateur (Angular)
├── 📂 Backend/                   # API Monolithe (.NET)
└── 📂 analytics-service/         # IA & Analytique (Python Multi-Agent)
```

### 1. Frontend Angular (Interface Utilisateur)
SPA (Single Page Application) développée en **TypeScript** et **Angular** structurée en trois grandes zones :
- `core/` : Services globaux (Singletons), Guards pour l'authentification et Interceptors HTTP.
- `shared/` : Composants UI purs et réutilisables (Boutons, Modals, Loaders) et Directives custom.
- `features/` : Logique métier encapsulée par domaines (Users, Products, Analytics) via des modules "lazy-loadés".

**Structure cible du Frontend (`frontend/src/app/`) :**
```text
📦 app/
├── 📂 core/                     # 🔒 Services globaux, singletons
│   ├── 📂 services/             
│   │   ├── 📄 api.service.ts    # Client HTTP générique
│   │   ├── 📄 auth.service.ts   # Authentification & tokens
│   │   └── 📄 user.service.ts   # Gestion utilisateur global
│   ├── 📂 guards/               
│   │   └── 📄 auth.guard.ts     # Route guards (auth, rôles)
│   ├── 📂 interceptors/         
│   │   └── 📄 jwt.interceptor.ts# HTTP interceptors (JWT, erreurs)
│   └── 📂 models/               
│       └── 📄 base.dto.ts       # DTOs partagés
├── 📂 shared/                   # 🔁 Composants UI réutilisables
│   ├── 📂 components/           
│   │   ├── 📄 button.component.ts  # Boutons, modals, loaders…
│   │   └── 📄 loader.component.ts
│   └── 📂 directives/           
│       └── 📄 hover.directive.ts   # Directives Angular custom
├── 📂 features/                 # 📦 Modules fonctionnels par domaine
│   ├── 📂 users/                # Module Utilisateurs
│   │   ├── 📂 components/       
│   │   │   ├── 📄 user-list.component.ts # Composants spécifiques (liste, profil...)
│   │   │   └── 📄 user-profile.component.ts
│   │   ├── 📂 pages/            
│   │   │   └── 📄 users-page.component.ts # Pages routées
│   │   ├── 📂 services/         
│   │   │   └── 📄 user-feature.service.ts # Services scopés à la feature
│   │   └── 📄 user.module.ts
│   └── 📂 products/             # Autre domaine (même pattern)
├── 📄 app-routing.module.ts
└── 📄 app.module.ts
```

**Conventions de nommage (Angular) :**
- **Fichiers** : `kebab-case` (ex: `user-profile.component.ts`, `auth.service.ts`).
- **Classes/Interfaces** : `PascalCase` (ex: `UserProfileComponent`, `AuthService`).
- **Variables/Fonctions** : `camelCase` (ex: `getUserData()`, `isLoggedIn`).

### 2. Backend Monolithe .NET (Logique Métier & Données)
Un backend robuste développé en **.NET**, orchestré autour d'une approche modulaire (Domain-Driven Design).
- **Controllers** : Endpoints REST.
- **Services** : Logique métier.
- **Repositories** : Accès aux données avec Entity Framework Core (EF Core) vers une base de données SQL.
- **Domain** : Entités et règles métier fondamentales.

**Structure verticale cible du Backend (`Backend/`) :**
```text
📦 Backend/
├── 📂 Modules/                  # 📦 Domaines métier (Vertical Slices)
│   ├── 📂 Users/
│   │   ├── 📂 Controllers/      
│   │   │   └── 📄 UserController.cs      # Endpoints (GET, POST...)
│   │   ├── 📂 Services/         
│   │   │   └── 📄 UserService.cs         # Logique métier spécifique
│   │   ├── 📂 Interfaces/       
│   │   │   └── 📄 IUserService.cs        # Contrat d'inversion de dépendance
│   │   ├── 📂 Repositories/     
│   │   │   └── 📄 UserRepository.cs      # Accès à Entity Framework (DB)
│   │   └── 📂 Domain/           
│   │       └── 📄 User.cs                # Entité métier pure
│   └── 📂 Products/             # Même structure verticale pour chaque module
├── 📂 Shared/                   # 🔁 Utilitaires transversaux
│   ├── 📂 DTOs/                 
│   │   └── 📄 UserDTO.cs                 # Objets de transfert partagés
│   ├── 📂 Utils/                
│   │   └── 📄 Logger.cs                  # Logger global, Helpers...
│   └── 📂 Exceptions/           
│       └── 📄 CustomException.cs         # Exceptions globales métiers
├── 📄 Program.cs                # Point d'entrée
└── 📄 appsettings.json          # Configuration
```

**Conventions de nommage (.NET) :**
- **Fichiers / Classes / Interfaces / Méthodes / Propriétés publiques** : `PascalCase` (ex: `UserController.cs`, `GetUserDetails()`, `IUserService`).
- **Variables locales / Paramètres de méthodes** : `camelCase` (ex: `userId`, `userDto`).
- **Champs privés** : `_camelCase` avec un underscore (ex: `_userRepository`).

### 3. Service Analytics Python (AI Multi-Agents)
Un service orienté Data & Intelligence Artificielle composé d'agents spécialisés disposant chacun d'une "Single Responsibility" :
- 🤖 **Ingestion Agent** : Collecte, validation et stockage brut des données.
- 🤖 **Processing Agent** : Nettoyage, enrichissement et normalisation (Structuration).
- 🤖 **Analysis Agent** : Calculs statistiques, KPIs, agrégations.
- 🤖 **Prediction Agent** : Modèles d'Inférence, Machine Learning (Scoring, prévisions).
- 🤖 **Reporting Agent** : Génération de rapports et d'exports (PDF, CSV).
- 🤖 **Notification Agent** : Déclenchements d'alertes, envois d'emails et webhooks.

**Conventions de nommage (Python) :**
- **Fichiers / Modules / Variables / Fonctions** : `snake_case` (ex: `ingestion_agent.py`, `calculate_kpis()`).
- **Classes** : `PascalCase` (ex: `IngestionAgent`, `DataProcessor`).
- **Constantes** : `UPPER_SNAKE_CASE` (ex: `MAX_RETRIES`, `DEFAULT_TIMEOUT`).

Un composant **Orchestrator** gère le routing des tâches et les files d'attentes entre ces agents.

### 🔄 Communication Inter-Services
- **Frontend ↔ Backend** : API REST / HTTPS (Flux synchrone).
- **Backend ↔ Analytics Service** : REST (pour requêtes directes) ou **Message Broker** (Kafka / RabbitMQ) pour l'événementiel (asynchrone).
- **Analytics (Inter-Agents)** : Event-driven et messaging interne (Message broker).

---

## 🔒 Règles Absolues & Déontologie

Ce projet intègre nativement des gardes-fous stricts concernant le traitement des données et l'interaction IA-Humain :

1. **Validation Systématique** : L'IA ne peut générer de communications de manière autonome. **Une validation humaine est toujours requise** avant l'envoi du moindre email.
2. **Confidentialité et Vie Privée** : Protection rigoureuse des données personnelles. **Il est interdit de mémoriser des données sensibles (PII) d'une session à l'autre** sans consentement explicite.
3. **Transparence IA** : L'application signalera de manière claire, esthétique et sans ambiguïté les parties de contenu générées par l'IA vis-à-vis des parties validées/saisies par l'utilisateur.

---

## 💾 Spécifications Techniques

### Format de Sortie Backend (Exemple Analytique)
Lors des processus analytiques (ex. analyse de CV/Offres), le backend et le service Python produiront le schéma structuré (JSON) de base suivant :

```json
{
  "analysedSkills": ["string", "string"],
  "cvSections": {
    "summary": "string",
    "experience": ["..."],
    "education": ["..."]
  },
  "confidenceScore": 0.85 
}
```
*(Le `confidenceScore` varie obligatoirement entre 0 et 1)*.

---

## ⚙️ Comment Accéder à l'Application (Lancement)

Pour faire tourner le projet intégralement sur votre environnement de développement local, assurez-vous d'avoir les prérequis suivants :
- **Node.js** & **npm**
- **.NET 8 SDK** (ou version associée)
- **Python 3.10+**
- **Docker & Docker-Compose** (Pour les environnements multi-agents et RabbitMQ/Kafka)

### Étape 1 : Base de Données & Broker de messages
Lancez les services d'infrastructure (PostgreSQL, SQL Server, RabbitMQ, etc.) via Docker :
```bash
docker-compose up -d
```

### Étape 2 : Lancement du Service Analytics (Intelligence Artificielle)
Depuis le dossier `analytics-service` :
```bash
cd analytics-service
pip install -r requirements.txt
# Ou si lancé via docker
docker-compose -f docker/docker-compose.yml up --build
```

### Étape 3 : Lancement du Backend (.NET)
Depuis le dossier `BackendMonolith` :
```bash
cd Backend
dotnet build
dotnet run
```
*(L'API sera exposée sur l'URL configurée dans `appsettings.json`, ex: `http://localhost:5000`)*

### Étape 4 : Lancement du Client Angular (Frontend)
Ouvrez un nouveau terminal, depuis le dossier racine Angular :
```bash
cd frontend
npm install
npm start
```
Une fois compilée, l'application sera accessible depuis votre navigateur à l'adresse suivante : **http://localhost:4200**.
