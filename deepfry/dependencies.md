<!-- deepfry:commit=b9be8dc1501e31ea9edfa99c938527818fa2aca5 agent=dependency-analyzer timestamp=2025-07-17T12:00:00Z -->

# Dependency Analysis — CarFacts

> Auto-generated dependency inventory for the `CarFacts` Azure Functions project.

---

## Package Dependencies

### Main Project (`src/CarFacts.Functions/CarFacts.Functions.csproj`)

| Package | Version | Source | Purpose |
|---------|---------|--------|---------|
| Azure.Identity | 1.14.2 | NuGet | Azure AD / Managed Identity authentication (DefaultAzureCredential) |
| Azure.Security.KeyVault.Secrets | 4.7.0 | NuGet | Azure Key Vault secret retrieval |
| Microsoft.Azure.Cosmos | 3.43.1 | NuGet | Azure Cosmos DB NoSQL client |
| Microsoft.Azure.Functions.Worker | 2.0.0 | NuGet | .NET isolated Azure Functions worker runtime |
| Microsoft.Azure.Functions.Worker.Extensions.DurableTask | 1.1.6 | NuGet | Durable Functions orchestration framework |
| Microsoft.Azure.Functions.Worker.Extensions.Timer | 4.3.1 | NuGet | Timer trigger binding for scheduled execution |
| Microsoft.Azure.Functions.Worker.Extensions.Http | 3.2.0 | NuGet | HTTP trigger binding |
| Microsoft.Azure.Functions.Worker.Sdk | 2.0.0 | NuGet | Functions worker SDK tooling |
| Microsoft.Extensions.Configuration.AzureAppConfiguration | 8.1.0 | NuGet | Azure App Configuration integration |
| Microsoft.Extensions.Http | 8.0.1 | NuGet | IHttpClientFactory for typed HTTP clients |
| Microsoft.ApplicationInsights.WorkerService | 2.22.0 | NuGet | Application Insights telemetry for worker services |
| Microsoft.Azure.Functions.Worker.ApplicationInsights | 2.0.0 | NuGet | Application Insights integration for Functions worker |
| Serilog.Extensions.Hosting | 8.0.0 | NuGet | Serilog host integration for structured logging |
| Serilog.Sinks.File | 6.0.0 | NuGet | Serilog file sink (local log files) |
| Serilog.Sinks.Console | 6.0.0 | NuGet | Serilog console sink |
| Microsoft.SemanticKernel | 1.47.0 | NuGet | AI orchestration — Azure OpenAI / OpenAI chat completion |

### Test Project (`tests/CarFacts.Functions.Tests/CarFacts.Functions.Tests.csproj`)

| Package | Version | Source | Purpose |
|---------|---------|--------|---------|
| Microsoft.NET.Test.Sdk | 17.11.1 | NuGet | .NET test host |
| xunit | 2.9.2 | NuGet | xUnit test framework |
| xunit.runner.visualstudio | 2.8.2 | NuGet | VS Test adapter for xUnit |
| Moq | 4.20.72 | NuGet | Mocking framework |
| FluentAssertions | 6.12.2 | NuGet | Fluent assertion library |
| Microsoft.SemanticKernel | 1.47.0 | NuGet | Semantic Kernel (referenced for test helpers) |

---

## Cloud Service Dependencies

### Azure Functions (Compute Host)
- **Type**: compute
- **Runtime**: .NET 8 isolated worker (`dotnet-isolated`), Azure Functions v4
- **Triggers**: Timer trigger (`0 0 6 * * *` — daily at 6 AM UTC), HTTP trigger, Durable Task orchestration
- **Connection pattern**: Hosted on Azure App Service plan (Dynamic/Y1 SKU)
- **Config source**: `src/CarFacts.Functions/host.json`, `local.settings.json`, `infra/azuredeploy.json`

### Azure Cosmos DB
- **Type**: database
- **Connection pattern**: `CosmosClient` singleton instantiated in `Program.cs` → `RegisterCosmosDb()` with connection string from Key Vault secret `CosmosDb-ConnectionString`
- **Sub-dependencies**:
  - Database: `carfacts`
    - Container: `fact-keywords` — Stores `FactKeywordRecord` documents; partitioned by `id`. Used by `CosmosFactKeywordStore` for keyword matching, backlink tracking, Pinterest count tracking, and social media count tracking.
    - Container: `social-media-queue` — Stores `SocialMediaQueueItem` documents; partitioned by `platform`. Used by `CosmosSocialMediaQueueStore` for scheduling/dequeuing social media posts.
- **Config source**: `local.settings.json` keys `CosmosDb__DatabaseName`, `CosmosDb__ContainerName`, `Secrets:CosmosDb-ConnectionString`
- **Code files**: `Services/CosmosFactKeywordStore.cs`, `Services/CosmosSocialMediaQueueStore.cs`, `Program.cs` lines 212–255, `Configuration/AppSettings.cs` (`CosmosDbSettings`)

### Azure Key Vault
- **Type**: auth-service
- **Connection pattern**: `SecretClient` (from `Azure.Security.KeyVault.Secrets`) using `DefaultAzureCredential` (Managed Identity in production)
- **Sub-dependencies** (secrets stored):
  - `AzureOpenAI-ApiKey` — Azure OpenAI API key
  - `StabilityAI-ApiKey` — Stability AI API key
  - `TogetherAI-ApiKey` — Together AI API key
  - `WordPress-OAuthToken` — WordPress.com OAuth2 bearer token
  - `CosmosDb-ConnectionString` — Cosmos DB connection string
  - `Twitter-ConsumerKey`, `Twitter-ConsumerSecret`, `Twitter-AccessToken`, `Twitter-AccessTokenSecret` — Twitter/X OAuth 1.0a credentials
  - `Facebook-PageAccessToken` — Facebook Graph API page access token
  - `Reddit-AppSecret`, `Reddit-Username`, `Reddit-Password` — Reddit script-app credentials
  - `Pinterest-AccessToken` — Pinterest API v5 OAuth2 token
  - `AdSense-ClientId`, `AdSense-SlotId` — Google AdSense identifiers (declared but usage not found in code)
- **Config source**: `local.settings.json` key `KeyVault__VaultUri`, `Configuration/SecretNames.cs`, `infra/azuredeploy.json` (provisions vault + 3 secrets + RBAC role assignment)
- **Code files**: `Services/KeyVaultSecretProvider.cs`, `Program.cs` lines 136–138, 225–230

### Azure Application Insights + Log Analytics
- **Type**: monitoring
- **Connection pattern**: `AddApplicationInsightsTelemetryWorkerService()` + `ConfigureFunctionsApplicationInsights()` in `Program.cs`; `host.json` configures sampling and log levels
- **Sub-dependencies**:
  - Application Insights component (`ai-carfacts`) — Web type, linked to Log Analytics workspace
  - Log Analytics workspace (`log-carfacts`) — PerGB2018 SKU, 30-day retention
- **Config source**: `local.settings.json` key `APPLICATIONINSIGHTS_CONNECTION_STRING`, `host.json`, `infra/azuredeploy.json`

### Azure Storage Account
- **Type**: storage
- **Connection pattern**: Used as `AzureWebJobsStorage` for the Azure Functions runtime (triggers, Durable Task hub state, content share). Connection string auto-generated from ARM template.
- **Sub-dependencies**:
  - Used implicitly by Durable Functions for orchestration state (task hub tables/blobs/queues)
  - File share: `func-carfacts` (WEBSITE_CONTENTSHARE)
- **Config source**: `local.settings.json` key `AzureWebJobsStorage` (`UseDevelopmentStorage=true` locally), `infra/azuredeploy.json`

### Azure App Configuration (Optional)
- **Type**: cloud-service
- **Connection pattern**: `AddAzureAppConfiguration()` in `Program.cs` — connects via `DefaultAzureCredential` to an endpoint from `AppConfiguration__Endpoint`. Only activates if the config key is non-empty.
- **Sub-dependencies**: Cannot be determined — no key filters or labels specified beyond `Select("*")`
- **Config source**: `Program.cs` lines 33–42

### Azure OpenAI Service
- **Type**: ai-service
- **Connection pattern**: Semantic Kernel `AddAzureOpenAIChatCompletion()` with API key from Key Vault; supports fallback to direct OpenAI via `AddOpenAIChatCompletion()` based on `AI:TextProvider` setting
- **Sub-dependencies**:
  - Endpoint: `https://aifoundry-demo-idc-1.cognitiveservices.azure.com/`
  - Deployment: `gpt-4o-mini`
  - API version: `2025-01-01-preview` (production setting in ARM template)
- **Config source**: `local.settings.json` keys `AI__TextProvider`, `AI__AzureOpenAIEndpoint`, `AI__AzureOpenAIDeploymentName`, `Configuration/AppSettings.cs` (`AISettings`)
- **Code files**: `Program.cs` lines 122–164, `Services/ContentGenerationService.cs`, `Services/SeoGenerationService.cs`

---

## External API Dependencies (Non-Azure)

### Stability AI (Image Generation)
- **Type**: ai-service
- **Connection pattern**: Direct REST API calls via `HttpClient` to `https://api.stability.ai/v1/generation/{model}/text-to-image`
- **Sub-dependencies**:
  - Model: `stable-diffusion-xl-1024-v1-0`
  - Resolution: 1024×1024, 30 steps, cfg_scale 7
- **Config source**: `local.settings.json` keys `StabilityAI__*`, `Configuration/AppSettings.cs` (`StabilityAISettings`)
- **Code files**: `Services/ImageGenerationService.cs`

### Together AI (Image Generation — Fallback)
- **Type**: ai-service
- **Connection pattern**: Direct REST API calls via `HttpClient` to `https://api.together.xyz/v1/images/generations`
- **Sub-dependencies**:
  - Model: `black-forest-labs/FLUX.1-schnell-Free` (local) / `FLUX.1.1-pro` (default in settings class)
  - Resolution: 1024×1024, 20 steps
- **Config source**: `local.settings.json` keys `TogetherAI__*`, `Configuration/AppSettings.cs` (`TogetherAISettings`)
- **Code files**: `Services/TogetherAIImageGenerationService.cs`

### OpenAI (Text Generation — Alternative)
- **Type**: ai-service
- **Connection pattern**: Semantic Kernel `AddOpenAIChatCompletion()` — only used when `AI:TextProvider` is set to `"OpenAI"` (default is `"AzureOpenAI"`)
- **Sub-dependencies**:
  - Model: `gpt-4o-mini`
- **Config source**: `local.settings.json` keys `AI__OpenAIModelId`, `Secrets:OpenAI-ApiKey`
- **Code files**: `Program.cs` lines 146–149

### WordPress.com REST API
- **Type**: other (CMS / publishing platform)
- **Connection pattern**: `HttpClient` calls to `https://public-api.wordpress.com/rest/v1.1/sites/{siteId}/...` with OAuth2 Bearer token from Key Vault
- **Sub-dependencies**:
  - Site: `carfacts5.wordpress.com`
  - Endpoints: `posts/new`, `posts/{id}`, `media/new`, `media/{id}`
- **Config source**: `local.settings.json` keys `WordPress__*`, `Configuration/AppSettings.cs` (`WordPressSettings`)
- **Code files**: `Services/WordPressService.cs`

### Twitter/X API v2
- **Type**: other (social media)
- **Connection pattern**: `HttpClient` with OAuth 1.0a HMAC-SHA1 signature; endpoints at `https://api.twitter.com/2/tweets`, `/search/recent`, `/users/me`, `/users/{id}/likes`
- **Config source**: `local.settings.json` keys `SocialMedia__Twitter*`, `Configuration/SecretNames.cs`
- **Code files**: `Services/TwitterService.cs`

### Facebook Graph API v21.0
- **Type**: other (social media)
- **Connection pattern**: `HttpClient` POST to `https://graph.facebook.com/v21.0/{pageId}/feed` with page access token
- **Config source**: `local.settings.json` keys `SocialMedia__Facebook*`, `Configuration/SecretNames.cs`
- **Code files**: `Services/FacebookService.cs`

### Reddit API (OAuth2 Script App)
- **Type**: other (social media)
- **Connection pattern**: Two-step — OAuth2 password grant via `https://www.reddit.com/api/v1/access_token`, then link submission via `https://oauth.reddit.com/api/submit`
- **Sub-dependencies**:
  - Target subreddits: `cars`, `todayilearned`, `carshistoricalfacts` (configured)
- **Config source**: `local.settings.json` keys `SocialMedia__Reddit*`, `Configuration/SecretNames.cs`
- **Code files**: `Services/RedditService.cs`

### Pinterest API v5
- **Type**: other (social media)
- **Connection pattern**: `HttpClient` calls to `https://api.pinterest.com/v5/` (boards, pins) with OAuth2 Bearer token from Key Vault
- **Sub-dependencies**:
  - Default board: `Car Facts`
  - Taxonomy boards: 10 categorized boards (American Muscle Cars, Electric Vehicles, Classic European Cars, etc.) defined in `PinterestBoardTaxonomy.cs`
- **Config source**: `local.settings.json` keys `SocialMedia__Pinterest*`, `Configuration/SecretNames.cs`
- **Code files**: `Services/PinterestService.cs`, `Configuration/PinterestBoardTaxonomy.cs`

---

## Duplicate Dependencies

### ⚠️ Image Generation has 2 providers (by design — fallback chain)
- **Provider 1**: `ImageGenerationService` → Stability AI (`stable-diffusion-xl-1024-v1-0`)
- **Provider 2**: `TogetherAIImageGenerationService` → Together AI (`FLUX.1-schnell-Free`)
- **Pattern**: Production uses `FallbackImageGenerationService` which tries StabilityAI first, then TogetherAI. Local dev uses one or the other based on `AI:ImageProvider` setting, wrapped in `CachedImageGenerationService`.
- **Recommendation**: ✅ Intentional redundancy — fallback chain for resilience. No consolidation needed.

### ⚠️ Text Generation has 2 providers (switchable, not simultaneous)
- **Provider 1**: Azure OpenAI via Semantic Kernel `AddAzureOpenAIChatCompletion()` (default)
- **Provider 2**: OpenAI via Semantic Kernel `AddOpenAIChatCompletion()` (when `AI:TextProvider = "OpenAI"`)
- **Pattern**: Compile-time switch in `Program.cs` → `RegisterTextProvider()`. Only one provider is registered per app instance.
- **Recommendation**: ✅ Intentional flexibility — switch statement, not duplicate registration. No issue.

### ⚠️ Logging has 2 frameworks
- **Framework 1**: **Application Insights** — production telemetry via `Microsoft.ApplicationInsights.WorkerService` + `Microsoft.Azure.Functions.Worker.ApplicationInsights`
- **Framework 2**: **Serilog** — local-only file + console logging via `Serilog.Extensions.Hosting`, `Serilog.Sinks.File`, `Serilog.Sinks.Console`
- **Pattern**: Serilog is only activated when running locally (`!isAzure`); Application Insights is always registered. The `NoWarn` suppressions (`SKEXP0001`, `SKEXP0010`) relate to Semantic Kernel experimental APIs, not logging.
- **Recommendation**: ⚠️ Minor concern — in local dev both App Insights and Serilog may be active simultaneously. Consider conditionally registering App Insights only in production, or using Serilog's App Insights sink to unify.

### ⚠️ Cosmos DB `CosmosDbSettings.ContainerName` vs hardcoded container name
- **Store 1**: `CosmosFactKeywordStore` uses `settings.Value.ContainerName` (configured as `"fact-keywords"`)
- **Store 2**: `CosmosSocialMediaQueueStore` hardcodes `"social-media-queue"` instead of reading from settings
- **Recommendation**: ⚠️ Inconsistency — consider adding a second container name to `CosmosDbSettings` (e.g., `QueueContainerName`) rather than hardcoding in `CosmosSocialMediaQueueStore.cs` line 20.

---

## Infrastructure Resources (from `infra/azuredeploy.json`)

| Resource | ARM Type | Naming Pattern |
|----------|----------|----------------|
| Log Analytics Workspace | `Microsoft.OperationalInsights/workspaces` | `log-{prefix}` |
| Application Insights | `Microsoft.Insights/components` | `ai-{prefix}` |
| Storage Account | `Microsoft.Storage/storageAccounts` | `st{prefix}` |
| App Service Plan | `Microsoft.Web/serverfarms` | `asp-{prefix}` (Y1/Dynamic) |
| Key Vault | `Microsoft.KeyVault/vaults` | `kv-{prefix}` |
| Key Vault Secrets (×3) | `Microsoft.KeyVault/vaults/secrets` | `AzureOpenAI-ApiKey`, `StabilityAI-ApiKey`, `WordPress-OAuthToken` |
| Function App | `Microsoft.Web/sites` | `func-{prefix}` (SystemAssigned identity) |
| RBAC Role Assignment | `Microsoft.Authorization/roleAssignments` | Key Vault Secrets User for Function App MSI |

> **Note**: Cosmos DB is **not** provisioned by the ARM template — it must be created separately. The connection string is stored in Key Vault.

---

## Dependency Summary

| Metric | Count |
|--------|-------|
| **NuGet packages (main project)** | 16 |
| **NuGet packages (test project)** | 6 (5 unique + 1 shared) |
| **Azure cloud services** | 6 (Functions, Cosmos DB, Key Vault, App Insights + Log Analytics, Storage, App Configuration) |
| **External API services** | 7 (Azure OpenAI, OpenAI, Stability AI, Together AI, WordPress, Twitter, Facebook, Reddit, Pinterest — 9 total endpoints, 7 distinct services) |
| **Cosmos DB containers** | 2 (`fact-keywords`, `social-media-queue`) |
| **Key Vault secrets** | 16 declared in `SecretNames.cs` |
| **ARM-provisioned resources** | 7 |
| **Detected duplicates / inconsistencies** | 4 (image providers ✅, text providers ✅, logging ⚠️, Cosmos container config ⚠️) |
