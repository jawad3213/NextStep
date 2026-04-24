# Spécification Frontend : Refactorisation de la Page Profil — CV Builder

## 1. Objectif de la Refactorisation
L'objectif est de transformer les pages de profil actuelles en une interface de saisie haute performance. On abandonne la prévisualisation en temps réel sur la page principale pour instaurer un **Mode Focus**, permettant une saisie fluide et une meilleure gestion des ressources côté client.

## 1. Architecture UI & Mode Focus
L'interface a été refactorisée pour privilégier une saisie haute performance sans la charge visuelle du rendu PDF constant. Le layout est optimisé pour le "Deep Work" rédactionnel.

- **Layout 2 Colonnes (Desktop) :**
    - **Navigation (240px) :** Stepper vertical persistant avec indicateurs d'état.
    - **Saisie (Flex/Center) :** Conteneur de formulaire fluide (max 800px) pour une ergonomie de lecture optimale.
- **Stratégie Responsive :** Le stepper bascule en barre de progression horizontale sur mobile pour libérer 100% du viewport pour le formulaire.

---

## 2. Logique de Navigation (Stepper)
Le stepper gère l'index de l'étape active (`activeStep`) et l'état de validation de chaque section.

| Étape | Icône | Comportement Frontend |
| :--- | :--- | :--- |
| **1. Coordonnées** | `User` | Upload photo avec preview immédiate + Autocomplete géo. |
| **2. Formation** | `Graduation` | Gestion de liste dynamique (FieldArray). |
| **3. Expérience** | `Briefcase` | Trigger IA pour l'optimisation des puces de texte. |
| **4. Compétences** | `Code` | Système de Tags avec sliders de niveau (1-5). |
| **5. Résumé** | `FileText` | Calculateur de score sémantique en temps réel. |
| **6. Projets** | `Folder` | Validation d'URL (GitHub/Live) + Tags technos. |
| **7. Certifications**| `Award` | Fetching automatique des logos d'organismes. |

---

## 3. Gestion des Données & Formulaires (Frontend Only)
Le code repose sur une gestion d'état centralisée et une persistance locale réactive.

- **State Management :** Utilisation d'un store global (ou contexte) pour synchroniser les données entre les 7 étapes.
- **Persistence :** Système de "Silent Save" dans le `localStorage` déclenché au `onBlur` pour prévenir toute perte de données sans rechargement de page.
- **UX Dynamique :** - Réorganisation des listes par **Drag & Drop** (via `dnd-kit`).
    - Validation `onChange` pour les formats critiques (Email, Tel, URL).

---

## 4. Intégrations IA (Interactions Client)
L'IA est traitée comme un service d'assistance asynchrone intégré directement dans les inputs.

- **Optimizer Client-Side :** Appel à l'API de génération lors du clic sur "✨ Améliorer", avec gestion d'un état de chargement local (Skeleton/Spinner) sur la textarea cible.
- **Scoring Engine :** Composant de jauge visuelle réagissant à la densité et à la structure du texte saisi dans la section Résumé.
- **Mapping d'Import :** Logique de distribution des données parsées (depuis PDF/LinkedIn) vers les champs correspondants du state frontend.

---

## 5. Système d'Aperçu Déporté
La preview n'étant plus dans le flux principal, elle est gérée comme une vue superposée.

- **Modal d'Aperçu :** Un composant `Portal` ou une modal plein écran déclenchée par le bouton sticky en barre de navigation.
- **Preview Renderer :** Rendu dynamique du template choisi via un switch de composants CSS-in-JS ou modules CSS dédiés à chaque style (Classique, Moderne, etc.).
- **Debounce de rendu :** Le rendu dans la modal ne se déclenche qu'à l'ouverture pour économiser les ressources navigateur.

---

## 6. Navigation & Feedback Bas de Page
Barre de contrôle fixe en bas de viewport pour faciliter le passage entre les sections.

```text
[ Indicateur Sauvegarde ]      [ ← Précédent ]  [ 👁 Aperçu ]  [ Suivant → ]