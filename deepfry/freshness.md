# Freshness Report

## Current Analysis
- **Generated at**: 2026-04-26T18:28:43Z
- **Commit**: `5360e5707b59a6cf919f9c880a227006d8f33b09` on branch `master`
- **Working tree**: ⚠️ Dirty — 1 uncommitted change
  - Modified (tracked): `src/CarFacts.Functions/Services/TwitterService.cs`
  - Untracked (excluded): `src/CarFacts.VideoPoC/poc_output/` *(excluded from scope)*
- **Scope**: `src/` only — excluding `bin/`, `obj/`, `publish_out/`, `poc_output/`
- **Source files analyzed**: 138 files
- **Newest file touched**: `2026-04-26T18:24:43Z`

### Files by Extension (src/ scope)
| Extension | Count | Notes                         |
|-----------|-------|-------------------------------|
| `.cs`     | 119   | C# source files               |
| `.txt`    | 11    | Prompt template files         |
| `.csproj` | 3     | Project files                 |
| `.json`   | 3     | Config files (host.json, appsettings.json) |
| `.py`     | 1     | Python helper script          |
| `.zip`    | 1     | publish.zip artifact          |
| **Total** | **138** |                             |

### Fingerprints
| Key              | Value                                                              |
|------------------|--------------------------------------------------------------------|
| Root tree hash   | `8201c9a3b0ea978cec07fd7993ae62c9879d47b7`                        |
| src/ subtree hash| `583e6eaac0e1ff4b6b22120c837afc84e21399cf`                        |
| src/ content fingerprint (SHA-256 of blob hashes) | `49ab0baadc29183941dc31fa49123c910d8beaadec0651ee7e43e9193076ae7a` |

## Prior Analysis Comparison

> ⚠️ **Stale** — analysis is 1 commit behind HEAD. Significant new source was added.

| Field               | Prior                                        | Current                                      |
|---------------------|----------------------------------------------|----------------------------------------------|
| Commit              | `b9be8dc1501e31ea9edfa99c938527818fa2aca5`   | `5360e5707b59a6cf919f9c880a227006d8f33b09`   |
| Generated at        | 2025-07-24T06:42:00Z                         | 2026-04-26T18:28:43Z                         |
| Root tree hash      | `d87df4005513cbf96eaf612e8b01110d66096b4f`   | `8201c9a3b0ea978cec07fd7993ae62c9879d47b7`   |
| Source file count   | 127                                          | 138 (+11)                                    |
| Working tree        | Clean                                        | Dirty (1 modified file)                      |

### Commits Since Prior Analysis
- **1 commit** since `b9be8dc` — all existing analysis pages are stale

### Source Files Changed Since Prior Analysis (src/ scope — 40 files)
All additions from two new project modules introduced in the single new commit:

**New: `CarFacts.VideoFunction/`** (25 files — Azure Durable Functions video pipeline)
- `Activities/`: FetchClipActivity, PlanSegmentsActivity, RenderVideoActivity, SynthesizeTtsActivity
- `Functions/`: GenerateVideoFunction, HttpStartFunction, LogsFunction, StatusFunction, VideoOrchestrator
- `Models/`: ActivityModels, VideoSegment, WordTiming
- `Services/`: ComputerVisionService, FfmpegManager, PexelsApiKeyHolder, PexelsVideoService, SegmentPlanner, SubtitleGenerator, TtsService, VideoGenerator, VideoStorageService, YouTubeVideoService, YtDlpManager
- Root: `CarFacts.VideoFunction.csproj`, `host.json`, `Program.cs`

**New: `CarFacts.VideoPoC/`** (15 files — local video generation proof-of-concept)
- `Models/`: VideoSegment, WordTiming
- `Services/`: EdgeTtsService, PexelsVideoService, SegmentPlanner, SubtitleGenerator, TtsService, VideoGenerator, WindowsTtsService
- Root: `CarFacts.VideoPoC.csproj`, `Program.cs`, `appsettings.json`, `tts_timing.py`

### Agent Staleness
All analysis agents are marked **stale** and should be re-run to reflect the new VideoFunction and VideoPoC modules:

| Agent               | Status  | Reason                                      |
|---------------------|---------|---------------------------------------------|
| dependency-analyzer | ⚠️ stale | 2 new .csproj files with new NuGet deps      |
| code-grapher        | ⚠️ stale | 40 new source files, 2 new dependency graphs |
| security-analyzer   | ⚠️ stale | New HTTP clients, API keys, storage access   |
| connection-analyzer | ⚠️ stale | New external service integrations            |
| log-analyzer        | ⚠️ stale | New logging patterns in video services       |
| test-analyzer       | ⚠️ stale | Coverage gaps for new modules not assessed   |
| cost-analyzer       | ⚠️ stale | New AI/video API calls not costed            |
| architect           | ⚠️ stale | Architecture diagram missing video pipeline  |
| summarizer          | ⚠️ stale | Summary does not mention video generation    |
| freshness-tracker   | ✅ current | Just updated                               |
