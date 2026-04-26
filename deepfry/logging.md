<!-- deepfry:commit=5360e5707b59a6cf919f9c880a227006d8f33b09 agent=log-analyzer timestamp=2026-04-26T18:30:41Z -->

# Logging Analysis — CarFacts.VideoFunction

> **Scope**: `src/CarFacts.VideoFunction/` only. This is the Durable Functions video-generation
> pipeline (HTTP start → orchestrator → 4 activities → render → upload).
> App Insights resource: `adf71ab0-69e2-4d63-837a-49c287cd6bad` (`func-poc-vidgen` in `rg-carfacts`).

---

## Framework

| Property | Value |
|----------|-------|
| **Primary library** | `Microsoft.Extensions.Logging` (`ILogger<T>`) — injected into all Functions and Activities |
| **App Insights SDK** | `Microsoft.Azure.Functions.Worker.ApplicationInsights` v1.4.0 (isolated worker model) |
| **Worker service** | `Microsoft.ApplicationInsights.WorkerService` v2.22.0 |
| **Configuration location** | `Program.cs:12-13`, `host.json:3-10` |
| **Sinks** | Console (via Functions Worker) + Application Insights |
| **Sinks (dev vs prod)** | No environment split — same pipeline in both. No Serilog or file sink. |
| **Multiple frameworks** | ❌ No — single ILogger pipeline. However, 7 service classes bypass ILogger entirely with `Console.WriteLine` (see below). |

**`Program.cs` registration** (correct for isolated worker):
```csharp
services.AddApplicationInsightsTelemetryWorkerService();   // line 12
services.ConfigureFunctionsApplicationInsights();           // line 13
```

**`host.json` sampling** (`host.json:4-9`):
```json
"applicationInsights": {
  "samplingSettings": {
    "isEnabled": true,
    "excludedTypes": "Request"
  }
}
```
> ⚠️ Sampling is enabled for **traces** (only Requests are excluded). Under load, individual
> trace log lines can be dropped. There is no `excludedTypes: "Trace"` guard.

---

## ILogger vs Console.WriteLine — Per-Class Breakdown

### Classes using `ILogger<T>` (routed to App Insights) ✅

| Class | File | ILogger injected via |
|-------|------|----------------------|
| `VideoOrchestrator` | `Functions/VideoOrchestrator.cs` | `ctx.CreateReplaySafeLogger<T>()` (correct for Durable replay) |
| `HttpStartFunction` | `Functions/HttpStartFunction.cs` | Constructor injection |
| `GenerateVideoFunction` | `Functions/GenerateVideoFunction.cs` | Constructor injection |
| `StatusFunction` | `Functions/StatusFunction.cs` | Constructor injection |
| `LogsFunction` | `Functions/LogsFunction.cs` | Constructor injection |
| `SynthesizeTtsActivity` | `Activities/SynthesizeTtsActivity.cs` | Constructor injection |
| `PlanSegmentsActivity` | `Activities/PlanSegmentsActivity.cs` | Constructor injection |
| `FetchClipActivity` | `Activities/FetchClipActivity.cs` | Constructor injection |
| `RenderVideoActivity` | `Activities/RenderVideoActivity.cs` | Constructor injection |

### Classes using `Console.WriteLine` (NOT routed to App Insights) ❌

| Class | File | Console.WriteLine calls | What is missed |
|-------|------|------------------------|----------------|
| `YouTubeVideoService` | `Services/YouTubeVideoService.cs` | 7 | No YouTube CC candidate found; candidate selected (videoId + title); watermark detected per-video; no car in thumbnail per-video; YouTube search API exception; yt-dlp exit code + stderr snippet; yt-dlp exception |
| `YtDlpManager` | `Services/YtDlpManager.cs` | 6 | yt-dlp binary download start/size/cache-hit; cookies downloaded; no cookies found; cookies blob check failed |
| `FfmpegManager` | `Services/FfmpegManager.cs` | 3 | ffmpeg binary download start/size/cache-hit |
| `VideoGenerator` | `Services/VideoGenerator.cs` | 4 | Full ffmpeg command line (before each render); entire ffmpeg stderr |
| `SegmentPlanner` | `Services/SegmentPlanner.cs` | 2 | Brand/model detection result; per-segment shot type and query |
| `PexelsVideoService` | `Services/PexelsVideoService.cs` | 1 | Per-segment Pexels failure (all 3 fallback tiers exhausted) |
| `ComputerVisionService` | `Services/ComputerVisionService.cs` | 1 | Computer Vision API failure (optimistic pass-through) |

> **Impact**: The richest operational decisions — YouTube candidate scoring, yt-dlp error codes,
> watermark/car detection per-video, ffmpeg stderr, and segment query planning — are entirely
> invisible in App Insights. `LogsFunction` (which queries App Insights traces) cannot surface
> these events.

---

## Structured Logging

- **Adopted**: ⚠️ **Partial** — ILogger calls are fully structured; Console.WriteLine calls are all string interpolation
- **Template style**: Structured in `ILogger` layer; raw interpolation in service layer
- **Correlation ID**: `{JobId}` propagated through orchestrator → activities → `FetchClipActivity`'s Pexels fallback helper. **One exception**: `PlanSegmentsActivity` omits `{JobId}` (see gaps).

### ILogger examples — ✅ correct structured templates

```csharp
// VideoOrchestrator.cs:24
logger.LogInformation("[{JobId}] Orchestrator started", input.JobId);

// FetchClipActivity.cs:32
logger.LogInformation("[{JobId}] FetchClip[{Index}] [{Shot}]: query='{Query}'",
    input.JobId, input.Index, input.ShotType, input.SearchQuery);

// FetchClipActivity.cs:60
logger.LogInformation("[{JobId}] FetchClip[{Index}]: used YouTube CC — {Attr}",
    input.JobId, input.Index, attribution);

// RenderVideoActivity.cs:91
logger.LogInformation("[{JobId}] Render complete: {MB:F1} MB",
    input.JobId, fileSize / 1024.0 / 1024.0);
```

### Console.WriteLine examples — ❌ string interpolation, invisible to App Insights

```csharp
// YouTubeVideoService.cs:220 — yt-dlp exit code NOT in App Insights
Console.WriteLine($"  ⚠️  yt-dlp failed (exit {proc.ExitCode}): {stderr[..Math.Min(200, stderr.Length)]}");

// SegmentPlanner.cs:256 — query planning NOT in App Insights
Console.WriteLine($"  Segment {i}: [{shot}] \"{query}\"");

// VideoGenerator.cs:184 — full ffmpeg command NOT in App Insights
Console.WriteLine($"  ffmpeg {string.Join(" ", psi.ArgumentList...)}");
```

---

## Log Level Usage

| Level | Count (ILogger only) | Assessment |
|-------|---------------------|------------|
| **Critical** | 0 | ⚠️ Unused — no alertable signal when the entire orchestration fails |
| **Error** | 1 | ⚠️ Underused — only `GenerateVideoFunction.cs:109` (`LogError(ex, "Video generation failed")`). The Durable path has zero explicit `LogError` calls. |
| **Warning** | 6 | ⚠️ Underused — 5 in `FetchClipActivity` (YouTube/Pexels fallback failures), 1 in `LogsFunction` (App Insights query failure) |
| **Information** | 31 | ✅ Good coverage across orchestrator steps and activity milestones |
| **Debug** | 0 | — No Debug-level logging anywhere |
| **Trace** | 0 | — |
| **Total ILogger** | **38** | |
| **Console.WriteLine** | **24** | ❌ Not in App Insights |

### Log level detail

- **Only 1 `LogError`** exists in the entire codebase, in the legacy `GenerateVideoFunction` (non-Durable path). The Durable path (Orchestrator + 4 Activities) has **zero** `LogError` or `LogCritical` calls. When `RenderVideoActivity` throws or `SynthesizeTtsActivity` fails, the exception propagates to the Durable framework with no application-level error log.
- **LogWarning is only used for expected fallbacks** (YouTube → Pexels, Pexels tier 1 → tier 2 → tier 3). No warnings for infrastructure failures (blob upload errors, rendering failures, TTS synthesis errors).

---

## Events Coverage — Key Scenarios

| Event | Logged? | Location | Notes |
|-------|---------|----------|-------|
| Job start (HTTP request accepted) | ✅ | `HttpStartFunction.cs:58` | `instanceId` + truncated fact |
| Orchestrator start | ✅ | `VideoOrchestrator.cs:24` | `{JobId}` |
| TTS synthesis start/done | ✅ | `SynthesizeTtsActivity.cs:25,34,46` | char count → word count → blob URL |
| Segment planning result | ⚠️ | `PlanSegmentsActivity.cs:20` | Count only, **missing `{JobId}`** — won't match LogsFunction KQL |
| Clip fetch start (per clip) | ✅ | `FetchClipActivity.cs:32` | `{JobId}` `{Index}` `{Shot}` `{Query}` |
| YouTube CC selected | ✅ | `FetchClipActivity.cs:60` | `{JobId}` `{Index}` + attribution |
| YouTube attempt failed | ✅ | `FetchClipActivity.cs:70` | `{JobId}` `{Index}` + exception message (outer exception only) |
| Pexels fallback triggered | ✅ | `FetchClipActivity.cs:79` | `{JobId}` `{Index}` |
| Pexels tier 1/2/3 failure | ✅ | `FetchClipActivity.cs:158,168,179` | `{JobId}` `{Index}` + error message |
| Clip done | ✅ | `FetchClipActivity.cs:105` | `{JobId}` `{Index}` + truncated URL |
| Clip fetch failed (entire clip) | ✅ | `FetchClipActivity.cs:111` | `{JobId}` `{Index}` — returns null URL |
| Clips ready count | ✅ | `VideoOrchestrator.cs:77` | `{JobId}` `{Ready}/{Total}` |
| Render start / downloaded clips | ✅ | `RenderVideoActivity.cs:24,67` | `{JobId}` + clip count + duration |
| Render complete + upload | ✅ | `RenderVideoActivity.cs:91,97` | `{JobId}` + MB + URL |
| Orchestration complete | ✅ | `VideoOrchestrator.cs:96` | `{JobId}` + URL |
| Status endpoint called | ✅ | `StatusFunction.cs:24` | `{JobId}` |
| Logs endpoint called | ✅ | `LogsFunction.cs:30` | `{JobId}` |
| **yt-dlp exit code / stderr** | ❌ | `YouTubeVideoService.cs:220,232` | Console.WriteLine only |
| **YouTube search API failure** | ❌ | `YouTubeVideoService.cs:129` | Console.WriteLine only |
| **Watermark detected per-video** | ❌ | `YouTubeVideoService.cs:85` | Console.WriteLine only |
| **No car in thumbnail** | ❌ | `YouTubeVideoService.cs:90` | Console.WriteLine only |
| **No YouTube candidate found** | ❌ | `YouTubeVideoService.cs:57` | Console.WriteLine only |
| **Segment query planning** | ❌ | `SegmentPlanner.cs:256` | Console.WriteLine only |
| **Brand/model detection** | ❌ | `SegmentPlanner.cs:239` | Console.WriteLine only |
| **ffmpeg command + stderr** | ❌ | `VideoGenerator.cs:184,195` | Console.WriteLine only |
| **CV API failure** | ❌ | `ComputerVisionService.cs:58` | Console.WriteLine only |
| **ffmpeg/yt-dlp binary download** | ❌ | `FfmpegManager.cs:37-46`, `YtDlpManager.cs:41-50` | Console.WriteLine only |
| **Cookies blob check** | ❌ | `YtDlpManager.cs:92-110` | Console.WriteLine only |
| TTS failure | ⚠️ | `TtsService.cs:40` | Throws `InvalidOperationException` — no LogError before throw |
| Render failure | ⚠️ | `RenderVideoActivity.cs` | Exception propagates with no LogError; Durable catches it |
| 0 clips resolved (abort) | ⚠️ | `VideoOrchestrator.cs:80` | Throws but no `LogError` before the throw |

---

## LogsFunction — App Insights REST API Query

`LogsFunction` (`Functions/LogsFunction.cs`) is a first-class observability endpoint:

```
GET /api/logs/{jobId}
```

**Design**: Issues a KQL query against the App Insights REST API (`api.applicationinsights.io/v1`)
filtered by `message has '{jobId}'`, then extracts clip-source lines by string matching on known
keywords (`"used YouTube CC"`, `"falling back to Pexels"`, `"FetchClip["`, `"yt-dlp failed"`,
`"YouTube ["`).

**What works well:**
- Graceful degradation when `AppInsights:ApiKey` is not set (returns guidance JSON + az CLI hint)
- Returns structured `clipActivity` + `allLogs` arrays — useful for diagnosing which clips used YouTube vs Pexels
- The 24h lookback window is appropriate for typical usage

**Structural limitation — Console.WriteLine is invisible:**
The KQL filter `message has '{jobId}'` will **only** match `ILogger` trace records. All
`Console.WriteLine` output from `YouTubeVideoService`, `SegmentPlanner`, `VideoGenerator`, etc.
is written to stdout but not forwarded to the App Insights `traces` table. This means:

- `clipActivity` will show Pexels fallback decisions ✅ (logged via `ILogger`)
- `clipActivity` will **not** show watermark/car rejection, yt-dlp errors, or ffmpeg stderr ❌
- The string filter `"yt-dlp failed"` in `clipLines` will **never** match — that string comes
  from `Console.WriteLine` in `YouTubeVideoService.cs:220`, not from `ILogger`

**Other concerns:**
- `AppInsightsAppId` is hardcoded (`LogsFunction.cs:22`) — survivable but brittle
- `AppInsights:ApiKey` is not validated at startup; absence is discovered lazily at query time
- Sampling (`isEnabled: true`) in `host.json` may cause some trace lines to be dropped before
  they reach App Insights, causing `LogsFunction` to return incomplete timelines

---

## Silent Exception Swallowing

| Location | Code | Impact |
|----------|------|--------|
| `FetchClipActivity.cs:117` | `try { Directory.Delete(...) } catch { }` | ✅ Intentional — best-effort temp cleanup |
| `SynthesizeTtsActivity.cs:51` | `try { Directory.Delete(...) } catch { }` | ✅ Intentional — best-effort temp cleanup |
| `RenderVideoActivity.cs:102` | `try { Directory.Delete(...) } catch { }` | ✅ Intentional — best-effort temp cleanup |
| `PexelsVideoService.cs:75` | `try { File.Delete(sourceTmp) } catch { }` | ✅ Intentional — best-effort temp file delete |
| `GenerateVideoFunction.cs:38` | `catch { /* use default */ }` | ✅ Low risk — falls back to hardcoded default fact if request body is invalid JSON |
| `HttpStartFunction.cs:41` | `catch { /* use default */ }` | ✅ Low risk — same pattern as above |
| `YouTubeVideoService.cs:129` (catch) | `Console.WriteLine(...)` then `return []` | ⚠️ **Medium** — YouTube Data API failures are swallowed into an empty result set. The Console.WriteLine is invisible in App Insights. This will look like "no YouTube candidates found" rather than "YouTube API is down". |
| `ComputerVisionService.cs:58` (catch) | `Console.WriteLine(...)` then optimistic pass | ⚠️ **Medium** — CV API failures silently approve all videos (HasWatermark=false, HasCar=true). No signal in App Insights when CV is consistently failing. |
| `YtDlpManager.cs:110` (catch) | `Console.WriteLine(...)` then `_cachedCookiesPath = ""` | ⚠️ **Low-Medium** — Cookie blob check failure is silently swallowed. If Storage is misconfigured, yt-dlp runs without cookies with no trace in App Insights. |

---

## Coverage Gaps

| Area | Status | Details |
|------|--------|---------|
| HTTP entry points | ✅ Covered | `HttpStartFunction`, `GenerateVideoFunction`, `StatusFunction`, `LogsFunction` all log with ILogger |
| Durable orchestrator steps | ✅ Covered | All 4 pipeline steps logged at start |
| Activities (all 4) | ✅ Covered | ILogger injected; milestone events logged with `{JobId}` |
| `PlanSegmentsActivity` `{JobId}` | ❌ Missing | `"PlanSegments: planned {Count} segments"` has no `{JobId}` → won't appear in LogsFunction results |
| YouTube candidate evaluation | ❌ Missing | Per-video watermark/car scoring entirely in Console.WriteLine |
| yt-dlp errors (exit code, stderr) | ❌ Missing | `YouTubeVideoService.cs:218-224` — Console.WriteLine only |
| YouTube Data API failures | ❌ Missing | `YouTubeVideoService.cs:129` — Console.WriteLine only |
| Segment query planning | ❌ Missing | `SegmentPlanner.cs:239,256` — Console.WriteLine only |
| ffmpeg command + stderr | ❌ Missing | `VideoGenerator.cs:184,195` — Console.WriteLine only |
| Binary cold-start downloads | ❌ Missing | `FfmpegManager`, `YtDlpManager` — Console.WriteLine only |
| Computer Vision API failures | ❌ Missing | `ComputerVisionService.cs:58` — Console.WriteLine only |
| Activity-level error logging | ❌ Missing | `SynthesizeTtsActivity`, `RenderVideoActivity` have no `LogError` — exceptions propagate naked |
| Render failure (0 clips) | ❌ Missing | `VideoOrchestrator.cs:80` throws without a prior `LogError` |
| Timing / duration metrics | ❌ Missing | No `Stopwatch` or `TelemetryClient.TrackMetric` for per-clip or render duration |
| Custom App Insights events | ❌ Missing | No `TrackEvent()` calls — no charting of success/failure counts |
| Health checks | ❌ Missing | No `IHealthCheck` for blob storage, Speech, Vision, Pexels, YouTube APIs |

---

## Monitoring Readiness

| Aspect | Status | Notes |
|--------|--------|-------|
| **Custom metrics** | ❌ None | No `TelemetryClient.TrackMetric()` — can't alert on render failure rate or clip success ratio |
| **Health checks** | ❌ None | No proactive connectivity checks for downstream services |
| **Request/dependency tracking** | ✅ Auto | App Insights Worker SDK auto-tracks `HttpClient` calls as dependencies |
| **Sufficient for incident response** | ⚠️ Partial | You can trace a job through orchestrator + activities via `{JobId}`. But yt-dlp errors, YouTube API failures, and ffmpeg failures require tailing console logs or reading the Durable exception detail — not queryable from the App Insights `traces` table. |
| **`LogsFunction` utility** | ⚠️ Partial | Works well for Pexels fallback path. Fails silently for YouTube-path failures because those events are in Console, not traces. The `"yt-dlp failed"` keyword filter in the function will never match. |
| **Trace sampling risk** | ⚠️ Moderate | `samplingSettings.isEnabled: true` applies to traces. A high-volume job (many parallel `FetchClipActivity` executions) could have some traces sampled out, creating gaps in the `LogsFunction` timeline. |

---

## Summary

| Aspect | Rating | Details |
|--------|--------|---------|
| Framework setup | ✅ Good | Correct isolated-worker App Insights DI; single framework; no Serilog overhead |
| Structured logging (ILogger layer) | ✅ Excellent | 100% structured templates in all Functions and Activities |
| Structured logging (service layer) | ❌ Poor | 7 service classes (24 statements) use `Console.WriteLine` with string interpolation |
| `{JobId}` correlation | ⚠️ Good | Present in orchestrator + 3 of 4 activities; missing in `PlanSegmentsActivity` |
| Log level discipline | ⚠️ Weak | Only 1 `LogError` (legacy path); Durable path has zero error-level signals |
| Coverage breadth | ⚠️ Partial | Pipeline milestones covered; operational decisions in service layer invisible to App Insights |
| LogsFunction design | ⚠️ Clever but incomplete | Smart KQL self-query, but half the interesting events are unreachable |
| Monitoring readiness | ❌ Limited | No metrics, no health checks, no alertable signals beyond Durable failure status |

---

## Recommendations

**Priority 1 — Migrate service layer to `ILogger` (highest impact)**

The 7 service classes that use `Console.WriteLine` should accept `ILogger<T>` via constructor injection. This is the single change that would make yt-dlp errors, YouTube candidate scoring, ffmpeg commands, and segment planning visible in App Insights — and queryable by `LogsFunction`.

```csharp
// Before (YouTubeVideoService.cs:220)
Console.WriteLine($"  ⚠️  yt-dlp failed (exit {proc.ExitCode}): {stderr[..200]}");

// After
_logger.LogWarning("[{JobId}] yt-dlp failed (exit {ExitCode}): {Stderr}",
    _jobId, proc.ExitCode, stderr[..200]);
```

Affected files: `YouTubeVideoService.cs`, `YtDlpManager.cs`, `FfmpegManager.cs`,
`VideoGenerator.cs`, `SegmentPlanner.cs`, `PexelsVideoService.cs`, `ComputerVisionService.cs`.

**Priority 2 — Add `{JobId}` to `PlanSegmentsActivity`**

Change `PlanSegmentsActivity.cs:20`:
```csharp
// Before
logger.LogInformation("PlanSegments: planned {Count} segments", segments.Count);

// After
logger.LogInformation("[{JobId}] PlanSegments: planned {Count} segments", input.JobId, segments.Count);
```
Without this fix, `PlanSegmentsActivity` logs are invisible to `LogsFunction`'s KQL query.

**Priority 3 — Add `LogError` before activity throws**

Add error logging in activities before propagating failures to the Durable framework:

```csharp
// RenderVideoActivity — add before the render step throws
catch (Exception ex)
{
    logger.LogError(ex, "[{JobId}] RenderVideo failed after {ClipCount} clips", input.JobId, clipPaths.Count);
    throw;
}
```

Same pattern for `SynthesizeTtsActivity` and the 0-clips guard in `VideoOrchestrator.cs:80`.

**Priority 4 — Fix `"yt-dlp failed"` keyword filter in `LogsFunction`**

The `clipLines` filter in `LogsFunction.cs:79` searches for `"yt-dlp failed"` — a string that
only appears in `Console.WriteLine` (invisible to App Insights). After Priority 1 is done, this
will work. Until then, remove or annotate the dead filter to avoid misleading "no yt-dlp errors"
results.

**Priority 5 — Exclude traces from App Insights sampling**

Add to `host.json` to prevent trace drop under load:
```json
"samplingSettings": {
  "isEnabled": true,
  "excludedTypes": "Request;Trace"
}
```

**Priority 6 — Emit a custom metric per completed job**

```csharp
// In VideoOrchestrator after success
_telemetryClient.TrackEvent("VideoJob.Completed", new Dictionary<string, string>
{
    ["JobId"]      = input.JobId,
    ["ClipSource"] = youtubeCount > 0 ? "YouTube" : "Pexels",
}, new Dictionary<string, double>
{
    ["ClipCount"]       = clipResults.Length,
    ["YouTubeClips"]    = youtubeCount,
    ["DurationSeconds"] = result.DurationSeconds,
});
```

This enables dashboard charts and threshold alerts (e.g., alert if YouTube clip success rate
drops below 20%).

**Priority 7 — Validate `AppInsights:ApiKey` at startup**

`LogsFunction` silently returns guidance JSON when the key is missing. Add a startup validation
(or at least a `LogWarning` in `Program.cs`) so the missing key is surfaced immediately on cold
start rather than discovered at query time.
