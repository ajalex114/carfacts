# Security Analysis

## Authentication

- **Method**: Mixed — Timer triggers (no auth required), Durable Functions orchestrators (internal only), one HTTP trigger with Function-level key auth
- **Provider**: Azure Functions host-key authentication for HTTP triggers; no user-identity authentication (this is a headless automation app)
- **Implementation**:
  - `src/CarFacts.Functions/Functions/TweetReplyTrigger.cs:24` — `AuthorizationLevel.Function` on the only HTTP endpoint
  - All other triggers are `TimerTrigger` (system-internal, no external HTTP surface)
- **Findings**:
  - ✅ The single HTTP trigger (`TweetReplyTrigger`) requires a Function-level API key — appropriate for a backend automation endpoint.
  - ✅ All other entry points are timer-based — they have no external attack surface.
  - ⚠️ No user identity or RBAC model exists, but this is expected for a headless content-publishing pipeline. If this ever exposes more HTTP endpoints, an auth framework should be added.

## Authorization

- **Model**: None — this is a headless Azure Functions app with no user-facing endpoints
- **Coverage**: 100% of HTTP endpoints (1/1) have `AuthorizationLevel.Function`
- **Implementation**: `src/CarFacts.Functions/Functions/TweetReplyTrigger.cs:24`
- **Unprotected endpoints**: None identified
- **Findings**:
  - ✅ The only HTTP endpoint uses `AuthorizationLevel.Function`, requiring a host key.
  - ℹ️ No role-based or resource-level authorization is present. Not needed for the current architecture (timer + durable orchestrations with a single function-key-protected HTTP trigger).

## Secret Management

| Secret | Storage Location | Method | Rating | File |
|--------|-----------------|--------|--------|------|
| Azure OpenAI API Key | Key Vault (prod) / `local.settings.json` (dev) | `DefaultAzureCredential` → `SecretClient` | 🟢 Prod / 🟡 Dev | `Program.cs:136-138`, `local.settings.json:53` |
| Stability AI API Key | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `ImageGenerationService.cs:37`, `local.settings.json:54` |
| Together AI API Key | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `TogetherAIImageGenerationService.cs:42`, `local.settings.json:55` |
| WordPress OAuth Token | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `WordPressService.cs:362`, `local.settings.json:56` |
| Twitter Consumer Key/Secret | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `TwitterService.cs:58-61` |
| Twitter Access Token/Secret | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `TwitterService.cs:89-90` |
| Facebook Page Access Token | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `FacebookService.cs:45` |
| Reddit App Secret / Username / Password | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `RedditService.cs:58-60` |
| Pinterest Access Token | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `PinterestService.cs:131` |
| Cosmos DB Connection String | Key Vault (prod) / `local.settings.json` (dev) | `DefaultAzureCredential` → `SecretClient` | 🟢 Prod / 🟡 Dev | `Program.cs:225-229`, `local.settings.json:63` |
| Azure OpenAI API Key (startup) | Key Vault (prod) / config (dev) | Direct `SecretClient.GetSecret` | 🟢 Prod | `Program.cs:136-138` |

### Architecture Strengths

- **Well-designed `ISecretProvider` abstraction** (`Services/Interfaces/ISecretProvider.cs`) — production uses `KeyVaultSecretProvider.cs` (Key Vault + `DefaultAzureCredential`), development uses `LocalSecretProvider.cs` (reads from `Secrets:*` config keys).
- **Environment-aware switching** in `Program.cs:85-88` — automatically selects the correct provider based on whether the app runs locally or in Azure.
- **ARM template uses `securestring` parameters** for all secrets (`infra/azuredeploy.json:16-26`), preventing accidental logging of deployment parameters.
- **Key Vault uses RBAC authorization** (`enableRbacAuthorization: true` at `infra/azuredeploy.json:115`).
- **Managed Identity** — Function App has `SystemAssigned` identity (`infra/azuredeploy.json:159`) with `Key Vault Secrets User` role assignment (`infra/azuredeploy.json:271-283`).
- **`local.settings.json` is in `.gitignore`** and not tracked in git history — confirmed via `git ls-files`.

### ⚠️ Critical Finding: Hardcoded API Keys in `local.settings.json`

**File**: `src/CarFacts.Functions/local.settings.json:53-56`

The local settings file contains **real, non-empty API keys** for:
- `Secrets:AzureOpenAI-ApiKey` — Full Azure OpenAI key (redacted: `7i4J...vfG0`)
- `Secrets:StabilityAI-ApiKey` — Full Stability AI key (redacted: `sk-9o...Z9w`)
- `Secrets:TogetherAI-ApiKey` — Full Together AI key (redacted: `tgp_v1_...E1k`)
- `Secrets:WordPress-OAuthToken` — Full WordPress OAuth token (redacted: `TBN7...MwW`)

While this file **is** gitignored and **not** tracked, the keys are:
- 🔴 **Plaintext on disk** — anyone with file system access can read them
- 🔴 **At risk if `.gitignore` is bypassed** (e.g., `git add -f`)
- 🔴 **Potentially present in backups, IDE caches, or file sync services**

**Recommendation**: Replace with Azure Key Vault references even for local development, or use `dotnet user-secrets` to move secrets out of the project directory entirely.

## Secret Leakage Risk

| Risk | Location | Severity | Details |
|------|----------|----------|---------|
| Secret name logged (not value) | `KeyVaultSecretProvider.cs:24` | 🟢 Low | Logs `{SecretName}` — this is the name, not the value. Structured logging prevents accidental interpolation. |
| Secret name logged (not value) | `LocalSecretProvider.cs:26` | 🟢 Low | Logs `{SecretName}` — warning message about local usage, no value exposed. |
| Facebook access_token in form POST body | `FacebookService.cs:60` | 🟡 Medium | Access token sent as form field `["access_token"] = accessToken`. This is per Facebook API spec, but if HTTP logging middleware or diagnostics are enabled, the full request body (including the token) could be captured in logs. |
| Reddit password sent in form POST body | `RedditService.cs:69-70` | 🟡 Medium | Username and password sent as form fields for Reddit OAuth `password` grant. Same logging risk as above. If an HTTP handler logs request bodies, credentials would be exposed. |
| WordPress post payload logged at Debug level | `WordPressService.cs:323` | 🟡 Medium | `LogDebug("WordPress post payload: {Payload}", jsonPayload)` — while this doesn't contain secrets, Debug-level logging of full payloads could expose content if logging is misconfigured. Serilog minimum level is `Debug` in `Program.cs:14`. |
| API error response bodies logged | Multiple files | 🟡 Medium | `TwitterService.cs:73`, `FacebookService.cs:67`, `RedditService.cs:78`, `PinterestService.cs:144`, `WordPressService.cs:279` — all log raw API error response bodies. These could potentially contain tokens echoed back by the API. |
| Cosmos DB connection string in memory at startup | `Program.cs:217-229` | 🟢 Low | Connection string is retrieved from Key Vault and passed to `CosmosClient` constructor. Not logged, but held in memory. Standard pattern. |
| HMAC-SHA1 signing key constructed in memory | `TwitterService.cs:319` | 🟢 Low | OAuth signing key (`consumerSecret&tokenSecret`) is in memory during request signing. Not logged. Ephemeral. |

### Positive Findings

- ✅ **No secrets are logged at any level** — all `Log*` calls use structured logging with `{SecretName}` placeholders, never `{SecretValue}`.
- ✅ **No secrets in comments, TODOs, or source code** — all secrets are fetched at runtime via `ISecretProvider`.
- ✅ **No sensitive HTTP headers logged** — Authorization headers are constructed and set, but never logged.

## Inter-Service Security

| Service Pair | Auth Method | TLS | Rating |
|-------------|-------------|-----|--------|
| App → Azure Key Vault | Managed Identity (`DefaultAzureCredential`) | Yes (HTTPS) | 🟢 |
| App → Azure App Configuration | Managed Identity (`DefaultAzureCredential`) | Yes (HTTPS) | 🟢 |
| App → Azure Cosmos DB | Connection string (from Key Vault) | Yes (HTTPS) | 🟡 |
| App → Azure OpenAI | API Key (from Key Vault) | Yes (HTTPS) | 🟡 |
| App → Stability AI | API Key (Bearer token) | Yes (HTTPS) | 🟡 |
| App → Together AI | API Key (Bearer token) | Yes (HTTPS) | 🟡 |
| App → WordPress.com REST API | OAuth2 Bearer token (from Key Vault) | Yes (HTTPS) | 🟢 |
| App → Twitter/X API | OAuth 1.0a (HMAC-SHA1 signature) | Yes (HTTPS) | 🟡 |
| App → Facebook Graph API | Page Access Token (in form body) | Yes (HTTPS) | 🟡 |
| App → Reddit API | OAuth2 (password grant → Bearer token) | Yes (HTTPS) | 🟡 |
| App → Pinterest API | OAuth2 Bearer token | Yes (HTTPS) | 🟢 |
| App → Application Insights | Connection string (env var) | Yes (HTTPS) | 🟢 |

### Analysis

- ✅ **All external calls use HTTPS** — no plaintext HTTP endpoints.
- ✅ **Key Vault and App Configuration use Managed Identity** — best practice, no shared secrets.
- 🟡 **Cosmos DB uses connection string** (`Program.cs:217-229`) — could be upgraded to `DefaultAzureCredential` with Entra ID RBAC for zero-secret access.
- 🟡 **Twitter uses HMAC-SHA1** (`TwitterService.cs:301,321`) — this is the OAuth 1.0a spec requirement (not a choice), but SHA1 is cryptographically weak. OAuth 2.0 with PKCE would be preferable if X/Twitter supports it for the required endpoints.
- 🟡 **Reddit uses `password` grant type** (`RedditService.cs:69`) — this is the least secure OAuth flow, transmitting username and password directly. The Reddit API requires this for script-type apps, but it means the actual Reddit account password is stored as a secret.

## Dependency Security

| Package | Version | Status |
|---------|---------|--------|
| `Azure.Identity` | 1.14.2 | 🟢 Current |
| `Azure.Security.KeyVault.Secrets` | 4.7.0 | 🟢 Current |
| `Microsoft.Azure.Cosmos` | 3.43.1 | 🟢 Current |
| `Microsoft.Azure.Functions.Worker` | 2.0.0 | 🟢 Current |
| `Microsoft.SemanticKernel` | 1.47.0 | 🟢 Current |
| `Serilog.Extensions.Hosting` | 8.0.0 | 🟢 Current |
| `Serilog.Sinks.File` | 6.0.0 | 🟢 Current |
| `Microsoft.ApplicationInsights.WorkerService` | 2.22.0 | 🟢 Current |

- ✅ All dependencies are pinned to specific versions (no floating ranges).
- ✅ Target framework is `net8.0` with Azure Functions v4 — current and supported.
- ℹ️ No known CVEs were identified for the listed package versions at the time of analysis, but a periodic `dotnet list package --vulnerable` scan is recommended.

## Security Score

| Area | Rating | Rationale |
|------|--------|-----------|
| Authentication | ✅ | Function-key auth on HTTP trigger; timer triggers have no attack surface |
| Authorization | ✅ | Only one HTTP endpoint, properly gated; no multi-user model needed |
| Secret Management | ⚠️ | Excellent production architecture (Key Vault + MI), but **real API keys exist in plaintext in `local.settings.json` on disk** |
| Secret Leakage | ✅ | No secret values logged; structured logging used consistently; API error bodies logged (minor risk) |
| Inter-Service | ⚠️ | All HTTPS, MI for Azure services, but Cosmos DB still on connection string; Reddit uses password grant |
| Dependencies | ✅ | All pinned, current versions, no known vulnerabilities |

## Recommendations

1. **🔴 HIGH — Remove plaintext API keys from `local.settings.json`**
   Move secrets to `dotnet user-secrets` or use Key Vault references with `local.settings.json` containing only the vault URI. This eliminates the risk of accidental commit, backup exposure, or IDE sync.
   *File: `src/CarFacts.Functions/local.settings.json:53-56`*

2. **🟡 MEDIUM — Migrate Cosmos DB to Entra ID (Managed Identity) authentication**
   Replace the connection-string-based `CosmosClient` with `DefaultAzureCredential`. This eliminates the Cosmos DB connection string from Key Vault entirely.
   *Files: `Program.cs:217-254`, `infra/azuredeploy.json`*

3. **🟡 MEDIUM — Reduce Serilog minimum level from Debug to Information in production**
   `Program.cs:14` sets `MinimumLevel.Debug()` unconditionally. In production, Debug-level logging could capture verbose payloads (e.g., `WordPressService.cs:323`).
   *File: `Program.cs:14`*

4. **🟡 MEDIUM — Sanitize or redact API error response bodies before logging**
   All service classes log raw error response bodies from external APIs. If an API echoes back tokens or credentials in error responses, they would appear in logs.
   *Files: `TwitterService.cs:73`, `FacebookService.cs:67`, `RedditService.cs:78`, `PinterestService.cs:144`, `WordPressService.cs:279`*

5. **🟢 LOW — Add `dotnet list package --vulnerable` to CI/CD**
   Automate dependency vulnerability scanning in the build pipeline to catch newly disclosed CVEs.
   *File: `.github/` (CI configuration)*

6. **🟢 LOW — Consider migrating Twitter OAuth 1.0a to OAuth 2.0 with PKCE**
   The current HMAC-SHA1 implementation is correct but uses a deprecated hash algorithm. If the X/Twitter API supports OAuth 2.0 user context for the tweet/search endpoints, migrating would improve the security posture.
   *File: `TwitterService.cs:287-328`*

7. **🟢 LOW — Add Azure Storage Managed Identity for `AzureWebJobsStorage`**
   The ARM template uses a connection string with account key for the Functions storage account (`infra/azuredeploy.json:176`). Migrating to identity-based connections would eliminate another shared key.
   *File: `infra/azuredeploy.json:174-176`*
