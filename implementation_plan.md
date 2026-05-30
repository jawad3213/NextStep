# Email Module — Update/Approve/Send Flow

## Background

The existing Email module already handles:
- `POST /api/emails/generate` → calls Python agent → saves `EmailDraft` (IsApproved=false, IsSent=false)
- `GET /api/emails/candidature/{id}` → list drafts

**What's missing:**
- Update draft endpoint
- Approve draft endpoint
- Send (Gmail) endpoint
- `IEmailSenderService` abstraction + `GmailEmailSenderService` skeleton
- Missing `EmailDraft` fields: `ApprovedAtUtc`, `ProviderMessageId`, `SendAttemptCount`
- A bug: `RecipientEmail` is currently set to `user.Email` (the candidate's OWN email) — must be fixed to `null`

---

## Key Findings from Inspection

| Item | Finding |
|---|---|
| **Auth pattern** | `User.FindFirstValue("sub")` → keycloak ID → `IUserRepository.GetByKeycloakIdAsync()` |
| **Ownership check** | Load `Candidature` → compare `IdUtilisateur` == local user `Id` |
| **DB schema style** | Manual `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` in `Program.cs` startup (no EF migrations) |
| **RecipientEmail bug** | `EmailService.cs` line 195: `RecipientEmail = user.Email` — sets it to candidate's own email ❌ |
| **Missing fields** | `ApprovedAtUtc`, `ProviderMessageId`, `SendAttemptCount` not in model/DB |
| **csproj** | .NET 10, no Gmail/Google NuGet packages yet |

---

## Proposed Changes

### 1. `EmailDraft` Model — add 3 fields

#### [MODIFY] [EmailDraft.cs](file:///d:/Projects/NextStep/backend/Modules/Email/Models/EmailDraft.cs)
- Add `ApprovedAtUtc: DateTime?`
- Add `ProviderMessageId: string?`
- Add `SendAttemptCount: int`

---

### 2. `AppDbContext` — map the 3 new fields

#### [MODIFY] [AppDbContext.cs](file:///d:/Projects/NextStep/backend/data/AppDbContext.cs)
- Map `ApprovedAtUtc` → column `date_approbation`
- Map `ProviderMessageId` → column `provider_message_id`
- Map `SendAttemptCount` → column `send_attempt_count`

---

### 3. `Program.cs` — patch schema at startup

#### [MODIFY] [Program.cs](file:///d:/Projects/NextStep/backend/Program.cs)
- Add 3 `ALTER TABLE` calls for the new `email_draft` columns

---

### 4. New DTOs

#### [NEW] `UpdateEmailDraftDto.cs`
- `string? RecipientEmail`
- `string? Subject`
- `string? Body`

#### [NEW] `SendEmailResultDto.cs`
- `bool Success`
- `Guid DraftId`
- `string? ProviderMessageId`
- `string? ErrorMessage`
- `DateTime? SentAtUtc`

#### [MODIFY] `EmailDraftDto.cs`
- Add `ApprovedAtUtc: DateTime?`
- Add `ProviderMessageId: string?`
- Add `SendAttemptCount: int`

---

### 5. `IEmailSenderService` + `SendEmailResult` — NEW files

#### [NEW] `IEmailSenderService.cs` (in `Modules/Email/Services/`)
```csharp
Task<SendEmailResult> SendAsync(Guid userId, string recipientEmail, string subject, string body, CancellationToken ct);
```

#### [NEW] `SendEmailResult.cs` (in `Modules/Email/Services/`)
```csharp
bool Success
string? ProviderMessageId
string? ErrorMessage
```

#### [NEW] `GmailEmailSenderService.cs` (in `Modules/Email/Services/`)
- Throws `NotImplementedException("Gmail OAuth connection is not implemented yet.")`
- Correctly structured with all method signatures for future implementation

---

### 6. `IEmailService` + `EmailService` — add 3 new methods

#### [MODIFY] [IEmailService.cs](file:///d:/Projects/NextStep/backend/Modules/Email/Services/IEmailService.cs)
- `Task<EmailDraftDto> UpdateDraftAsync(Guid draftId, Guid userId, UpdateEmailDraftDto dto, CancellationToken ct)`
- `Task<EmailDraftDto> ApproveDraftAsync(Guid draftId, Guid userId, CancellationToken ct)`
- `Task<SendEmailResultDto> SendDraftAsync(Guid draftId, Guid userId, CancellationToken ct)`

#### [MODIFY] [EmailService.cs](file:///d:/Projects/NextStep/backend/Modules/Email/Services/EmailService.cs)
- Fix `RecipientEmail = null` (was incorrectly `user.Email`)
- Inject `IEmailSenderService`
- Implement `UpdateDraftAsync` — load, verify ownership, reject if sent, update fields
- Implement `ApproveDraftAsync` — load, verify ownership, reject if sent, validate fields, set flags
- Implement `SendDraftAsync` — load, verify ownership, reject if not approved/already sent, call sender
- Update `MapToDto` to include new fields

---

### 7. `EmailController` — add 3 new endpoints

#### [MODIFY] [EmailController.cs](file:///d:/Projects/NextStep/backend/Modules/Email/Controllers/EmailController.cs)
- Inject `IUserRepository`
- Add `PUT /api/emails/drafts/{draftId}` → `UpdateDraft`
- Add `POST /api/emails/drafts/{draftId}/approve` → `ApproveDraft`
- Add `POST /api/emails/drafts/{draftId}/send` → `SendDraft`
- All endpoints extract `keycloakId` → resolve `userId` → pass to service

---

### 8. `Program.cs` DI — register `IEmailSenderService`

#### [MODIFY] [Program.cs](file:///d:/Projects/NextStep/backend/Program.cs)
- `builder.Services.AddScoped<IEmailSenderService, GmailEmailSenderService>()`

---

## Ownership Check Logic

```
keycloakId = User.FindFirstValue("sub")
user = IUserRepository.GetByKeycloakIdAsync(keycloakId)
draft = IEmailDraftRepository.GetByIdAsync(draftId)
candidature = ICandidatureRepository.GetByIdAsync(draft.CandidatureId)
if (candidature.IdUtilisateur != user.Id) → 403 Forbidden
```

---

## Bug Fix

`EmailService.GenerateDraftAsync` line 195:
```diff
- RecipientEmail = user.Email,
+ RecipientEmail = null,   // Recruiter email — must be set by user before sending
```

---

## Verification Plan

Build: `dotnet build` from `backend/`

Manual tests (documented in walkthrough):
1. POST /api/emails/generate → draft created, RecipientEmail=null
2. PUT /api/emails/drafts/{id} → fields updated
3. POST /api/emails/drafts/{id}/send (unapproved) → 400
4. POST /api/emails/drafts/{id}/approve → IsApproved=true
5. POST /api/emails/drafts/{id}/send → GmailEmailSenderService throws NotImplementedException (500 with clear message)
6. Try sending same draft again → 400 Already sent

> [!IMPORTANT]
> Gmail sending is intentionally stubbed. The `NotImplementedException` will surface as a 500 error with a clear message. A TODO comment in the service explains what needs to be implemented.

> [!NOTE]
> No EF Core migrations are used. New columns are added via `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` in `Program.cs` startup — consistent with existing pattern.
