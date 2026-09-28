# Frontend structure

The Angular app (`frontend/src/app`) is organised **by feature**. Each feature owns its pages,
its components, its HTTP calls and its types. Code that every feature uses lives in `core/` or
`shared/`.

```
src/app/
├── app.routes.ts          top-level routes: guards + lazy loadChildren / loadComponent
├── core/                  app-wide singletons (one instance for the whole app)
│   ├── auth/              Keycloak session (AuthService), onboarding status
│   ├── guards/            auth and onboarding guards
│   ├── http/              api-url helpers, error interceptor, extractApiError, PagedResponse
│   ├── layout/            main layout, sidebar, theme
│   └── notifications/     toasts
├── shared/                reusable building blocks with no business logic (pipes, …)
└── features/<feature>/
    ├── <feature>.routes.ts    the feature's child routes (only for features with several pages)
    ├── pages/<page>/          routed components
    ├── components/<name>/     components used by the feature's pages
    └── data-access/           *-api.service.ts (HTTP), *.models.ts (types), state services
```

Path aliases: `@core/*`, `@shared/*`, `@features/*`, `@env/*` (in `tsconfig.json`, mirrored
in `vitest.config.ts`).

## Features

| Feature | Routes | Notes |
|---|---|---|
| `applications` | `/applications`, `/applications/:id`, `/letters`, `/letters/:id` | candidatures, email drafts, letters; board rules in `pages/applications/candidature-board.ts` |
| `offers` | `/offers`, `/offers/analyze`, `/offers/:id`, `/sourced-offers`, `/sourced-offers/:id` | analysis pipeline (stepper + steps), resume editor, SignalR pipeline state |
| `profile` | `/profile` | `ProfileService` = UI state, `ProfileApiService` = HTTP, `profile.mapper` = DTO ↔ UI |
| `cv-builder` | `/cv-builder` | CV templates, history, PDF export (`CvApiService`) |
| `company-intel` | `/offers/company-analysis`, company overview | agent-produced company analysis |
| `chatbot`, `sn-copilot` | arena, copilot drawer | AI coaching |
| `dashboard`, `settings`, `skill-gap`, `notifications`, `onboarding`, `signup`, `landing` | one page each | |

## Rules

1. **HTTP only in `data-access/*-api.service.ts`** (or a feature's data service). Components
   never inject `HttpClient`.
2. **URLs come from `@core/http/api-url`**: `apiUrl('/offers')` for `/api/...` calls,
   `backendOrigin()` for the few non-`/api` endpoints. Components never read
   `environment.apiBaseUrl`.
3. **Typed responses.** Each API method returns a type from the feature's `*.models.ts`.
   `unknown` is used only for raw agent JSON that a normaliser then reads.
4. **Pure logic lives outside components**, as plain functions next to the component that
   uses them. Examples:
   - `resume-editor/cv-hydration.ts` merges the AI output into the editor's CV.
   - `step-generation/cv-payload.normalizer.ts` builds the CV payload sent to the backend.
   - `profile/data-access/profile-import.normalizer.ts` normalises the CV/LinkedIn import.
   - `offers/utils/cv-json.utils.ts` holds helpers the two CV modules share.
5. **Every route is lazy.** A feature with several pages exposes `<FEATURE>_ROUTES`, and
   `app.routes.ts` loads them with `loadChildren`.
6. **Tests sit next to the file they test** (`x.ts` → `x.test.ts`) and run with
   `npx vitest run`.
7. **Features don't reach into each other's `pages/` or `components/`.** When one feature
   needs another's data, it imports that feature's `data-access/`, for example
   `applications` using `OfferApiService`. The one exception is `core/layout/main-layout`,
   which hosts the offers stepper and the SN copilot drawer.
