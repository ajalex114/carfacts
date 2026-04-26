<!-- deepfry:commit=b9be8dc1501e31ea9edfa99c938527818fa2aca5 agent=summarizer timestamp=2025-07-24T15:00:00Z -->

# CarFacts — Quick Overview

## What is this?
A daily Azure Function that AI-generates car-history blog posts, publishes to WordPress, and distributes across social media.

## Tech Stack
- **Language**: C# / .NET 8
- **Runtime**: Azure Functions v4 (isolated worker) + Durable Functions
- **AI**: Azure OpenAI (gpt-4o-mini) via Semantic Kernel
- **Images**: Stability AI (primary) → Together AI (fallback)
- **Data**: Azure Cosmos DB (2 containers), Azure Key Vault (16 secrets)
- **Publishing**: WordPress.com REST API
- **Social**: Twitter/X, Facebook, Reddit, Pinterest
- **Monitoring**: Application Insights + Serilog (local)

## Components
| Component | What it does | Key file |
|-----------|-------------|----------|
| CarFactsOrchestrator | Main daily pipeline — facts → images → publish | `Functions/CarFactsOrchestrator.cs` |
| ContentGenerationService | Generates 5 car facts via Azure OpenAI | `Services/ContentGenerationService.cs` |
| ImageGenerationService | Creates images via Stability AI with fallback | `Services/ImageGenerationService.cs` |
| WordPressService | Uploads images + publishes posts | `Services/WordPressService.cs` |
| SocialMediaPublisher | Fan-out posts to Twitter, Facebook, Reddit | `Services/SocialMediaPublisher.cs` |
| PinterestService | Creates categorized pins (10-board taxonomy) | `Services/PinterestService.cs` |
| CosmosFactKeywordStore | Tracks keywords, backlinks, social counts | `Services/CosmosFactKeywordStore.cs` |

## How it works
- **6 AM UTC**: Timer fires → orchestrator generates 5 facts + SEO + 5 images in parallel
- Draft created on WordPress → images uploaded → HTML with TOC/FAQ/backlinks → published
- Social media queue generated with US-timezone slots → stored in Cosmos DB (48h TTL)
- Separate timers execute queued posts throughout the day (tweets, replies, likes, pins)
- Pinterest timer runs 6×/day, selecting least-pinned facts for categorized boards

## Key things to know
1. **All secrets in Azure Key Vault** via Managed Identity — `ISecretProvider` swaps to local config in dev
2. **Durable Functions orchestrate everything** — 6 orchestrators, 26 activities, per-dependency retry policies
3. **Twitter API Basic plan ($200/mo) is 97% of total cost** — evaluate ROI vs. free-tier alternatives

## Health at a glance
| Area | Status | Details |
|------|--------|---------|
| Security | ⚠️ | KV + MI in prod ✅; Cosmos DB uses conn string not MI; local plaintext secrets |
| Connections | ✅ | All HttpClients via IHttpClientFactory; zero resource leaks; minor captive-dep issue |
| Logging | ✅ | 214 structured logs, 100% templates, zero interpolation; 2 silent catch blocks |
| Test Coverage | ⚠️ | 58 unit tests; core services covered; social/Cosmos stores untested; no CI pipeline |
| API Costs | ⚠️ | ~$205/mo; Twitter dominates; images ~$3/mo; LLM negligible (~$0.05/mo) |
| Architecture | ✅ | Clean DI, fallback chains, null-object pattern, TTL cleanup, replay-safe orchestrators |

## Where to start
1. Read `src/CarFacts.Functions/Program.cs` — DI composition root
2. Trace `Functions/CarFactsOrchestrator.cs` — the main daily pipeline
3. Check `deepfry/architecture.md` for the full Mermaid diagram and data flow
