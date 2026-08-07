# Task 12: Microsoft Entra ID Authentication & Authorization Migration

> **POST-ROADMAP IDENTITY TASK (2026-08-07)** — This task implements [`architecture/decision-log.md`](../architecture/decision-log.md) D15. The shipped application currently owns usernames, password hashes, and an eight-hour HS256 JWT issuer. Production must instead trust the organization's single Microsoft Entra ID workforce tenant while preserving D11's fail-closed authorization posture, actor-attributed audit trail, SignalR authentication, and Task 11's authenticated cache isolation.

- **Phase**: Identity & Access Hardening (Phase 4C, after Task 11 and before Tasks 09/10)
- **Lead / Owner**: Identity Platform Engineer with Frontend and Backend Security reviewers
- **Complexity**: Very High
- **Prerequisites**: Task 02 (JWT policies and actor attribution), Task 03 (SignalR bearer transport), Task 07 (Azure Terraform), Task 11 (typed auth routes and session boundaries)
- **Status**: Specified
- **Target Files**:
  - `src/Frontend/package.json`
  - `src/Frontend/package-lock.json`
  - `src/Frontend/.env.example`
  - `src/Frontend/src/main.tsx`
  - `src/Frontend/src/app/routes/login.tsx`
  - `src/Frontend/src/lib/auth/msalConfig.ts`
  - `src/Frontend/src/lib/auth/msalInstance.ts`
  - `src/Frontend/src/lib/auth/tokenProvider.ts`
  - `src/Frontend/src/lib/api/client.ts`
  - `src/Frontend/src/lib/realtime/signalRClient.ts`
  - `src/Frontend/src/lib/session/sessionController.ts`
  - `src/Frontend/src/lib/state/useAuthStore.ts`
  - `src/Backend/src/Tradebook.Api/Tradebook.Api.csproj`
  - `src/Backend/src/Tradebook.Api/Program.cs`
  - `src/Backend/src/Tradebook.Api/appsettings.json`
  - `src/Backend/src/Tradebook.Api/Security/AuthenticationServiceCollectionExtensions.cs`
  - `src/Backend/src/Tradebook.Api/Security/ActorId.cs`
  - `src/Backend/src/Tradebook.Api/Features/Auth/Login/` (retired from production)
  - `src/Backend/src/Tradebook.Core/DTOs/AuthDtos.cs` (retired and regenerated into TypeScript)
  - `infra/terraform/versions.tf`
  - `infra/terraform/entra.tf`
  - `infra/terraform/variables.tf`
  - `infra/terraform/outputs.tf`
  - `infra/terraform/containerapp.tf`
  - `infra/terraform/README.md`
  - `docs/architecture/master-architecture-blueprint.md`
  - `docs/operations/production-runbook.md`
  - `tests/Tradebook.UnitTests/Authentication/`
  - `tests/Tradebook.IntegrationTests/Authentication/`
  - `src/Frontend/tests/auth/`
  - `tests/e2e/src/specs/entra-authentication.spec.ts`

---

## 1. Detailed Scope & Feature Coverage

### 1.1 Objective

Replace Tradebook's production-owned password and symmetric JWT issuer with Microsoft Entra ID without weakening any existing boundary:

- users authenticate through the organization's Microsoft tenant and inherit its MFA, Conditional Access, sign-in risk, and account lifecycle controls;
- the React SPA obtains delegated access tokens for the Tradebook API through authorization code + PKCE;
- the API accepts only tokens issued for its own app registration and tenant;
- every user API request has the delegated `access_as_user` scope and one of the existing Tradebook app roles;
- audit and dashboard ownership use the validated Entra `oid` UUID as the actor;
- access tokens never become Zustand state or application-managed persistent browser state;
- SignalR and REST use the same token provider and authorization contract;
- production contains no password-verification or application JWT-signing path.

### 1.2 Current Baseline and Gaps

| Area | Current implementation | Required end state |
| :--- | :--- | :--- |
| Frontend sign-in | Username/password form calls `POST /api/v1/auth/login` | Microsoft sign-in through MSAL React |
| Token ownership | Raw eight-hour JWT stored in Zustand memory | MSAL owns acquisition/renewal; app receives tokens only at call boundaries |
| API issuer | Tradebook signs HS256 tokens using `Jwt:SigningKey` | Entra signs tokens; API validates authority metadata and asymmetric signing keys |
| Actor claim | `sub` must parse as internal UUID | Single-tenant Entra `oid` is the actor UUID; `tid` must equal configured tenant |
| Authorization | Locally stored role strings become JWT `role` claims | Entra API app roles emit `roles`; API also requires delegated scope |
| SignalR | Zustand token passed to `accessTokenFactory` | Async MSAL token provider passed to `accessTokenFactory` |
| Tests | Tests mint production-shaped symmetric JWTs | Test-only authentication handler for hermetic tests; real Entra smoke tests remain separate |
| Infrastructure | Container App receives issuer/audience/signing-key settings | Terraform manages/references Entra registrations and supplies non-secret tenant/client IDs |

### 1.3 Library and Architecture Decision

| Candidate | Decision | Rationale |
| :--- | :--- | :--- |
| `@azure/msal-browser` + `@azure/msal-react` | **Adopt** | Microsoft's supported SPA/React libraries implement authorization code + PKCE, account selection, silent renewal, and Entra interaction-required flows. |
| `Microsoft.Identity.Web` | **Adopt** | Microsoft's ASP.NET Core library validates Entra bearer tokens and integrates scope/app-role requirements with authorization policies. |
| Better Auth Microsoft provider | **Reject for this repository** | Better Auth is a TypeScript server/client session framework. It would require a Node auth runtime or sidecar, its own cookie/session and database contract, and duplicated authority beside the .NET API; its Microsoft provider documentation is also currently on a beta track. |
| `react-oidc-context` / `oidc-client-ts` | **Viable fallback, not selected** | Standards-based and provider-neutral, but offers less Entra-specific guidance and integration than MSAL. Reconsider only if supporting multiple interchangeable OIDC providers becomes a requirement. |
| ASP.NET Core cookie BFF | **Deferred** | Keeps access tokens out of JavaScript, but changes the API from bearer-first to cookie/CSRF semantics and complicates non-browser clients. Reconsider under a threat model that forbids browser-held access tokens. |
| Auth.js, Clerk, Auth0, or another identity authority | **Do not add** | The organization already has Entra ID; another authority adds federation, operational cost, and a second user/session lifecycle without a stated requirement. |

Primary references:

- [MSAL React getting started](https://learn.microsoft.com/en-us/entra/msal/javascript/react/getting-started)
- [Authorization code flow with PKCE](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-auth-code-flow)
- [Protect an ASP.NET Core API with Microsoft.Identity.Web](https://learn.microsoft.com/en-us/entra/msidweb/getting-started/quickstart-webapi)
- [Verify scopes and app roles](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-protected-web-api-verification-scope-app-roles)
- [Entra access-token claims](https://learn.microsoft.com/en-us/entra/identity-platform/access-token-claims-reference)
- [Better Auth installation and server contract](https://better-auth.com/docs/installation)
- [Better Auth Microsoft provider](https://better-auth.com/docs/beta/authentication/microsoft)

### 1.4 In Scope

- Provision two single-tenant Entra registrations: public SPA and protected API.
- Expose one delegated API scope, `access_as_user`.
- Define the exact app-role values `Trader`, `BackOffice`, and `Admin` on the API registration.
- Require explicit enterprise-application assignment and admin consent.
- Add exact-pinned stable MSAL browser/React packages and `Microsoft.Identity.Web`.
- Replace the local sign-in form with Microsoft redirect sign-in and explicit error/loading UI.
- Use one MSAL `PublicClientApplication` and one token-provider abstraction for REST and SignalR.
- Integrate MSAL account identity with Task 11's router guard and session controller.
- Clear authenticated Query/UI state before exposing a different account's application view.
- Validate Entra issuer, audience, tenant, lifetime, delegated scope, roles, `tid`, and `oid`.
- Resolve `ActorId` from the validated `oid` only after the configured `tid` succeeds.
- Retire the local password/JWT issuer from production configuration and code paths.
- Add hermetic unit/integration tests and a separately gated real-Entra deployment smoke test.
- Update Terraform, deployment documentation, secret inventory, threat model, and rollback instructions.
- Preserve the existing exact-pinned XState dependency; this task neither removes it nor makes it the owner of MSAL protocol state.

### 1.5 Out of Scope

- Microsoft Entra External ID/B2C, consumer Microsoft accounts, or multi-tenant SaaS sign-in.
- Calling Microsoft Graph or using Graph group lookups.
- Authorizing from `email`, `preferred_username`, display name, raw group names, or unvalidated ID-token claims.
- Direct group-claim authorization; groups may be assigned to app roles administratively.
- Password reset, local registration, magic links, passkeys, or social login managed by Tradebook.
- A Node authentication sidecar, Better Auth schema, Auth.js, or third-party identity SaaS.
- A cookie BFF, refresh tokens managed by Tradebook, or client secrets in the SPA.
- Service-to-service authorization on user endpoints except a separately scoped non-production automation principal defined for Task 09 load testing.
- Multi-tenant `(tid, oid)` persistence. A multi-tenant requirement needs a new ADR and identity mapping model.

---

## 2. Key Deliverables & File Layout

```text
src/Frontend/src/lib/auth/
├── msalConfig.ts                 # validated Vite configuration and API scopes
├── msalInstance.ts               # sole PublicClientApplication instance
└── tokenProvider.ts              # acquireTokenSilent + interaction-required result

src/Backend/src/Tradebook.Api/Security/
├── AuthenticationServiceCollectionExtensions.cs
├── ActorId.cs
└── Testing/
    └── TestAuthenticationHandler.cs   # compiled/registered only for Testing

infra/terraform/
├── entra.tf                      # SPA/API apps, service principals, scope and roles
├── variables.tf                  # tenant ID, redirect origins, role-assignment inputs
└── outputs.tf                    # non-secret tenant/client/API scope identifiers

tests/
├── Tradebook.UnitTests/Authentication/
├── Tradebook.IntegrationTests/Authentication/
└── e2e/src/specs/entra-authentication.spec.ts
```

### 2.1 Ownership Matrix

| Concern | Owner | Forbidden duplication |
| :--- | :--- | :--- |
| OAuth/OIDC protocol, account cache, token renewal | MSAL browser | Zustand, XState, Query, hand-written refresh timers |
| Route access and validated return path | TanStack Router from Task 11 | MSAL component templates as a second router |
| Server data/session boundary clearing | Task 11 session controller + TanStack Query | MSAL cache containing application data |
| Shared non-secret auth presentation | React/Zustand derived state only | Access, ID, or refresh token strings |
| Token validation | Microsoft.Identity.Web/JwtBearer | Custom signature or JWKS implementation |
| Authorization | ASP.NET Core policies using scopes + Entra app roles | Frontend-only role checks or group names |
| Actor attribution | Backend `ActorId` from validated `oid` under configured `tid` | Request DTOs, email, username, frontend actor IDs |
| Identity infrastructure | Terraform plus tenant-admin approval | Portal-only undocumented drift |

---

## 3. Architecture & Code Contract Blueprints

### 3.1 End-to-End Authentication Flow

```text
Browser requests protected route
  -> Task 11 route guard observes no active MSAL account
  -> preserve validated internal return URL
  -> MSAL authorization-code + PKCE redirect to configured tenant
  -> Entra applies assignment, MFA, and Conditional Access
  -> MSAL processes callback and establishes the active account
  -> session controller clears prior-account Query/UI state
  -> tokenProvider.acquireForApi() requests api://<api-client-id>/access_as_user
  -> REST Authorization: Bearer <Entra access token>
     or SignalR accessTokenFactory returns the same scoped token
  -> Microsoft.Identity.Web validates signature/issuer/audience/lifetime
  -> policy requires access_as_user + expected app role
  -> ActorId validates tid and parses oid UUID
  -> repository records that actor in app.actor_id/audit_log
```

No API authorization decision uses the ID token. The API accepts only access tokens whose audience is the Tradebook API registration.

### 3.2 Entra Registration Contract

#### API registration

- Sign-in audience: this organization's directory only.
- Application ID URI: `api://<api-client-id>` unless the tenant requires a verified custom URI.
- Delegated scope: `access_as_user`, admin-consent display text naming Tradebook.
- User app roles with exact claim values: `Trader`, `BackOffice`, `Admin`.
- Enterprise application: `appRoleAssignmentRequired = true`.
- No client secret or certificate is required merely to validate incoming access tokens.
- No Microsoft Graph permission is requested.

#### SPA registration

- Public client SPA; never create a client secret.
- Exact `spa` redirect URIs for local, staging, and production origins.
- Exact post-logout redirect URIs for the same environments.
- Delegated permission to the API's `access_as_user` scope with tenant admin consent.
- Single-tenant authority `https://login.microsoftonline.com/<tenant-id>`; never `common`, `organizations`, or a user-controlled authority.

Terraform uses the official `hashicorp/azuread` provider and exposes only non-secret IDs to the frontend build and Container App. Role assignments may be a documented tenant-admin step when CI lacks directory-write privileges, but the desired assignments and drift check remain versioned.

### 3.3 Frontend MSAL Contract

```ts
export const apiScopes = [`api://${apiClientId}/access_as_user`] as const;

export const msalConfig: Configuration = {
  auth: {
    clientId: spaClientId,
    authority: `https://login.microsoftonline.com/${tenantId}`,
    redirectUri,
    postLogoutRedirectUri: redirectUri,
  },
  cache: {
    cacheLocation: BrowserCacheLocation.MemoryStorage,
  },
};
```

- Validate required `VITE_ENTRA_TENANT_ID`, `VITE_ENTRA_SPA_CLIENT_ID`, `VITE_ENTRA_API_CLIENT_ID`, and redirect-origin configuration before rendering.
- Wrap the application once in `MsalProvider`; do not create per-component client instances.
- Use redirect interaction for sign-in and interaction-required renewal. Preserve only a validated internal return URL.
- Select and set exactly one active account. Ambiguous account state returns to account selection; it never silently chooses an arbitrary account.
- `tokenProvider.acquireForApi()` calls `acquireTokenSilent` for every REST request/SignalR connection and returns a typed interaction-required outcome instead of starting navigation from non-React code.
- Task 11's session controller keys the application session by MSAL `homeAccountId` plus tenant/account claims, clears prior Query/UI state, then invalidates router guards.
- Logout clears application caches and active account, calls MSAL logout, and never leaves protected UI mounted with stale data.
- Zustand may expose derived, non-secret presentation such as display name or `isAuthenticated`; it stores no token strings and does not become the MSAL source of truth.
- The exact-pinned `xstate` dependency remains installed. Authentication protocol and interaction state stay inside MSAL.

### 3.4 REST and SignalR Token Contract

- `apiFetch` obtains a fresh/cached token through `tokenProvider`; callers never pass a token manually.
- One terminal 401 may trigger a forced silent acquisition and single replay only when the original method is safe under its idempotency contract. Unsafe mutations are not automatically replayed.
- An MSAL interaction-required result routes to an explicit reauthentication screen/redirect with the internal return URL.
- HTTP 403 means authenticated but unauthorized; it shows an access-denied view and must not cause a login loop.
- SignalR `accessTokenFactory` is asynchronous and obtains the API-scoped token immediately before connect/reconnect.
- Query-string bearer extraction remains limited to `/hubs/*`; access-token query values are redacted from application, proxy, and telemetry logs.
- Account change/logout stops the old SignalR connection before clearing state and starting a connection for the new account.

### 3.5 Backend Validation and Policy Contract

Production uses `Microsoft.Identity.Web` over ASP.NET Core JwtBearer:

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(configuration.GetSection("Entra"));

services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireScope("access_as_user")
        .Build();
    options.AddPolicy("TraderPolicy", policy =>
        policy.RequireScope("access_as_user").RequireRole("Trader", "Admin"));
    options.AddPolicy("BackOfficePolicy", policy =>
        policy.RequireScope("access_as_user").RequireRole("BackOffice", "Admin"));
    options.AddPolicy("AdminPolicy", policy =>
        policy.RequireScope("access_as_user").RequireRole("Admin"));
});
```

Equivalent FastEndpoints-compatible registration is acceptable, but these invariants are not negotiable:

- configured single tenant only;
- API client ID/audience only;
- v2 issuer metadata and normal signature/lifetime validation;
- delegated `access_as_user` scope on all human-user API calls;
- exact app-role values for named authorization policies;
- `tid` and `oid` must both be present and valid UUIDs;
- `tid` must equal the configured tenant before `oid` becomes `ActorId`;
- `email`, `preferred_username`, `name`, `upn`, and frontend values never determine authorization or audit identity;
- health probes remain the only production anonymous endpoints;
- username/password login, `Jwt:SigningKey`, and local JWT issuance are absent from production startup/configuration.

The production application fails startup on missing/placeholder Entra configuration. A test authentication scheme is registered only when the host environment is exactly `Testing`; configuration cannot enable it in staging or production.

### 3.6 Actor Cutover Contract

The actor UUID changes from locally issued JWT `sub` to the validated single-tenant Entra `oid`. Before production cutover:

1. Inventory every existing `users.id`, `workspace_dashboards.actor_id`, and distinct `audit_log.actor_id`.
2. Obtain an approved local-user-to-Entra-`oid` mapping from the tenant/application owner.
3. Migrate mutable ownership rows such as `workspace_dashboards.actor_id` in one reviewed transaction, or record evidence that the environment contains no rows requiring migration.
4. Never rewrite historical `audit_log` rows silently. Retain the approved mapping artifact with the release evidence so old actor UUIDs remain attributable.
5. Abort cutover on an unmapped active owner; never infer identity from email or username.

### 3.7 Testing and Automation Contract

- Unit tests cover configuration validation, actor extraction, scope/role policies, and no-token-storage boundaries.
- Integration tests use a test-only authentication handler or local asymmetric test issuer; they never call Entra over the network and never reuse production credentials.
- Integration tests prove wrong issuer, audience, tenant, missing scope, missing role, malformed `oid`, expired token, and anonymous access fail closed.
- A protected deployment job runs the real Entra smoke flow against staging with a dedicated test user assigned a least-privilege app role.
- Task 09 load tests use a separately approved non-production automation registration/role and record its service-principal `oid` as the actor. It is not accepted by production user-only policies.
- Secrets, authorization codes, tokens, `access_token` query parameters, and MSAL cache contents are never attached to CI artifacts, screenshots, traces, or logs.

### 3.8 Operational and Rollback Contract

- Terraform plan shows the SPA/API registrations, scope, app roles, service principals, assignment-required setting, redirect URIs, and non-secret outputs.
- Container App receives `Entra__Instance`, `Entra__TenantId`, and `Entra__ClientId`; it receives no symmetric signing key for production authentication.
- Frontend build receives only public tenant/client IDs and redirect origin.
- Production rollout uses a tested maintenance/cutover window because actor identity changes.
- Rollback restores the prior immutable application image and configuration only under explicit incident approval; it must not leave both production issuers enabled indefinitely.
- Entra sign-in logs, API 401/403 metrics, role-assignment changes, and claims-validation failures have an operations dashboard/alert path without logging token material.

---

## 4. Subagent Implementation Step-by-Step Workflow

1. **Capture baseline and tenant decisions**
   - Record existing auth tests, issuer configuration, actor-owned row counts, and current frontend dependency pins.
   - Confirm tenant ID, environment origins, app owners, role assignees, admin-consent owner, and Conditional Access expectations.
2. **Provision Entra infrastructure**
   - Add the exact-pinned AzureAD Terraform provider.
   - Create/import SPA and API registrations, service principals, delegated scope, app roles, redirect URIs, and assignment-required configuration.
   - Produce a plan and tenant-admin checklist; never apply role assignments with broader directory permissions than required.
3. **Replace backend issuer validation**
   - Add exact-pinned `Microsoft.Identity.Web`.
   - Replace local HS256 configuration with Entra validation and scope/role policies.
   - Update `ActorId` to validate configured `tid` and resolve `oid`.
   - Retire the production login endpoint, password hasher, local JWT options, and generated login DTO contracts.
4. **Create hermetic test authentication**
   - Add a `Testing`-only handler/local issuer with explicit claims builders.
   - Migrate unit/integration tests away from hand-minted production HS256 tokens.
   - Add the complete negative validation matrix.
5. **Integrate MSAL in React**
   - Add exact-pinned stable `@azure/msal-browser` and `@azure/msal-react` versions and commit the lockfile.
   - Initialize one public client, provider, validated config, login route, callback processing, and logout flow.
   - Preserve the existing exact-pinned XState dependency unchanged.
6. **Integrate Task 11 session boundaries**
   - Replace raw Zustand token ownership with the token-provider boundary.
   - Clear Query/UI state and stop SignalR on active-account changes before protected routes remount.
   - Preserve safe internal return URLs through interaction-required redirects.
7. **Wire REST and SignalR**
   - Acquire the API scope silently for calls and reconnects.
   - Implement bounded 401/403/interaction-required behavior without unsafe mutation replay or login loops.
   - Verify token redaction for hub query strings.
8. **Plan and execute actor cutover**
   - Produce the approved mapping or zero-row evidence.
   - Migrate mutable ownership safely; preserve historical audit rows plus mapping evidence.
9. **Update deployment and operations**
   - Replace Container App JWT settings, update runbooks, configure sign-in/authorization monitoring, and document rollback.
   - Run staging real-Entra smoke tests with least-privilege principals.
10. **Hand off to Tasks 09 and 10**
   - Add stable E2E fixtures without exposing secrets.
   - Require Task 09 and Task 10 to verify Entra sign-in, authorization, SignalR, actor attribution, and deployment drift.

---

## 5. Independent Verification & Acceptance Workflow

### 5.1 Verification Commands

```powershell
npm --prefix src/Frontend ci
npm --prefix src/Frontend run lint
npm --prefix src/Frontend test -- --run
npm --prefix src/Frontend run build

dotnet build src/Backend/Tradebook.sln -c Debug
dotnet test tests/Tradebook.UnitTests/Tradebook.UnitTests.csproj
dotnet test tests/Tradebook.IntegrationTests/Tradebook.IntegrationTests.csproj
dotnet test tests/Tradebook.ArchitectureTests/Tradebook.ArchitectureTests.csproj

terraform -chdir=infra/terraform fmt -check
terraform -chdir=infra/terraform init -backend=false
terraform -chdir=infra/terraform validate

node scripts/verify-doc-links.mjs
```

Static audits:

```powershell
# Production no longer owns passwords or signs JWTs.
rg -n "Jwt__SigningKey|Jwt:SigningKey|new JwtSecurityToken|PasswordHasher|/api/v1/auth/login" src/Backend/src infra/terraform src/Frontend/src

# Tokens are not copied into app state or browser storage.
rg -n "accessToken|idToken|refreshToken|localStorage|sessionStorage" src/Frontend/src/lib/state src/Frontend/src/lib/session

# XState remains intentionally available.
rg -n '"xstate"' src/Frontend/package.json src/Frontend/package-lock.json
```

The first audit may match test-only code or migration documentation but must find no deployed issuer/login path. The second may match typed boundary comments but must find no stored token value. The XState audit must resolve the same exact pin in both dependency files.

### 5.2 Acceptance Criteria

| ID | Acceptance criterion | Evidence |
| :--- | :--- | :--- |
| **ENTRA-01** | Terraform defines/imports single-tenant SPA/API registrations, API scope, exact roles, redirect URIs, service principals, and assignment-required behavior. | Reviewed plan plus tenant-admin verification |
| **ENTRA-02** | The SPA registration has no secret; API/frontend receive only the configuration needed for their role. | Terraform/state-sensitive-output audit |
| **AUTHN-01** | A valid API access token from the configured tenant/audience authenticates; wrong issuer, audience, tenant, expiry, signature, or token type returns 401. | Integration validation matrix |
| **AUTHN-02** | Production exposes no username/password login and holds no JWT signing key. | Route/configuration/static audit |
| **AUTHN-03** | Tests remain hermetic and cannot enable the test auth scheme in staging/production. | Environment-gating tests |
| **AUTHZ-01** | Human API calls require `access_as_user`; named policies enforce the exact `Trader`, `BackOffice`, and `Admin` role matrix. | 401/403/allowed policy tests |
| **AUTHZ-02** | No authorization or actor decision uses email, username, display name, or raw group membership. | Claims and static audit |
| **ACTOR-01** | `ActorId` is the validated UUID `oid` only after `tid` matches the configured tenant, and mutation audit rows record it. | Unit plus Testcontainers mutation test |
| **ACTOR-02** | Existing mutable ownership has an approved mapping/zero-row proof; historical audit rows were not silently rewritten. | Signed cutover evidence |
| **SPA-01** | Microsoft sign-in, callback, protected-route return, logout, account selection, and interaction-required renewal work without redirect loops. | Vitest plus staging Playwright smoke |
| **SPA-02** | MSAL owns tokens in memory; Zustand, Query, localStorage, sessionStorage, IndexedDB, and logs contain no access/ID/refresh token values. | Storage/state/log audit |
| **SPA-03** | Switching accounts clears Query/UI state and stops the old SignalR connection before the new protected view mounts. | Two-account integration test |
| **API-01** | REST gets a silent API-scoped token at the call boundary; unsafe mutations are never automatically replayed after 401. | Token-provider and fetch-call-count tests |
| **HUB-01** | SignalR initial connect and reconnect use the same API scope; logout/account change disconnects and token query strings are redacted. | SignalR integration and logging tests |
| **OPS-01** | Runbook covers registration ownership, consent, assignments, Conditional Access, monitoring, rotation/recovery, cutover, and rollback. | Operations review |
| **STATE-01** | The exact-pinned XState dependency remains installed but is not used as a parallel authentication protocol owner. | Dependency and import audit |
| **BUILD-01** | Frontend, backend, architecture, Terraform, and documentation verification commands all pass. | Captured zero-error output |

### 5.3 Manual Staging Verification

1. An assigned Trader signs in with Microsoft and can read/mutate trader resources.
2. An authenticated user with no Tradebook app role is denied without a login loop.
3. BackOffice and Admin role boundaries match existing policies.
4. MFA/Conditional Access interaction returns to the validated internal route.
5. Account switch does not display prior-account Query data.
6. SignalR reconnects after silent token renewal and stops on logout.
7. Entra sign-in logs and API metrics show the attempt without exposing token material.

---

## 6. Anti-Cheating & Integrity Guardrails

1. Do not accept an ID token as authorization for the API; accept only an API-audience access token.
2. Do not use `common`, `organizations`, issuer wildcards, disabled issuer validation, or an unvalidated tenant claim.
3. Do not authorize from `email`, `preferred_username`, display name, UPN, or raw group names/IDs.
4. Do not create a client secret for the SPA or commit tenant credentials/tokens to source, Terraform variables, CI output, screenshots, or traces.
5. Do not copy access, ID, or refresh tokens into Zustand, TanStack Query, XState, localStorage, sessionStorage, IndexedDB, or application cookies.
6. Do not implement OAuth, PKCE, JWKS rotation, or token refresh by hand when MSAL/Microsoft.Identity.Web provide the contract.
7. Do not add Better Auth, Auth.js, a Node sidecar, or a second session database as a convenience wrapper.
8. Do not rely only on `[Authorize]`; validate delegated scope and the appropriate app role.
9. Do not silently choose the first account when multiple MSAL accounts are present.
10. Do not turn every 401 into an unconditional redirect/retry loop, and never replay a non-idempotent mutation without its existing idempotency guarantee.
11. Do not permit the test authentication scheme outside the exact `Testing` environment.
12. Do not rewrite historical `audit_log` actor IDs or infer an actor migration from mutable usernames/emails.
13. Do not remove XState because it is currently unused; preserve the exact pin for future workflow tasks.
14. Do not mark the task Implemented from mocked authentication alone; the protected staging Entra smoke test and actor-cutover evidence are required.
