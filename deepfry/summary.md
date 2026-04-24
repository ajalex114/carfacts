# CarFacts — Quick Overview

## What is this?
Azure Functions app that auto-publishes daily AI-generated car-facts blog posts to WordPress and social media.

## Tech Stack
- **Language**: C# / .NET 8
- **Compute**: Azure Functions v4 (isolated worker) + Durable Functions orchestration
- **AI**: Semantic Kernel → Azure OpenAI (GPT-4o-mini) for text; Stability AI / Together AI for images
- **Data**: Cosmos DB (2 containers: `fact-keywords`, `social-media-queue`)
- **Auth**: Managed Identity → Azure Key Vault (14 secrets)
- **Monitoring**: Application Insights (prod), Serilog (local dev)
- **Publishing**: WordPress.com REST API, Twitter/X, Facebook, Reddit, Pinterest

## Components
| Component | What it does | Key file |
|-----------|-------------|----------|
| CarFactsOrchestrator | Main pipeline: facts → SEO → images → publish → social | `Functions/CarFactsOrchestrator.cs` |
| ContentGenerationService | LLM generates 5 "on this day" car facts | `Services/ContentGenerationService.cs` |
| ImageGenerationService | Stability AI text-to-image with Together AI fallback | `Services/ImageGenerationService.cs` |
| WordPressService | Uploads images + creates/publishes blog posts | `Services/WordPressService.cs` |
| SocialMediaPublisher | Fans out posts to all enabled platforms | `Services/SocialMediaPublisher.cs` |
| 26 Activity Functions | Thin wrappers connecting orchestrator to services | `Functions/Activities/` |
| CosmosFactKeywordStore | Tracks keywords, backlinks, and social counts | `Services/CosmosFactKeywordStore.cs` |

## How it works
- Timer fires daily at 6 AM UTC → starts Durable Functions orchestrator
- LLM generates 5 car facts + SEO metadata, then 5 images are created
- Draft post is created on WordPress, images uploaded, HTML formatted and published
- Social media posts are queued to Cosmos DB and posted on separate schedules
- Pinterest pins are posted 6×/day, selecting the least-pinned fact each time

## Key things to know
1. **All secrets live in Key Vault** — Managed Identity in prod, `ISecretProvider` abstraction swaps to local config in dev
2. **Durable Functions orchestration** — every external call is a retry-able activity (26 total); failures don't block the pipeline
3. **Structured logging is exemplary** — 100% message templates, zero string interpolation, full coverage across all 48+ source files

## Health at a glance
| Area | Status | Notes |
|------|--------|-------|
| Security | ⚠️ | Solid prod setup (MI + KV), but real API keys sit in `local.settings.json` on disk |
| Connections | ✅ | All HTTP clients via `IHttpClientFactory`; Cosmos singleton; no resource leaks |
| Logging | ✅ | Full structured logging, App Insights in prod, replay-safe orchestrator loggers |
| Dependencies | ✅ | 16 NuGet packages, all pinned and current; no known CVEs |

## Where to start
1. Read `src/CarFacts.Functions/Program.cs` — DI wiring and startup config
2. Read `Functions/CarFactsOrchestrator.cs` — the main daily pipeline
3. Check `deepfry/code-graph.md` for the full module map and data flow
