# Cost & API Usage Analysis

<!-- deepfry:commit=b9be8dc agent=cost-analyzer timestamp=2025-07-14T12:00:00Z -->

## Paid Service Inventory

| Service | Category | Client/SDK | Endpoint/Model | Call Location | Path Type |
|---------|----------|-----------|----------------|---------------|-----------|
| Azure OpenAI | AI/ML | Semantic Kernel `IChatCompletionService` | `gpt-4o-mini` (default deployment) | `Services/ContentGenerationService.cs:29` | 🟡 Warm (1×/day) |
| Azure OpenAI | AI/ML | Semantic Kernel `IChatCompletionService` | `gpt-4o-mini` | `Services/SeoGenerationService.cs:34` | 🟡 Warm (1×/day) |
| Azure OpenAI | AI/ML | Semantic Kernel `IChatCompletionService` | `gpt-4o-mini` | `Activities/GenerateTweetFactsActivity.cs:33` | 🟡 Warm (1×/day) |
| Azure OpenAI | AI/ML | Semantic Kernel `IChatCompletionService` | `gpt-4o-mini` | `Activities/GenerateTweetLinkActivity.cs:67` | 🟡 Warm (1–N×/day) |
| Azure OpenAI | AI/ML | Semantic Kernel `IChatCompletionService` | `gpt-4o-mini` | `Activities/GenerateTweetReplyActivity.cs:125` | 🟡 Warm (3–6×/day) |
| Azure OpenAI | AI/ML | Semantic Kernel `IChatCompletionService` | `gpt-4o-mini` | `Activities/GeneratePinContentActivity.cs:43` | 🟡 Warm (6×/day) |
| Stability AI | AI/ML (Image) | Direct HTTP (`ImageGenerationService`) | `stable-diffusion-xl-1024-v1-0` (SDXL) | `Services/ImageGenerationService.cs:94` | 🟡 Warm (5×/day) |
| Together AI | AI/ML (Image) | Direct HTTP (`TogetherAIImageGenerationService`) | `black-forest-labs/FLUX.1.1-pro` | `Services/TogetherAIImageGenerationService.cs:88` | 🟡 Warm (fallback, 5×/day) |
| WordPress.com | CMS/Publishing | Direct HTTP REST v1.1 | Media upload + Post create/update | `Services/WordPressService.cs` (multiple methods) | 🟡 Warm (~8–12 API calls/day) |
| Twitter/X API | Social/Data | Direct HTTP (OAuth 1.0a) | v2 tweets, search, likes | `Services/TwitterService.cs` (5 endpoints) | 🟡 Warm (10–30+ calls/day) |
| Facebook Graph API | Social/Data | Direct HTTP | v21.0 page feed | `Services/FacebookService.cs:63` | 🟢 Cold (disabled) |
| Reddit API | Social/Data | Direct HTTP (OAuth2) | /api/submit | `Services/RedditService.cs:92` | 🟢 Cold (disabled) |
| Pinterest API | Social/Data | Direct HTTP (OAuth2 Bearer) | v5 pins, boards | `Services/PinterestService.cs` (3 endpoints) | 🟡 Warm (6×/day) |
| Azure Cosmos DB | Cloud DB | `Microsoft.Azure.Cosmos` SDK | Serverless/provisioned container | `Services/CosmosFactKeywordStore.cs`, `CosmosSocialMediaQueueStore.cs` | 🟡 Warm (~20–50 RU/day) |
| Azure Key Vault | Cloud Secrets | `Azure.Security.KeyVault.Secrets` | GetSecret | `Services/KeyVaultSecretProvider.cs`, `Program.cs` | 🟡 Warm (~15–25 calls/day) |
| Azure App Configuration | Cloud Config | `Microsoft.Extensions.Configuration.AzureAppConfiguration` | Select("*") | `Program.cs:36` | 🟡 Warm (on startup) |
| Azure Application Insights | Monitoring | `Microsoft.ApplicationInsights.WorkerService` | Telemetry ingestion | `Program.cs:47` | 🟡 Warm (continuous) |
| Azure Functions (Consumption) | Cloud Compute | Durable Functions v4 | Timer + Orchestrator + Activities | All `Functions/` files | 🟡 Warm (daily pipeline) |
| Google AdSense | Advertising | Client-side JS (via Key Vault config) | Auto-ads | `SecretNames.cs:28-29` (config only) | 🟢 Cold (revenue, not cost) |

## Detailed LLM Call Analysis

The app uses **Azure OpenAI `gpt-4o-mini`** for all text generation via Microsoft Semantic Kernel. All 7 LLM call sites are warm-path (scheduled daily). Token estimates are rough approximations based on prompt template lengths.

| LLM Call | Prompt Tokens (est.) | Completion Tokens (est.) | Calls/Day | Daily Token Cost (est.) |
|----------|---------------------|-------------------------|-----------|------------------------|
| **GenerateFactsAsync** (5 facts) | ~600 (system+user) | ~2,500 (5 facts × 10-20 sentences) | 1 | ~$0.0005 |
| **GenerateSeoAsync** (SEO metadata) | ~1,200 (system+user+content summary) | ~500 (JSON metadata) | 1 | ~$0.0003 |
| **GenerateTweetFactsActivity** (5 tweets) | ~500 (system+user) | ~400 (5 × 280 chars) | 1 | ~$0.0001 |
| **GenerateTweetLinkActivity** (per link) | ~400 (system+user) | ~100 (1 tweet JSON) | 1 | ~$0.0001 |
| **GenerateTweetReplyActivity** (per reply) | ~400 (system+user) | ~80 (1 reply JSON) | 3–6 | ~$0.0003 |
| **GeneratePinContentActivity** (per pin) | ~300 (system+user) | ~100 (title+desc JSON) | 6 | ~$0.0003 |
| **Total LLM** | | | **~13–17** | **~$0.0016/day** |

> **Note**: Azure OpenAI `gpt-4o-mini` pricing: ~$0.15/1M input tokens, ~$0.60/1M output tokens. At this volume, LLM costs are negligible — **~$0.05/month**.

## Cost-Per-Flow Estimates

### Flow 1: Daily Blog Post Pipeline (CarFactsOrchestrator)

This is the primary daily flow: generate 5 car facts → SEO metadata → 5 images → publish to WordPress.

| Step | Service | Estimated Cost | Notes |
|------|---------|---------------|-------|
| 1. Generate 5 car facts | Azure OpenAI (gpt-4o-mini) | ~$0.0005 | ~600 in + ~2,500 out tokens |
| 2. Generate SEO metadata | Azure OpenAI (gpt-4o-mini) | ~$0.0003 | ~1,200 in + ~500 out tokens |
| 3. Generate 5 images | Stability AI (SDXL 1024×1024) | **~$0.10** | 5 images × ~$0.02/image (30 steps) |
| 3b. Fallback: 5 images | Together AI (FLUX.1.1-pro) | **~$0.20** | 5 images × ~$0.04/image (only if Stability fails) |
| 4. Create draft post | WordPress.com API | Free | Free tier (wordpress.com REST API) |
| 5. Upload 5 images | WordPress.com API | Free | Included with wordpress.com hosting |
| 6. Publish post | WordPress.com API | Free | Included with wordpress.com hosting |
| 7. Store keywords | Azure Cosmos DB | ~$0.000005 | ~5 RU per write, serverless |
| 8. Azure Functions execution | Azure Functions (Consumption) | ~$0.001 | ~60s total execution, negligible |
| **Total per invocation** | | **~$0.10** | Stability AI dominates |

| Scale | Daily Cost | Monthly Cost |
|-------|-----------|-------------|
| 1×/day (current) | ~$0.10 | **~$3.00** |
| 2×/day (if increased) | ~$0.20 | ~$6.00 |

### Flow 2: Social Media Content Generation (SocialMediaOrchestrator)

Runs daily as part of the blog pipeline — generates tweet content and queues it.

| Step | Service | Estimated Cost | Notes |
|------|---------|---------------|-------|
| 1. Generate 5 standalone tweets | Azure OpenAI (gpt-4o-mini) | ~$0.0001 | ~500 in + ~400 out tokens |
| 2. Generate 1 link tweet | Azure OpenAI (gpt-4o-mini) | ~$0.0001 | ~400 in + ~100 out tokens |
| 3. Store in Cosmos queue | Azure Cosmos DB | ~$0.000002 | ~6 writes |
| **Total per invocation** | | **~$0.0002** | Negligible |

| Scale | Daily Cost | Monthly Cost |
|-------|-----------|-------------|
| 1×/day (current) | ~$0.0002 | **~$0.006** |

### Flow 3: Scheduled Social Media Posting (ScheduledPostingOrchestrator)

Executes queued tweets, replies, and likes throughout the day.

| Step | Service | Estimated Cost | Notes |
|------|---------|---------------|-------|
| 1. Post 5 fact tweets | Twitter/X API v2 | Free | Free tier (Basic: 500 tweets/mo) |
| 2. Post 1 link tweet | Twitter/X API v2 | Free | Free tier |
| 3. Like 10–20 tweets | Twitter/X API v2 (search+like) | Free¹ | Each like = 1 search + 1 getUserMe + 1 like = 3 API calls |
| 4. Reply to 3–6 tweets | Twitter/X API v2 (search+reply) | Free¹ | Each reply needs: 1 search + 1 AI gen + 1 post |
| 4b. AI reply generation | Azure OpenAI (gpt-4o-mini) | ~$0.0003 | 3–6 calls × ~100 tokens each |
| 5. Delete queue items | Azure Cosmos DB | ~$0.000003 | ~15–30 deletes |
| **Total per day** | | **~$0.0003** | Negligible |

> ¹ **Twitter/X API pricing note**: The app uses the Twitter API v2 **Basic** plan ($200/month) which allows 500 tweets/month posting + 10,000 tweet reads/month. The current usage (~6 posts/day + ~6 searches × 100 tweets = ~600 reads/day) fits within Basic limits. **This is the single largest fixed cost in the system.**

| Scale | Daily Cost | Monthly Cost |
|-------|-----------|-------------|
| Current usage | ~$0.0003 (variable) | **$200 (Twitter Basic plan fixed)** |

### Flow 4: Pinterest Posting (PinterestPostingOrchestrator)

Runs 6×/day via timer trigger. Each invocation selects a fact, generates pin content via LLM, and creates a pin.

| Step | Service | Estimated Cost | Notes |
|------|---------|---------------|-------|
| 1. Select fact from Cosmos | Azure Cosmos DB | ~$0.000001 | 1 query |
| 2. Generate pin title/desc | Azure OpenAI (gpt-4o-mini) | ~$0.00005 | ~300 in + ~100 out tokens |
| 3. List/create boards | Pinterest API v5 | Free | Free tier |
| 4. Create pin | Pinterest API v5 | Free | Free tier |
| 5. Update tracking | Azure Cosmos DB | ~$0.000001 | 1 write |
| **Total per invocation** | | **~$0.00005** | Negligible |

| Scale | Daily Cost | Monthly Cost |
|-------|-----------|-------------|
| 6×/day (current) | ~$0.0003 | **~$0.01** |

### Flow 5: Tweet Reply Generation (TweetReplyOrchestrator)

On-demand or scheduled. Searches Twitter for car tweets, generates AI reply, posts it.

| Step | Service | Estimated Cost | Notes |
|------|---------|---------------|-------|
| 1. Search Twitter | Twitter/X API v2 | Free¹ | 1 search (100 tweets) |
| 2. Generate reply via AI | Azure OpenAI (gpt-4o-mini) | ~$0.0001 | ~400 in + ~80 out tokens |
| 3. Post reply | Twitter/X API v2 | Free¹ | 1 tweet |
| **Total per invocation** | | **~$0.0001** | Negligible per-call |

| Scale | Daily Cost | Monthly Cost |
|-------|-----------|-------------|
| 3–6×/day (current) | ~$0.0006 | **~$0.02** |

## Cost Bombs 💣

### 💣 Twitter API Basic Plan is the #1 Cost

- **Location**: Entire `Services/TwitterService.cs` integration
- **Pattern**: The app requires Twitter API v2 Basic plan ($200/month) just for the search endpoint + posting capabilities. At 6 posts/day + 6 searches/day + 10-20 likes/day, the app uses ~30% of tweet write limits and ~60% of read limits.
- **Estimated cost**: **$200/month fixed** — this is **98% of the total monthly cost**
- **Fix**: Evaluate if the Twitter engagement features (likes, replies) justify the $200/month. The free Twitter tier allows 1,500 tweets/month writes but **zero reads** — so search-based features (replies, likes) would need to be dropped. Alternatively, consider whether the Twitter channel's ROI justifies the cost vs. focusing on Pinterest (free) and WordPress (free/cheap).

### 💣 Image Generation In-Loop (Mitigated but Fragile)

- **Location**: `Services/ImageGenerationService.cs:40-48` (Stability AI), `Services/TogetherAIImageGenerationService.cs:45-49` (Together AI)
- **Pattern**: Both image services iterate over all 5 facts sequentially, making 5 individual API calls. There is a 2-second delay between Stability AI calls (rate-limit handling) but no batching.
- **Estimated waste**: Currently acceptable at 5 images/day (~$0.10–$0.20/day). However, if facts-per-day increases or multiple runs occur, costs scale linearly with no batch discount.
- **Fix**: Both APIs have no batch endpoint, so sequential is correct. The `FallbackImageGenerationService` is well-designed (tries Stability AI first, falls back to Together AI, then gracefully degrades to text-only). The `CachedImageGenerationService` prevents re-generation during local dev. **No immediate action needed**, but add a circuit-breaker or daily image budget cap to prevent runaway costs if the timer trigger misfires.

### 💣 No LLM Response Caching Across Runs

- **Location**: `Services/ContentGenerationService.cs:29`, `Services/SeoGenerationService.cs:34`, all `GenerateTweet*Activity` classes
- **Pattern**: Every LLM call is fresh — no caching of responses. If the orchestrator retries (it has `maxNumberOfAttempts: 3`), the same LLM call is repeated with identical prompts, burning tokens for the same output.
- **Estimated waste**: At `gpt-4o-mini` pricing this is ~$0.001 per retry cycle — trivial today. But if the model is upgraded to `gpt-4o` ($2.50/$10 per 1M tokens), retry waste would become 10× more expensive.
- **Fix**: Durable Functions replay-safety should prevent duplicate activity calls, so this is partially mitigated by the orchestrator design. However, if an activity fails mid-execution (after the LLM call but before returning), the LLM call is wasted. Consider caching LLM responses in Cosmos DB keyed by prompt hash for the content generation calls.

### 💣 Twitter Like Activity Makes 3 API Calls Per Like

- **Location**: `Services/TwitterService.cs:258-285` (`LikeTweetAsync`)
- **Pattern**: Every like requires: (1) `GET /2/users/me` to get the authenticated user ID, then (2) `POST /2/users/{id}/likes`. The user ID never changes but is fetched fresh every single time. With 10–20 likes/day, that's 10–20 unnecessary API calls.
- **Estimated waste**: No direct cost (within Twitter plan), but wastes rate limit quota — the Basic plan has 500 tweet reads/15min and 100 requests/15min for user lookup.
- **Fix**: Cache `GetAuthenticatedUserIdAsync()` result in a static field or singleton-scoped property. One call per app lifetime instead of per like.

### 💣 Unbounded Twitter Search Results (100 per call)

- **Location**: `Activities/GenerateTweetReplyActivity.cs:70`, `Activities/GenerateTweetLikeActivity.cs:69`
- **Pattern**: Both activities request `maxResults: 100` tweets per search, then filter down to a handful. The Twitter Basic plan allows 10,000 tweet reads/month. At 6 searches/day × 100 tweets = 600 reads/day = ~18,000/month — **exceeding the monthly limit**.
- **Estimated waste**: Could cause API failures in the second half of the month if limits are hard-enforced.
- **Fix**: Reduce `maxResults` to 25–50. The engagement filters are aggressive enough that 25 tweets should yield sufficient candidates. This cuts read consumption by 50–75%.

## Caching & Optimization Status

| Service | Cached | Rate Limited | Batched | Fallback |
|---------|--------|-------------|---------|----------|
| Azure OpenAI (LLM) | ❌ No response cache | ✅ Built-in (Azure throttling) | N/A | ❌ No cheaper fallback |
| Stability AI (Images) | ✅ Local dev only (`CachedImageGenerationService`) | ✅ 2s delay + 429 retry with exponential backoff | ❌ Individual calls | ✅ → Together AI → text-only |
| Together AI (Images) | ✅ Local dev only (via cached decorator) | ❌ No rate limiting | ❌ Individual calls | ✅ Graceful degradation to empty |
| WordPress.com API | ❌ | ❌ No client-side rate limiting | ❌ Sequential uploads | ❌ |
| Twitter/X API v2 | ❌ | ❌ No client-side rate limiting | ❌ Individual calls | ❌ Failure = hard error |
| Pinterest API v5 | ✅ Board name→ID cached in memory | ❌ No client-side rate limiting | ❌ Individual calls | ❌ |
| Azure Cosmos DB | ❌ | ✅ Built-in (Azure throttling) | ❌ | ✅ `NullFactKeywordStore` fallback |
| Azure Key Vault | ❌ No secret caching | ✅ Built-in (Azure throttling) | ❌ | ✅ `LocalSecretProvider` for dev |
| Application Insights | N/A | ✅ Sampling enabled | ✅ Auto-batched | N/A |

## Cost Summary

| Metric | Value |
|--------|-------|
| **Total paid services** | 8 active (+ 2 disabled: Facebook, Reddit) |
| **Hot-path services (per-request)** | 0 (all scheduled/warm-path) |
| **Warm-path services (daily scheduled)** | 8 (Azure OpenAI, Stability AI, Together AI, WordPress, Twitter, Pinterest, Cosmos DB, Functions) |
| **Cost bombs found** | 5 |
| **Estimated monthly cost (current usage)** | **~$206** |
| **Largest cost driver** | Twitter/X API Basic Plan: **$200/month (97%)** |
| **Second largest cost driver** | Stability AI images: **~$3/month (1.5%)** |
| **LLM costs (Azure OpenAI gpt-4o-mini)** | ~$0.05/month (<0.1%) |
| **All other services** | ~$3/month combined (Functions, Cosmos, Key Vault, App Insights) |

### Monthly Cost Breakdown

```
Twitter/X API Basic Plan ............. $200.00  (97.1%)  [fixed]
Stability AI (5 images/day) .......... $3.00    (1.5%)   [variable]
Azure Functions (Consumption) ........ $1.00    (0.5%)   [variable]
Azure Cosmos DB (Serverless) ......... $0.50    (0.2%)   [variable]
Azure OpenAI (gpt-4o-mini) .......... $0.05    (<0.1%)  [variable]
Azure Key Vault ...................... $0.03    (<0.1%)  [variable]
Azure App Configuration .............. $0.01    (<0.1%)  [variable]
Application Insights ................. $0.50    (0.2%)   [variable]
Pinterest API ........................ $0.00    (free)
WordPress.com API .................... $0.00    (free tier)
─────────────────────────────────────────────────────────
TOTAL                                  ~$205/month
```

## Recommendations (by savings impact)

1. 💰💰💰 **Evaluate Twitter/X API ROI — potential $200/month savings**
   The Twitter Basic plan ($200/month) is 97% of total costs. If the engagement features (likes, replies, search) are not driving meaningful traffic, downgrade to the Free tier ($0/month, 1,500 tweets/month, no reads). This would require removing: `GenerateTweetReplyActivity`, `GenerateTweetLikeActivity`, and `SearchRecentTweetsAsync`. Standalone fact tweets and link tweets would still work under the free tier's 1,500 tweet/month write limit (~180 tweets/month current usage).

2. 💰💰 **Reduce Twitter search `maxResults` from 100 to 25 — prevents rate limit overages**
   In `GenerateTweetReplyActivity.cs:70` and `GenerateTweetLikeActivity.cs:69`, change `maxResults: 100` to `maxResults: 25`. This reduces monthly tweet reads from ~18,000 to ~4,500, staying safely under the 10,000/month Basic plan limit. The aggressive engagement filters mean most of the 100 results are discarded anyway.

3. 💰 **Cache `GetAuthenticatedUserIdAsync()` — eliminates 10–20 redundant API calls/day**
   In `TwitterService.cs`, cache the user ID in a `private string? _cachedUserId` field:
   ```csharp
   private string? _cachedUserId;
   public async Task<string> GetAuthenticatedUserIdAsync(...)
   {
       if (_cachedUserId != null) return _cachedUserId;
       // ... existing logic ...
       _cachedUserId = userId;
       return userId;
   }
   ```
   This is zero-cost to implement and saves rate limit budget.

4. 💰 **Add a daily image budget cap — prevents runaway Stability AI costs**
   If the timer trigger double-fires or the orchestrator retries the entire pipeline, image generation repeats with no guardrail. Add a simple check in `ImageGenerationService.GenerateImagesAsync()` that reads a "last-generated-date" flag from Cosmos or blob storage and skips regeneration if images were already produced today.

5. 💰 **Consider downgrading Stability AI to a cheaper model or Together AI as primary**
   `stable-diffusion-xl-1024-v1-0` at ~$0.02/image is reasonable, but if image quality requirements are flexible, consider:
   - Using Together AI (`FLUX.1.1-pro` at ~$0.04/image) as primary only if quality is clearly better
   - Testing lower step counts (20 instead of 30) on Stability AI to reduce per-image cost by ~33%
   - Using 512×512 resolution for social media cards (Pinterest, Twitter) where 1024×1024 is overkill
