# Architecture des Agents IA & Orchestration (LangGraph)

Ce document décrit comment structurer et implémenter les agents IA au sein du projet **NextStep**, en utilisant **LangChain** pour la logique des agents et **LangGraph** pour l'orchestration complexe.

---

## 1. Vision Globale de l'Architecture

L'orchestration repose sur un système découplé :
1.  **Backend .NET** : Gère les données métier, l'authentification et les API REST.
2.  **Service Agents (Python)** : Héberge l'intelligence, les graphes de décision et les appels aux LLM.
3.  **LangGraph** : Sert de "cerveau" pour orchestrer quel agent doit agir à quel moment.

---

## 2. Structure de Dossiers Recommandée

Pour maintenir la clarté, nous recommandons la structure suivante dans le dossier `agents/` :

```text
agents/
├── app/
│   ├── core/               # Configuration (LLM, variables d'env)
│   ├── schemas/            # Modèles Pydantic (State, API requests/responses)
│   ├── tools/              # Fonctions outils (ex: recherche, appel API .NET)
│   ├── agents/             # Définition individuelle des agents (prompts)
│   └── graphs/             # Définition des graphes LangGraph
├── main.py                 # Point d'entrée FastAPI
└── requirements.txt        # Dépendances (LangChain, LangGraph, etc.)
```

---

## 3. Étape 1 : Définition du "State" (État du Graphe)

Avec LangGraph, tout tourne autour d'un objet `State` qui circule entre les agents.

**Fichier : `app/schemas/state.py`**
- Définit les données partagées (historique des messages, données utilisateur, flags de décision).
- Utilise `Annotated` et `operator.add` pour gérer l'accumulation des messages.

---

## 4. Étape 2 : Création des Agents (LangChain)

Un agent est essentiellement une fonction ou un "runnable" qui prend le `State` en entrée et retourne une mise à jour de cet état.

**Fichier : `app/agents/profile_agent.py`**
- **Prompt** : Définit la personnalité et les instructions de l'agent.
- **LLM Binding** : Lie le LLM (OpenAI, Groq) aux outils spécifiques à cet agent.
- **Logique** : L'agent analyse le contexte et décide s'il doit utiliser un outil ou répondre directement.

---

## 5. Étape 3 : Définition des Outils (Tools)

Les outils permettent aux agents d'interagir avec le monde réel (votre backend .NET).

**Fichier : `app/tools/backend_api.py`**
- Fonctions décorées avec `@tool`.
- Exemple : `get_user_profile(user_id: str)` qui fait un appel `httpx` vers le backend .NET.

---

## 6. Étape 4 : Orchestration avec LangGraph

C'est ici que l'on définit le flux de travail (workflow).

**Fichier : `app/graphs/main_workflow.py`**
- **Nodes (Noeuds)** : Chaque agent ou outil est un noeud.
- **Edges (Arêtes)** : Définissent le chemin (ex: de l'Agent A vers l'Agent B).
- **Conditional Edges** : Logique de décision (ex: "Si l'utilisateur demande une lettre de motivation, aller vers `CoverLetterAgent`, sinon rester sur `GeneralAgent`").

---

## 7. Résumé du Flux d'Exécution

1.  **Réception** : FastAPI reçoit une requête de l'utilisateur.
2.  **Initialisation** : Le graphe LangGraph est initialisé avec un état vide ou chargé depuis une DB.
3.  **Boucle de Réflexion** :
    - Le noeud "Router" décide quel agent appeler.
    - L'agent sélectionné génère une réponse ou appelle un outil.
    - Si un outil est appelé, le résultat est réinjecté dans l'état.
4.  **Finalisation** : Une fois que le graphe atteint un point d'arrêt (END), la réponse finale est renvoyée au frontend via FastAPI.

---

## 8. Pourquoi cette approche ?

- **Cyclique** : Contrairement aux chaînes LangChain classiques, LangGraph permet de revenir en arrière ou de boucler jusqu'à ce qu'un objectif soit atteint.
- **Stateful** : La mémoire est gérée de manière native et persistante.
- **Modulaire** : On peut ajouter un nouvel agent spécialisé (ex: Expert LinkedIn) simplement en ajoutant un noeud au graphe.
