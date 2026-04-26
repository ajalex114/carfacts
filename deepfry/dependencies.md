<!-- deepfry:commit=5360e5707b59a6cf919f9c880a227006d8f33b09 agent=dependency-analyzer timestamp=2026-04-24T18:00:00Z -->

# Dependency Analysis — CarFacts

> Comprehensive dependency inventory covering all three projects in the CarFacts solution:
> `src/CarFacts.VideoFunction/` (main AI video pipeline), `src/CarFacts.Functions/` (daily content + social media), `src/CarFacts.VideoPoC/` (local prototype).

---

## Package Dependencies

### `src/CarFacts.VideoFunction/` — AI Video Pipeline (main production project)

| Package | Version | Source | Purpose |
|---------|---------|--------|---------|
| Microsoft.Azure.Functions.Worker | 2.0.0 | NuGet | .NET isolated Azure Functions worker runtime |
| Microsoft.Azure.Functions.Worker.Extensions.DurableTask | 1.1.3 | NuGet | Durable Functions orchestration (fan-out clip fetching) |
| Microsoft.Azure.Functions.Worker.Extensions.Http | 3.2.0 | NuGet | HTTP trigger binding (`/api/start-video`, `/api/status/*`) |
| Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore | 1.3.2 | NuGet | ASP.NET Core integration for HTTP functions |
| Microsoft.Azure.Functions.Worker.Sdk | 2.0.0 | NuGet | Functions worker SDK tooling (build-time, `PrivateAssets=All`) |
| Microsoft.ApplicationInsights.WorkerService | 2.22.0 | NuGet | Application Insights telemetry |
| Microsoft.Azure.Functions.Worker.ApplicationInsights | 1.4.0 | NuGet | Application Insights Functions worker integration |
| Microsoft.CognitiveServices.Speech | 1.43.0 | NuGet | Azure Cognitive Services Speech SDK — TTS narration synthesis |
| Azure.AI.Vision.ImageAnalysis | 1.0.0 | NuGet | Azure Computer Vision — thumbnail watermark + car detection |
| Azure.Storage.Blobs | 12.23.0 | NuGet | Azure Blob Storage — tool download, clip upload, final video upload |

### `src/CarFacts.Functions/` — Daily Content & Social Media

| Package | Version | Source | Purpose |
|---------|---------|--------|---------|
| Azure.Identity | 1.14.2 | NuGet | Azure AD / Managed Identity authentication (DefaultAzureCredential) |
| Azure.Security.KeyVault.Secrets | 4.7.0 | NuGet | Azure Key Vault secret retrieval |
| Microsoft.Azure.Cosmos | 3.43.1 | NuGet | Azure Cosmos DB NoSQL client |
| Microsoft.Azure.Functions.Worker | 2.0.0 | NuGet | .NET isolated Azure Functions worker runtime |
| Microsoft.Azure.Functions.Worker.Extensions.DurableTask | 1.1.6 | NuGet | Durable Functions orchestration |
| Microsoft.Azure.Functions.Worker.Extensions.Timer | 4.3.1 | NuGet | Timer trigger binding (`0 0 6 * * *` — daily at 6 AM UTC) |
| Microsoft.Azure.Functions.Worker.Extensions.Http | 3.2.0 | NuGet | HTTP trigger binding |
| Microsoft.Azure.Functions.Worker.Sdk | 2.0.0 | NuGet | Functions worker SDK tooling |
| Microsoft.Extensions.Configuration.AzureAppConfiguration | 8.1.0 | NuGet | Azure App Configuration integration |
| Microsoft.Extensions.Http | 8.0.1 | NuGet | IHttpClientFactory for typed HTTP clients |
| Microsoft.ApplicationInsights.WorkerService | 2.22.0 | NuGet | Application Insights telemetry for worker services |
| Microsoft.Azure.Functions.Worker.ApplicationInsights | 2.0.0 | NuGet | Application Insights Functions worker integration |
| Serilog.Extensions.Hosting | 8.0.0 | NuGet | Serilog host integration (local dev only) |
| Serilog.Sinks.File | 6.0.0 | NuGet | Serilog file sink — writes to `logs/carfacts-{timestamp}.log` locally |
| Serilog.Sinks.Console | 6.0.0 | NuGet | Serilog console sink |
| Microsoft.SemanticKernel | 1.47.0 | NuGet | AI orchestration — Azure OpenAI / OpenAI chat completion |

### `src/CarFacts.VideoPoC/` — Local Video Prototype (console app, not deployed)

| Package | Version | Source | Purpose |
|---------|---------|--------|---------|
| Microsoft.CognitiveServices.Speech | 1.40.0 | NuGet | Azure Speech SDK — TTS (older version vs VideoFunction's 1.43.0) |
| Microsoft.Extensions.Configuration | 8.0.0 | NuGet | Configuration abstraction |
| Microsoft.Extensions.Configuration.Json | 8.0.0 | NuGet | JSON config file support (`appsettings.json`) |
| Microsoft.Extensions.Configuration.EnvironmentVariables | 8.0.0 | NuGet | Env var config with `CARFACTS_` prefix |
| System.Speech | 8.0.0 | NuGet | Windows built-in TTS fallback (Windows only) |

---

## Cloud Service Dependencies

---

### Azure Functions (Compute Host — both projects)
- **Type**: compute
- **Runtime**: .NET 8 isolated worker (`dotnet-isolated`), Azure Functions v4
- **Projects**: `CarFacts.VideoFunction` and `CarFacts.Functions`
- **Triggers**:
  - `CarFacts.VideoFunction`: HTTP trigger (`POST /api/start-video`), Durable Task orchestration triggers (orchestrator, activity), HTTP status/log polling triggers
  - `CarFacts.Functions`: Timer trigger (`0 0 6 * * *` — daily 6 AM UTC), HTTP triggers, Durable Task orchestration
- **Durable task hubs**:
  - `CarFactsVideoHub` — used by `CarFacts.VideoFunction` (`host.json`)
  - (default hub) — used by `CarFacts.Functions`
- **Config source**: `src/CarFacts.VideoFunction/host.json`, `src/CarFacts.Functions/host.json`, `infra/azuredeploy.json`

---

### Azure Blob Storage — `CarFacts.VideoFunction`
- **Type**: storage
- **Connection pattern**: `Azure.Storage.Blobs.BlobContainerClient` and `BlobClient` using a connection string from `Storage:ConnectionString` config key; also used as `AzureWebJobsStorage` for the Functions runtime
- **Storage account**: `stpocvideogen` (from `local.settings.json`)
- **Sub-dependencies (containers)**:

| Container | Purpose | Code |
|-----------|---------|------|
| `poc-tools` | Stores `ffmpeg.exe` and `yt-dlp.exe` binaries + optional `youtube-cookies.txt`. Downloaded to `C:\local\Temp` on cold start. | `FfmpegManager.cs`, `YtDlpManager.cs` |
| `poc-jobs/{jobId}/clip_{NN}.mp4` | Intermediate trimmed video clips (one per segment, per job). 4-hour SAS URL returned to orchestrator. | `FetchClipActivity.cs` line 103 |
| `poc-videos` | Final rendered MP4 files (`carfact-{date}-{jobId}.mp4`). 48-hour SAS URL returned as job result. | `RenderVideoActivity.cs` line 120 |

- **Config source**: `src/CarFacts.VideoFunction/local.settings.json` keys `AzureWebJobsStorage` and `Storage:ConnectionString`
- **Code files**: `Services/FfmpegManager.cs`, `Services/YtDlpManager.cs`, `Services/VideoStorageService.cs`, `Activities/FetchClipActivity.cs`, `Activities/RenderVideoActivity.cs`

---

### Azure Blob Storage — `CarFacts.Functions`
- **Type**: storage
- **Connection pattern**: Used as `AzureWebJobsStorage` for the Azure Functions runtime (Durable Task state, content share). Connection string auto-generated from ARM template.
- **Sub-dependencies**:
  - Durable Functions orchestration state (tables/blobs/queues — implicit, managed by runtime)
  - File share: `func-carfacts` (`WEBSITE_CONTENTSHARE` in ARM template)
- **Config source**: `local.settings.json` key `AzureWebJobsStorage` (`UseDevelopmentStorage=true` locally), `infra/azuredeploy.json`

---

### Azure Cognitive Services — Speech (Text-to-Speech)
- **Type**: ai-service
- **Connection pattern**: `Microsoft.CognitiveServices.Speech.SpeechConfig.FromSubscription(key, region)` → `SpeechSynthesizer` with SSML input. Word boundary events capture per-word timestamps for subtitle generation.
- **Projects**: `CarFacts.VideoFunction` (primary), `CarFacts.VideoPoC` (prototype, v1.40.0)
- **Sub-dependencies**:
  - Endpoint region: `centralindia` (from `Speech:Region` config)
  - Voice: `en-US-AndrewNeural` (Neural TTS)
  - SSML prosody: `rate='0.88'` for slightly slower narration
- **Config source**: `src/CarFacts.VideoFunction/local.settings.json` keys `Speech:Key`, `Speech:Region`, `Speech:VoiceName`; configured directly in `Program.cs` `AddSingleton<TtsService>()`
- **Code files**: `Services/TtsService.cs`, `Activities/SynthesizeTtsActivity.cs`, `Program.cs` lines 24–27

---

### Azure Cognitive Services — Computer Vision (Image Analysis)
- **Type**: ai-service
- **Connection pattern**: `Azure.AI.Vision.ImageAnalysis.ImageAnalysisClient` using `AzureKeyCredential`. Analyzes YouTube thumbnail URLs (not local images) via `VisualFeatures.Read | VisualFeatures.Tags`.
- **Projects**: `CarFacts.VideoFunction`
- **Sub-dependencies**:
  - Endpoint: `https://cog-poc-vision-f4f54.cognitiveservices.azure.com/` (from `Vision:Endpoint`)
  - Features used: OCR (`Read`) for watermark detection in corner zones; `Tags` for car-presence detection
  - Car tag vocabulary: 25 tags (`car`, `vehicle`, `automobile`, `sports car`, etc.); confidence threshold ≥ 0.60
  - Watermark threshold: ≥ 8 chars of text in corner zones (30% width, 20% height)
- **Config source**: `src/CarFacts.VideoFunction/local.settings.json` keys `Vision:Endpoint`, `Vision:ApiKey`; passed through orchestrator input chain
- **Code files**: `Services/ComputerVisionService.cs`, `Activities/FetchClipActivity.cs` line 51, `Functions/HttpStartFunction.cs` lines 49–51

---

### Azure Cosmos DB — `CarFacts.Functions`
- **Type**: database
- **Connection pattern**: `CosmosClient` singleton in `Program.cs` → `RegisterCosmosDb()` with connection string from Key Vault secret `CosmosDb-ConnectionString`; `DefaultAzureCredential` used to read Key Vault in production
- **Sub-dependencies**:
  - **Database**: `carfacts`
    - **Container**: `fact-keywords` — `FactKeywordRecord` documents; partition key `/id`. Stores car fact keywords, backlink counts, social share counts, Pinterest board history, image URLs. Used by `CosmosFactKeywordStore`.
    - **Container**: `social-media-queue` — `SocialMediaQueueItem` documents; partition key `/platform`. Queues pre-generated social media posts for later publishing. Used by `CosmosSocialMediaQueueStore`. ⚠️ Container name is hardcoded in `CosmosSocialMediaQueueStore.cs` line 20, not read from `CosmosDbSettings`.
- **Config source**: `local.settings.json` keys `CosmosDb__DatabaseName` (`"carfacts"`), `CosmosDb__ContainerName` (`"fact-keywords"`), `Secrets:CosmosDb-ConnectionString`
- **Code files**: `Services/CosmosFactKeywordStore.cs`, `Services/CosmosSocialMediaQueueStore.cs`, `Program.cs` lines 212–255, `Configuration/AppSettings.cs` (`CosmosDbSettings`)
- **Note**: Cosmos DB is **not provisioned** by `infra/azuredeploy.json` — must be created separately

---

### Azure Key Vault — `CarFacts.Functions`
- **Type**: auth-service
- **Connection pattern**: `Azure.Security.KeyVault.Secrets.SecretClient` using `DefaultAzureCredential` (Managed Identity in production, local env vars in dev). `ISecretProvider` abstraction switches to `LocalSecretProvider` (reads from `Secrets:*` config) when running locally.
- **Sub-dependencies (secrets)**:

| Secret Name | Used For |
|-------------|---------|
| `AzureOpenAI-ApiKey` | Azure OpenAI chat completion via Semantic Kernel |
| `StabilityAI-ApiKey` | Stability AI image generation |
| `TogetherAI-ApiKey` | Together AI image generation (fallback) |
| `WordPress-OAuthToken` | WordPress.com REST API v1.1 OAuth2 Bearer token |
| `CosmosDb-ConnectionString` | Azure Cosmos DB connection |
| `Twitter-ConsumerKey` | Twitter/X API v2 OAuth 1.0a |
| `Twitter-ConsumerSecret` | Twitter/X API v2 OAuth 1.0a |
| `Twitter-AccessToken` | Twitter/X API v2 OAuth 1.0a |
| `Twitter-AccessTokenSecret` | Twitter/X API v2 OAuth 1.0a |
| `Facebook-PageAccessToken` | Facebook Graph API page token |
| `Reddit-AppSecret` | Reddit script-app OAuth2 |
| `Reddit-Username` | Reddit OAuth2 password grant |
| `Reddit-Password` | Reddit OAuth2 password grant |
| `Pinterest-AccessToken` | Pinterest API v5 OAuth2 Bearer token |
| `AdSense-ClientId` | Google AdSense (declared in `SecretNames.cs`, usage not found in code) |
| `AdSense-SlotId` | Google AdSense (declared in `SecretNames.cs`, usage not found in code) |

- **Config source**: `local.settings.json` key `KeyVault__VaultUri` (`https://kv-carfacts.vault.azure.net/`), `Configuration/SecretNames.cs`, `infra/azuredeploy.json` (provisions vault + 3 secrets + RBAC assignment)
- **Code files**: `Services/KeyVaultSecretProvider.cs`, `Services/LocalSecretProvider.cs`, `Program.cs` lines 85–91, 136–138, 225–230

---

### Azure Application Insights + Log Analytics (both projects)
- **Type**: monitoring
- **Connection pattern**: `AddApplicationInsightsTelemetryWorkerService()` + `ConfigureFunctionsApplicationInsights()` in both `Program.cs` files; `host.json` configures sampling (excluding `Request` type) and log levels
- **Sub-dependencies**:
  - Application Insights component `ai-carfacts` (Web type)
  - Log Analytics workspace `log-carfacts` (PerGB2018 SKU, 30-day retention)
- **Config source**: `local.settings.json` key `APPLICATIONINSIGHTS_CONNECTION_STRING`, `host.json` in both projects, `infra/azuredeploy.json`

---

### Azure App Configuration (optional — `CarFacts.Functions` only)
- **Type**: cloud-service
- **Connection pattern**: `config.AddAzureAppConfiguration()` using `DefaultAzureCredential` to endpoint from `AppConfiguration__Endpoint`. Only activates if the config key is non-empty (not set in local.settings.json, so effectively production-only).
- **Sub-dependencies**: Key filter `Select("*")` with no label — fetches all keys
- **Config source**: `src/CarFacts.Functions/Program.cs` lines 33–42; endpoint key `AppConfiguration__Endpoint`

---

### Azure OpenAI Service — `CarFacts.Functions`
- **Type**: ai-service
- **Connection pattern**: Semantic Kernel `AddAzureOpenAIChatCompletion()` with API key from Key Vault or local config. Supports switchable fallback to direct OpenAI via `AddOpenAIChatCompletion()` based on `AI:TextProvider` config.
- **Sub-dependencies**:
  - Endpoint: `https://aifoundry-demo-idc-1.cognitiveservices.azure.com/` (from `local.settings.json`)
  - Deployment: `gpt-4o-mini`
  - API version: `2025-01-01-preview` (ARM template app setting `AI__AzureOpenAIApiVersion`)
- **Config source**: `local.settings.json` keys `AI__TextProvider`, `AI__AzureOpenAIEndpoint`, `AI__AzureOpenAIDeploymentName`; `Configuration/AppSettings.cs` (`AISettings`)
- **Code files**: `Program.cs` lines 122–164, `Services/ContentGenerationService.cs`, `Services/SeoGenerationService.cs`

---

## External API Dependencies

### Pexels Video API — `CarFacts.VideoFunction` and `CarFacts.VideoPoC`
- **Type**: other (stock video)
- **Connection pattern**: Raw `HttpClient` with API key in `Authorization` header. `GET https://api.pexels.com/videos/search?query=...&per_page=10&orientation=portrait`
- **Three-tier fallback query strategy** (in `FetchClipActivity.cs`):
  1. Model-specific query (e.g., `"Ford Mustang exterior rolling b-roll footage"`)
  2. Brand fallback (e.g., `"Ford Mustang car driving road footage"`)
  3. Brand-only fallback (e.g., `"Ford car driving road footage"`)
  4. Generic last resort: `"car driving road footage"`
- **Clip selection**: Portrait orientation preferred, smallest MP4 file ≥ 540px wide
- **Local clip cache**: `poc_output/clips_cache/` (VideoPoC), `C:\local\Temp\clip-{jobId}-{idx}\` (VideoFunction)
- **Config source**: `src/CarFacts.VideoFunction/local.settings.json` key `Pexels:ApiKey`; `src/CarFacts.VideoPoC/appsettings.json` key `Pexels:ApiKey`
- **Code files**: `Services/PexelsVideoService.cs` (VideoPoC), `Activities/FetchClipActivity.cs` (VideoFunction)

---

### YouTube Data API v3 — `CarFacts.VideoFunction`
- **Type**: other (video search)
- **Connection pattern**: Raw `HttpClient`. `GET https://www.googleapis.com/youtube/v3/search?part=snippet&q=...&type=video&videoLicense=creativeCommon&videoDefinition=high&videoDuration=short&maxResults=10&key={apiKey}`
- **Usage**: Primary clip source (Creative Commons licensed videos). Three-layer filter:
  1. API filter: `videoLicense=creativeCommon`, `videoDefinition=high`, `videoDuration=short`
  2. Title scoring: skip review/vlog/tutorial keywords; bonus for footage/cinematic/4k
  3. Azure Computer Vision check on `hqdefault.jpg` thumbnail (watermark + car presence)
- **yt-dlp integration**: After finding a candidate, `yt-dlp.exe` (downloaded from `poc-tools` blob) downloads only the needed segment via `--download-sections`
- **Config source**: `src/CarFacts.VideoFunction/local.settings.json` key `YouTube:ApiKey`; passed to orchestrator via `OrchestratorInput`
- **Code files**: `Services/YouTubeVideoService.cs`, `Activities/FetchClipActivity.cs`

---

### Stability AI (Image Generation) — `CarFacts.Functions`
- **Type**: ai-service
- **Connection pattern**: `HttpClient` POST to `https://api.stability.ai/v1/generation/{model}/text-to-image` with Bearer token. Retries up to 3× on 429, with exponential backoff.
- **Sub-dependencies**:
  - Model: `stable-diffusion-xl-1024-v1-0`
  - Resolution: 1024×1024, 30 steps, cfg_scale 7
- **Config source**: `local.settings.json` keys `StabilityAI__*`, `Configuration/AppSettings.cs` (`StabilityAISettings`)
- **Code files**: `Services/ImageGenerationService.cs`

---

### Together AI (Image Generation — Fallback) — `CarFacts.Functions`
- **Type**: ai-service
- **Connection pattern**: `HttpClient` POST to `https://api.together.xyz/v1/images/generations` with Bearer token
- **Sub-dependencies**:
  - Model: `black-forest-labs/FLUX.1-schnell-Free` (local) / `FLUX.1.1-pro` (settings class default)
  - Resolution: 1024×1024, 20 steps
- **Config source**: `local.settings.json` keys `TogetherAI__*`, `Configuration/AppSettings.cs` (`TogetherAISettings`)
- **Code files**: `Services/TogetherAIImageGenerationService.cs`

---

### OpenAI (Text Generation — Alternative) — `CarFacts.Functions`
- **Type**: ai-service
- **Connection pattern**: Semantic Kernel `AddOpenAIChatCompletion()` — only active when `AI:TextProvider = "OpenAI"` (default is `"AzureOpenAI"`)
- **Sub-dependencies**:
  - Model: `gpt-4o-mini`
- **Config source**: `local.settings.json` keys `AI__OpenAIModelId`, `Secrets:OpenAI-ApiKey`
- **Code files**: `Program.cs` lines 146–149

---

### WordPress.com REST API v1.1 — `CarFacts.Functions`
- **Type**: other (CMS / publishing)
- **Connection pattern**: `HttpClient` to `https://public-api.wordpress.com/rest/v1.1/sites/{siteId}/...` with OAuth2 Bearer token from Key Vault
- **Sub-dependencies**:
  - Site: `carfacts5.wordpress.com`
  - Endpoints: `posts/new`, `posts/{id}`, `media/new`, `media/{id}`
  - Post types: `standard`, `web-story`
- **Config source**: `local.settings.json` keys `WordPress__SiteId`, `WordPress__PostStatus`, `WordPress__Username`
- **Code files**: `Services/WordPressService.cs`

---

### Twitter/X API v2 — `CarFacts.Functions`
- **Type**: other (social media)
- **Connection pattern**: `HttpClient` with OAuth 1.0a HMAC-SHA1 signatures (computed inline, no library)
- **Endpoints used**:
  - `POST https://api.twitter.com/2/tweets` — post new tweet
  - `GET https://api.twitter.com/2/tweets/search/recent` — search for car tweets to reply to
  - `GET https://api.twitter.com/2/users/me` — get authenticated user ID
  - `POST https://api.twitter.com/2/users/{id}/likes` — like a tweet
- **Config source**: `local.settings.json` keys `SocialMedia__Twitter*`, Key Vault secrets `Twitter-*`
- **Code files**: `Services/TwitterService.cs`

---

### Facebook Graph API v21.0 — `CarFacts.Functions`
- **Type**: other (social media)
- **Connection pattern**: `HttpClient` POST to `https://graph.facebook.com/v21.0/{pageId}/feed` with page access token from Key Vault
- **Status**: Configured but `FacebookEnabled` defaults to `false` in `local.settings.json`
- **Config source**: `local.settings.json` keys `SocialMedia__Facebook*`, Key Vault secret `Facebook-PageAccessToken`
- **Code files**: `Services/FacebookService.cs`

---

### Reddit API (OAuth2 Script App) — `CarFacts.Functions`
- **Type**: other (social media)
- **Connection pattern**: Two-step — OAuth2 password grant via `https://www.reddit.com/api/v1/access_token`, then link submission via `https://oauth.reddit.com/api/submit`
- **Sub-dependencies**:
  - Target subreddits: `cars`, `todayilearned`, `carshistoricalfacts` (from `local.settings.json`)
- **Status**: Configured but `RedditEnabled` defaults to `false`
- **Config source**: `local.settings.json` keys `SocialMedia__Reddit*`, Key Vault secrets `Reddit-*`
- **Code files**: `Services/RedditService.cs`

---

### Pinterest API v5 — `CarFacts.Functions`
- **Type**: other (social media)
- **Connection pattern**: `HttpClient` to `https://api.pinterest.com/v5/` (boards + pins endpoints) with OAuth2 Bearer token from Key Vault
- **Sub-dependencies**:
  - Default board: `Car Facts`
  - Taxonomy: 10 categorized boards defined in `PinterestBoardTaxonomy.cs` (American Muscle Cars, Electric Vehicles, Classic European Cars, Japanese Performance Cars, Luxury Exotic Cars, Vintage Antique Cars, Motorsport Racing, SUV Trucks Offroad, Car Technology Innovation, Concept Future Cars)
- **Status**: Configurable via `SocialMedia__PinterestEnabled`; posting schedule `0 0 1,6,10,15,19,21 * * *` (6×/day)
- **Config source**: `local.settings.json` keys `SocialMedia__Pinterest*`, Key Vault secret `Pinterest-AccessToken`
- **Code files**: `Services/PinterestService.cs`, `Configuration/PinterestBoardTaxonomy.cs`

---

## External Tool Dependencies (Binary — `CarFacts.VideoFunction`)

These are not NuGet packages but OS-level executables invoked via `Process.Start`.

| Tool | Version | Source | Purpose |
|------|---------|--------|---------|
| **FFmpeg** (`ffmpeg.exe`) | not pinned | Downloaded from `stpocvideogen/poc-tools/ffmpeg.exe` blob on cold start | Video trim, encode (libx264), Ken Burns zoom-pan, subtitle burn-in (libass), audio mix (amix), xfade transitions |
| **yt-dlp** (`yt-dlp.exe`) | not pinned | Downloaded from `stpocvideogen/poc-tools/yt-dlp.exe` blob on cold start | YouTube CC video download via `--download-sections *0-N`; falls back to Pexels if unavailable |

- **Cold-start behavior**: Both binaries are downloaded from blob to `C:\local\Temp\poc-ffmpeg-bin\` / `poc-ytdlp-bin\` on first invocation, then cached statically (singleton managers) across warm invocations.
- **Cookies**: Optional `youtube-cookies.txt` in `poc-tools` blob — enables authenticated yt-dlp downloads to bypass bot detection on Azure datacenter IPs.
- **Code files**: `Services/FfmpegManager.cs`, `Services/YtDlpManager.cs`

---

## Duplicate Dependencies & Design Notes

### ⚠️ Image Generation has 2 providers — `CarFacts.Functions` (by design: fallback chain)
- **Provider 1**: `ImageGenerationService` → Stability AI (`stable-diffusion-xl-1024-v1-0`)
- **Provider 2**: `TogetherAIImageGenerationService` → Together AI (`FLUX.1-schnell-Free`)
- **Pattern**: Production uses `FallbackImageGenerationService` — tries StabilityAI first, then TogetherAI. Local dev uses one, wrapped in `CachedImageGenerationService` (saves to `cache/images/`).
- **Recommendation**: ✅ Intentional resilience — no consolidation needed.

---

### ⚠️ Video clip source has 2 providers — `CarFacts.VideoFunction` (by design: fallback chain)
- **Provider 1**: `YouTubeVideoService` (Creative Commons, 3-layer filter, requires yt-dlp)
- **Provider 2**: `PexelsVideoService` (stock video, 4-tier query fallback)
- **Pattern**: YouTube CC is attempted first per segment; falls back to Pexels automatically if YouTube has no clean candidate or yt-dlp fails.
- **Recommendation**: ✅ Intentional resilience — no consolidation needed.

---

### ⚠️ Text Generation has 2 providers — `CarFacts.Functions` (switchable, not simultaneous)
- **Provider 1**: Azure OpenAI via Semantic Kernel (default, `AI:TextProvider = "AzureOpenAI"`)
- **Provider 2**: OpenAI direct via Semantic Kernel (`AI:TextProvider = "OpenAI"`)
- **Pattern**: Compile-time switch in `Program.cs` → `RegisterTextProvider()`. Only one is registered per instance.
- **Recommendation**: ✅ Intentional flexibility — no issue.

---

### ⚠️ TTS SDK version mismatch — `CarFacts.VideoFunction` vs `CarFacts.VideoPoC`
- **VideoFunction**: `Microsoft.CognitiveServices.Speech` **1.43.0**
- **VideoPoC**: `Microsoft.CognitiveServices.Speech` **1.40.0**
- **Recommendation**: ⚠️ Both use the same `TtsService.cs` pattern. VideoPoC is not deployed, but the version gap is 3 minor releases. Low risk; update VideoPoC if ever promoted.

---

### ⚠️ Logging has 2 frameworks — `CarFacts.Functions`
- **Framework 1**: **Application Insights** — production telemetry (always registered)
- **Framework 2**: **Serilog** — local dev file + console (`!isAzure` guard in `Program.cs`)
- **Pattern**: Serilog activated only when `WEBSITE_INSTANCE_ID` env var is absent (local). Both may be active locally.
- **Recommendation**: ⚠️ Low concern — consider using Serilog's App Insights sink to unify, or conditionally skip App Insights in local dev.

---

### ⚠️ Cosmos container name hardcoded — `CarFacts.Functions`
- **Store 1**: `CosmosFactKeywordStore` uses `settings.Value.ContainerName` (reads `"fact-keywords"` from config)
- **Store 2**: `CosmosSocialMediaQueueStore` hardcodes `"social-media-queue"` in constructor (line 20)
- **Recommendation**: ⚠️ Add `QueueContainerName` property to `CosmosDbSettings` and pass through `IOptions<CosmosDbSettings>` for consistency and configurability.

---

### ℹ️ AdSense secrets declared but code not found
- `AdSense-ClientId` and `AdSense-SlotId` are declared in `Configuration/SecretNames.cs` but no service file referencing them was found in `src/CarFacts.Functions/Services/`.
- **Recommendation**: ℹ️ Likely used in a web template or deleted service. Verify and remove dead declarations if unused.

---

## Infrastructure Resources (`infra/azuredeploy.json`)

| Resource | ARM Type | Naming Pattern |
|----------|----------|----------------|
| Log Analytics Workspace | `Microsoft.OperationalInsights/workspaces` | `log-{prefix}` |
| Application Insights | `Microsoft.Insights/components` | `ai-{prefix}` |
| Storage Account | `Microsoft.Storage/storageAccounts` | `st{prefix}` (Standard_LRS, StorageV2) |
| App Service Plan | `Microsoft.Web/serverfarms` | `asp-{prefix}` (Y1/Dynamic — Consumption) |
| Key Vault | `Microsoft.KeyVault/vaults` | `kv-{prefix}` (Standard, RBAC, soft-delete 7d) |
| Key Vault Secrets (×3) | `Microsoft.KeyVault/vaults/secrets` | `AzureOpenAI-ApiKey`, `StabilityAI-ApiKey`, `WordPress-OAuthToken` |
| Function App | `Microsoft.Web/sites` | `func-{prefix}` (SystemAssigned managed identity) |
| RBAC Role Assignment | `Microsoft.Authorization/roleAssignments` | Key Vault Secrets User for Function App MSI |

> **Not provisioned by ARM**: Azure Cosmos DB (must be created separately), Azure App Configuration, Azure Speech, Azure Computer Vision, `stpocvideogen` storage account used by VideoFunction.

---

## Dependency Summary

| Metric | Count |
|--------|-------|
| **NuGet packages — CarFacts.VideoFunction** | 10 |
| **NuGet packages — CarFacts.Functions** | 16 |
| **NuGet packages — CarFacts.VideoPoC** | 5 |
| **Azure cloud services** | 8 (Functions ×2, Blob Storage ×2, Speech TTS, Computer Vision, Cosmos DB, Key Vault, App Insights + Log Analytics, App Configuration) |
| **External API services** | 9 (Azure OpenAI, OpenAI, Stability AI, Together AI, Pexels, YouTube Data API, WordPress, Twitter/X, Facebook, Reddit, Pinterest) |
| **External binaries (blob-downloaded)** | 2 (FFmpeg, yt-dlp) |
| **Azure Blob containers** | 3 (`poc-tools`, `poc-jobs/*`, `poc-videos`) |
| **Cosmos DB containers** | 2 (`fact-keywords`, `social-media-queue`) |
| **Durable Task hubs** | 2 (`CarFactsVideoHub`, default) |
| **Key Vault secrets** | 16 declared in `SecretNames.cs`; 3 provisioned by ARM |
| **ARM-provisioned resources** | 8 |
| **Detected issues** | 6 (2 ✅ intentional, 3 ⚠️ minor, 1 ℹ️ informational) |
