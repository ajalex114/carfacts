<!-- deepfry:commit=b9be8dc1501e31ea9edfa99c938527818fa2aca5 agent=connection-analyzer timestamp=2025-07-15T21:45:00Z -->

# Connection Analysis

## Connection Inventory

| # | Dependency | Client Type | Lifetime | Creation Point | File |
|---|-----------|-------------|----------|----------------|------|
| 1 | WordPress.com API | `HttpClient` (via `IHttpClientFactory`) | Transient (factory-managed) ✅ | `AddHttpClient<IWordPressService, WordPressService>()` | `Program.cs:102` |
| 2 | Twitter/X API | `HttpClient` (via `IHttpClientFactory`) | Transient (factory-managed) ✅ | `AddHttpClient<TwitterService>()` | `Program.cs:105` |
| 3 | Facebook Graph API | `HttpClient` (via `IHttpClientFactory`) | Transient (factory-managed) ✅ | `AddHttpClient<FacebookService>()` | `Program.cs:108` |
| 4 | Reddit API | `HttpClient` (via `IHttpClientFactory`) | Transient (factory-managed) ✅ | `AddHttpClient<RedditService>()` | `Program.cs:110` |
| 5 | Pinterest API | `HttpClient` (via `IHttpClientFactory`) | Transient (factory-managed) ✅ | `AddHttpClient<PinterestService>()` | `Program.cs:115` |
| 6 | Stability AI API | `HttpClient` (via `IHttpClientFactory`) | Transient (factory-managed) ✅ | `AddHttpClient<ImageGenerationService>()` | `Program.cs:191, 199` |
| 7 | Together AI API | `HttpClient` (via `IHttpClientFactory`) | Transient (factory-managed) ✅ | `AddHttpClient<TogetherAIImageGenerationService>()` | `Program.cs:179, 200` |
| 8 | Azure Cosmos DB | `CosmosClient` | Singleton ✅ | `new CosmosClient(connectionString, ...)` | `Program.cs:239` |
| 9 | Azure Key Vault (runtime) | `SecretClient` | Singleton ✅ | `new SecretClient(vaultUri, new DefaultAzureCredential())` | `KeyVaultSecretProvider.cs:19` |
| 10 | Azure Key Vault (startup — text) | `SecretClient` | Startup-only (not registered) | `new SecretClient(new Uri(vaultUri), new DefaultAzureCredential())` | `Program.cs:136-137` |
| 11 | Azure Key Vault (startup — Cosmos) | `SecretClient` | Startup-only (not registered) | `new SecretClient(new Uri(vaultUri), new DefaultAzureCredential())` | `Program.cs:225-226` |
| 12 | Azure App Configuration | SDK-managed | Startup-only | `options.Connect(new Uri(appConfigEndpoint), new DefaultAzureCredential())` | `Program.cs:38` |
| 13 | Azure OpenAI / OpenAI | `IChatCompletionService` (Semantic Kernel) | Singleton ✅ | `kernelBuilder.AddAzureOpenAIChatCompletion(...)` | `Program.cs:148-155` |
| 14 | Application Insights | SDK-managed | Singleton ✅ | `AddApplicationInsightsTelemetryWorkerService()` | `Program.cs:47` |

## Connection Lifecycle Issues

### ⚠️ Singleton services wrapping factory-scoped HttpClients (Captive Dependency)

- **Location**: `Program.cs:105-116`
- **Pattern**:
  ```csharp
  services.AddHttpClient<TwitterService>();                                     // line 105
  services.AddSingleton<ISocialMediaService>(sp => sp.GetRequiredService<TwitterService>());  // line 106
  services.AddSingleton<ITwitterService>(sp => sp.GetRequiredService<TwitterService>());      // line 107
  ```
  Same pattern repeated for `FacebookService` (108-109), `RedditService` (110-111), and `PinterestService` (115-116).
- **Problem**: `AddHttpClient<T>()` registers `T` as **Transient** by default. The subsequent `AddSingleton` forwarding resolves the transient service once and holds it forever. Since `HttpClient` is injected via constructor, the underlying `HttpMessageHandler` is captured as a singleton and **never rotated** by `IHttpClientFactory`. This defeats the factory's DNS rotation and handler pooling benefits.
- **Impact**: In a long-lived Azure Functions host, DNS changes for Twitter/Facebook/Reddit/Pinterest APIs are never picked up, potentially causing connection failures after IP changes. The `HttpClient` itself won't leak sockets (the factory still created it), but handler rotation is frozen.
- **Severity**: Low-to-medium in practice — these are stable, well-provisioned API endpoints that rarely change IP. But it is architecturally incorrect.
- **Fix**: Either remove the singleton forwarding and resolve `ISocialMediaService` from a factory/enumerable, or accept the trade-off given stable endpoints. The cleanest fix:
  ```csharp
  // Option A: Let the typed clients stay transient, resolve ISocialMediaService dynamically
  services.AddHttpClient<TwitterService>();
  services.AddTransient<ISocialMediaService>(sp => sp.GetRequiredService<TwitterService>());
  services.AddTransient<ITwitterService>(sp => sp.GetRequiredService<TwitterService>());
  ```

### ⚠️ Duplicate SecretClient creation at startup (not reused)

- **Location**: `Program.cs:136-137` and `Program.cs:225-226`
- **Problem**: Two separate `SecretClient` instances are created during startup — one in `RegisterTextProvider()` to fetch `AzureOpenAI-ApiKey`, and another in `RegisterCosmosDb()` to fetch `CosmosDb-ConnectionString`. Both use the same vault URI and credential but are created independently. Meanwhile, a third long-lived `SecretClient` is created in `KeyVaultSecretProvider` (line 19) for runtime use.
- **Impact**: Minor — these are startup-only allocations and don't cause connection leaks. `SecretClient` is thread-safe and designed for reuse, so creating duplicates wastes a small amount of initialization overhead and auth token negotiation.
- **Fix**: Create a shared `SecretClient` once at startup and pass it to both methods:
  ```csharp
  SecretClient? startupSecretClient = null;
  if (!isLocal)
  {
      var vaultUri = config["KeyVault:VaultUri"] ?? "";
      startupSecretClient = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
  }
  RegisterTextProvider(config, services, isLocal, startupSecretClient);
  RegisterCosmosDb(config, services, isLocal, startupSecretClient);
  ```

### ✅ No `new HttpClient()` anti-pattern detected

All 7 HTTP-based services receive their `HttpClient` via constructor injection from `IHttpClientFactory` (`AddHttpClient<T>()`). No manual `new HttpClient()` calls exist anywhere in the codebase. This correctly avoids socket exhaustion.

### ✅ CosmosClient is a proper singleton

- **Location**: `Program.cs:239-246`
- **Assessment**: `CosmosClient` is created once with `CosmosClientOptions` and registered as `AddSingleton(...)`. Both `CosmosFactKeywordStore` and `CosmosSocialMediaQueueStore` receive it via DI and are also singletons. The `CosmosClient` instance is designed to be a singleton — this is the recommended pattern from Microsoft.
- **Serialization configured**: `CamelCase` property naming policy is properly set.

### ✅ SecretClient (runtime) is correctly singleton-scoped

- **Location**: `KeyVaultSecretProvider.cs:19`
- **Assessment**: `KeyVaultSecretProvider` is registered as `AddSingleton`, and it creates a single `SecretClient` in its constructor. `SecretClient` is thread-safe and designed for reuse as a singleton. The `DefaultAzureCredential` handles token caching internally.

### ✅ Semantic Kernel / Chat Completion service is correctly singleton-scoped

- **Location**: `Program.cs:159-163`
- **Assessment**: The `Kernel` is built once and registered as singleton. The `IChatCompletionService` extracted from it is also registered as singleton. Both `ContentGenerationService` and `SeoGenerationService` consume it as singletons. This correctly avoids re-establishing Azure OpenAI connections per call.

## Resilience Patterns

| Dependency | Retry | Circuit Breaker | Timeout | Rate-Limit Handling |
|-----------|-------|----------------|---------|---------------------|
| Azure OpenAI (LLM) | ✅ Durable Functions RetryPolicy (3 attempts, 5s backoff) | ❌ | ❌ Not configured | ❌ |
| Stability AI (images) | ✅ Custom retry loop (3 attempts, exponential backoff) + Durable Functions RetryPolicy (3 attempts, 10s) | ❌ | ❌ Not configured | ✅ HTTP 429 detection with exponential backoff (`ImageGenerationService.cs:108-114`) |
| Together AI (images) | ✅ Durable Functions RetryPolicy (via orchestrator) | ❌ | ❌ Not configured | ❌ |
| WordPress.com API | ✅ Durable Functions RetryPolicy (3 attempts, 3s backoff) | ❌ | ❌ Not configured | ❌ |
| Twitter/X API | ✅ Durable Functions RetryPolicy (2 attempts, 5s backoff) | ❌ | ❌ Not configured | ❌ |
| Facebook Graph API | ✅ Durable Functions RetryPolicy (via orchestrator) | ❌ | ❌ Not configured | ❌ |
| Reddit API | ✅ Durable Functions RetryPolicy (via orchestrator) | ❌ | ❌ Not configured | ❌ |
| Pinterest API | ✅ Durable Functions RetryPolicy (2 attempts, 10s backoff) | ❌ | ❌ Not configured | ❌ |
| Azure Cosmos DB | ✅ Built-in SDK retry (Cosmos SDK default) + Durable Functions RetryPolicy | ❌ | ✅ Built-in SDK defaults | ✅ Built-in (429 auto-retry) |
| Azure Key Vault | ✅ Built-in SDK retry (Azure SDK default) | ❌ | ✅ Built-in SDK defaults | ✅ Built-in (429 auto-retry) |
| Azure App Configuration | ✅ Built-in SDK retry | ❌ | ✅ Built-in SDK defaults | ✅ Built-in |

### Resilience Assessment

**Strengths:**
- The Durable Functions orchestrator layer provides a solid retry envelope around every external call. Different retry policies are tuned per dependency type (LLM = 3×5s, Images = 3×10s, WordPress = 3×3s, Social = 2×5s).
- `ImageGenerationService` has manual rate-limit (HTTP 429) handling with exponential backoff (`Program.cs:108-114`) — this is important since image APIs aggressively rate-limit.
- Best-effort patterns: social media, keyword storage, and web stories are wrapped in try/catch at the orchestrator level so failures don't block the main pipeline.
- Azure SDKs (Cosmos, Key Vault, App Configuration) have built-in retry and rate-limit handling.

**Gaps:**
- No circuit breaker on any dependency. If an external API (e.g., Stability AI) is down, the system will still attempt all retries on every trigger invocation. This is acceptable for a daily-scheduled function but could be an issue if trigger frequency increases.
- No explicit `HttpClient.Timeout` configuration on any of the 7 `AddHttpClient<T>()` registrations — all rely on the default 100-second timeout. Image generation calls can be slow; consider setting explicit timeouts.
- No Polly policies configured on `IHttpClientFactory`. All retry logic is at the Durable Functions level (coarse-grained activity retries, not HTTP-level).

## Connection Configuration

| Dependency | Pooling | Pool Size | Keep-Alive | Notes |
|-----------|---------|-----------|------------|-------|
| HttpClient (all 7 services) | ✅ IHttpClientFactory handler pooling | Default (no custom) | Default (SDK default) | Handler lifetime = default 2 min rotation |
| Cosmos DB | ✅ Built-in TCP connection pool | Default | ✅ Built-in | `CosmosClientOptions` only configures serialization, no pool tuning |
| Key Vault | ✅ Built-in HTTP pipeline | Default | Default | Single instance, thread-safe |
| Azure OpenAI (Semantic Kernel) | ✅ Internal HttpClient (SK-managed) | Default | Default | SK manages its own HTTP connections internally |

## Disposal & Leak Analysis

| Resource | Disposal Pattern | Status |
|----------|-----------------|--------|
| `HttpRequestMessage` | `using var request = new HttpRequestMessage(...)` | ✅ All 20+ usages properly disposed |
| `HttpResponseMessage` | `using var response = await _httpClient.SendAsync(...)` | ✅ All usages properly disposed |
| `JsonDocument` | `using var doc = JsonDocument.Parse(...)` | ✅ All usages properly disposed |
| `HMACSHA1` | `using var hmac = new HMACSHA1(...)` | ✅ Disposed (`TwitterService.cs:321`) |
| `MultipartFormDataContent` | `using var content = new MultipartFormDataContent()` | ✅ Disposed (`WordPressService.cs:256`) |
| `FeedIterator<T>` (Cosmos) | `using var iterator = _container.GetItemQueryIterator(...)` | ✅ All 7 usages properly disposed |
| `CosmosClient` | Singleton — lives for process lifetime | ✅ Correct (Functions host manages lifecycle) |
| `Stream` / `StreamReader` | `using var stream = ...`, `using var reader = ...` | ✅ Disposed (`PromptLoader.cs:61-64`) |

**No resource leaks detected.** Every disposable resource uses `using var` declarations.

## Summary

| Metric | Count |
|--------|-------|
| **Total external connections** | 14 |
| **Well-managed** | 12 ✅ |
| **Needs improvement** | 2 ⚠️ |
| **Critical issues** | 0 ❌ |

This codebase demonstrates strong connection management practices:

- **All HTTP clients** use `IHttpClientFactory` via `AddHttpClient<T>()` — zero instances of `new HttpClient()`.
- **Cosmos DB** client is correctly a singleton with proper serialization options.
- **Azure Key Vault** runtime client is correctly a singleton with `DefaultAzureCredential`.
- **Every disposable resource** (`HttpRequestMessage`, `HttpResponseMessage`, `JsonDocument`, `FeedIterator`, `HMACSHA1`, etc.) is properly disposed via `using var` declarations.
- **Retry logic** is comprehensive — Durable Functions orchestrator RetryPolicies cover every activity, plus manual rate-limit handling for image APIs.

The two ⚠️ findings are low-severity architectural observations, not runtime correctness issues.

## Recommendations

### 1. Fix captive dependency (Singleton capturing Transient HttpClient-backed services)

**Priority:** Low | **Effort:** Small

The `AddSingleton` forwarding for social media services freezes `HttpMessageHandler` rotation. Switch to transient forwarding:

```csharp
// Before (current)
services.AddHttpClient<TwitterService>();
services.AddSingleton<ISocialMediaService>(sp => sp.GetRequiredService<TwitterService>());

// After (recommended)
services.AddHttpClient<TwitterService>();
services.AddTransient<ISocialMediaService>(sp => sp.GetRequiredService<TwitterService>());
```

> **Note:** This also applies to `FacebookService`, `RedditService`, and `PinterestService` registrations. Downstream consumers (`SocialMediaPublisher`) should already handle this correctly since `IEnumerable<ISocialMediaService>` is resolved once at its own construction time.

### 2. Consolidate startup SecretClient instances

**Priority:** Low | **Effort:** Minimal

```csharp
static void RegisterServices(HostBuilderContext context, IServiceCollection services)
{
    var config = context.Configuration;
    var isLocal = /* ... */;

    // Create shared startup SecretClient
    SecretClient? startupVault = null;
    if (!isLocal)
    {
        var vaultUri = config["KeyVault:VaultUri"] ?? "";
        startupVault = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
    }

    RegisterTextProvider(config, services, isLocal, startupVault);
    RegisterCosmosDb(config, services, isLocal, startupVault);
    // ...
}
```

### 3. Configure explicit HttpClient timeouts for slow APIs

**Priority:** Medium | **Effort:** Small

Image generation APIs can take 30-60 seconds. Add explicit timeout configuration:

```csharp
services.AddHttpClient<ImageGenerationService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(90);
});

services.AddHttpClient<TogetherAIImageGenerationService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(90);
});
```

### 4. Consider adding transient fault handling policies via Polly (optional)

**Priority:** Low | **Effort:** Medium

While Durable Functions retry provides coarse-grained resilience, adding Polly policies to `IHttpClientFactory` would give fine-grained HTTP-level retry with jitter:

```csharp
services.AddHttpClient<WordPressService>()
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));
```

This is optional given the existing Durable Functions retry coverage, but would help with transient network blips that don't warrant a full activity retry.
