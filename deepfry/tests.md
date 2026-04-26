# Test Coverage Analysis

<!-- deepfry:commit=5360e5707b59a6cf919f9c880a227006d8f33b09 agent=test-analyzer timestamp=2025-07-24T08:00:00Z -->

## ⚠️ Important Scope Note

The solution contains **two separate production projects** with very different test coverage:

| Project | Path | Has Tests? |
|---------|------|-----------|
| `CarFacts.Functions` | `src/CarFacts.Functions/` | ✅ Partially tested (29–59% depending on scope) |
| `CarFacts.VideoFunction` | `src/CarFacts.VideoFunction/` | ❌ **Zero coverage — entirely untested** |

The single test project (`tests/CarFacts.Functions.Tests/`) references only `CarFacts.Functions`. `CarFacts.VideoFunction` has no test project and no test files anywhere in the repository.

---

## Test Frameworks

| Framework | Version | Config File | Test Command |
|-----------|---------|-------------|--------------|
| xUnit | 2.9.2 | `tests/CarFacts.Functions.Tests/CarFacts.Functions.Tests.csproj` | `dotnet test` |
| Moq | 4.20.72 | (same csproj) | — |
| FluentAssertions | 6.12.2 | (same csproj) | — |
| Microsoft.NET.Test.Sdk | 17.11.1 | (same csproj) | — |

No test configuration files (`xunit.runner.json`, `.runsettings`, etc.) found outside the `.csproj`.

---

## Test Inventory

- **Total test files**: 6
- **Total test methods**: 58 `[Fact]` methods · 0 `[Theory]` methods
- **Framework**: xUnit + Moq + FluentAssertions
- **Target**: .NET 8.0

### Test File Breakdown

| Test File | `[Fact]` Count | Covers |
|-----------|---------------|--------|
| `tests/.../Functions/ActivityTests.cs` | 18 | 11 Activity classes |
| `tests/.../Services/ContentFormatterServiceTests.cs` | 14 | `ContentFormatterService` |
| `tests/.../Services/ImageGenerationServiceTests.cs` | 8 | `ImageGenerationService` |
| `tests/.../Services/WordPressServiceTests.cs` | 8 | `WordPressService` |
| `tests/.../Services/ContentGenerationServiceTests.cs` | 5 | `ContentGenerationService` |
| `tests/.../Services/FallbackImageGenerationServiceTests.cs` | 5 | `FallbackImageGenerationService` |
| **Total** | **58** | |

### Test Helpers

| File | Purpose |
|------|---------|
| `tests/.../Helpers/FakeHttpMessageHandler.cs` | HTTP stub — queues fake responses, captures sent requests for assertion |
| `tests/.../Helpers/TestDataBuilder.cs` | Data factory — creates `CarFactsResponse`, `RawCarFactsContent`, `SeoMetadata`, `GeneratedImage`, WP JSON payloads |
| `tests/.../GlobalUsings.cs` | Global `using Xunit;` import |

---

## Test Type Distribution

| Type | Count | Percentage | Notes |
|------|-------|------------|-------|
| Unit | 58 | 100% | All tests mock dependencies, use `FakeHttpMessageHandler` for HTTP, test single classes in isolation |
| Integration | 0 | 0% | No tests against CosmosDB, Key Vault, WordPress API, or Azure Functions runtime |
| E2E | 0 | 0% | No end-to-end workflow tests |

---

## Coverage Map — `CarFacts.Functions`

### ✅ Tested Modules

| Source File | Test File(s) | Tests | Quality |
|------------|-------------|-------|---------|
| `src/.../Services/ContentFormatterService.cs` | `ContentFormatterServiceTests.cs` | 14 | ✅ Excellent — HTML output structure, ToC anchors, schema markup, FAQ, image tags, special char encoding, empty media |
| `src/.../Services/ImageGenerationService.cs` | `ImageGenerationServiceTests.cs` | 8 | ✅ Excellent — image count, filenames, base64 decode, bearer auth, endpoint URL, rate limit retry (exhausted + retry-then-succeed), secret retrieval |
| `src/.../Services/WordPressService.cs` | `WordPressServiceTests.cs` | 8 | ✅ Good — upload images (count, endpoint, auth, media IDs), create post (result, endpoint, failure path, secret retrieval) |
| `src/.../Services/ContentGenerationService.cs` | `ContentGenerationServiceTests.cs` | 5 | ✅ Good — happy path, markdown-wrapped JSON, wrong fact count throws, missing field throws, service call verified |
| `src/.../Services/FallbackImageGenerationService.cs` | `FallbackImageGenerationServiceTests.cs` | 5 | ✅ Good — primary success, primary fails → fallback, both fail → empty, primary empty → fallback, cancellation propagated |
| `src/.../Functions/Activities/ExecuteScheduledPostActivity.cs` | `ActivityTests.cs` | 4 | ✅ Excellent — fact/link/reply/like item paths; verifies post+delete+social-count-increment behavior |
| `src/.../Functions/Activities/StoreSocialMediaQueueActivity.cs` | `ActivityTests.cs` | 2 | ✅ Good — item count (posts + replies), multi-platform duplication |
| `src/.../Functions/Activities/StoreFactKeywordsActivity.cs` | `ActivityTests.cs` | 2 | ✅ Good — store call verified, anchor ID generation verified (slug format, composite ID, fact URL) |
| `src/.../Functions/Activities/FormatAndPublishActivity.cs` | `ActivityTests.cs` | 2 | ✅ Good — draft-update path + new-post path |
| `src/.../Functions/Activities/GenerateAllImagesActivity.cs` | `ActivityTests.cs` | 2 | ✅ Good — success path + HTTP exception returns empty list |
| `src/.../Functions/Activities/GenerateRawContentActivity.cs` | `ActivityTests.cs` | 1 | ⚠️ Thin — single delegation test, no error/retry paths |
| `src/.../Functions/Activities/GenerateSeoActivity.cs` | `ActivityTests.cs` | 1 | ⚠️ Thin — single delegation test |
| `src/.../Functions/Activities/CreateDraftPostActivity.cs` | `ActivityTests.cs` | 1 | ⚠️ Thin — single delegation test |
| `src/.../Functions/Activities/UploadSingleImageActivity.cs` | `ActivityTests.cs` | 1 | ⚠️ Thin — single delegation test |
| `src/.../Functions/Activities/PublishSocialMediaActivity.cs` | `ActivityTests.cs` | 1 | ⚠️ Thin — only tests with no registered services (empty publisher) |
| `src/.../Functions/Activities/GetEnabledPlatformsActivity.cs` | `ActivityTests.cs` | 1 | ⚠️ Thin — single test, mixed enabled/disabled platforms |
| `src/.../Services/SocialMediaPublisher.cs` | `ActivityTests.cs` (indirect) | — | ⚠️ Indirectly exercised via Activity tests; no dedicated test file |

### ❌ Untested Modules — `CarFacts.Functions`

#### 🔴 High Priority (Core Business Logic / External Integrations)

| Source File | Type | Risk |
|------------|------|------|
| `src/.../Services/TwitterService.cs` | Social Media Service | OAuth tokens, posting, replying, liking tweets — auth failures, rate limits entirely untested |
| `src/.../Services/FacebookService.cs` | Social Media Service | Facebook Graph API integration — auth, page posting untested |
| `src/.../Services/RedditService.cs` | Social Media Service | Reddit API — OAuth flow, subreddit posting untested |
| `src/.../Services/PinterestService.cs` | Social Media Service | Pinterest API — pin creation, board taxonomy, auth untested |
| `src/.../Services/SeoGenerationService.cs` | AI Content Service | SEO metadata AI generation — parsing, field validation, AI error recovery untested (same risk profile as `ContentGenerationService`, which IS tested) |
| `src/.../Services/TogetherAIImageGenerationService.cs` | AI Image Service | Secondary image provider — API contract, response parsing, auth untested |
| `src/.../Services/CosmosFactKeywordStore.cs` | Data Access | Cosmos DB upsert/query/increment — production data persistence entirely untested |
| `src/.../Services/CosmosSocialMediaQueueStore.cs` | Data Access | Cosmos DB queue add/delete/list — production queue persistence entirely untested |
| `src/.../Services/KeyVaultSecretProvider.cs` | Security | Azure Key Vault retrieval — secret caching, expiry, exception handling untested |

#### 🟡 Medium Priority (Orchestrators / Untested Activities)

| Source File | Type | Risk |
|------------|------|------|
| `src/.../Functions/CarFactsOrchestrator.cs` | Durable Orchestrator | Main workflow — activity fan-out, retry policies, sequencing untested |
| `src/.../Functions/CarFactsTimerTrigger.cs` | Timer Trigger | Entry point — orchestrator start logic untested |
| `src/.../Functions/SocialMediaOrchestrator.cs` | Durable Orchestrator | Social post generation workflow untested |
| `src/.../Functions/SocialMediaPostingTrigger.cs` | Timer Trigger | Scheduled posting entry point untested |
| `src/.../Functions/ScheduledPostOrchestrator.cs` | Durable Orchestrator | Scheduled post execution loop untested |
| `src/.../Functions/ScheduledPostingOrchestrator.cs` | Durable Orchestrator | Posting orchestration untested |
| `src/.../Functions/PinterestPostingOrchestrator.cs` | Durable Orchestrator | Pinterest workflow untested |
| `src/.../Functions/PinterestPostingTrigger.cs` | Timer Trigger | Pinterest trigger untested |
| `src/.../Functions/TweetReplyOrchestrator.cs` | Durable Orchestrator | Reply generation workflow untested |
| `src/.../Functions/TweetReplyTrigger.cs` | Timer Trigger | Tweet reply trigger untested |
| `src/.../Functions/Activities/GenerateTweetFactsActivity.cs` | Activity | Tweet fact content generation untested |
| `src/.../Functions/Activities/GenerateTweetLinkActivity.cs` | Activity | Tweet link generation untested |
| `src/.../Functions/Activities/GenerateTweetLikeActivity.cs` | Activity | Tweet like content generation untested |
| `src/.../Functions/Activities/GenerateTweetReplyActivity.cs` | Activity | AI-generated tweet reply untested |
| `src/.../Functions/Activities/GeneratePinContentActivity.cs` | Activity | Pinterest pin content generation untested |
| `src/.../Functions/Activities/CreatePinterestPinActivity.cs` | Activity | Pinterest pin creation untested |
| `src/.../Functions/Activities/SelectPinterestFactActivity.cs` | Activity | Pinterest fact selection logic untested |
| `src/.../Functions/Activities/CreateWebStoryActivity.cs` | Activity | Web story creation untested |
| `src/.../Functions/Activities/FindBacklinksActivity.cs` | Activity | Backlink discovery logic untested |
| `src/.../Functions/Activities/GetPendingScheduledItemsActivity.cs` | Activity | Queue query/pagination untested |
| `src/.../Functions/Activities/GetSocialMediaSettingsActivity.cs` | Activity | Settings retrieval untested |
| `src/.../Functions/Activities/GetWebStoriesEnabledActivity.cs` | Activity | Feature flag check untested |
| `src/.../Functions/Activities/IncrementSocialCountsActivity.cs` | Activity | Cosmos counter update untested |
| `src/.../Functions/Activities/StoreTweetReplyQueueActivity.cs` | Activity | Tweet reply queue storage untested |
| `src/.../Functions/Activities/UpdatePinterestTrackingActivity.cs` | Activity | Pinterest tracking update untested |

#### 🟢 Low Priority (Utilities / Config / Models)

| Source File | Type | Risk |
|------------|------|------|
| `src/.../Helpers/SlugHelper.cs` | Pure Utility | Tested **indirectly** via `StoreFactKeywordsActivity` tests (anchor ID assertions). No dedicated unit test. |
| `src/.../Helpers/UsPostingScheduler.cs` | Scheduling Logic | Pure functions with complex time-slot logic — untested, but no I/O |
| `src/.../Services/CachedImageGenerationService.cs` | Caching Decorator | Local dev caching only — low production risk |
| `src/.../Services/LocalSecretProvider.cs` | Dev-only Utility | Returns env vars / config — development use only |
| `src/.../Services/NullFactKeywordStore.cs` | Null Object | No-op implementation — trivial |
| `src/.../Services/NullSocialMediaQueueStore.cs` | Null Object | No-op implementation — trivial |
| `src/.../Configuration/*.cs` (3 files) | Config POCOs | Settings classes / constants — no logic |
| `src/.../Models/*.cs` (12 files) | Models / DTOs | Data classes — no business logic |
| `src/.../Services/Interfaces/*.cs` (11 files) | Interfaces | Contracts only — nothing to test |
| `src/.../Program.cs` | Host Startup | DI wiring — integration test candidate |

---

## Coverage Map — `CarFacts.VideoFunction` (entirely untested)

> This project has **no test project, no test files, and is not referenced by any test assembly**.
> All 23 source files are untested.

### ❌ Untested (All 23 source files)

| Source File | Type | Priority | Risk |
|------------|------|----------|------|
| `src/CarFacts.VideoFunction/Services/SegmentPlanner.cs` | Core Logic | 🔴 High | Static class with complex sentence-splitting, stop-word filtering, brand mapping, shot-type assignment — pure functions ideal for unit tests, currently zero coverage |
| `src/CarFacts.VideoFunction/Services/SubtitleGenerator.cs` | Core Logic | 🔴 High | Word-timing to SRT/ASS subtitle conversion — time formatting and text layout logic untested |
| `src/CarFacts.VideoFunction/Services/VideoGenerator.cs` | Core Logic | 🔴 High | Main video assembly orchestration — clip selection, ffmpeg invocation, error handling untested |
| `src/CarFacts.VideoFunction/Services/FfmpegManager.cs` | External Process | 🔴 High | Shells out to ffmpeg — process start, argument construction, output parsing untested |
| `src/CarFacts.VideoFunction/Services/YtDlpManager.cs` | External Process | 🔴 High | Shells out to yt-dlp — download args, error handling, timeout untested |
| `src/CarFacts.VideoFunction/Services/TtsService.cs` | Azure AI | 🔴 High | Azure Cognitive Services TTS — synthesis, output format, auth untested |
| `src/CarFacts.VideoFunction/Services/ComputerVisionService.cs` | Azure AI | 🔴 High | Azure Vision API — image analysis, tag extraction untested |
| `src/CarFacts.VideoFunction/Services/PexelsVideoService.cs` | External API | 🔴 High | Pexels video search/download — API auth, pagination, fallback untested |
| `src/CarFacts.VideoFunction/Services/YouTubeVideoService.cs` | External API | 🔴 High | YouTube video download — yt-dlp integration, URL construction untested |
| `src/CarFacts.VideoFunction/Services/VideoStorageService.cs` | Data Access | 🔴 High | Azure Blob Storage upload/download — SAS URL generation, content type untested |
| `src/CarFacts.VideoFunction/Services/PexelsApiKeyHolder.cs` | Config Service | 🟡 Medium | API key management — key rotation logic untested |
| `src/CarFacts.VideoFunction/Functions/VideoOrchestrator.cs` | Durable Orchestrator | 🟡 Medium | Video generation workflow — activity fan-out untested |
| `src/CarFacts.VideoFunction/Functions/GenerateVideoFunction.cs` | HTTP Trigger | 🟡 Medium | HTTP trigger entry point untested |
| `src/CarFacts.VideoFunction/Functions/HttpStartFunction.cs` | HTTP Trigger | 🟡 Medium | Orchestrator start trigger untested |
| `src/CarFacts.VideoFunction/Functions/StatusFunction.cs` | HTTP Trigger | 🟡 Medium | Orchestration status endpoint untested |
| `src/CarFacts.VideoFunction/Functions/LogsFunction.cs` | HTTP Trigger | 🟡 Medium | Log retrieval endpoint untested |
| `src/CarFacts.VideoFunction/Activities/FetchClipActivity.cs` | Activity | 🟡 Medium | Clip fetching from Pexels/YouTube untested |
| `src/CarFacts.VideoFunction/Activities/PlanSegmentsActivity.cs` | Activity | 🟡 Medium | Calls `SegmentPlanner` — untested |
| `src/CarFacts.VideoFunction/Activities/RenderVideoActivity.cs` | Activity | 🟡 Medium | Calls `VideoGenerator` and stores result — untested |
| `src/CarFacts.VideoFunction/Activities/SynthesizeTtsActivity.cs` | Activity | 🟡 Medium | Calls `TtsService` — untested |
| `src/CarFacts.VideoFunction/Models/ActivityModels.cs` | Models | 🟢 Low | Data records — no logic |
| `src/CarFacts.VideoFunction/Models/VideoSegment.cs` | Model | 🟢 Low | Data record — no logic |
| `src/CarFacts.VideoFunction/Models/WordTiming.cs` | Model | 🟢 Low | Data record — no logic |

---

## Coverage Summary

### `CarFacts.Functions`

| Scope | Source Files | Tested | Untested | Coverage |
|-------|-------------|--------|----------|----------|
| All source files | 71 | 16 | 55 | 23% |
| Excluding interfaces/models/config/nulls | 41 | 16 | 25 | 39% |
| Substantive logic files only (services + activities) | 30 | 16 | 14 | **53%** |

### `CarFacts.VideoFunction`

| Scope | Source Files | Tested | Untested | Coverage |
|-------|-------------|--------|----------|----------|
| All source files | 23 | 0 | 23 | **0%** |

### Combined

- **Total source modules**: ~94 .cs files across both production projects
- **Test files**: 6 (all under `CarFacts.Functions.Tests`)
- **Test methods**: 58
- **Critically untested**: 19 files handling external APIs (social media, AI, Cosmos DB, Key Vault, ffmpeg, yt-dlp)

---

## Test Anti-Patterns

| Pattern | Count | Details |
|---------|-------|---------|
| ❌ No assertions | **0** | All 58 tests use FluentAssertions `.Should()` or Moq `.Verify()` |
| ❌ Commented-out tests | **0** | No commented-out `[Fact]`, `[Theory]`, or `// [Test]` blocks found |
| ❌ Flaky markers | **0** | No `Skip`, `[Trait("flaky")]`, or retry attributes |
| ❌ Excessive mocking | **0** | Max mocked dependencies in any single test: 4 (`ExecuteScheduledPostActivity` tests) — within acceptable limits |
| ❌ Snapshot overuse | **0** | No snapshot testing |
| ⚠️ God test file | **1** | `ActivityTests.cs` (616 lines, 18 tests) covers 11 different Activity classes in one file, separated by `#region` blocks. Impedes discoverability and increases merge conflict risk |
| ⚠️ API mismatch in tests | **1** | `ContentFormatterServiceTests.cs` tests the **old** `FormatPostHtml(CarFactsResponse, List<UploadedMedia>, string)` overload. Orchestration calls the **new** `FormatPostHtml(RawCarFactsContent, SeoMetadata, ..., backlinks, relatedPosts)` overload. The new overload bridges to the old one internally, so it is indirectly covered — but the new richer parameters (backlinks, related posts) are never tested directly. |

### Positive Patterns Observed

| Pattern | Details |
|---------|---------|
| ✅ Meaningful test names | All tests follow `MethodName_Scenario_ExpectedBehavior` naming |
| ✅ Centralized test data | `TestDataBuilder` provides consistent, realistic objects and JSON payloads |
| ✅ Reusable HTTP stub | `FakeHttpMessageHandler` enables request inspection (URI, headers, body) without real HTTP calls |
| ✅ Arrange-Act-Assert | All tests clearly separate setup, execution, and assertion |
| ✅ Edge case coverage | Rate limiting, empty results, API errors, markdown-wrapped JSON, special character encoding all covered |
| ✅ Side effect verification | Moq `.Verify()` confirms service calls were made with correct arguments, not just that return values are correct |

---

## Test Infrastructure

| Aspect | Status | Details |
|--------|--------|---------|
| CI pipeline | ❌ **Missing** | `.github/workflows/` is empty — no automated test execution on PR or push |
| Code coverage tool | ❌ **Not configured** | No `coverlet.collector`, `<CollectCoverage>`, or ReportGenerator in test csproj |
| Test scripts | ❌ **Not configured** | No `Makefile`, shell scripts, or `dotnet-tools.json` test runner shortcuts |
| Test utilities | ✅ Present | `FakeHttpMessageHandler` + `TestDataBuilder` — well-designed, reusable |
| Test isolation | ✅ Good | Each test creates its own SUT and fresh mocks. No shared mutable state |
| Test data management | ✅ Present | Centralized `TestDataBuilder` with configurable `factCount` parameter |

---

## Recommendations

1. **🔴 Create a CI pipeline** — `.github/workflows/` is empty. Add a `dotnet test` GitHub Actions workflow triggered on PRs and pushes to `master`. This is the single most impactful improvement: without CI, regressions can be merged undetected.

2. **🔴 Add a test project for `CarFacts.VideoFunction`** — The entire video generation codebase (23 files, including complex `SegmentPlanner`, `FfmpegManager`, `SubtitleGenerator`) has zero test coverage. Start with `SegmentPlanner` and `SubtitleGenerator` — both are pure static functions with no I/O, making them ideal first targets.

3. **🔴 Test social media services** (`TwitterService`, `FacebookService`, `RedditService`, `PinterestService`) — These post user-visible content to live platforms using OAuth tokens. Use `FakeHttpMessageHandler` (already in the project) to test auth header construction, API endpoint URLs, rate limit handling, and failure paths.

4. **🔴 Test `SeoGenerationService`** — Mirrors the risk profile of `ContentGenerationService` (which IS tested). Apply the identical test pattern: mock `IChatCompletionService`, test happy path, markdown-wrapped JSON, and field validation errors.

5. **🟡 Test Cosmos DB stores** (`CosmosFactKeywordStore`, `CosmosSocialMediaQueueStore`) — Mock `CosmosClient`/`Container` to verify upsert payloads, partition key usage, query construction, and exception mapping. Alternatively, add integration tests against the [Cosmos DB emulator](https://learn.microsoft.com/en-us/azure/cosmos-db/local-emulator).

6. **🟡 Add `[Theory]` tests for `UsPostingScheduler`** — The scheduling logic (time windows, jitter, gap enforcement) is complex pure logic with no external dependencies. Parameterize with `[InlineData]` for different dates, edge times (midnight, DST boundary), and platform combinations.

7. **🟡 Fix the `ContentFormatterService` test gap** — `ContentFormatterServiceTests` tests the old `FormatPostHtml(CarFactsResponse, ...)` overload. Add tests for the new `FormatPostHtml(RawCarFactsContent, SeoMetadata, ..., backlinks, relatedPosts)` overload — especially the backlink and related post HTML sections, which are currently completely untested.

8. **🟡 Split `ActivityTests.cs`** — At 616 lines covering 11 activity classes, this file should be split into per-activity test files (e.g., `ExecuteScheduledPostActivityTests.cs`, `StoreSocialMediaQueueActivityTests.cs`). This improves CI failure attribution and reduces merge conflicts.

9. **🟢 Configure code coverage** — Add `<PackageReference Include="coverlet.collector" />` to the test project. Set a minimum coverage threshold (suggested: 60% for `CarFacts.Functions` to start) in CI to prevent regression.

10. **🟢 Test `SlugHelper` directly** — The slug/anchor ID generation logic is currently only tested indirectly through `StoreFactKeywordsActivity`. Add a dedicated `SlugHelperTests.cs` with `[Theory]` tests for edge cases: special characters, periods in model names (`BMW 3.0 CSL`), numeric-only models, very long names, and non-ASCII characters.

