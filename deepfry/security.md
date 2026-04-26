<!-- deepfry:commit=b9be8dc1501e31ea9edfa99c938527818fa2aca5 agent=security-analyzer timestamp=2025-07-24T12:00:00Z -->

# Security Analysis

## Authentication
- **Method**: N/A — this is an Azure Functions (timer-triggered) application with no inbound user authentication. The single HTTP endpoint (`TweetReplyTrigger`) uses Azure Functions' built-in `AuthorizationLevel.Function` (function-key auth).
- **Provider**: Azure Functions host-level key management
- **Implementation**: `Functions/TweetReplyTrigger.cs`
- **Findings**: Timer triggers have no authentication surface. The only HTTP-facing endpoint correctly uses `AuthorizationLevel.Function`, which requires a valid function key in the query string or `x-functions-key` header. This is acceptable for internal/automation endpoints but not suitable for public-facing APIs.

## Authorization
- **Model**: None (no user identity model)
- **Coverage**: 100% of HTTP endpoints have function-key auth; timer triggers require no authorization
- **Implementation**: `Functions/TweetReplyTrigger.cs:24`
- **Unprotected endpoints**: None identified — all orchestrators are triggered internally via Durable Task framework

## Secret Management
| Secret | Storage Location | Method | Rating | File |
|--------|-----------------|--------|--------|------|
| Azure OpenAI API Key | Key Vault (prod) / `local.settings.json` (dev) | `DefaultAzureCredential` → KV / Config (dev) | 🟢 Prod / 🟡 Dev | `Program.cs:132-139`, `KeyVaultSecretProvider.cs:19` |
| Stability AI API Key | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `ImageGenerationService.cs:37` |
| Together AI API Key | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `TogetherAIImageGenerationService.cs:42` |
| WordPress OAuth Token | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `WordPressService.cs:362` |
| Twitter Consumer Key/Secret | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `TwitterService.cs:58-61` |
| Twitter Access Token/Secret | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `TwitterService.cs:60-61` |
| Facebook Page Access Token | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `FacebookService.cs:45` |
| Reddit App Secret / Credentials | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `RedditService.cs:57-60` |
| Pinterest Access Token | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `PinterestService.cs:131` |
| Cosmos DB Connection String | Key Vault (prod) / `local.settings.json` (dev) | `DefaultAzureCredential` → KV / Config (dev) | 🟡 Prod (key-based) | `Program.cs:217-235` |
| AdSense Client/Slot ID | Key Vault (declared) | Not yet consumed in code | 🟢 | `SecretNames.cs:28-29` |
| Azure Storage Connection String | ARM `listKeys()` inline (prod) | Inline in app settings via ARM template | 🟡 | `azuredeploy.json:176` |

### Positive findings
- **`ISecretProvider` abstraction**: Clean separation between dev (`LocalSecretProvider`) and production (`KeyVaultSecretProvider`).
- **`DefaultAzureCredential` used for Key Vault**: No hardcoded KV credentials; uses managed identity in production.
- **ARM template uses `securestring`**: Deployment parameters for API keys are typed as `securestring`.
- **`local.settings.json` is `.gitignore`d** and not tracked in git history — confirmed via `git ls-files` and `git log --all`.
- **Key Vault RBAC Authorization enabled**: Infrastructure uses `enableRbacAuthorization: true` (best practice).
- **Function App has System-Assigned Managed Identity** with Key Vault Secrets User role assigned.

### Negative findings
- **Plaintext secrets in `local.settings.json`**: The file contains real API keys for Azure OpenAI, Stability AI, Together AI, and WordPress OAuth on disk. While `.gitignore`d, any developer with file-system access can read them. **Severity: Medium** — acceptable for local dev but the file should never leak.
- **Cosmos DB uses connection string (key-based auth) even in production**: The production path fetches a connection string from Key Vault rather than using `DefaultAzureCredential` with Cosmos DB. This means the Cosmos DB primary key is stored as a secret rather than eliminated via managed identity. **Severity: Medium**.
- **Azure Storage connection string uses `listKeys()` in ARM**: The `AzureWebJobsStorage` app setting embeds the storage account key directly. Consider using managed identity for storage. **Severity: Low** (standard Azure Functions pattern, but improvable).

## Secret Leakage Risk
| Risk | Location | Severity | Details |
|------|----------|----------|---------|
| Secret names logged at DEBUG level | `KeyVaultSecretProvider.cs:24` | 🟡 Medium | Logs `"Retrieving secret {SecretName} from Key Vault"` — leaks secret *names* (not values) to logs. Acceptable but name exposure could aid attackers. |
| Local secret usage logged as WARNING | `LocalSecretProvider.cs:26` | 🟡 Medium | Logs `"Using local secret for {SecretName}"` — reveals which secrets are in use. OK for dev; ensure this provider never runs in production. |
| WordPress post payload logged at DEBUG | `WordPressService.cs:323` | 🟢 Low | Logs full post JSON payload. Payload does not contain secrets (auth header is separate), but could leak content if Debug logging is enabled in production. |
| Facebook access token sent as form body parameter | `FacebookService.cs:60` | 🟡 Medium | `access_token` is sent as a `FormUrlEncodedContent` field in the POST body. While sent over HTTPS, this follows Graph API convention. However, if request logging middleware is added, the token could be captured. |
| API error responses logged verbatim | Multiple services (e.g., `TwitterService.cs:73`, `RedditService.cs:78`, `WordPressService.cs:279`) | 🟡 Medium | Error response bodies from external APIs are logged. These *could* echo back tokens or sensitive data in error payloads. Truncation or redaction is recommended. |
| Log files on disk in source tree | `src/logs/*.log` | 🟢 Low | Log files exist on disk at `src/logs/` and `src/CarFacts.Functions/logs/`. They contain secret *names* (not values) and stack traces. `.gitignore` excludes `logs/` but the physical files are present. Confirmed not committed to git. |

## Inter-Service Security
| Service Pair | Auth Method | TLS | Rating |
|-------------|-------------|-----|--------|
| Function App → Azure Key Vault | Managed Identity (`DefaultAzureCredential`) | Yes (HTTPS) | 🟢 |
| Function App → Azure OpenAI | API Key (Bearer token) via Key Vault | Yes (HTTPS) | 🟢 |
| Function App → Stability AI | API Key (Bearer token) via Key Vault | Yes (HTTPS) | 🟢 |
| Function App → Together AI | API Key (Bearer token) via Key Vault | Yes (HTTPS) | 🟢 |
| Function App → WordPress.com | OAuth2 Bearer token via Key Vault | Yes (HTTPS) | 🟢 |
| Function App → Twitter/X API | OAuth 1.0a (HMAC-SHA1 signature) via Key Vault | Yes (HTTPS) | 🟡 |
| Function App → Facebook Graph API | Page Access Token (form body) via Key Vault | Yes (HTTPS) | 🟡 |
| Function App → Reddit API | OAuth2 (password grant → Bearer) via Key Vault | Yes (HTTPS) | 🟡 |
| Function App → Pinterest API | OAuth2 Bearer token via Key Vault | Yes (HTTPS) | 🟢 |
| Function App → Cosmos DB | Connection string (key-based) via Key Vault | Yes (HTTPS) | 🟡 |
| Function App → Azure App Configuration | Managed Identity (`DefaultAzureCredential`) | Yes (HTTPS) | 🟢 |
| Function App → Azure Storage | Account Key (via ARM `listKeys()`) | Yes (HTTPS) | 🟡 |

### Notes
- **Twitter OAuth 1.0a uses HMAC-SHA1**: This is a legacy signing algorithm. While Twitter still requires it, HMAC-SHA1 is considered weak. The codebase correctly implements the OAuth 1.0a spec. **Rating: 🟡** — dictated by platform constraints.
- **Reddit uses OAuth2 password grant**: This is a deprecated OAuth flow. Reddit requires it for "script" app types but it means the user's Reddit password is stored as a secret. **Rating: 🟡**.
- **All external calls use HTTPS**: No plaintext HTTP connections found.

## Dependency Security
| Package | Version | Status |
|---------|---------|--------|
| Azure.Identity | 1.14.2 | 🟢 Current |
| Azure.Security.KeyVault.Secrets | 4.7.0 | 🟢 Current |
| Microsoft.Azure.Cosmos | 3.43.1 | 🟢 Current |
| Microsoft.SemanticKernel | 1.47.0 | 🟢 Current |
| Microsoft.Azure.Functions.Worker | 2.0.0 | 🟢 Current |
| Serilog.Extensions.Hosting | 8.0.0 | 🟢 Current |
| All packages | Pinned versions | 🟢 No floating versions |

## Security Score
- Authentication: ✅ (function-key protected HTTP endpoint; timer triggers inherently internal)
- Authorization: ✅ (no user-facing endpoints requiring RBAC)
- Secret Management: ⚠️ (strong in production with Key Vault + MI; local dev has plaintext secrets on disk; Cosmos DB should use MI)
- Secret Leakage: ⚠️ (no secret *values* logged, but secret names and full API error bodies are logged; error response logging could echo tokens)
- Inter-Service: ⚠️ (all HTTPS; managed identity for KV and App Config; but Cosmos DB, Storage, and some third-party APIs use key-based auth)

## Recommendations
1. **Migrate Cosmos DB to Managed Identity auth** — Replace connection-string-based `CosmosClient` with `DefaultAzureCredential` in `Program.cs:239`. This eliminates the `CosmosDb-ConnectionString` secret entirely. Ref: `Program.cs:212-255`.
2. **Migrate Azure Storage to Managed Identity** — Replace `listKeys()` in `azuredeploy.json:176` with a managed-identity-based connection for `AzureWebJobsStorage`. Use `AzureWebJobsStorage__accountName` instead.
3. **Redact or truncate API error response bodies in logs** — Services like `TwitterService.cs:73`, `RedditService.cs:78`, `FacebookService.cs:66`, and `WordPressService.cs:279` log full error bodies from external APIs. Add length truncation (e.g., 500 chars) and scrub known token fields.
4. **Use User Secrets or environment variables for local development** — Instead of plaintext secrets in `local.settings.json`, use `dotnet user-secrets` or a local `.env` file loaded at startup. This reduces risk of accidental file sharing.
5. **Consider upgrading Twitter OAuth to PKCE/OAuth 2.0** — Twitter API v2 supports OAuth 2.0 with PKCE for user-context operations. This would replace the legacy HMAC-SHA1 signing in `TwitterService.cs:287-328`.
6. **Add secret caching with TTL to `KeyVaultSecretProvider`** — Currently, every API call to external services fetches secrets from Key Vault (e.g., `TwitterService` makes 4 KV calls per tweet). Add in-memory caching with a 5-minute TTL to reduce KV request volume and latency.
7. **Remove DEBUG-level payload logging in production** — `WordPressService.cs:323` logs the full post payload at Debug level. Ensure `host.json` log levels prevent Debug output in production (currently set to `Information` — ✅ safe, but add explicit guard).
8. **Ensure `src/logs/` directory is cleaned before any distribution** — While `.gitignore` covers `logs/`, the physical files exist and contain operational details. Add a cleanup step or move log output outside the source tree.
