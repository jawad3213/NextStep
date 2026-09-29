# Backend modules

The backend is a **modular monolith**: one deployable, split into modules that each own their
code and their data. Any module can later become a separate service without changing the others.

## Modules and data ownership

| Module | Owns | PostgreSQL schema |
|---|---|---|
| **Profile** | users, experience, education, projects, skills, certifications, skill keywords | `profile` |
| **Applications** | job offers, applications (candidatures), notes, status history, generated documents | `applications` |
| **CvDocuments** | CV templates, CV history, HTML/PDF rendering | `cv` |
| **Messaging** | email drafts, Gmail connections and OAuth, reply monitoring jobs | `messaging` |
| **Coaching** | interview sessions and questions, salary coach, SN copilot | `coaching` |
| **Sourcing** | job-board scraping sessions and sourced offers | `sourcing` |
| *Python agents* | offer analysis, company intelligence, matching results | `agents` |

Dependencies are one-way, with no cycles:
`Profile ← Applications ← CvDocuments ← Messaging`. Coaching and Sourcing use Profile and Applications.

## Inside a module

Every module has the same layout:

```
Modules/<Module>/
├── Contracts/                  public API for the other modules (interfaces + records)
├── Api/                        controllers only: resolve the user, call a service, return a typed DTO
├── Application/
│   ├── Dtos/                   request and response DTOs (the JSON the frontend sees)
│   ├── Services/               use cases, one service per feature, with their interfaces
│   ├── Mappings/               entity → DTO mapping (extension methods such as ToDto())
│   ├── Validation/             input rules that throw BadRequestException
│   ├── EventHandlers/          reactions to other modules' integration events
│   └── Jobs/                   background jobs (Hangfire)
├── Domain/                     entities and enums
├── Infrastructure/
│   ├── Persistence/            <Module>DbContext, Migrations/, seeders
│   ├── Repositories/
│   └── Gmail/ Agents/ Rendering/ JobBoards/   clients of external systems
└── <Module>Module.cs           registers the module (DbContext, services, handlers)
```

Dependencies point inward: `Api → Application → Domain`. Domain uses nothing else in the module and
Application never uses Api.

### Errors

Services report expected failures by throwing an exception from `Shared/ErrorHandling/AppExceptions.cs`:
`NotFoundException` (404), `ForbiddenException` (403), `UnauthorizedException` (401), `BadRequestException` (400),
`ConflictException` (409), `UpstreamServiceException` (502) or `OperationFailedException` (500, safe message).
`GlobalExceptionHandler` turns them into the standard body `{ "error": "<message>", "type": ..., "traceId": ... }`,
so controllers have no try/catch for error translation. Confirmations use `Shared/Api/MessageResponse`
(`{ "message": ... }`); no endpoint returns an anonymous object.

## Rules (enforced by `backend/tests/ModuleBoundaryTests.cs`)

1. **A module uses another module only through its `Contracts` folder.** It never uses another module's
   `Models`, `Services`, `Repositories` or `DbContext`.
   - `Profile/Contracts/IProfileApi`: resolve the current user, read the candidate profile.
   - `Applications/Contracts/IApplicationsApi`: offers, applications, response tracking, CV document.
   - `CvDocuments/Contracts/ICvDocumentsApi`: find a final CV and get its PDF.
2. **Each module has its own `DbContext`** (`Modules/<Module>/Infrastructure/Persistence/<Module>DbContext.cs`) that maps only its
   own tables, in its own schema.
3. **No foreign keys between modules.** Another module's row is referenced by id only (e.g.
   `email_draft.id_candidature`). Foreign keys inside a module are fine.
4. **Contracts expose only contract types** (records such as `ApplicationSnapshot`), never entities.

When one module's change must affect another module's data, the first module publishes an
**integration event** (`Shared/Events`). The modules that care react in their own schema. Example: deleting
applications publishes `CandidaturesDeleted`; Messaging then deletes the drafts and Coaching deletes the sessions.
Events are dispatched in-process today, and would go through a message broker once modules are separate services.

## Database schema: EF migrations per module

The schema has a single source: each module's EF migrations (`Modules/<Module>/Infrastructure/Persistence/Migrations`).
At startup, `Shared/Persistence/DatabaseInitializer` applies every module's migrations, then runs the module seeders
(`IModuleSeeder`: skill keywords, CV templates, storage bucket).

Add a migration after changing a module's entities or `DbContext`:

```bash
dotnet ef migrations add <Name> --context <Module>DbContext --output-dir Modules/<Module>/Infrastructure/Persistence/Migrations
```

(from `backend/`; `dotnet tool install --global dotnet-ef` once). A test fails when a model change has no migration.

**Databases created before the modular split** (all tables in `public`) are upgraded automatically, once, by
`LegacySchemaUpgrader`. It moves each table into its module's schema, drops the cross-module foreign keys,
and marks the first migration as applied. The Python agents move their own tables into `agents` the same way
(`agents/app/core/schema_bootstrap.py`).

## Python agents and the database

The agents own the `agents` schema only. They reach profiles and applications through internal
backend endpoints, never through the tables (`agents/app/core/backend_client.py`):

| Endpoint | Module | Used by |
|---|---|---|
| `GET /internal/agents/users/{userRef}` | Profile | chatbot (user id) |
| `GET /internal/agents/profiles/{userRef}` | Profile | profile retriever (CV pipeline, match), SN Copilot |
| `GET /internal/agents/users/{userRef}/candidatures` | Applications | SN Copilot, chatbot |
| `POST …/candidatures`, `…/candidatures/{id}/status`, `…/candidatures/{id}/notes` | Applications | SN Copilot (history source `ai_sn`) |
| `GET …/offers/{offerId}/analysis` | Applications | chatbot (offer context) |

`userRef` is the local user id or the Keycloak subject. These endpoints take no user token: they require
the shared secret `AGENTS_API_KEY` in `X-Internal-Api-Key` (`[InternalApiKey]`), the same secret the
backend sends to the agents.

The interview coach's sessions and questions are saved by the Coaching module (`ArenaService`):
it reuses questions already generated for the same application or Arena configuration, creates
the interview session before calling the agents, and stores the score and feedback they return.
The agents only compute; they no longer write any backend table.

## Adding a module

1. Create `Modules/<Name>/` with the layout above: `Infrastructure/Persistence/<Name>DbContext.cs` (inherit
   `ModuleDbContext`, own schema), and `<Name>Module.cs` with `Add<Name>Module(...)`.
2. Register it in `Shared/Config/DependencyInjection.cs`.
3. Add it to `Modules` and `ModuleContexts()` in `ModuleBoundaryTests`, then create its first migration.
