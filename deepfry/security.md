<!-- deepfry:commit=5360e5707b59a6cf919f9c880a227006d8f33b09 agent=security-analyzer timestamp=2026-04-27T00:00:22Z -->

# Security Analysis

---

> **Scope of this update (commit `5360e57`):** Full re-analysis covering both `src/CarFacts.VideoFunction/` (new) and `src/CarFacts.Functions/` (existing). VideoFunction findings are new; CarFacts.Functions findings at the bottom are carried forward with minor corrections.

---

## ⚠️ CRITICAL — Live Credentials on Disk

Both `local.settings.json` files contain **real, active API keys and connection strings**. They are correctly `.gitignore`d (confirmed: no commits found in `git log --all` for either file), but the credential values are sitting in plaintext on the filesystem. Anyone with access to this machine or the source tree can read them.

| File | Contains |
|------|---------|
| `src/CarFacts.VideoFunction/local.settings.json` | Storage account key (`stpocvideogen`), Azure Speech key, Pexels API key, YouTube Data API key, Azure Computer Vision key |
| `src/CarFacts.Functions/local.settings.json` | Azure OpenAI key, Stability AI key, Together AI key, WordPress OAuth token |

**Immediate action required**: Rotate all credentials listed in these files. Storage account key, Speech key, Vision key, and the third-party API keys should all be regenerated now. See Recommendation #1 below.

---

## Authentication

### CarFacts.VideoFunction
- **Method**: Azure Functions host-level function-key authentication
- **Provider**: Azure Functions runtime (no identity provider; no OAuth/JWT)
- **Implementation**: All four HTTP functions
- **Findings**:
  - `StartVideo` (`HttpStartFunction.cs:30`) — `AuthorizationLevel.Function` ✅
  - `GenerateVideo` (`GenerateVideoFunction.cs:28`) — `AuthorizationLevel.Function` ✅
  - `GetVideoStatus` (`StatusFunction.cs:19`) — `AuthorizationLevel.Function` ✅
  - `GetJobLogs` (`LogsFunction.cs:25`) — `AuthorizationLevel.Function` ✅
  - No unauthenticated (`AuthorizationLevel.Anonymous`) endpoints. All callers must supply a valid function key via `?code=` query param or `x-functions-key` header.

### CarFacts.Functions
- **Method**: Timer-triggered (no HTTP surface requiring auth)
- **Findings**: Unchanged from previous analysis — acceptable.

## Authorization
- **Model**: None (no user identity model in either function app)
- **Coverage**: 100% of HTTP-triggered endpoints use function-key auth; activity and orchestrator triggers are internal-only
- **Unprotected endpoints**: None — Durable orchestrators and activities fire via internal Durable Task signals only

## Secret Management

### CarFacts.VideoFunction — All secrets flow through `IConfiguration` (app settings)

The VideoFunction has **no Key Vault integration**. Secrets are read directly from `IConfiguration` in `Program.cs` and `HttpStartFunction.cs` and then passed as constructor arguments or as fields in `OrchestratorInput`. There is no `ISecretProvider` abstraction, no `DefaultAzureCredential`, and no Key Vault reference pattern.

| Secret | Storage Location | Method | Rating | File |
|--------|-----------------|--------|--------|------|
| Storage account connection string (`stpocvideogen`) | `local.settings.json` (dev) / App Settings (prod) | Plaintext account key in connection string | 🔴 | `local.settings.json:4-6`, `Program.cs:17` |
| Azure Speech API Key | `local.settings.json` (dev) / App Settings (prod) | Plaintext string injected into `TtsService` constructor | 🔴 Dev / 🟡 Prod | `local.settings.json:7`, `Program.cs:25` |
| Pexels API Key | `local.settings.json` (dev) / App Settings (prod) | Plaintext, read by `HttpStartFunction`, passed via `OrchestratorInput` | 🔴 Dev / 🟡 Prod | `local.settings.json:10`, `HttpStartFunction.cs:47` |
| YouTube Data API Key | `local.settings.json` (dev) / App Settings (prod) | Plaintext, passed via `OrchestratorInput` | 🔴 Dev / 🟡 Prod | `local.settings.json:11`, `HttpStartFunction.cs:49` |
| Azure Computer Vision API Key | `local.settings.json` (dev) / App Settings (prod) | Plaintext, passed via `OrchestratorInput` | 🔴 Dev / 🟡 Prod | `local.settings.json:13`, `HttpStartFunction.cs:50-51` |
| App Insights API Key | App Settings (optional) | Plaintext, read at request time | 🟡 | `LogsFunction.cs:32` |

### CarFacts.Functions — Key Vault integration (unchanged)

| Secret | Storage Location | Method | Rating | File |
|--------|-----------------|--------|--------|------|
| Azure OpenAI API Key | Key Vault (prod) / `local.settings.json` (dev) | `DefaultAzureCredential` → KV / Config (dev) | 🟢 Prod / 🟡 Dev | `Program.cs:132-139`, `KeyVaultSecretProvider.cs:19` |
| Stability AI API Key | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `ImageGenerationService.cs:37` |
| Together AI API Key | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `TogetherAIImageGenerationService.cs:42` |
| WordPress OAuth Token | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `WordPressService.cs:362` |
| Twitter Consumer Key/Secret + Tokens | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `TwitterService.cs:58-61` |
| Facebook Page Access Token | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `FacebookService.cs:45` |
| Reddit App Secret / Username / Password | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `RedditService.cs:57-60` |
| Pinterest Access Token | Key Vault (prod) / `local.settings.json` (dev) | `ISecretProvider` abstraction | 🟢 Prod / 🟡 Dev | `PinterestService.cs:131` |
| Cosmos DB Connection String | Key Vault (prod) / `local.settings.json` (dev) | `DefaultAzureCredential` → KV (prod) | 🟡 (key-based conn string in KV) | `Program.cs:217-235` |
| Azure Storage Connection String | ARM `listKeys()` → App Settings | Plaintext key in app setting | 🟡 | `azuredeploy.json:176` |

### Positive findings (both projects)
- All `local.settings.json` files are correctly listed in `.gitignore` and **confirmed absent from all git commits** (`git log --all` returns no history for either file).
- `CarFacts.Functions` uses `DefaultAzureCredential` + Key Vault RBAC — no hardcoded KV credentials.
- ARM template uses `securestring` parameters for all deployment-time secrets (`azuredeploy.json:16-28`).
- Key Vault has `enableRbacAuthorization: true` and `enableSoftDelete: true` (`azuredeploy.json:115-117`).
- Function App has System-Assigned Managed Identity with Key Vault Secrets User role assigned at deploy time (`azuredeploy.json:158-162`, `azuredeploy.json:272-284`).

## Secret Leakage Risk

### 🔴 Critical — Secrets serialized into Durable Task Hub storage

`OrchestratorInput` (`VideoOrchestrator.cs:101-108`) is a C# record that carries `StorageConnectionString`, `PexelsApiKey`, `YouTubeApiKey`, `VisionEndpoint`, and `VisionApiKey` as plain string fields. The Durable Task Framework **serializes orchestrator inputs to JSON and stores them in Azure Table Storage** (the task hub). This means all five secrets are written to Azure Table Storage rows in plaintext and will be visible to:

- Anyone with read access to the `CarFactsVideoHub` storage tables
- Azure portal / Storage Explorer users with storage account access
- Any diagnostic tooling that queries the Durable task hub

**Severity: 🔴 Critical** — Secrets that should be ephemeral are being durably persisted.

| Risk | Location | Severity | Details |
|------|----------|----------|---------|
| Secrets persisted to Durable Task Hub | `VideoOrchestrator.cs:101-108`, `FetchClipActivityInput` (ActivityModels.cs:14-27) | 🔴 Critical | `StorageConnectionString`, `PexelsApiKey`, `YouTubeApiKey`, `VisionApiKey` serialized as JSON into Azure Table Storage task hub. Also repeated in every `FetchClipActivityInput` record per-segment. |
| App Insights App ID hardcoded in source | `LogsFunction.cs:23` | 🟡 Medium | `AppInsightsAppId = "adf71ab0-69e2-4d63-837a-49c287cd6bad"` is hardcoded. Not a secret (it's a read path identifier), but leaks Azure resource GUID. Move to app settings. |
| `Console.WriteLine` in production services | `ComputerVisionService.cs:59`, `YouTubeVideoService.cs:61`, `PexelsVideoService.cs:70` | 🟡 Medium | These services use `Console.WriteLine` instead of the injected `ILogger`. Output bypasses App Insights, log filtering, and sampling controls. If any of these print debug info that includes URLs with auth tokens (e.g., SAS URLs), those bypass monitoring entirely. |
| SAS URLs logged in activity output | `FetchClipActivity.cs:106`, `SynthesizeTtsActivity.cs:46`, `RenderVideoActivity` | 🟡 Medium | Trimmed SAS URLs are logged at `Information` level. SAS tokens encode a shared-key signature granting read access for 4–48 hours. While only partial URLs are logged (`[..60]`), the full URL is present in orchestrator output stored in Durable Task Hub. |
| Secret names logged at DEBUG level | `KeyVaultSecretProvider.cs:24` (CarFacts.Functions) | 🟢 Low | Logs secret names, not values. Acceptable. |
| Local secret usage logged as WARNING | `LocalSecretProvider.cs:26` | 🟢 Low | Warns on each local secret usage — good signal that this provider is dev-only. |
| API error response bodies logged verbatim | `TwitterService.cs:73`, `RedditService.cs:78`, `WordPressService.cs:279` | 🟡 Medium | External API errors may echo back token fragments. Truncate to 500 chars max. |
| Log files on disk in source tree | `src/logs/*.log` | 🟢 Low | `.gitignore`d; not committed. Contains operation details but no secret values confirmed. |

## Inter-Service Security

### CarFacts.VideoFunction

| Service Pair | Auth Method | TLS | Rating |
|-------------|-------------|-----|--------|
| VideoFunction → Azure Blob Storage | Storage account key (connection string) | Yes (HTTPS enforced) | 🔴 |
| VideoFunction → Azure Durable Task Hub (Table Storage) | Storage account key (connection string) | Yes | 🔴 |
| VideoFunction → Azure Speech (TTS) | API key passed as constructor arg | Yes (HTTPS) | 🟡 |
| VideoFunction → Azure Computer Vision | API key passed as constructor arg | Yes (HTTPS) | 🟡 |
| VideoFunction → Pexels API | API key in `Authorization` header | Yes (HTTPS) | 🟡 |
| VideoFunction → YouTube Data API v3 | API key in URL query string | Yes (HTTPS) | 🟡 |
| VideoFunction → App Insights REST API | API key in `x-api-key` header | Yes (HTTPS) | 🟡 |
| VideoFunction → yt-dlp (subprocess) | N/A (local binary) | N/A | 🟢 |
| VideoFunction → FFmpeg (subprocess) | N/A (local binary) | N/A | 🟢 |

> **Note — YouTube API key in URL**: `YouTubeVideoService.cs:116` appends `&key={youTubeApiKey}` directly to the URL. API keys in URLs are captured in server logs, proxy logs, browser history, and referrer headers. Prefer the `x-goog-api-key` request header instead.

### CarFacts.Functions (unchanged)

| Service Pair | Auth Method | TLS | Rating |
|-------------|-------------|-----|--------|
| App → Azure Key Vault | Managed Identity (`DefaultAzureCredential`) | Yes | 🟢 |
| App → Azure OpenAI | API Key via Key Vault | Yes | 🟢 |
| App → Stability AI | Bearer token via Key Vault | Yes | 🟢 |
| App → Together AI | Bearer token via Key Vault | Yes | 🟢 |
| App → WordPress.com | OAuth2 Bearer via Key Vault | Yes | 🟢 |
| App → Twitter/X API | OAuth 1.0a (HMAC-SHA1) via Key Vault | Yes | 🟡 |
| App → Facebook Graph API | Page Access Token (form body) via Key Vault | Yes | 🟡 |
| App → Reddit API | OAuth2 password grant via Key Vault | Yes | 🟡 |
| App → Pinterest API | OAuth2 Bearer via Key Vault | Yes | 🟢 |
| App → Cosmos DB | Connection string (key-based) via Key Vault | Yes | 🟡 |
| App → Azure Storage | Account key via ARM `listKeys()` | Yes | 🟡 |
| App → Azure App Configuration | Managed Identity | Yes | 🟢 |

## Dependency Security

### CarFacts.VideoFunction
| Package | Version | Notes |
|---------|---------|-------|
| `Microsoft.CognitiveServices.Speech` | (see .csproj) | Speech SDK — closed-source Microsoft package |
| `Azure.AI.Vision.ImageAnalysis` | (see .csproj) | Azure CV SDK — uses `AzureKeyCredential`, not `DefaultAzureCredential` |
| `Azure.Storage.Blobs` | (see .csproj) | Used with connection string, not MI |
| `Microsoft.Azure.Functions.Worker` | (see .csproj) | Isolated worker; Durable extensions |
| All packages | Pinned versions | 🟢 No floating `*` versions |

### CarFacts.Functions (unchanged)
| Package | Version | Status |
|---------|---------|--------|
| Azure.Identity | 1.14.2 | 🟢 |
| Azure.Security.KeyVault.Secrets | 4.7.0 | 🟢 |
| Microsoft.Azure.Cosmos | 3.43.1 | 🟢 |
| Microsoft.SemanticKernel | 1.47.0 | 🟢 |
| Serilog.Extensions.Hosting | 8.0.0 | 🟢 |

## Security Score

|  | CarFacts.VideoFunction | CarFacts.Functions |
|--|----------------------|-------------------|
| **Authentication** | ✅ All HTTP endpoints use `AuthorizationLevel.Function` | ✅ Timer-only; function-key for any HTTP |
| **Authorization** | ✅ No user-facing RBAC needed | ✅ Same |
| **Secret Management** | ❌ No Key Vault. Secrets in config only. | ⚠️ KV + MI in prod; plaintext in dev local settings |
| **Secret Leakage** | ❌ Secrets serialized to Durable Task Hub. YouTube key in URL. `Console.WriteLine` bypasses log filtering. | ⚠️ Secret names in logs; error bodies not truncated |
| **Inter-Service** | ❌ Storage account key used for all blob/table ops. No managed identity anywhere. | ⚠️ MI for KV/AppConfig; keys for Storage/Cosmos/3rd-party |

## Recommendations

### VideoFunction — Immediate (High Priority)

1. **🔴 ROTATE ALL CREDENTIALS NOW** — The following live credentials are present in plaintext on disk in `src/CarFacts.VideoFunction/local.settings.json`. Regenerate all of them immediately via their respective portals/APIs, regardless of whether they are still in active use:
   - Azure Storage account key for `stpocvideogen` (Azure Portal → Storage Account → Access Keys)
   - Azure Speech API key (Azure Portal → Cognitive Services → Keys and Endpoints)
   - Azure Computer Vision API key (same)
   - Pexels API key (pexels.com → API settings)
   - YouTube Data API v3 key (console.cloud.google.com → Credentials)

2. **🔴 Stop passing secrets through `OrchestratorInput`** — Move all secrets out of `OrchestratorInput` (`VideoOrchestrator.cs:101-108`) and `FetchClipActivityInput` (`ActivityModels.cs:14-27`). Activities should resolve secrets from `IConfiguration` (injected at DI registration time) rather than accepting them as serialized parameters. This prevents secrets from being durably persisted to the Durable Task Hub table rows. Refactor: inject `IConfiguration` or a dedicated `VideoFunctionSecrets` options class into each activity via DI, and remove secret fields from all activity input records.

3. **🔴 Add Key Vault integration to VideoFunction** — Mirror the pattern from `CarFacts.Functions`. Register `KeyVaultSecretProvider` with `DefaultAzureCredential` in `Program.cs`. Replace the current pattern of passing raw strings via DI constructor arguments for `TtsService` and `FfmpegManager` with a secrets abstraction. Example: the ARM template already has the managed identity and Key Vault infrastructure in place — just wire up `AddAzureKeyVault` in `Program.cs`.

4. **🟡 Move YouTube API key from URL query string to request header** — `YouTubeVideoService.cs:116` appends `&key={youTubeApiKey}` to the URL. This leaks the key to proxy logs, server access logs, and CDN logs. Use `request.Headers.Add("x-goog-api-key", youTubeApiKey)` instead. The YouTube Data API v3 accepts this header.

5. **🟡 Replace `Console.WriteLine` with `ILogger` in services** — `ComputerVisionService.cs:59`, `YouTubeVideoService.cs:61,86,129`, `PexelsVideoService.cs:70` all use `Console.WriteLine`. These bypass App Insights, log sampling, and filtering. Inject `ILogger<T>` into these services via constructor injection.

6. **🟡 Move App Insights App ID out of source code** — `LogsFunction.cs:23` hardcodes `AppInsightsAppId = "adf71ab0-69e2-4d63-837a-49c287cd6bad"`. Move to app settings as `AppInsights:AppId`. While this GUID is not a credential, it identifies an Azure resource and should not be committed to source.

7. **🟡 Use Managed Identity for Azure Storage** — Replace the storage account key connection string with a managed-identity-based connection. Set `AzureWebJobsStorage__accountName=stpocvideogen` and grant the Function App's managed identity the **Storage Blob Data Contributor** and **Storage Queue Data Contributor** roles. This eliminates the account key entirely.

### CarFacts.Functions — Carried Forward

8. **Migrate Cosmos DB to Managed Identity auth** — Replace connection-string-based `CosmosClient` with `DefaultAzureCredential` in `Program.cs:239`. Grant the Function App's MI **Cosmos DB Built-in Data Contributor** role. This eliminates the `CosmosDb-ConnectionString` secret from Key Vault entirely.

9. **Migrate Azure Storage to Managed Identity** — Replace `listKeys()` in `azuredeploy.json:176` with `AzureWebJobsStorage__accountName` pattern.

10. **Redact or truncate API error response bodies in logs** — `TwitterService.cs:73`, `RedditService.cs:78`, `FacebookService.cs:66`, `WordPressService.cs:279` log full external API error bodies. Limit to 500 chars and strip known token field names.

11. **Add secret caching with TTL to `KeyVaultSecretProvider`** — Currently every external API call fetches from Key Vault at runtime. Add in-memory caching with a 5-minute TTL to reduce latency and KV request volume.

12. **Use `dotnet user-secrets` for local development** — Replace plaintext `local.settings.json` secrets with `dotnet user-secrets set` entries, which are stored outside the source tree in the OS user profile directory. See: `dotnet user-secrets --project src/CarFacts.Functions/`.

