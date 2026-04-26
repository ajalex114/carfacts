<!-- deepfry:commit=5360e5707b59a6cf919f9c880a227006d8f33b09 agent=summarizer timestamp=2025-07-25T00:00:00Z -->

# CarFacts — Quick Overview

## What is this?
Two Azure Function apps: one runs a daily car-facts blog + social media pipeline; the other generates 15–20s portrait videos (Instagram Reels / YouTube Shorts) from a car fact on demand.

## Tech Stack
- **Language**: C# / .NET 8, Azure Functions v4 (isolated worker) + Durable Functions
- **AI**: Azure OpenAI `gpt-4o-mini` (Semantic Kernel), Azure TTS (AndrewNeural voice), Azure Computer Vision
- **Video**: ffmpeg (xfade transitions, karaoke subtitles), yt-dlp (YouTube CC clips), Pexels Videos API
- **Images**: Stability AI SDXL → Together AI FLUX.1.1-pro (fallback)
- **Data**: Azure Cosmos DB, Azure Key Vault (16 secrets), Azure Blob Storage
- **Social**: Twitter/X, Pinterest, WordPress.com (Facebook + Reddit disabled)
- **Monitoring**: Application Insights (both apps) + Serilog file sink (local dev)

## Projects
| Project | What it does | Entry point |
|---------|-------------|-------------|
| `CarFacts.Functions` | Daily blog post + social media scheduling pipeline | `Functions/CarFactsOrchestrator.cs` |
| `CarFacts.VideoFunction` | On-demand short-form video generator (720×1280 portrait) | `Functions/HttpStartFunction.cs` |

## Components — CarFacts.VideoFunction
| Component | What it does | Key file |
|-----------|-------------|----------|
| `HttpStartFunction` | `POST /api/start-video` — queues a Durable job, returns jobId | `Functions/HttpStartFunction.cs` |
| `VideoOrchestrator` | Chains 4 activities; fan-out clip fetch per segment | `Functions/VideoOrchestrator.cs` |
| `SynthesizeTtsActivity` | Azure TTS → WAV + word-timed ASS subtitle file | `Activities/SynthesizeTtsActivity.cs` |
| `PlanSegmentsActivity` | Sentence-splits fact; detects brand/model; assigns shot types | `Activities/PlanSegmentsActivity.cs` |
| `FetchClipActivity` | YouTube CC first → Pexels 4-tier fallback; trims to 720×1280 | `Activities/FetchClipActivity.cs` |
| `RenderVideoActivity` | ffmpeg: xfade clips + karaoke subtitles + watermark → blob | `Activities/RenderVideoActivity.cs` |
| `StatusFunction` | `GET /api/status/{jobId}` — polls Durable instance state | `Functions/StatusFunction.cs` |
| `FfmpegManager` / `YtDlpManager` | Download binaries from blob on cold start; cached after | `Services/FfmpegManager.cs` |

## How it works — VideoFunction pipeline
- `POST /api/start-video` with `{ "fact": "..." }` → returns `{ jobId, statusUrl }`
- Activity 1: Azure TTS synthesises the fact → WAV + per-word timings → blob
- Activity 2: Segment planner splits by sentence/pause, detects car brand/model, assigns shot keywords
- Activity 3 (fan-out): Each segment fetches a clip in parallel — YouTube CC first, Pexels fallback
- Activity 4: ffmpeg stitches clips with xfade fades, burns karaoke subtitles and watermark → final MP4

## Key things to know
1. **YouTube CC downloads are currently blocked** on Azure datacenter IPs (bot detection) — Pexels fallback is working; residential proxy fix is pending
2. **ffmpeg and yt-dlp are not bundled** — downloaded from `poc-tools` blob on first cold start (~130 MB each); warm invocations use a cached path
3. **Twitter API Basic plan ($200/mo) is ~98% of total monthly cost** — all other services (TTS, images, LLM) cost ~$3–5/mo combined

## Health at a glance
| Area | Functions | VideoFunction |
|------|-----------|---------------|
| Security | ⚠️ KV+MI in prod; Cosmos uses conn string | ⚠️ No Key Vault; secrets in app settings; plaintext in local.settings.json |
| Connections | ✅ IHttpClientFactory throughout | ⚠️ 4 static HttpClients; BlobContainerClient recreated per call; ImageAnalysisClient recreated per thumbnail |
| Logging | ✅ Structured ILogger + Serilog; 214 log calls | ⚠️ ILogger in Functions/Activities; 7 service classes use Console.WriteLine |
| Test Coverage | ⚠️ 58 tests; social/Cosmos untested; no CI | ❌ Zero tests |
| API Costs | ⚠️ ~$205/mo (Twitter dominates) | ✅ Negligible per video (TTS + Pexels free tier) |
| Architecture | ✅ Clean DI, fallback chains, replay-safe | ⚠️ Durable orchestration solid; several services not in DI (`new` inside activities) |

## Where to start
1. **Video pipeline**: `src/CarFacts.VideoFunction/Functions/VideoOrchestrator.cs` — the 4-step chain
2. **Content pipeline**: `src/CarFacts.Functions/Functions/CarFactsOrchestrator.cs` — the daily flow
3. **Full diagrams**: `deepfry/architecture.md` · **Known issues**: `deepfry/connections.md` · **Cost breakdown**: `deepfry/costs.md`
