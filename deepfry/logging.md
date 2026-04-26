<!-- deepfry:commit=b9be8dc1501e31ea9edfa99c938527818fa2aca5 agent=log-analyzer timestamp=2026-04-24T14:36:39Z -->

# Logging Analysis

## Framework

- **Primary Library**: `Microsoft.Extensions.Logging` (`ILogger<T>`) — used throughout all services, activities, triggers, and orchestrators
- **Secondary Library**: `Serilog` — used **only** for local development file logging
- **Application Insights**: Configured for production via `Microsoft.ApplicationInsights.WorkerService` and `Microsoft.Azure.Functions.Worker.ApplicationInsights`
- **Configuration**:
  - Serilog setup: `Program.cs:13-27` (file sink, local only, conditional on `WEBSITE_INSTANCE_ID` env var)
  - Application Insights registration: `Program.cs:47-48`
  - Serilog host integration: `Program.cs:54-55` (local only)
  - Azure Functions log levels: `host.json:3-17`
- **Sinks**:
  - **Local**: Serilog file sink → `logs/carfacts-{timestamp}.log` with structured output template
  - **Production (Azure)**: Application Insights (with sampling enabled, Request type excluded)
  - **Console**: Implicitly via Azure Functions Worker defaults
- **Multiple frameworks detected**: Yes — Serilog + ILogger, but intentionally scoped (Serilog local-only, ILogger everywhere). Well-partitioned; not a concern.

## Structured Logging

- **Adopted**: ✅ **Yes — 100%**
- **Template style**: Structured message templates throughout — **zero** instances of string interpolation in log calls
- **Verification**: `grep` for `\.Log(Information|Warning|Error|Debug|Trace|Critical)\(\$"` returned **zero matches** across the entire `src/` tree
- **Correlation ID**: ✅ Propagated via Durable Functions — all orchestrators use `context.CreateReplaySafeLogger()` which ties logs to the orchestration instance ID. Application Insights provides request/dependency correlation in production.
- **Examples**:
  - ✅ `logger.LogInformation("Starting CarFacts pipeline for {Date}", todayDate)` — `CarFactsOrchestrator.cs:35`
  - ✅ `_logger.LogInformation("Uploading {Count} images to WordPress.com", images.Count)` — `WordPressService.cs:42`
  - ✅ `_logger.LogError("Twitter post failed ({Status}): {Body}", response.StatusCode, body)` — `TwitterService.cs:73`
  - ✅ `_logger.LogWarning(ex, "Image provider {Provider} failed: {Message}. Trying next provider", providerName, ex.Message)` — `FallbackImageGenerationService.cs:49`

## Log Level Usage

| Level | Count | Assessment |
|-------|-------|------------|
| **Critical** | 0 | ⚠️ Unused — no Critical-level logs for unrecoverable failures (e.g., pipeline abort) |
| **Error** | 19 | ✅ Appropriately used for external API failures (WordPress, Twitter, Reddit, Facebook, Pinterest, Stability AI, Together AI) |
| **Warning** | 40 | ✅ Well-used for non-blocking failures, fallback paths, degraded operations, and configuration gaps |
| **Information** | 153 | ✅ Good coverage of pipeline milestones, activity execution, and operational state |
| **Debug** | 2 | ✅ Appropriately minimal — used for WordPress payload (`WordPressService.cs:323`) and Key Vault retrieval (`KeyVaultSecretProvider.cs:24`) |
| **Trace** | 0 | ✅ Acceptable — no need for trace-level logging in this architecture |
| **Total** | **214** | |

### Log Level Assessment

- **Error usage is appropriate**: All 19 Error-level logs fire on HTTP failure responses from external APIs, always capturing the status code and response body. The exception object is included where relevant.
- **Warning usage is strong**: Covers non-blocking failures (backlinks, social media queue, web stories), fallback triggers (image providers, pin content), configuration missing (Cosmos DB), and operational decisions (cached images, platform disabled).
- **Information usage is thorough**: Every orchestrator step, activity entry/exit, external service call, and pipeline milestone is logged with contextual properties.
- **No Critical**: The codebase lacks a `LogCritical` path. If the main `CarFactsOrchestrator` fails entirely (e.g., content generation throws), the exception propagates to the Durable Functions framework with no explicit Critical-level log from application code.

## Coverage

### Entry Points (Triggers)

| Entry Point | File | Logging |
|-------------|------|---------|
| `CarFactsTimerTrigger` | `Functions/CarFactsTimerTrigger.cs` | ✅ Trigger fire + orchestration start |
| `SocialMediaPostingTrigger` | `Functions/SocialMediaPostingTrigger.cs` | ✅ Trigger fire + orchestration start |
| `PinterestPostingTrigger` | `Functions/PinterestPostingTrigger.cs` | ✅ Trigger fire + disabled check + orchestration start |
| `TweetReplyTrigger` | `Functions/TweetReplyTrigger.cs` | ✅ Trigger fire + orchestration start |

### Orchestrators

| Orchestrator | File | Logging |
|-------------|------|---------|
| `CarFactsOrchestrator` | `Functions/CarFactsOrchestrator.cs` | ✅ Every pipeline step logged with contextual properties |
| `SocialMediaOrchestrator` | `Functions/SocialMediaOrchestrator.cs` | ✅ Input, generation counts, platform selection, queue storage |
| `ScheduledPostingOrchestrator` | `Functions/ScheduledPostingOrchestrator.cs` | ✅ Item count, scheduled times, completion |
| `ScheduledPostOrchestrator` | `Functions/ScheduledPostOrchestrator.cs` | ✅ Extensive — retry loops, sleep/wake, reply/like lifecycle |
| `PinterestPostingOrchestrator` | `Functions/PinterestPostingOrchestrator.cs` | ✅ Fact selection, pin creation, tracking |
| `TweetReplyOrchestrator` | `Functions/TweetReplyOrchestrator.cs` | ✅ Search, reply generation, queue storage |

### Activities (26 total)

| Activity | Logging | Details |
|----------|:---:|---------|
| `GenerateRawContentActivity` | ✅ | Entry + count |
| `GenerateSeoActivity` | ✅ | Entry + title |
| `GenerateAllImagesActivity` | ✅ | Entry + count + error handling |
| `CreateDraftPostActivity` | ✅ | Entry + PostId |
| `UploadSingleImageActivity` | ✅ | Entry + MediaId + URL |
| `FormatAndPublishActivity` | ✅ | Entry + published URL |
| `FindBacklinksActivity` | ✅ | Extensive — per-fact candidates, weighted selection |
| `StoreFactKeywordsActivity` | ✅ | Entry + backlink increment details |
| `StoreSocialMediaQueueActivity` | ✅ | Extensive — per-platform scheduling, reply/like slots |
| `ExecuteScheduledPostActivity` | ✅ | Extensive — activity type, platform, completion |
| `GenerateTweetFactsActivity` | ✅ | Entry + count |
| `GenerateTweetLinkActivity` | ✅ | Entry + per-link generation |
| `GenerateTweetReplyActivity` | ✅ | Extensive — search, candidates, selection, AI reply |
| `GenerateTweetLikeActivity` | ✅ | Extensive — search, candidates, selection with metrics |
| `GetPendingScheduledItemsActivity` | ✅ | Count |
| `GetEnabledPlatformsActivity` | ✅ | Platform list |
| `GetWebStoriesEnabledActivity` | ✅ | Enabled state |
| `PublishSocialMediaActivity` | ✅ | Entry + error handling |
| `IncrementSocialCountsActivity` | ✅ | Platform + URL |
| `CreatePinterestPinActivity` | ✅ | Board + title + PinId |
| `SelectPinterestFactActivity` | ✅ | Extensive — candidate selection, board logic |
| `UpdatePinterestTrackingActivity` | ✅ | RecordId + board |
| `CreateWebStoryActivity` | ✅ | Entry + published URL |
| `StoreTweetReplyQueueActivity` | ✅ | Author + TweetId |
| `GetSocialMediaSettingsActivity` | ⚠️ | **No logger injected, no logging** — reads settings only |
| `GeneratePinContentActivity` | ✅ | Entry + fallback warning on failure |

### Services (19 total)

| Service | Logging | Details |
|---------|:---:|---------|
| `WordPressService` | ✅ | All API calls logged — success + failure with status/body |
| `TwitterService` | ✅ | Post, reply, like, search — all with error details |
| `FacebookService` | ✅ | Page selection, post success, error with status/body |
| `RedditService` | ✅ | Subreddit selection, auth, submission, errors |
| `PinterestService` | ✅ | Board management, pin creation, API errors |
| `ContentGenerationService` | ✅ | LLM call entry + parsed count |
| `SeoGenerationService` | ✅ | LLM call entry + parsed title |
| `ImageGenerationService` | ✅ | Per-image generation, rate-limit retries |
| `TogetherAIImageGenerationService` | ✅ | Per-image generation, API errors |
| `FallbackImageGenerationService` | ✅ | Provider chain — try/fail/succeed/exhaust |
| `CachedImageGenerationService` | ✅ | Cache hit/miss |
| `CosmosFactKeywordStore` | ✅ | All CRUD operations, per-record details |
| `CosmosSocialMediaQueueStore` | ✅ | Queue add/delete/read, item selection |
| `SocialMediaPublisher` | ✅ | Platform fan-out, per-platform errors |
| `KeyVaultSecretProvider` | ✅ | Debug-level retrieval |
| `LocalSecretProvider` | ✅ | Warning on local secret usage |
| `NullFactKeywordStore` | ✅ | Warning on every operation (Cosmos not configured) |
| `NullSocialMediaQueueStore` | ✅ | Warning on every operation |
| `ContentFormatterService` | ⚠️ | **No logger — pure HTML formatter, no ILogger injected** |

## Silent Exception Swallowing

| Location | Code | Exception Type | Impact |
|----------|------|---------------|--------|
| `Program.cs:231-233` | `catch { connectionString = ""; }` | Any exception from Key Vault `GetSecret("CosmosDb-ConnectionString")` | **Medium** — If Cosmos DB connection string retrieval fails in production, the app silently falls back to `NullFactKeywordStore`/`NullSocialMediaQueueStore` with no log indicating *why*. Could mask Key Vault permission errors, network issues, or missing secrets. |
| `CreateWebStoryActivity.cs:79-81` | `catch { return string.Empty; }` | Any exception from `GetSecretAsync` for optional AdSense IDs | **Low** — Intentionally optional (AdSense config). However, swallowing all exceptions means transient Key Vault failures for AdSense config are invisible. |

### Handled Exceptions (Correctly Logged — 15 total)

All other `catch` blocks in the codebase log the exception before continuing:

| Location | Pattern | Assessment |
|----------|---------|------------|
| `CarFactsOrchestrator.cs:91-94` | `catch (Exception ex)` → `LogWarning` | ✅ Non-blocking backlink failure |
| `CarFactsOrchestrator.cs:221` | `catch (Exception ex)` → `LogWarning` | ✅ Non-blocking social media |
| `CarFactsOrchestrator.cs:224` | `catch (Exception ex)` → `LogWarning` | ✅ Non-blocking keyword storage |
| `CarFactsOrchestrator.cs:233` | `catch (Exception ex)` → `LogWarning` | ✅ Non-blocking web story |
| `ScheduledPostOrchestrator.cs:81-86` | `catch (TaskFailedException ex) when (...)` → `LogWarning` | ✅ Retry loop |
| `ScheduledPostOrchestrator.cs:127-132` | `catch (TaskFailedException ex) when (...)` → `LogWarning` | ✅ Retry loop |
| `FallbackImageGenerationService.cs:47-51` | `catch (Exception ex) when (...)` → `LogWarning` | ✅ Provider fallback |
| `SocialMediaPublisher.cs:43-46` | `catch (Exception ex)` → `LogError` | ✅ Per-platform isolation |
| `WordPressService.cs:109-112` | `catch (Exception ex)` → `LogWarning` | ✅ Non-critical media association |
| `GenerateAllImagesActivity.cs:39-42` | `catch (Exception ex) when (...)` → `LogError` | ✅ Returns empty list |
| `GeneratePinContentActivity.cs:65-68` | `catch (Exception ex)` → `LogWarning` | ✅ Falls back to template |
| `PublishSocialMediaActivity.cs:34-37` | `catch (Exception ex)` → `LogError` | ✅ Returns false |
| `CosmosFactKeywordStore.cs:119-122` | `catch (CosmosException) when (NotFound)` → `LogWarning` | ✅ Expected race condition |
| `CosmosFactKeywordStore.cs:210-213` | `catch (CosmosException) when (NotFound)` → `LogWarning` | ✅ Expected race condition |
| `CosmosSocialMediaQueueStore.cs:66-69` | `catch (CosmosException) when (NotFound)` → `LogWarning` | ✅ Idempotent delete |

## Monitoring Readiness

- **Custom metrics**: ❌ No custom metrics emitted via `TelemetryClient.TrackMetric()`. Pipeline success/failure is inferable from logs but not charted as metrics.
- **Health checks logged**: ⚠️ Partial — Azure Functions provides built-in health endpoints, but no application-level health checks exist (e.g., Cosmos DB connectivity, Key Vault accessibility, external API reachability).
- **Request/response middleware**: ✅ Application Insights Worker Service provides automatic dependency tracking for HTTP calls via `HttpClient`.
- **Sufficient for incident response**: ✅ **Yes** — 214 structured log statements with contextual properties (PostId, Platform, TweetId, Count, Status, Body) provide a clear timeline for any pipeline run. Durable Functions orchestration context adds instance-level correlation. API error responses include status code and body content.

## Summary

| Aspect | Rating | Details |
|--------|--------|---------|
| Framework setup | ✅ Excellent | Clean ILogger DI + Serilog local + App Insights production |
| Structured logging | ✅ Excellent | 100% structured templates, zero string interpolation |
| Log level discipline | ✅ Very Good | Appropriate use across Error/Warning/Info/Debug |
| Coverage | ✅ Excellent | 44/46 injectable components have logging |
| Error handling | ✅ Very Good | 15 catch blocks properly logged; only 2 silent |
| Monitoring readiness | ⚠️ Good | Strong logging, but no custom metrics or health probes |

## Recommendations

1. **Add logging to the 2 silent catch blocks** — this is the **most impactful gap**:
   - `Program.cs:231-233`: Log a Warning when Cosmos DB secret retrieval fails in production. A Key Vault misconfiguration would silently degrade the entire backlink/social queue system with no diagnostic trail.
   - `CreateWebStoryActivity.cs:79-81`: Log a Debug message when optional AdSense secrets are missing.

2. **Add `LogCritical` for unrecoverable pipeline failures**: If the main `CarFactsOrchestrator` pipeline fails (e.g., LLM returns invalid JSON after all retries), the exception propagates to the Durable Task framework but no application-level `Critical` log is emitted. A top-level try/catch with `LogCritical` would surface these in alerting dashboards.

3. **Emit custom Application Insights metrics** for key business KPIs:
   - `pipeline.success` / `pipeline.failure` counters
   - `images.generated.count` per run
   - `social.posts.queued.count` per run
   - `external.api.latency` per provider
   These enable dashboard charts and threshold-based alerting beyond log queries.

4. **Add application-level health checks**: Register `IHealthCheck` implementations for Cosmos DB, Key Vault, and external APIs (WordPress, Twitter) to enable proactive monitoring and faster incident detection.

5. **Add a logger to `ContentFormatterService`** (`Services/ContentFormatterService.cs`): While it's a pure formatter, logging the output HTML size or fact count would aid debugging when WordPress receives unexpected content.

6. **Add minimal logging to `GetSocialMediaSettingsActivity`** (`Functions/Activities/GetSocialMediaSettingsActivity.cs`): A single `LogInformation` showing the resolved settings (factsPerDay, linkPostsPerDay, enabled toggles) would help diagnose configuration issues without requiring Key Vault/App Config inspection.
