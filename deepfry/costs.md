# Cost & API Usage Analysis

<!-- deepfry:commit=5360e57 agent=cost-analyzer timestamp=2025-07-25T00:00:00Z incremental=true scope=CarFacts.VideoFunction -->

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

---

## CarFacts.VideoFunction — Cost & API Analysis

> **Incremental update** — This section covers the AI video generation pipeline in `src/CarFacts.VideoFunction/` added in commit `5360e57`. All findings below are scoped to that module; the `CarFacts.Functions` analysis above is unchanged.

### Pricing Reference (used for all estimates below)

| Service | Pricing basis | Unit price |
|---------|--------------|------------|
| Azure TTS Neural (`en-US-AndrewNeural`) | Per character synthesized | **$16.00 / 1M chars** |
| Azure Computer Vision Image Analysis v4 | Per API transaction (any feature combo) | **$1.00 / 1K calls** ($0.001 each) |
| YouTube Data API v3 — `search.list` | Units per call | **100 units / call**; 10,000 units/day free |
| Pexels Videos API | Free tier | $0 (200 req/hr, 20,000/month hard cap) |
| Azure Blob Storage (LRS Hot) — writes | Per 10K ops | $0.065 → **$0.0000065/op** |
| Azure Blob Storage (LRS Hot) — reads | Per 10K ops | $0.0052 → **$0.00000052/op** |
| Azure Blob Storage — data at rest | Per GB/month | **$0.018 / GB / month** |
| Azure Blob Storage — egress (same region) | Intra-region to Azure services | **$0 (free)** |
| Azure Functions Consumption | Per GB-second (after 400K free) | **$0.0000016 / GB-s** |
| Azure Functions Consumption | Per execution (after 1M free) | **$0.20 / 1M executions** |

> All cost figures are order-of-magnitude estimates. Token/character counts are derived from code inspection of template strings and typical car fact lengths.

---

### VideoFunction — Paid Service Inventory

| Service | Category | Client/SDK | Endpoint/Model | Call Location | Path Type |
|---------|----------|-----------|----------------|---------------|-----------|
| Azure TTS (Cognitive Services Speech) | AI/ML | `Microsoft.CognitiveServices.Speech` `SpeechSynthesizer` | `en-US-AndrewNeural` (SSML, rate 0.88) | `Services/TtsService.cs:37` via `Activities/SynthesizeTtsActivity.cs:33` | 🔴 Hot (1× per video) |
| Azure Computer Vision Image Analysis | AI/ML (Vision) | `Azure.AI.Vision.ImageAnalysis` `ImageAnalysisClient` | `VisualFeatures.Read \| VisualFeatures.Tags` on YouTube hqdefault thumbnails | `Services/ComputerVisionService.cs:47` called from `Services/YouTubeVideoService.cs:82` | 🔴 Hot (1–5× per segment, N segments per video) |
| YouTube Data API v3 | Social/Data | Direct HTTP (`HttpClient`) | `search.list` — `videoLicense=creativeCommon`, `maxResults=10` | `Services/YouTubeVideoService.cs:108` via `Activities/FetchClipActivity.cs:52` | 🔴 Hot (1× per segment) |
| Pexels Videos API | Media/Stock | Direct HTTP (`HttpClient`) | `/videos/search` — `per_page=10`, portrait | `Services/PexelsVideoService.cs:82` and `Activities/FetchClipActivity.cs:127` (duplicate impl) | 🔴 Hot (1–4× per segment, fallback only) |
| Azure Blob Storage | Cloud Storage | `Azure.Storage.Blobs` `BlobContainerClient` | LRS Hot — containers `poc-jobs`, `poc-videos` | `Activities/SynthesizeTtsActivity.cs:43`, `Activities/FetchClipActivity.cs:101`, `Activities/RenderVideoActivity.cs:120` | 🔴 Hot (9+ ops per video) |
| Azure Functions Consumption Plan | Cloud Compute | Durable Functions v4 — orchestrator + 4 activity types | .NET 8 isolated worker, 10-min timeout | `Functions/VideoOrchestrator.cs`, all `Activities/` | 🔴 Hot (5–13 executions per video depending on segment count) |

---

### VideoFunction — Pipeline Structure (Segment Count Derivation)

Understanding how many segments a video generates is the key multiplier for all per-call cost estimates.

```
Fact text (~300 chars, ~60 words)
  → TTS at rate 0.88 → ~20–25 s of audio (+2.3 s padding = ~24 s total)
  → SegmentPlanner splits at sentence boundaries (period/?.!/comma + 0.4 s pause)
     + force-splits any segment > 3.5 s (MaxClipDuration)
  → Typical result: 6–8 segments  ← baseline = 7 segments used below
```

Each segment triggers one parallel `FetchClipActivity` (fan-out in `VideoOrchestrator.cs:43`).

---

### VideoFunction — Cost-Per-Video Estimate

#### Baseline assumptions
- 7 segments per video (derived above)
- YouTube path: assume ~50% success rate per segment on Azure datacenter IPs (yt-dlp bot detection issues are documented in the code via cookie workarounds)
- CV calls: avg 2 candidates checked before passing or exhausting (range 1–5)
- Pexels: called for the ~50% of segments where YouTube fails; primary query succeeds on first attempt

#### Step-by-step cost breakdown

| # | Activity | Service | Calls | Est. cost | Notes |
|---|----------|---------|-------|-----------|-------|
| 1 | SynthesizeTts | Azure TTS Neural | 1 SSML synthesis | **~$0.005** | ~300 chars of fact text × $16/1M chars. SSML wrapper tags not billed. |
| 2 | PlanSegments | — | (CPU only) | $0 | No external API. |
| 3a | FetchClip × 7 — YouTube search | YouTube Data API v3 | 7 × 1 = 7 searches × 100 units = 700 units | **$0 (within free quota)** | 10,000 units/day free. At 14 videos/day the quota is exhausted; YouTube silently fails beyond that. |
| 3b | FetchClip × 7 — CV thumbnail checks | Azure Computer Vision | 7 segments × avg 2 CV calls = **14 calls** | **~$0.014** | $0.001/call. Ranges from 7 calls (1 per segment, first candidate always passes) to 35 calls (5 per segment, all rejected). |
| 3c | FetchClip × 7 — Pexels fallback (50% segments) | Pexels API | ~4 segments × 1 primary Pexels call = 4 calls | $0 (free) | Rate-limited, not billed. |
| 3d | FetchClip × 7 — yt-dlp download + ffmpeg trim | Network egress / CPU | — | $0 (ingress free, intra-Azure free) | YouTube downloads are internet ingress (free). Clip uploads to blob are intra-Azure. |
| 3e | FetchClip × 7 — clip blob upload | Azure Blob Storage | 7 write ops + 7 blobs (~4 MB each = 28 MB) | **~$0.0001** | Ops cost negligible; storage accumulates (see below). |
| 4 | RenderVideo — download clips + WAV | Azure Blob Storage | 8 read ops (~30 MB total) | **~$0** | Intra-region reads are free. Op cost negligible. |
| 4 | RenderVideo — ffmpeg encode + upload final MP4 | Azure Blob Storage | 1 write op (~25 MB) | **~$0** | Op cost negligible. |
| 5 | Blob storage at rest | Azure Blob Storage | ~60 MB total blobs kept indefinitely | **~$0.001/month/video** | At 100 videos/month → 6 GB → $0.11/month. Intermediate `poc-jobs/` clips are never deleted. |
| — | Azure Functions compute | Azure Functions Consumption | ~1,800 GB-seconds per video (see breakdown) | **~$0.003** | After 400K free GB-s/month (≈222 videos free). Breakdown: TTS 60 GB-s, PlanSegs 3 GB-s, 7× FetchClip 1,260 GB-s (parallel), RenderVideo 360 GB-s, orchestrator overhead 120 GB-s. |

**Functions GB-second breakdown per video:**

| Activity | Duration (est.) | Memory (est.) | GB-seconds |
|----------|----------------|--------------|------------|
| `SynthesizeTtsActivity` | 60–90 s | 1 GB | ~75 GB-s |
| `PlanSegmentsActivity` | 5 s | 0.5 GB | ~3 GB-s |
| `FetchClipActivity` × 7 (parallel) | 90–180 s each | 1.5 GB | ~180 GB-s each → **1,260 GB-s** |
| `RenderVideoActivity` | 120–240 s | 2 GB | ~360 GB-s |
| Orchestrator + HTTP trigger overhead | ~30 s | 0.5 GB | ~15 GB-s |
| **Total** | | | **~1,800 GB-s** |

#### Per-video cost summary

| Scenario | CV calls | Total cost | Dominant cost |
|----------|---------|-----------|--------------|
| **Best case** (1 CV call/segment, YouTube always succeeds) | 7 | **~$0.015** | CV ($0.007) + Functions ($0.003) + TTS ($0.005) |
| **Average** (2 CV calls/segment, 50% YouTube fallback to Pexels) | 14 | **~$0.025** | CV ($0.014) + Functions ($0.003) + TTS ($0.005) |
| **Worst case** (5 CV calls/segment, YouTube fails then exhausts all 5 candidates) | 35 | **~$0.046** | CV ($0.035) + Functions ($0.006 — longer runs) + TTS ($0.005) |

#### Cost at scale (average case, after free tiers)

| Volume | Daily Cost | Monthly Cost | Monthly Cost Breakdown |
|--------|-----------|-------------|----------------------|
| 10 videos/day | ~$0.25 | **~$7.50** | CV $4.20 + TTS $1.50 + Functions $0.90 + Storage $0.30 |
| 100 videos/day | ~$2.50 | **~$75** | CV $42 + TTS $15 + Functions $9 + Storage $3 |
| 1K videos/day | ~$25 | **~$750** | CV $420 + TTS $150 + Functions $90 + Storage $30 |

> **CV is ~56% of per-video cost** at average case. Reducing CV call count has the highest savings leverage.

> **YouTube API quota ceiling**: at 100 videos/day × 7 searches × 100 units = 70,000 units/day — 7× over the 10,000/day free quota. At this scale YouTube is effectively disabled (all segments fall back to Pexels), but CV calls still fire (silently wasted until `results.Count == 0` path is hit).

---

### Cost Bombs 💣

#### 💣 Cost Bomb #1: CV Fires Up to 5× Per Segment with Zero Cross-Segment Caching

- **Location**: `Services/YouTubeVideoService.cs:79-99` (`.Take(5)` loop) and `Activities/FetchClipActivity.cs:51` (new `ComputerVisionService` per activity)
- **Pattern**: Each `FetchClipActivity` instance creates a **brand-new** `ComputerVisionService` (and a brand-new `ImageAnalysisClient` inside `AnalyzeThumbnailAsync` at `ComputerVisionService.cs:43`). With 7 parallel activities, there is zero shared cache. The same popular YouTube video ID can appear in multiple segment search results and be CV-analyzed once per occurrence. The inner loop iterates up to 5 candidates before giving up, firing a CV call for each.
- **Estimated waste**: Worst case 35 CV calls/video × $0.001 = **$0.035/video** vs. a perfectly cached scenario of 7 calls = **$0.007/video** — a **5× cost multiplier** that is entirely avoidable.
- **Fix — two-part**:
  1. **Reduce `.Take(5)` to `.Take(3)`** in `YouTubeVideoService.cs:80`. Most watermark-free, car-present clips are found in the first 2–3 candidates; rarely does a 4th or 5th attempt succeed after the first three fail. This caps worst-case at 21 CV calls.
  2. **Add a cross-request CV result cache** keyed by `videoId`. A simple `ConcurrentDictionary<string, ThumbnailAnalysis>` in a singleton-scoped `ComputerVisionService` would eliminate duplicate analysis for the same video ID within and across activities. For a more durable cache, store results in Azure Blob Storage (e.g., `cv-cache/{videoId}.json`) with a TTL of 7 days — thumbnails don't change.

```csharp
// In ComputerVisionService — add singleton cache:
private static readonly ConcurrentDictionary<string, ThumbnailAnalysis> _cache = new();

public async Task<ThumbnailAnalysis> AnalyzeThumbnailAsync(string videoId)
{
    if (_cache.TryGetValue(videoId, out var cached)) return cached;
    // ... existing analysis logic ...
    _cache[videoId] = result;
    return result;
}
```

---

#### 💣 Cost Bomb #2: YouTube Quota Exhausted at ~14 Videos/Day — CV Calls Fire Silently Until Empty Results

- **Location**: `Services/YouTubeVideoService.SearchAsync()` (quota error caught at line 129, returns `[]`) → `FindBestCandidateAsync()` returns null immediately on empty list — **but only if the error throws**
- **Pattern**: When the YouTube Data API quota is exhausted (HTTP 403), the `catch` at `YouTubeVideoService.cs:129` silently returns `[]`, and `FindBestCandidateAsync` returns `null` immediately (no CV calls, correct fallback). **However**, if the API returns a partial/degraded result before quota cutoff (some videos returned, then subsequent searches fail), each segment that received *some* YouTube results will still attempt CV checks on those candidates, burning CV quota on videos that may all be rejected — all before falling back to Pexels anyway. More critically: at 100 videos/day **the entire YouTube layer is dead** (70,000 units/day needed vs. 10,000 free), meaning `FetchClipActivity` always goes straight to Pexels — burning 4 Pexels API calls per segment with no YouTube benefit.
- **Estimated waste**: At 100 videos/day: YouTube quota exceeded by 7×. Zero YouTube clips used, yet the YouTube API key check (`if (!string.IsNullOrEmpty(input.YouTubeApiKey))`) still enters the YouTube path, attempts the search (fails), then falls through to Pexels. The quota check adds latency on every clip fetch with no upside.
- **Fix**: Track daily quota usage in a blob-storage counter (e.g., `quota/youtube-{date}.json`). At the start of each `FetchClipActivity`, check if today's quota is near the ceiling (e.g., 9,500 units) and skip the YouTube path entirely for that day. Alternatively, apply for a higher quota on Google Cloud Console (quota increases to 1M units/day are free to request and typically approved).

---

#### 💣 Cost Bomb #3: Pexels Multi-Tier Fallback — Up to 4 API Calls Per Segment, No Response Caching

- **Location**: `Activities/FetchClipActivity.cs:150-186` (`SearchPexelsWithFallbackAsync`)
- **Pattern**: When YouTube fails (or is skipped), each `FetchClipActivity` fires up to **4 sequential Pexels API calls**: primary model-specific query → brand fallback query → brand-only query → absolute last resort generic query. None of these Pexels search responses are cached between activities or between video jobs. Two different segments of the same video with the same brand (`"Ford Mustang exterior rolling b-roll footage"` vs. `"Ford Mustang interior POV driving footage"`) will each hit Pexels for the brand fallback `"Ford Mustang car driving road footage"` independently. There is also a **duplicate `SearchPexelsAsync` implementation** — one in `FetchClipActivity.cs` and one in `PexelsVideoService.cs` — meaning any caching added to `PexelsVideoService` would not benefit `FetchClipActivity`.
- **Estimated waste**: 7 segments × 4 Pexels calls = 28 calls/video. Pexels monthly limit: 20,000 calls. At 28 calls/video this exhausts the limit at **~714 videos/month** (~24/day). At the more optimistic 7 calls/video (YouTube handles most), limit is hit at ~2,857 videos/month.
- **Fix**:
  1. **Remove the duplicate `SearchPexelsAsync` in `FetchClipActivity.cs`** — have `FetchClipActivity` delegate to `PexelsVideoService` (registered as a DI service) so caching lives in one place.
  2. **Cache successful Pexels query→videoUrl mappings in Azure Blob Storage** (`pexels-cache/{queryHash}.json`, TTL 24h). A cache hit saves the API call entirely.
  3. **Deduplicate fallback queries across segments** — before fan-out, the orchestrator could pre-compute unique Pexels queries and batch-search them, then distribute results to activities.

---

#### 💣 Cost Bomb #4: `ImageAnalysisClient` Re-Instantiated Inside Every CV Call

- **Location**: `Services/ComputerVisionService.cs:43-45`
- **Pattern**: `ImageAnalysisClient` is constructed *inside* `AnalyzeThumbnailAsync` on every single invocation — not just per activity instance, but per call within an activity's CV loop. The Azure SDK's `ImageAnalysisClient` maintains an internal `HttpClient` and connection pool; re-creating it per call discards the connection pool, forces a new TLS handshake to the Cognitive Services endpoint for every CV transaction, and adds 50–150 ms of overhead per call.
- **Estimated waste**: Not a direct billing impact (CV pricing is per-transaction regardless), but adds 50–150 ms × up to 35 CV calls = up to **~5 seconds of pure connection overhead per video** that shows up as extended Azure Functions execution time (billed GB-seconds).
- **Fix**: Hoist `ImageAnalysisClient` to a field, initialized once in the constructor:
  ```csharp
  public class ComputerVisionService(string endpoint, string apiKey)
  {
      private readonly ImageAnalysisClient _client =
          new(new Uri(endpoint), new AzureKeyCredential(apiKey));

      public async Task<ThumbnailAnalysis> AnalyzeThumbnailAsync(string videoId)
      {
          // use _client directly — no new() here
          var result = await _client.AnalyzeAsync(new Uri(thumbnailUrl), VisualFeatures.Read | VisualFeatures.Tags);
          ...
      }
  }
  ```

---

#### 💣 Cost Bomb #5: Intermediate Blobs in `poc-jobs/` Never Expire

- **Location**: `Activities/FetchClipActivity.cs:101-103` and `Activities/SynthesizeTtsActivity.cs:42-44`
- **Pattern**: Each video job writes to `poc-jobs/{jobId}/narration.wav` (~1 MB) and `poc-jobs/{jobId}/clip_00.mp4` through `clip_06.mp4` (~4 MB each = ~28 MB). These blobs are uploaded as durable storage for the render step, then **never deleted**. There is no blob lifecycle policy and no cleanup call after `RenderVideoActivity` completes. At 100 videos/day: 100 × 29 MB intermediate files = **~2.9 GB/day** of accumulating blob storage that serves no purpose after rendering.
- **Estimated waste**: At 100 videos/day × $0.018/GB/month: accumulated after 30 days = 87 GB → **$1.57/month and growing** (storage cost compounds as blobs accumulate indefinitely).
- **Fix — two options**:
  1. **Delete intermediate blobs at the end of `RenderVideoActivity`**: after the final MP4 is uploaded, call `DeleteBlobAsync` on the WAV and all clip blobs in `poc-jobs/{jobId}/`.
  2. **Add a Blob Lifecycle Policy** (zero code change): in the Azure portal or Bicep/ARM, add a lifecycle rule: `if lastModified > 1 day AND blobPath starts with "poc-jobs/" → delete`. This is the safer option as it doesn't require code changes and handles orphaned jobs (e.g., render activity failed before cleanup).

---

### VideoFunction — Caching & Optimization Status

| Service | Cached | Rate Limited (client-side) | Batched | Fallback |
|---------|--------|---------------------------|---------|----------|
| Azure TTS Neural | ❌ No WAV cache (same fact re-synthesizes) | ✅ N/A (1 call/video) | N/A | ❌ No fallback (throws on failure) |
| Azure Computer Vision | ❌ No videoId→result cache; client re-created per call | ❌ No client-side throttle | N/A (single URL per call) | ✅ Optimistic fallback on exception (`HasWatermark: false, HasCar: true`) |
| YouTube Data API v3 | ❌ No search result cache | ❌ No client-side quota tracking | ❌ 1 search per segment (no cross-segment dedup) | ✅ Returns null → Pexels fallback |
| Pexels Videos API | ❌ No query→URL cache in FetchClipActivity | ✅ `SemaphoreSlim(2)` in PexelsVideoService (but not in FetchClipActivity) | ❌ Individual calls; up to 4 per segment | ✅ 4-tier fallback to generic query |
| Azure Blob Storage | N/A | ✅ Built-in SDK retry | ❌ Individual uploads | ✅ `CreateIfNotExistsAsync` |
| Azure Functions Consumption | N/A | ✅ Durable Functions replay-safety | ✅ Fan-out pattern (parallel FetchClip) | ✅ Partial clip failure tolerated (readyCount > 0) |

---

### VideoFunction — Cost Summary

| Metric | Value |
|--------|-------|
| **Total paid services (VideoFunction)** | 4 billable (TTS, CV, Blob, Functions) + 2 free-tier (YouTube, Pexels) |
| **Hot-path services** | All 4 (every video generation invocation) |
| **Cost bombs found** | 5 |
| **Dominant cost driver** | Azure Computer Vision (~56% of per-video variable cost) |
| **Estimated cost per video (average)** | **~$0.025** |
| **Estimated cost per video (worst case)** | **~$0.046** |
| **Break-even with free Function tier** | ~222 videos/month (400K GB-s grant) |
| **YouTube quota ceiling (free tier)** | ~14 videos/day (10,000 units ÷ 700 units/video) |
| **Pexels rate limit ceiling (worst case)** | ~24 videos/day (20,000 calls/month ÷ 28 calls/video) |
| **Estimated monthly cost @ 100 videos/day** | **~$75** (CV ~$42, TTS ~$15, Functions ~$9, Storage ~$3, Blob accumulation ~$6+) |

### Monthly VideoFunction Cost Breakdown (100 videos/day, average case)

```
Azure Computer Vision (14 calls × 100 vids/day × 30d) ....  $42.00  (56%)
Azure TTS Neural (300 chars × 100 vids/day × 30d) ........  $14.40  (19%)
Azure Functions Consumption (1,800 GB-s × 3K vids/mo) ....   $8.00  (11%)
Azure Blob Storage — accumulating poc-jobs/ blobs ..........   $6.00  (8%)  [avoidable]
Azure Blob Storage — final poc-videos/ storage .............   $1.62  (2%)
Azure Blob Storage — operations (reads + writes) ...........   $0.02  (<1%)
YouTube Data API v3 .......................................   $0.00  [quota only; $0 billing]
Pexels Videos API .........................................   $0.00  [free, but rate-limited]
──────────────────────────────────────────────────────────────────────────────
TOTAL                                                        ~$72/month
```

---

### VideoFunction — Recommendations (by savings impact)

1. 💰💰💰 **Cache CV results by `videoId` + reduce `.Take(5)` to `.Take(3)`** — up to 80% CV cost reduction
   CV is the #1 cost driver. A `ConcurrentDictionary<string, ThumbnailAnalysis>` singleton (or blob-backed TTL cache) eliminates duplicate analysis of the same video ID across segments. Reducing the candidate limit from 5 to 3 caps the per-segment maximum at 3 calls instead of 5. Combined effect: average CV calls drop from 14 to ~8–10 per video, saving ~**$18–24/month at 100 videos/day**.
   - In `ComputerVisionService.cs`: add `private static readonly ConcurrentDictionary<string, ThumbnailAnalysis> _cache = new()` and check/populate it in `AnalyzeThumbnailAsync`.
   - In `YouTubeVideoService.cs:80`: change `.Take(5)` → `.Take(3)`.

2. 💰💰 **Add Azure Blob lifecycle rule to auto-delete `poc-jobs/` blobs after 1 day** — saves ~$6–60+/month (growing)
   Intermediate WAV and clip blobs in `poc-jobs/` serve no purpose after rendering. Without cleanup, storage grows unboundedly. Add a lifecycle policy in Bicep/ARM:
   ```json
   { "name": "delete-job-intermediates", "enabled": true,
     "definition": { "filters": { "blobTypes": ["blockBlob"], "prefixMatch": ["poc-jobs/"] },
                     "actions": { "baseBlob": { "delete": { "daysAfterModificationGreaterThan": 1 } } } } }
   ```

3. 💰💰 **Track YouTube quota usage and skip YouTube path when daily ceiling is near**
   At >14 videos/day the YouTube path adds latency with zero yield. A blob-stored daily counter (`quota/youtube-YYYY-MM-DD.json`) incremented per search call lets the activity skip the YouTube block when today's count ≥ 95 calls (9,500 units). This eliminates the wasted HTTP round-trips and makes the fallback to Pexels deterministic and immediate. Also apply for a quota increase (free, up to 1M units/day) at Google Cloud Console if YouTube CC clips are genuinely valuable.

4. 💰💰 **Consolidate the duplicate Pexels implementation and add query→URL caching**
   `FetchClipActivity.cs` contains a private `SearchPexelsAsync` that duplicates `PexelsVideoService.SearchPexelsAsync`. Delete the copy in `FetchClipActivity` and inject `PexelsVideoService` via DI. In `PexelsVideoService`, add a blob-backed cache keyed by `SHA256(query + orientation)` with a 24-hour TTL. This eliminates repeat Pexels searches for the same brand/shot type across videos and reduces exposure to the 20,000/month rate limit ceiling.

5. 💰 **Move `ImageAnalysisClient` construction out of `AnalyzeThumbnailAsync` into the constructor**
   Currently `new ImageAnalysisClient(...)` is called on every CV invocation, forcing a new TLS handshake and discarding the HTTP connection pool. Hoisting it to a field (initialized once) saves ~50–150 ms per CV call. At 14 CV calls/video × 100 videos/day this reclaims ~3–5 minutes of Functions execution time per day — translating to a modest but real GB-second savings at scale.

6. 💰 **Cache TTS WAV output keyed by fact hash**
   If the same fact text is submitted more than once (retry, test run, reprocessing), TTS is billed again for an identical synthesis. A blob check `tts-cache/{SHA256(fact)}.wav` before calling `SynthesizeAsync` would save $0.005 per duplicate. Low impact today but free to implement and eliminates any runaway cost from a retry loop.

---

### Combined CarFacts Platform Cost Summary (both modules)

| Module | Monthly cost (current / baseline) | Primary cost driver |
|--------|----------------------------------|---------------------|
| `CarFacts.Functions` (blog + social) | ~$205/month | Twitter/X API Basic Plan ($200 fixed) |
| `CarFacts.VideoFunction` (video gen, 100 vids/day) | ~$72/month | Azure Computer Vision ($42) |
| **Combined** | **~$277/month** | Twitter/X dominates at lower video volumes; CV grows linearly with video output |
