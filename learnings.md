# Learnings & Failure Log — CarFacts Azure Functions Project

Lessons from building, deploying, and debugging this Azure Functions (.NET 8 isolated) project. Focused on **failures and how to avoid them** in future projects.

---

## 1. Azure Functions Startup Crashes

### 1.1 File System Access in Azure Sandbox

**Failure:** `System.UnauthorizedAccessException: Access to the path 'C:\logs' is denied`

Azure Functions run in a restricted sandbox. Writing to arbitrary paths like `C:\logs` works locally but **crashes the function on startup** in Azure.

**Fix:** Detect the Azure environment using `WEBSITE_INSTANCE_ID` env var and skip file I/O operations that target local-only paths.

```csharp
var isAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
if (isAzure) return; // skip local-only file operations
```

**Lesson:** Always gate local-only file operations behind an environment check. Never assume the local file system layout exists in production.

---

### 1.2 Serilog Overrides App Insights Logging

**Failure:** Zero telemetry in App Insights — function appeared to never execute, even though it was running.

Calling `.UseSerilog()` on the `HostBuilder` **replaces all logging providers**, including the App Insights provider registered by `AddApplicationInsightsTelemetryWorkerService()`.

**Fix:** Only call `.UseSerilog()` when running locally:

```csharp
if (!isAzure)
    hostBuilder.UseSerilog();
```

**Lesson:** `.UseSerilog()` is a nuclear option — it wipes all other providers. In Azure Functions, App Insights logging is critical for observability. Use Serilog only for local development, or configure it as an *additional* sink rather than a replacement.

---

### 1.3 Config Key Mapping: `__` vs `:`

**Failure:** Empty API endpoint and keys at runtime — services threw `ArgumentNullException` or made requests to empty URLs.

Azure Functions environment variables use `__` (double underscore) as the hierarchy separator, which .NET maps to `:` in `IConfiguration`. The ARM template had keys like `AzureOpenAI__Endpoint`, but the code read `AI:AzureOpenAIEndpoint` (section `AI`).

**Fix:** ARM template app settings must match the code's config section structure:
- Code reads `AI:AzureOpenAIEndpoint` → env var must be `AI__AzureOpenAIEndpoint`
- NOT `AzureOpenAI__Endpoint` (wrong section)

**Lesson:** Before deploying, map every `IConfiguration` read path to its corresponding env var name. The `__` → `:` mapping is mechanical but easy to get wrong, especially with nested sections.

---

## 2. Key Vault & Secrets

### 2.1 Secrets Needed at DI Registration Time

**Failure:** Semantic Kernel's `AddAzureOpenAIChatCompletion()` requires the API key at DI registration time, but Key Vault is an async operation.

In local dev, the key comes from `local.settings.json` synchronously. In production, it must be fetched from Key Vault **before** the DI container is built.

**Fix:** Use synchronous `SecretClient.GetSecret()` (not async) during startup:

```csharp
var client = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
apiKey = client.GetSecret("AzureOpenAI-ApiKey").Value.Value;
```

**Lesson:** If a library requires secrets at registration time, fetch them synchronously during startup. Don't register empty strings and hope they'll be populated later.

---

### 2.2 Key Vault RBAC Access

**Failure:** User couldn't see secrets in the Azure Portal after deploying Key Vault.

The ARM template created the Key Vault with RBAC authorization but only assigned roles to the Function App's managed identity, not to the deploying user.

**Fix:** Assign `Key Vault Secrets Officer` role to the user's Azure AD object ID.

**Lesson:** Always assign Key Vault RBAC roles to both the application identity AND the operator/developer identity during deployment.

---

## 3. API Rate Limiting & Provider Failures

### 3.1 Parallel Requests Trigger Rate Limits

**Failure:** StabilityAI returned `429 Too Many Requests` immediately when 5 image requests fired in parallel via `Task.WhenAll()`.

**Fix:**
1. Changed to **sequential** image generation with a 2-second delay between requests
2. Added **exponential backoff retry** (3 attempts: 2s, 4s, 8s) on 429 responses

**Lesson:** External APIs (especially free tiers) have tight rate limits. Always:
- Start with sequential requests for image/media APIs
- Implement retry with exponential backoff for transient failures (429, 503)
- Don't assume `Task.WhenAll()` is safe for external API calls

---

### 3.2 No Fallback = Total Failure

**Failure:** When a single image provider failed, the entire pipeline crashed — no blog post was published at all.

**Fix:** Implemented a `FallbackImageGenerationService` that chains providers (StabilityAI → TogetherAI → empty list), and the function gracefully publishes text-only when all providers fail.

**Lesson:** For non-critical features (images enhance but aren't essential), always implement graceful degradation. A text-only post is better than no post.

---

### 3.3 Third-Party API Key Formats Change

**Failure:** Together AI legacy API key (`key_xxx...`) rejected with `401 Unauthorized`. The key format had changed to project-scoped keys (`tgp_v1_xxx...`).

**Lesson:** API providers deprecate key formats. When integrating third-party APIs:
- Document the expected key format and validation endpoint
- Add a health-check or key validation step during startup
- Monitor for auth failures specifically (not just generic errors)

---

### 3.4 "Free Tier" Doesn't Always Mean Free

**Failure:** Together AI's `FLUX.1-schnell` (marketed as free) returned `402 Payment Required` without account credits.

**Lesson:** "Free tier" models may still require:
- Account verification
- A minimum credit balance
- Active billing setup

Always test API access with a simple call before building a full integration.

---

## 4. Deployment & Publishing

### 4.1 Cold Start Breaks Trigger Sync

**Failure:** `func azure functionapp publish` succeeded in uploading but failed at "Syncing triggers" with `BadRequest`. Retrying after 30-45 seconds worked.

**Cause:** Azure Functions Consumption Plan has cold start delays. The trigger sync API call arrives before the app is fully initialized.

**Lesson:** After deploying to a Consumption Plan, allow 30-60 seconds before expecting the app to respond. A second publish attempt usually succeeds. Consider adding retry logic to CI/CD pipelines.

---

### 4.2 `--no-build` Can Deploy Stale Code

**Failure:** Published with `--no-build` flag, but the deployed function returned 404 — the binaries didn't include the latest code changes.

**Lesson:** Only use `--no-build` when you're certain the build output matches your source. When in doubt, let the publish command build fresh.

---

### 4.3 Storage Account Name Collisions

**Failure:** ARM deployment failed because the storage account name was globally taken.

**Lesson:** Azure storage account names must be globally unique (3-24 chars, lowercase alphanumeric). Use a unique prefix or suffix (project name + random digits) in ARM templates.

---

## 5. Observability

### 5.1 App Insights Telemetry Delay

**Gotcha:** App Insights telemetry can take 2-5 minutes to appear in queries. During debugging, this made it seem like the function wasn't executing at all.

**Workaround:** For immediate feedback, check:
- Kudu log stream: `https://<app>.scm.azurewebsites.net/api/logstream`
- Host log files: `https://<app>.scm.azurewebsites.net/api/vfs/LogFiles/Application/Functions/Host/`
- Event log: `https://<app>.scm.azurewebsites.net/api/vfs/LogFiles/eventlog.xml` (shows .NET runtime crashes)

**Lesson:** Don't rely solely on App Insights during active debugging. Use Kudu for real-time logs.

---

### 5.2 Missing Telemetry ≠ Not Running

**Failure:** Assumed the function wasn't running because App Insights showed zero telemetry. In reality, Serilog had overridden the App Insights provider (see 1.2).

**Lesson:** When telemetry is missing, check the logging pipeline configuration first. The function may be running perfectly but logging to the wrong sink.

---

## 6. Local vs Production Parity

### 6.1 Config Differences Cause Silent Failures

**Failure:** `local.settings.json` had the correct config keys, but the ARM template used different key names. The function worked locally but failed in Azure with empty config values.

**Lesson:** Maintain a mapping document or test that verifies every `local.settings.json` key has a corresponding ARM template / app setting entry. Consider generating one from the other.

---

### 6.2 Local Secrets ≠ Production Secrets

**Failure:** `local.settings.json` had a placeholder `YOUR_TOGETHER_AI_KEY_HERE` for TogetherAI. This was never noticed locally (different provider was used) but caused failures when the fallback chain tried TogetherAI in production.

**Lesson:** Even unused secrets should be validated or clearly marked. Placeholder values should fail fast with a clear error message, not silently pass through to an API call.

---

## 7. SEO Optimizations

### 7.1 Blog Title Strategy — Brand + Model Anchor with Multi-Fact Teaser

**Problem:** Early titles were either too generic ("5 Car Facts from April 10") or tried too hard with clickbait ("The Electric Sports Car That Changed Everything for EVs") — lacking searchable keywords and not signaling multi-fact content.

**Evolution:**
1. **V1** (initial): Generic listicle titles, no keyword targeting
2. **V2** (first update): Titles hinted at the most prominent fact — curiosity-driven but sometimes vague (e.g., "When a Seatbelt Revolutionized Automobile Safety Forever")
3. **V3** (current): Anchored on the FIRST fact's brand + model + event, with a teaser signaling additional content

**Current format:**
```
<Brand Model Event> — And 4 More Fascinating Car Facts
How the <Brand Model> Changed <Outcome> — Plus 4 More Automotive Stories
<Brand Model>: <Key Event> (And 4 More You Should Know)
```

**Rules:**
- Always include the specific brand and model from the first fact (e.g., "Ford Model A", "Tesla Roadster")
- Keep between 50-60 characters (hard max 70)
- Teaser must signal the article contains multiple facts (e.g., "+ 4 more facts")
- Confident, intriguing tone — NOT tabloid clickbait
- No overused phrases ("You Won't Believe", "Shocking Truth")
- Every title must contain real, searchable keywords

**Example output:** "Ford Model A: The Birth of the Affordable Car (And 4 More You Should Know)"

**Lesson:** Titles should serve dual purpose — rank for specific keywords (brand + model) while also honestly communicating the article format (multi-fact post). The "and X more" teaser sets proper expectations without sacrificing click-through rate.

---

### 7.2 Meaningful Anchor IDs for Deep Linking

**Problem:** Fact sections used generic anchors (`#fact-1`, `#fact-2`) — not SEO-friendly and causing ID collisions in Cosmos DB for same-date posts.

**Fix:** Generated slug-based anchors from car model + year: `#ford-model-48-1935`, `#bmw-3-0-csl-1972`

**Implementation:** `SlugHelper.GenerateAnchorId(fact)` creates URL-safe slugs from `{carModel}-{year}`

**Lesson:** Anchor IDs should be descriptive and keyword-rich — they're visible in URLs when shared and help search engines understand page structure.

---

### 7.3 Internal Backlinking via Cosmos DB Keywords

**Problem:** No cross-post linking — each post was an island. Search engines reward sites with strong internal link graphs.

**Solution — two-tier backlinking system:**

1. **Inline backlinks (per-fact):** Each fact section gets one contextual link to a related fact from another post. Presented subtly: *"🔗 Speaking of which — the BMW 328 (1936) has a story worth knowing too."*

2. **Related posts section (bottom of page):** Replaces the old FAQ section with "🔍 You Might Also Find These Interesting" — 4 post-level cards with thumbnail images in a 2×2 grid layout.

**Keyword-based matching:** Each fact has 5-8 keyword tags stored in Cosmos DB. `ARRAY_CONTAINS` queries find related facts/posts. Weighted random selection (`1/(backlinkCount+1)`) ensures even distribution.

**Backlink count tracking:** Every linked record's `backlinkCount` is incremented in Cosmos DB after publish — prevents the same posts from being over-linked.

**Total internal links per post:** ~9 (5 inline + 4 related posts) — up from zero before.

**Lesson:** Internal linking is a high-ROI SEO technique. Automating it via keyword matching + weighted selection creates an organic-feeling link graph that scales without manual curation.

---

### 7.4 Per-Fact Keyword Tagging for Cross-Referencing

**Problem:** No structured metadata per fact — couldn't programmatically find related content.

**Solution:** The SEO prompt generates 5-8 lowercase keyword tags per fact (e.g., `["ford", "v8", "flathead", "engine", "american", "sedan"]`). Tags cover: manufacturer, model, vehicle category, technology concepts, era descriptors.

**Storage:** Cosmos DB serverless container (`fact-keywords`), one record per fact with: `id`, `postUrl`, `factUrl`, `anchorId`, `title`, `carModel`, `year`, `keywords[]`, `imageUrl`, `postTitle`, `backlinkCount`, `createdAt`.

**Lesson:** Structured keyword metadata enables features beyond SEO (backlinking, related posts, search). Invest in generating good metadata upfront — it compounds in value.

---

### 7.5 WordPress.com Strips `<script>` Tags — No JSON-LD

**Attempted:** Adding JSON-LD structured data (`<script type="application/ld+json">`) for rich search results.

**Failed:** WordPress.com strips all `<script>` tags from post content. This is a platform restriction, not a bug.

**Workaround:** Use microdata attributes (`itemscope`, `itemprop`, `itemtype`) directly in HTML elements instead. Already implemented throughout the fact sections.

**Lesson:** Know your publishing platform's HTML sanitization rules before investing in structured data. WordPress.com is more restrictive than self-hosted WordPress.

---

### 7.6 Related Posts Section Design — Compact Thumbnail Cards

**Problem:** The FAQ section at the bottom of each post was generic boilerplate that added little SEO or engagement value.

**Replacement:** "You Might Also Find These Interesting" section with 4 related post cards. Each card has:
- Thumbnail image (120px height, `object-fit: cover`)
- Post title as a clickable link
- Compact 2×2 flexbox grid layout (`flex: 1 1 calc(50% - 12px)`)
- Subtle styling (light border, rounded corners, `#fafafa` background)

**Data source:** Cards are fetched from Cosmos DB, grouped by `postUrl` (distinct posts, not individual facts), with weighted random selection favoring lower `backlinkCount`.

**Lesson:** Replace generic SEO boilerplate with genuinely useful navigation elements. Related content cards serve both SEO (internal links) and UX (reader engagement).


```csharp
// Detect Azure environment
var isAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));

// Detect local development
var isLocal = string.Equals(
    config["AZURE_FUNCTIONS_ENVIRONMENT"], "Development",
    StringComparison.OrdinalIgnoreCase);
```

---

## Quick Reference: Retry Pattern

```csharp
for (int attempt = 0; attempt <= maxRetries; attempt++)
{
    var response = await httpClient.SendAsync(request, ct);

    if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < maxRetries)
    {
        var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
        await Task.Delay(backoff, ct);
        continue;
    }

    response.EnsureSuccessStatusCode();
    return await response.Content.ReadAsStringAsync(ct);
}
```

---

## 7. SEO Optimizations

### 7.1 Title Tag Strategy: Brand + Model Anchor with Multi-Fact Teaser

**Problem:** Early titles were generic clickbait like "The Electric Sports Car That Changed Everything for EVs" — vague, no searchable brand/model keywords, no signal that the post contains multiple facts.

**Fix:** Updated the SEO prompt to anchor titles on the **first fact's brand + model** and append a multi-fact teaser:

- ✅ `Ford Model A: The Birth of the Affordable Car (And 4 More You Should Know)`
- ✅ `How the Tesla Roadster Changed EVs — Plus 4 More Automotive Stories`
- ❌ `The Electric Sports Car That Changed Everything for EVs` (old — no brand, no teaser)

**Rules baked into prompt:**
- Always include brand + model from the first fact (e.g., "Volvo 3-Point Seatbelt")
- Keep between 50–60 characters (hard max 70)
- Teaser must signal multiple facts ("+ 4 more facts", "and four more milestones")
- Confident tone, not try-hard clickbait — let the fact speak for itself
- Never use "You Won't Believe" or "Shocking Truth"

**Lesson:** Title tags should contain the exact keywords a user would search for (brand + model + event). Vague curiosity-gap titles get clicks but don't rank in search.

**File:** `src/CarFacts.Functions/Prompts/SeoUserPrompt.txt`

---

### 7.2 Meaningful Anchor IDs for Deep Linking

**Problem:** Fact headings used generic anchors like `#fact-1`, `#fact-2`. These are:
- Not descriptive for users sharing links
- Not keyword-rich for search engines
- Not unique across posts (collision risk in Cosmos DB)

**Fix:** Generate SEO-friendly slugs from the car model + year: `#ford-model-48-1935`, `#bmw-3-0-csl-1972`.

**Implementation:** `SlugHelper.GenerateAnchorId(fact)` normalizes the model name (lowercase, hyphens, strip special chars) and appends the year. Used in both HTML rendering and Cosmos DB record IDs.

**Lesson:** Every anchor ID should be a keyword-rich slug that could stand alone as a URL fragment. Generic numeric IDs are a wasted SEO opportunity.

**File:** `src/CarFacts.Functions/Helpers/SlugHelper.cs`

---

### 7.3 Internal Backlinking via Cosmos DB Keywords

**Problem:** Posts were isolated islands — no internal links between them. Google values internal linking for:
- Helping crawlers discover and index more pages
- Distributing page authority (link juice) across the site
- Reducing bounce rate by guiding users to related content

**Fix (two-part system):**

**Part A — Per-fact inline backlinks:**
- After SEO generation, each fact's keywords are searched against Cosmos DB
- A related fact from a different post is selected (weighted random, favoring lower `backlinkCount`)
- Rendered as a subtle "Speaking of which" callout in each fact section
- Style: light blue background, left border accent — intriguing but not attention-seeking

**Part B — "You Might Also Find These Interesting" section:**
- Replaced the generic FAQ section at the bottom
- Shows 4 related post cards in a 2×2 grid with thumbnail images
- Posts selected from Cosmos DB, distinct by `postUrl`, weighted by lower backlink count
- Cards: 120px thumbnail, rounded corners, subtle border — compact and clean

**Backlink count tracking:** Every time a post/fact is linked to, its `backlinkCount` is incremented in Cosmos DB. This ensures link distribution is fair — rarely-linked content gets boosted over time.

**Pipeline flow:**
1. Generate content → 2. Generate SEO + images → **3. Find backlinks (Cosmos DB keyword search)** → 4. Draft → 5. Upload images → 6. Format HTML with backlinks → 7. Publish → **8. Store keywords + increment backlink counts**

**Lesson:** Internal linking is one of the highest-ROI SEO strategies for content sites. Automating it via keyword matching + weighted selection ensures consistent cross-linking without manual effort.

**Key files:**
- `src/CarFacts.Functions/Functions/Activities/FindBacklinksActivity.cs`
- `src/CarFacts.Functions/Services/CosmosFactKeywordStore.cs`
- `src/CarFacts.Functions/Services/ContentFormatterService.cs`

---

### 7.4 Per-Fact Keyword Tagging for Cross-Referencing

**Problem:** No machine-readable metadata existed per fact to enable automated linking, search, or categorization.

**Fix:** The SEO prompt generates 5–8 lowercase keyword tags per fact (brand, model, category, technology, era). These are stored in Cosmos DB alongside the fact's deep link URL.

**Cosmos DB record structure:**
```json
{
  "id": "2026-04-10_ford-model-a-v8-1925",
  "postUrl": "https://carfactsdaily.com/2026/04/10/...",
  "factUrl": "https://carfactsdaily.com/2026/04/10/...#ford-model-a-v8-1925",
  "keywords": ["ford", "model-a", "v8", "engine", "american", "sedan"],
  "postTitle": "Ford Model A: The Birth of the Affordable Car...",
  "imageUrl": "https://carfactsdaily.com/wp-content/.../car-fact-1925-0.png",
  "backlinkCount": 2
}
```

**Keyword search:** Uses `ARRAY_CONTAINS` queries in Cosmos DB to find facts sharing keywords with the current post's facts.

**Lesson:** Storing per-content-unit keyword metadata enables powerful automated features (backlinks, related content, search). Design the schema upfront to support future use cases — we later added `postTitle`, `imageUrl`, and `backlinkCount` to the same records.

**File:** `src/CarFacts.Functions/Models/FactKeywordRecord.cs`

---

### 7.5 GEO Optimization for AI Search Engines

**Problem:** AI search engines (ChatGPT, Perplexity, Claude) parse content differently than Google. Standard SEO meta tags may not be sufficient.

**Fix:** Each post includes:
- A `<!-- GEO Summary -->` HTML comment with a 2–3 sentence summary optimized for AI retrieval
- Schema.org microdata (`itemprop`, `itemscope`) for structured data (Article, NewsArticle, ImageObject)
- Note: `<script>` tags (JSON-LD) are stripped by WordPress.com, so microdata is used instead

**Lesson:** WordPress.com free tier strips `<script>` tags, making JSON-LD structured data impossible. Use microdata (`itemprop`/`itemscope`) as a fallback. GEO (Generative Engine Optimization) is an emerging field — adding AI-friendly summaries is low-effort and future-proofs content.

**File:** `src/CarFacts.Functions/Services/ContentFormatterService.cs` (AppendGeoHeader method)

---

## 8. Azure App Configuration Pitfalls

### 8.1 App Configuration Provider Can Silently Fail

**Failure:** Blog posts stopped publishing on April 13–14. `CreateDraftPostActivity` returned `404 (Not Found)` from WordPress API. Content generation worked fine.

**Root cause:** `WordPress:SiteId` was stored *only* in Azure App Configuration. The App Configuration provider (`AddAzureAppConfiguration`) connected via `DefaultAzureCredential` and loaded settings at startup — but if the connection fails (network issue, transient auth failure, cold start timing), it **fails silently**. All `IOptions<T>` values revert to their C# defaults (empty strings, false, etc.). The WordPress API URL became `https://public-api.wordpress.com/rest/v1.1/sites//posts/new` → 404.

**Fix:** Add all critical settings as Function App environment variables (`WordPress__SiteId`, `WordPress__PostStatus`, etc.) in addition to App Configuration. Environment variables serve as a reliable fallback and are always available.

```bash
az functionapp config appsettings set \
  --name func-carfacts5 --resource-group rg-carfacts \
  --settings "WordPress__SiteId=carfacts5.wordpress.com" \
             "WordPress__PostStatus=publish" \
             "WordPress__SkipImages=false"
```

**Lesson:** Never rely solely on Azure App Configuration for settings that are required for core functionality. Always duplicate critical settings in Function App environment variables. App Configuration is great for feature flags and non-critical overrides, but the startup connection can fail silently. The same issue was seen earlier with `SocialMedia__TwitterEnabled`.

**Pattern:** For any `IOptions<T>` binding, if a property defaulting to empty/false would cause a runtime failure, that property **must** also exist in Function App settings.

---

### 8.2 IOptions Binding Doesn't Fail Loudly

**Failure:** `SocialMedia:TwitterEnabled=true` was set in App Configuration, but `TwitterService.IsEnabled` returned `false`.

**Root cause:** `IOptions<SocialMediaSettings>` snapshot is taken from the configuration providers registered at startup. If the App Configuration provider hasn't fully loaded by the time DI resolves the options, the value comes from environment variables only — where it didn't exist.

**Fix:** Added `SocialMedia__TwitterEnabled=true` as a Function App setting. Both sources now provide the value, whichever loads first wins.

**Lesson:** When using App Configuration with `IOptions<T>`, treat Function App settings as the primary source and App Configuration as a secondary/override layer. Don't assume App Configuration values will be available at the moment DI resolves options.

---

## 9. Twitter/X API Integration

### 9.1 OAuth 1.0a Permission and Enrollment

**Failure:** Multiple 403 errors when posting tweets:
1. `"oauth1 app permissions"` — App only had Read permissions
2. `"client-not-enrolled"` — App wasn't attached to a Project in the developer portal

**Fix (multi-step):**
1. Changed app permissions to "Read and Write" under OAuth 1.0a settings
2. Attached app to a Project in the Twitter developer portal
3. **Regenerated all 4 keys** (Consumer Key, Consumer Secret, Access Token, Access Token Secret) — this is critical after any permission change

**Lesson:** After changing Twitter app permissions, you **must** regenerate all tokens. Old tokens retain the previous permission scope. The "client-not-enrolled" error means the app isn't inside a Project — a requirement for API v2 access.

---

### 9.2 Four Keys Must Be From the Same Regeneration

**Failure:** 401 Unauthorized after updating individual keys at different times.

**Root cause:** Twitter OAuth 1.0a requires all 4 keys (Consumer Key, Consumer Secret, Access Token, Access Token Secret) to form a valid set. If you regenerate the consumer key but keep old access tokens, the signature won't validate.

**Fix:** Always regenerate all 4 keys together from the same app page, and update all 4 in Key Vault simultaneously.

**Lesson:** Twitter API keys are a **set** — partial updates break authentication. When rotating credentials, update all 4 atomically.

---

## 10. JSON Serialization Gotchas

### 10.1 System.Text.Json Is Case-Sensitive by Default

**Failure:** `GenerateTweetFactsActivity` returned empty arrays (`[]`). The Durable Functions history showed "Redacted 2 characters" for the activity output.

**Root cause:** `System.Text.Json.JsonSerializer.Deserialize<T>()` is **case-sensitive by default**. The LLM returns JSON with lowercase keys (`"text"`, `"hashtags"`, `"tweets"`), but the C# model properties use PascalCase (`Text`, `Hashtags`, `Tweets`). Every property deserialized to its default value (empty string, empty list).

**Fix:** Added `PropertyNameCaseInsensitive = true` to `JsonSerializerOptions`:

```csharp
var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var parsed = JsonSerializer.Deserialize<TweetFactsResponse>(cleaned, options);
```

**Alternative:** Use `[JsonPropertyName("text")]` attributes on each property, but `PropertyNameCaseInsensitive` is simpler when you don't control the JSON producer (LLM output).

**Lesson:** When deserializing JSON from external sources (especially LLMs), always use `PropertyNameCaseInsensitive = true`. Unlike Newtonsoft.Json, System.Text.Json does NOT do case-insensitive matching by default. This is one of the most common migration pitfalls.

**Files:** `GenerateTweetFactsActivity.cs`, `GenerateTweetLinkActivity.cs`

---

## 11. Prompt Engineering for Social Media

### 11.1 Structure Beats Tone Direction

**Problem:** Initial tweet prompts told the LLM to write "like a real person casually sharing something cool" with "TIL energy." The output was inconsistent — some tweets were great, others were generic filler.

**Fix:** Replaced vague tone directions with an explicit 3-step structure:
1. **Hook** — Start with a bold claim, surprising number, or unexpected detail
2. **Fact** — Present it concisely and punchily
3. **Context** — One sentence explaining why it matters

Also: told the LLM to avoid hashtags and emojis *unless they genuinely add value* (instead of mandating 2–3 hashtags per tweet).

**Result:** More consistent, higher-quality output. Every tweet follows the same rhythm, and the "why it matters" line adds depth that makes posts more shareable.

**Lesson:** When prompting LLMs for structured content, give explicit structure (numbered steps) rather than just describing a tone. "Write like X" is ambiguous; "Start with Y, then Z, then W" is concrete. Also, telling the LLM to use something "only when it adds value" produces better results than either mandating or prohibiting it.

**Files:** `TweetFactsSystemPrompt.txt`, `TweetFactsUserPrompt.txt`
