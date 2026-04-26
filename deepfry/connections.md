<!-- deepfry:commit=5360e5707b59a6cf919f9c880a227006d8f33b09 agent=connection-analyzer timestamp=2025-07-16T00:00:00Z -->

# Connection Analysis — CarFacts.VideoFunction

> **Scope:** `src/CarFacts.VideoFunction/` — the Durable Functions–based video generation pipeline.
> Previous analysis (commit `b9be8dc`) covered `src/CarFacts.Functions` (social media publishing).

---

## Connection Inventory

| # | Dependency | Client Type | Lifetime | Creation Point | File |
|---|-----------|-------------|----------|----------------|------|
| 1 | YouTube Data API v3 | `HttpClient` (static field) | Process-lifetime static ⚠️ | `private static readonly HttpClient Http = new()` | `YouTubeVideoService.cs:23` |
| 2 | Pexels Videos API | `HttpClient` (static field) | Process-lifetime static ⚠️ | `private static readonly HttpClient Http = new()` | `PexelsVideoService.cs:17` |
| 3 | Pexels Videos API (duplicate) | `HttpClient` (static field) | Process-lifetime static ⚠️ | `private static readonly HttpClient Http = new()` | `FetchClipActivity.cs:25` |
| 4 | Blob SAS URL download | `HttpClient` (static field) | Process-lifetime static ⚠️ | `private static readonly System.Net.Http.HttpClient Http = new()` | `RenderVideoActivity.cs:106` |
| 5 | Azure Blob (ffmpeg.exe download) | `BlobClient` | One-time (guarded by semaphore) ✅ | `new BlobClient(storageConnectionString, ...)` | `FfmpegManager.cs:39` |
| 6 | Azure Blob (yt-dlp.exe download) | `BlobClient` | One-time (guarded by semaphore) ✅ | `new BlobClient(storageConnectionString, ...)` | `YtDlpManager.cs:43` |
| 7 | Azure Blob (cookies.txt download) | `BlobClient` | One-time (guarded by semaphore) ✅ | `new BlobClient(storageConnectionString, "youtube-cookies.txt")` | `YtDlpManager.cs:88` |
| 8 | Azure Blob (clip upload) | `BlobContainerClient` | Per-activity-call ⚠️ | `new BlobContainerClient(connectionString, parts[0])` | `FetchClipActivity.cs:221` |
| 9 | Azure Blob (audio upload) | `BlobContainerClient` | Per-activity-call ⚠️ | `new BlobContainerClient(connectionString, parts[0])` | `SynthesizeTtsActivity.cs:59` |
| 10 | Azure Blob (video upload) | `BlobContainerClient` | Per-activity-call ⚠️ | `new BlobContainerClient(connectionString, "poc-videos")` | `RenderVideoActivity.cs:120` |
| 11 | Azure Computer Vision | `ImageAnalysisClient` | Per-thumbnail-call ❌ | `new ImageAnalysisClient(new Uri(endpoint), ...)` | `ComputerVisionService.cs:43` |
| 12 | Azure Cognitive Speech | `SpeechSynthesizer` | Per-TTS-call ⚠️ | `new SpeechSynthesizer(config, audioConfig)` | `TtsService.cs:15` |
| 13 | Application Insights | SDK-managed | Singleton ✅ | `AddApplicationInsightsTelemetryWorkerService()` | `Program.cs:12` |

---

## DI Registration Audit

| Service | Registered | Lifetime | Notes |
|---------|-----------|----------|-------|
| `FfmpegManager` | ✅ Yes | Singleton | `Program.cs:16` |
| `YtDlpManager` | ✅ Yes | Singleton | `Program.cs:21` |
| `TtsService` | ✅ Yes | Singleton | `Program.cs:24` |
| `SubtitleGenerator` | ✅ Yes | Singleton | `Program.cs:29` |
| `YouTubeVideoService` | ❌ No | Transient (manual `new`) | Instantiated inside `FetchClipActivity.Run()` at line 52 |
| `ComputerVisionService` | ❌ No | Transient (manual `new`) | Instantiated inside `FetchClipActivity.Run()` at line 51 |
| `VideoGenerator` | ❌ No | Transient (manual `new`) | Instantiated inside `RenderVideoActivity.Run()` at line 86 — benign (no network connections) |
| `VideoStorageService` | ❌ No | N/A | Exists in codebase but appears entirely unused by any activity |
| `PexelsVideoService` | ❌ No | N/A | Exists in codebase but appears entirely unused; Pexels logic duplicated in `FetchClipActivity` |

---

## Connection Lifecycle Issues

### ❌ `ImageAnalysisClient` recreated on every thumbnail check

- **Location**: `ComputerVisionService.cs:43–45`
- **Code**:
  ```csharp
  var client = new ImageAnalysisClient(
      new Uri(endpoint),
      new AzureKeyCredential(apiKey));
  ```
- **Call frequency**: `AnalyzeThumbnailAsync` is called in a loop of up to 5 iterations inside `YouTubeVideoService.FindBestCandidateAsync`. With fan-out across N parallel `FetchClipActivity` runs, this can produce N×5 new client instances simultaneously.
- **Problem**: `ImageAnalysisClient` (Azure SDK) wraps an HTTP pipeline with connection pooling, TLS state, and auth context. Recreating it per call allocates a new `HttpClient` under the hood each time, bypassing any connection reuse the SDK would otherwise provide. Microsoft's Azure SDK guidance explicitly states these clients should be created once and shared.
- **Impact**: Elevated memory pressure and TCP connection churn in the fan-out phase. On a Consumption plan with 5-segment fan-out: up to 25 `ImageAnalysisClient` instances, each with its own HTTP stack, created and torn down within a single orchestration.
- **Fix**: Hoist the client to a field on `ComputerVisionService`, which is already created per activity run:
  ```csharp
  public class ComputerVisionService(string endpoint, string apiKey)
  {
      // Create once per ComputerVisionService instance (one per FetchClipActivity run)
      private readonly ImageAnalysisClient _client =
          new(new Uri(endpoint), new AzureKeyCredential(apiKey));

      public async Task<ThumbnailAnalysis> AnalyzeThumbnailAsync(string videoId)
      {
          // Use _client instead of local new ImageAnalysisClient(...)
          var result = await _client.AnalyzeAsync(...);
          ...
      }
  }
  ```
  For even better reuse, register `ComputerVisionService` as a Singleton in DI (it is currently newed up inside `FetchClipActivity.Run()`).

---

### ⚠️ Four raw `new HttpClient()` instances bypassing `IHttpClientFactory`

- **Locations**:
  - `YouTubeVideoService.cs:23` — YouTube Data API and thumbnail URL fetches
  - `PexelsVideoService.cs:17` — Pexels search and CDN video downloads
  - `FetchClipActivity.cs:25` — Pexels search and CDN video downloads (duplicates `PexelsVideoService.Http`)
  - `RenderVideoActivity.cs:106` — Blob SAS URL downloads (clips + audio)
- **What's correct**: All four are `static readonly`, which prevents socket exhaustion. This is the critical rule, and it is followed correctly here.
- **What's suboptimal**:
  1. **No DNS refresh**: A static `HttpClient` captures DNS entries at construction time. Long-lived Functions host instances will not pick up DNS changes on CDN or API endpoints (Pexels CDN, YouTube API) without a restart.
  2. **No configured timeout**: Default `HttpClient.Timeout` is 100 seconds. A stalled Pexels CDN download or slow YouTube API could hold an activity thread for the full 100 s with no control.
  3. **No retry / resilience policies**: No Polly or built-in retry on any of the four clients.
  4. **Not participating in `IHttpClientFactory` ecosystem**: The Azure Functions host's `IHttpClientFactory` provides handler lifecycle management (2-minute rotation by default) and enables centralized middleware (logging, retry, auth). All four clients opt out of this.
- **Impact**: Medium. No socket exhaustion risk (static field). But DNS staleness, unconfigured timeouts, and absent retry make the pipeline brittle against transient failures in Pexels CDN, YouTube API, or blob SAS URL fetches.
- **Fix**: Migrate to `IHttpClientFactory`. Since `YouTubeVideoService` and `PexelsVideoService` are currently newed up inside activity `Run()` methods rather than DI-injected, the minimal fix is to inject `IHttpClientFactory` into the activity and pass the resolved `HttpClient` into the service constructor:
  ```csharp
  // Program.cs — add named clients
  services.AddHttpClient("youtube", c => { c.Timeout = TimeSpan.FromSeconds(30); });
  services.AddHttpClient("pexels",  c => { c.Timeout = TimeSpan.FromSeconds(120); }); // CDN download
  services.AddHttpClient("blob",    c => { c.Timeout = TimeSpan.FromSeconds(120); }); // SAS downloads

  // FetchClipActivity — constructor inject IHttpClientFactory
  public class FetchClipActivity(
      FfmpegManager ffmpegManager,
      YtDlpManager ytDlpManager,
      IHttpClientFactory httpClientFactory,
      ILogger<FetchClipActivity> logger)
  {
      // Pass httpClientFactory.CreateClient("youtube") / "pexels" into service constructors
  }
  ```

---

### ⚠️ `BlobContainerClient` created per activity call (not reused)

- **Locations**:
  - `FetchClipActivity.cs:221` — `UploadClipAsync` creates a new `BlobContainerClient` for every clip upload
  - `SynthesizeTtsActivity.cs:59` — `UploadToBlobAsync` creates a new `BlobContainerClient` per audio upload
  - `RenderVideoActivity.cs:120` — `UploadVideoAsync` creates a new `BlobContainerClient` per video upload
- **Pattern**:
  ```csharp
  // Called once per activity invocation — new client each time
  var container = new BlobContainerClient(connectionString, parts[0]);
  await container.CreateIfNotExistsAsync(PublicAccessType.None);
  ```
- **Problem**: `BlobContainerClient` creates its own internal `HttpClient` (via `Azure.Core.Pipeline`). Each `new BlobContainerClient(...)` allocates a new HTTP pipeline. While not catastrophic (one client per activity run), it means connection pooling is scoped to a single upload call rather than being shared across the process lifetime.
- **Context**: `FetchClipActivity` runs in fan-out — all N clip uploads happen concurrently, each creating its own `BlobContainerClient`. `CreateIfNotExistsAsync` also makes a redundant existence-check call on every run.
- **Impact**: Low-to-medium. On Consumption plan with tight memory, repeated HTTP pipeline allocation per activity adds up. `CreateIfNotExistsAsync` generates unnecessary round-trips after the first call.
- **Fix**: Register a `BlobServiceClient` (or `BlobContainerClient`) as a Singleton in DI and inject it into activities:
  ```csharp
  // Program.cs
  services.AddSingleton(sp =>
      new BlobServiceClient(cfg["Storage:ConnectionString"]));

  // Activities — inject BlobServiceClient, call GetBlobContainerClient() as needed
  // CreateIfNotExistsAsync can be called once at startup or guarded with a static flag
  ```

---

### ⚠️ `SpeechSynthesizer` (and `SpeechConfig`) recreated per TTS invocation

- **Location**: `TtsService.cs:11–15`
- **Code**:
  ```csharp
  var config = SpeechConfig.FromSubscription(subscriptionKey, region);
  config.SpeechSynthesisVoiceName = voiceName;
  using var audioConfig = AudioConfig.FromWavFileOutput(outputWavPath);
  using var synthesizer = new SpeechSynthesizer(config, audioConfig);
  ```
- **Context**: `TtsService` is registered as a Singleton in DI and `SynthesizeAsync` is called once per job (inside `SynthesizeTtsActivity`). So in practice this executes at most once per orchestration run.
- **Problem**: `SpeechConfig.FromSubscription` creates a new internal connection context each call. `SpeechSynthesizer` establishes a WebSocket connection to the Speech service on construction. Disposing after each call tears this down, incurring re-connection overhead on the next call.
- **Mitigation**: The `AudioConfig.FromWavFileOutput` binding is file-path-specific, making the `SpeechSynthesizer` inherently per-call. The `using` disposal is correct and required here. However, `SpeechConfig` is stateless and immutable after construction — it can be safely stored as a readonly field:
  ```csharp
  public class TtsService(string subscriptionKey, string region, string voiceName = "en-US-AndrewNeural")
  {
      private readonly SpeechConfig _speechConfig = SpeechConfig.FromSubscription(subscriptionKey, region);
      // Set VoiceName once:
      // _speechConfig.SpeechSynthesisVoiceName = voiceName;
  }
  ```
- **Impact**: Minor — called once per job. Noted for completeness.

---

### ⚠️ `PexelsVideoService` exists but is entirely unused — duplicate `HttpClient` allocated

- **Location**: `PexelsVideoService.cs:17`
- **Problem**: `PexelsVideoService` declares `private static readonly HttpClient Http = new()` and `private static readonly SemaphoreSlim DownloadSem = new(2)`. Neither `PexelsVideoService` nor its `HttpClient` is used anywhere in the activity pipeline. `FetchClipActivity` reimplements Pexels search inline via its own `SearchPexelsAsync` static method (lines 123–148), which also uses its own separate static `Http` (line 25).
- **Result**: Two `static readonly HttpClient` instances allocated for Pexels — one on `PexelsVideoService`, one on `FetchClipActivity` — while only the latter is actually used. `PexelsVideoService.DownloadSem` (the 2-concurrent-download throttle) is also never engaged in the real execution path.
- **Impact**: Wasted static allocations; the documented disk-budget throttling (SemaphoreSlim with 2 permits) is silently bypassed since `FetchClipActivity` does not use `PexelsVideoService`.
- **Fix**: Either delete `PexelsVideoService` and consolidate all Pexels logic in `FetchClipActivity`, or register `PexelsVideoService` in DI and route `FetchClipActivity` through it (restoring the semaphore-based throttle):
  ```csharp
  // Program.cs — register and inject
  services.AddSingleton(sp => new PexelsVideoService(
      cfg["Pexels:ApiKey"] ?? throw new ...,
      /* ffmpegPath resolved at runtime */ "",
      Path.Combine(Path.GetTempPath(), "pexels-cache")));
  ```

---

### ⚠️ `YouTubeVideoService` and `ComputerVisionService` newed up inside activity `Run()` — not DI-managed

- **Location**: `FetchClipActivity.cs:51–52`
- **Code**:
  ```csharp
  var visionSvc  = new ComputerVisionService(input.VisionEndpoint, input.VisionApiKey);
  var youtubeSvc = new YouTubeVideoService(input.YouTubeApiKey, ytDlpPath, visionSvc, cookiesPath, ffmpegPath);
  ```
- **Problem**: Both services are created inside the activity's `Run()` method on every invocation. With fan-out across N clips, N instances of each service are created concurrently. Neither is registered in DI, so the Functions host cannot manage their lifecycle.
- **Context for `YouTubeVideoService`**: Its `static readonly HttpClient Http` means the HTTP client itself is not duplicated — only the service wrapper object is. Acceptable.
- **Context for `ComputerVisionService`**: This compounds with issue #1 above — a new service instance is created per activity run, and inside that instance a new `ImageAnalysisClient` is created per thumbnail call.
- **Fix**: Register both as Singletons in DI and inject via constructor. Pass the API key/endpoint through configuration rather than through the activity input payload:
  ```csharp
  // Program.cs
  services.AddSingleton(sp => new ComputerVisionService(
      cfg["Vision:Endpoint"] ?? "",
      cfg["Vision:ApiKey"]   ?? ""));
  services.AddSingleton<YouTubeVideoService>(); // after ComputerVisionService
  ```

---

### ✅ `BlobClient` in `FfmpegManager` and `YtDlpManager` — correct one-time pattern

- **Location**: `FfmpegManager.cs:27–55`, `YtDlpManager.cs:19–59`
- **Assessment**: Both managers follow the same correct pattern:
  1. Static `SemaphoreSlim` with initial count 1 acts as an async mutex
  2. Double-checked locking (`if (_cachedPath is not null)` before and after lock acquisition)
  3. `BlobClient` is created only if the local file does not exist, then immediately used for a single `DownloadToAsync` call — not held in a field
  4. The downloaded binary path is cached in a `static string? _cachedPath` so warm invocations skip the entire download path
- **This is the correct pattern for a cold-start blob download on Azure Functions Consumption plan.** The `BlobClient` is effectively a one-shot downloader: it lives only for the download call duration.

---

### ✅ Static `HttpClient` fields correctly prevent socket exhaustion

- `YouTubeVideoService.Http`, `PexelsVideoService.Http`, `FetchClipActivity.Http`, and `RenderVideoActivity.Http` are all `private static readonly`. This is the minimal correct pattern — socket exhaustion (the classic `new HttpClient()` per-request bug) does not occur here. ✅

---

### ✅ Temp directory isolation and cleanup

- Each Durable activity creates an isolated temp directory: `clip-{jobId}-{index}`, `tts-{jobId}`, `render-{jobId}`
- All activities clean up in `finally` blocks: `Directory.Delete(tempDir, recursive: true)` — `FetchClipActivity.cs:117`, `SynthesizeTtsActivity.cs:51`, `RenderVideoActivity.cs:102`
- Source temp files from Pexels downloads are deleted in `PexelsVideoService.cs:75` and the `FetchClipActivity` pattern at line 117 via directory cleanup
- No temp file leaks detected ✅

---

### ✅ Stream disposal uses `await using` throughout

Every `FileStream`, `MemoryStream`, and HTTP response stream is wrapped in `await using`:
- `FetchClipActivity.cs:94–96`: `await using (var stream = ...)` + `await using (var file = ...)`
- `SynthesizeTtsActivity.cs:63`: `await using var stream = File.OpenRead(filePath)`
- `RenderVideoActivity.cs:110–113`: `await using var stream = ...` + `await using var file = ...`
- `RenderVideoActivity.cs:124`: `await using var stream = File.OpenRead(filePath)`
- `PexelsVideoService.cs:117–120`: `await using var stream = ...` + `await using var file = ...`

No stream leaks detected ✅

---

### ✅ `Process` resources correctly disposed

All `yt-dlp`, `ffmpeg` subprocess invocations use `using var proc = Process.Start(psi)!`:
- `YouTubeVideoService.cs:210`, `PexelsVideoService.cs:148`, `FetchClipActivity.cs:208`, `VideoGenerator.cs:187`

`StandardError` is always read to end before `WaitForExitAsync()`, preventing deadlocks. ✅

---

## Resilience Patterns

| Dependency | Retry | Circuit Breaker | Timeout | Health Check |
|-----------|-------|----------------|---------|--------------|
| YouTube Data API | ❌ try/catch swallows, no retry | ❌ | ❌ default 100s | ❌ |
| Pexels API (search) | ⚠️ 4-tier fallback query (not HTTP retry) | ❌ | ❌ default 100s | ❌ |
| Pexels CDN (download) | ❌ | ❌ | ❌ default 100s | ❌ |
| Azure Blob (tools download) | ✅ Azure SDK built-in retry | ❌ | ✅ SDK default | ❌ |
| Azure Blob (clip/audio/video upload) | ✅ Azure SDK built-in retry | ❌ | ✅ SDK default | ❌ |
| Azure Computer Vision | ❌ catch returns optimistic fallback | ❌ | ❌ default (SDK) | ❌ |
| Azure Speech (TTS) | ❌ propagates exception to Durable | ❌ | ✅ SDK default | ❌ |
| Blob SAS URL download | ❌ | ❌ | ❌ default 100s | ❌ |

**Key gap**: No HTTP-level retry on YouTube, Pexels, or blob SAS downloads. The Durable Functions orchestrator does **not** wrap individual `FetchClipActivity` calls in a `RetryPolicy` (unlike in `CarFacts.Functions`). A transient 5xx from Pexels or a momentary CDN hiccup will immediately fail the clip fetch and fall through to the next Pexels fallback query — which is a reasonable degradation strategy, but a short HTTP retry before fallback would be more efficient.

---

## Connection Configuration

| Dependency | Internal Pooling | Timeout Configured | Keep-Alive | Notes |
|-----------|--------|---------|------------|-------|
| YouTube API `HttpClient` | ✅ TCP pool (static field) | ❌ default 100s | OS default | No DNS refresh on long-lived host |
| Pexels API `HttpClient` (×2) | ✅ TCP pool (static field) | ❌ default 100s | OS default | CDN downloads can be large; 100s may be tight |
| Blob SAS `HttpClient` | ✅ TCP pool (static field) | ❌ default 100s | OS default | Downloads clips+audio from blob SAS URLs |
| Azure Blob SDK (tools) | ✅ SDK-managed per `BlobClient` instance | ✅ SDK default | ✅ Built-in | One-shot download; client not retained |
| Azure Blob SDK (uploads) | ❌ New pipeline per upload call | ✅ SDK default | ✅ Built-in | One `BlobContainerClient` per activity call |
| Azure Computer Vision SDK | ❌ New pipeline per thumbnail call | ✅ SDK default | ✅ Built-in | See Issue #1 |
| Azure Speech SDK | N/A (WebSocket, per-call) | ✅ SDK default | N/A | File-bound; new connection per call is expected |

---

## Summary

| Metric | Count |
|--------|-------|
| **Total connection creation points** | 13 |
| **Well-managed** | 6 ✅ |
| **Needs improvement** | 6 ⚠️ |
| **Critical issues** | 1 ❌ |

**Critical (❌):**
- `ImageAnalysisClient` recreated per thumbnail check — up to N×5 clients in a fan-out scenario

**Needs improvement (⚠️):**
- 4× `new HttpClient()` without `IHttpClientFactory` (no DNS refresh, no timeout, no retry)
- `BlobContainerClient` created per upload call instead of being a shared singleton
- `SpeechConfig` recreated per TTS call (minor; called once per job)
- `PexelsVideoService` dead code allocating an unused `HttpClient` — throttle semaphore silently bypassed
- `YouTubeVideoService` and `ComputerVisionService` newed up inside activity `Run()` instead of DI-managed

**Well-managed (✅):**
- Static `HttpClient` fields prevent socket exhaustion
- `BlobClient` in `FfmpegManager`/`YtDlpManager`: correct one-time, semaphore-guarded cold-start download pattern
- All DI-registered services are Singletons (FfmpegManager, YtDlpManager, TtsService, SubtitleGenerator)
- Temp directory isolation per activity + `finally`-block cleanup — no file leaks
- `await using` on all streams — no stream leaks
- `using var proc` on all subprocess handles — no process leaks

---

## Recommendations

### 1. Fix `ImageAnalysisClient` — hoist to field on `ComputerVisionService`

**Priority: High** | **Effort: Minimal** | **File: `ComputerVisionService.cs`**

```csharp
public class ComputerVisionService(string endpoint, string apiKey)
{
    // ✅ Create once per service instance, not per thumbnail call
    private readonly ImageAnalysisClient _client =
        new(new Uri(endpoint), new AzureKeyCredential(apiKey));

    public async Task<ThumbnailAnalysis> AnalyzeThumbnailAsync(string videoId)
    {
        try
        {
            var thumbnailUrl = $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg";
            var result = await _client.AnalyzeAsync(   // ← use field, not new()
                new Uri(thumbnailUrl),
                VisualFeatures.Read | VisualFeatures.Tags);
            ...
        }
    }
}
```

Then register it as a Singleton in `Program.cs`:
```csharp
services.AddSingleton(sp => new ComputerVisionService(
    cfg["Vision:Endpoint"] ?? "",
    cfg["Vision:ApiKey"]   ?? ""));
```

---

### 2. Register a shared `BlobServiceClient` in DI for activity uploads

**Priority: Medium** | **Effort: Small** | **Files: `Program.cs`, `FetchClipActivity.cs`, `SynthesizeTtsActivity.cs`, `RenderVideoActivity.cs`**

```csharp
// Program.cs
services.AddSingleton(sp =>
    new BlobServiceClient(
        cfg["Storage:ConnectionString"]
        ?? throw new InvalidOperationException("Storage:ConnectionString not configured")));
```

Activities receive it via constructor injection and call `GetBlobContainerClient(name)` instead of `new BlobContainerClient(...)`. Move `CreateIfNotExistsAsync` to a startup/warm-up step or guard with a `static bool _containerReady` flag.

---

### 3. Migrate static `HttpClient` fields to `IHttpClientFactory` with explicit timeouts

**Priority: Medium** | **Effort: Small** | **File: `Program.cs`, activities, and services**

```csharp
// Program.cs — register named clients with appropriate timeouts
services.AddHttpClient("youtube", c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);  // API search calls should be fast
});
services.AddHttpClient("pexels-search", c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
});
services.AddHttpClient("pexels-download", c =>
{
    c.Timeout = TimeSpan.FromSeconds(120); // CDN video files can be large
});
services.AddHttpClient("blob-sas", c =>
{
    c.Timeout = TimeSpan.FromSeconds(120); // blob SAS downloads
});
```

Inject `IHttpClientFactory` into `FetchClipActivity` and `RenderVideoActivity`. Pass the resolved `HttpClient` into `YouTubeVideoService` and service wrappers via constructor.

---

### 4. Delete or wire up `PexelsVideoService` — eliminate duplicate HttpClient and dead throttle

**Priority: Low** | **Effort: Small** | **File: `PexelsVideoService.cs`, `FetchClipActivity.cs`**

Either:
- **Option A** (simplest): Delete `PexelsVideoService.cs`. Consolidate all Pexels logic in `FetchClipActivity.SearchPexelsAsync`.
- **Option B** (restores throttle): Register `PexelsVideoService` as a Singleton in DI, inject into `FetchClipActivity`, and remove the inline `SearchPexelsAsync` duplication. This also restores the 2-concurrent-download `SemaphoreSlim` throttle that is currently bypassed.

Option B is preferred since the semaphore was clearly intentional (comment: _"stay within the 500 MB temp disk budget"_) and is silently not working in the current code.

---

### 5. Register `YouTubeVideoService` in DI as a Singleton

**Priority: Low** | **Effort: Small** | **File: `Program.cs`, `FetchClipActivity.cs`**

```csharp
// Program.cs — after YtDlpManager and ComputerVisionService
services.AddSingleton<YouTubeVideoService>(sp =>
{
    var ytdlp  = sp.GetRequiredService<YtDlpManager>();
    var vision = sp.GetRequiredService<ComputerVisionService>();
    return new YouTubeVideoService(
        cfg["YouTube:ApiKey"] ?? "",
        /* ytDlpPath resolved at first use via ytdlp.EnsureReadyAsync() */ "",
        vision,
        /* cookiesPath resolved at first use */ null,
        /* ffmpegPath resolved at first use  */ null);
});
```

Note: `ytDlpPath` and `ffmpegPath` are resolved async at runtime. Consider adding an `InitializeAsync` method or lazily resolving via the manager at call time.

---

### 6. Add HTTP-level retry for Pexels and YouTube calls

**Priority: Low** | **Effort: Medium**

The Durable orchestrator has no `RetryPolicy` on `FetchClipActivity`. Short transient retries at the HTTP level (before falling to the next Pexels tier) would improve resilience without the overhead of a full Durable retry:

```csharp
// After migrating to IHttpClientFactory (Recommendation #3):
services.AddHttpClient("pexels-search")
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(2, _ => TimeSpan.FromSeconds(1)));
```
