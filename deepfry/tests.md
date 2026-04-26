# Test Coverage Analysis

<!-- deepfry:commit=b9be8dc1501e31ea9edfa99c938527818fa2aca5 agent=test-analyzer timestamp=2026-04-24T11:00:00Z -->

## Test Frameworks

| Framework | Version | Config File | Test Command |
|-----------|---------|-------------|--------------|
| xUnit | 2.9.2 | `tests/CarFacts.Functions.Tests/CarFacts.Functions.Tests.csproj` | `dotnet test` |
| Moq | 4.20.72 | (same csproj) | — |
| FluentAssertions | 6.12.2 | (same csproj) | — |
| Microsoft.NET.Test.Sdk | 17.11.1 | (same csproj) | — |

## Test Inventory

- **Total test files**: 6 (+ 2 helper/utility files)
- **Total test methods**: 58 `[Fact]` methods (0 `[Theory]` methods)
- **Framework**: xUnit with Moq for mocking and FluentAssertions for assertions
- **Target framework**: .NET 8.0

### Test File Breakdown

| Test File | `[Fact]` Count | Class |
|-----------|---------------|-------|
| `tests/.../Functions/ActivityTests.cs` | 18 | `ActivityTests` |
| `tests/.../Services/ContentFormatterServiceTests.cs` | 14 | `ContentFormatterServiceTests` |
| `tests/.../Services/ImageGenerationServiceTests.cs` | 8 | `ImageGenerationServiceTests` |
| `tests/.../Services/WordPressServiceTests.cs` | 8 | `WordPressServiceTests` |
| `tests/.../Services/ContentGenerationServiceTests.cs` | 5 | `ContentGenerationServiceTests` |
| `tests/.../Services/FallbackImageGenerationServiceTests.cs` | 5 | `FallbackImageGenerationServiceTests` |

### Test Helpers

| File | Purpose |
|------|---------|
| `tests/.../Helpers/FakeHttpMessageHandler.cs` | HTTP message handler stub for testing HTTP-based services |
| `tests/.../Helpers/TestDataBuilder.cs` | Builder/factory for test data (`CarFactsResponse`, `GeneratedImage`, WP responses) |
| `tests/.../GlobalUsings.cs` | Global `using Xunit;` import |

## Test Type Distribution

| Type | Count | Percentage | Notes |
|------|-------|------------|-------|
| Unit | 58 | 100% | All tests mock dependencies and test single classes in isolation |
| Integration | 0 | 0% | No tests against real databases, APIs, or Azure services |
| E2E | 0 | 0% | No end-to-end tests (Playwright, Selenium, etc.) |

All 58 tests are **unit tests**. They use Moq to isolate dependencies (IChatCompletionService, IWordPressService, ISecretProvider, etc.) and `FakeHttpMessageHandler` to stub HTTP calls. No tests connect to real CosmosDB, Key Vault, WordPress API, Stability AI, or social media APIs.

## Coverage Map

### ✅ Tested Modules

| Source File/Module | Test File(s) | Test Count | Quality |
|-------------------|-------------|------------|---------|
| `src/.../Services/ContentFormatterService.cs` | `tests/.../Services/ContentFormatterServiceTests.cs` | 14 | ✅ Excellent — thorough HTML output validation, edge cases (empty media, special chars) |
| `src/.../Services/ContentGenerationService.cs` | `tests/.../Services/ContentGenerationServiceTests.cs` | 5 | ✅ Good — happy path, markdown-wrapped JSON, validation errors, service call verification |
| `src/.../Services/ImageGenerationService.cs` | `tests/.../Services/ImageGenerationServiceTests.cs` | 8 | ✅ Excellent — image count, filenames, base64 decode, auth header, endpoint, rate limit retry, secret retrieval |
| `src/.../Services/FallbackImageGenerationService.cs` | `tests/.../Services/FallbackImageGenerationServiceTests.cs` | 5 | ✅ Good — primary success, primary fail → fallback, both fail, empty result → fallback, cancellation |
| `src/.../Services/WordPressService.cs` | `tests/.../Services/WordPressServiceTests.cs` | 8 | ✅ Good — upload images, create post, correct API endpoint, bearer auth, error handling, secret retrieval |
| `src/.../Functions/Activities/GenerateRawContentActivity.cs` | `tests/.../Functions/ActivityTests.cs` | 1 | ✅ OK — basic delegation test |
| `src/.../Functions/Activities/GenerateSeoActivity.cs` | `tests/.../Functions/ActivityTests.cs` | 1 | ✅ OK — basic delegation test |
| `src/.../Functions/Activities/GenerateAllImagesActivity.cs` | `tests/.../Functions/ActivityTests.cs` | 2 | ✅ Good — success path + failure returns empty |
| `src/.../Functions/Activities/CreateDraftPostActivity.cs` | `tests/.../Functions/ActivityTests.cs` | 1 | ✅ OK — delegation test |
| `src/.../Functions/Activities/UploadSingleImageActivity.cs` | `tests/.../Functions/ActivityTests.cs` | 1 | ✅ OK — delegation test |
| `src/.../Functions/Activities/FormatAndPublishActivity.cs` | `tests/.../Functions/ActivityTests.cs` | 2 | ✅ Good — draft update path + new post path |
| `src/.../Functions/Activities/PublishSocialMediaActivity.cs` | `tests/.../Functions/ActivityTests.cs` | 1 | ✅ OK — basic success test |
| `src/.../Functions/Activities/StoreFactKeywordsActivity.cs` | `tests/.../Functions/ActivityTests.cs` | 2 | ✅ Good — store call verification + anchor ID correctness |
| `src/.../Functions/Activities/StoreSocialMediaQueueActivity.cs` | `tests/.../Functions/ActivityTests.cs` | 2 | ✅ Good — item count + multi-platform duplication |
| `src/.../Functions/Activities/ExecuteScheduledPostActivity.cs` | `tests/.../Functions/ActivityTests.cs` | 4 | ✅ Excellent — fact/link/reply/like paths, verifies post+delete+increment behavior |
| `src/.../Functions/Activities/GetEnabledPlatformsActivity.cs` | `tests/.../Functions/ActivityTests.cs` | 1 | ✅ OK — filters enabled platforms |

### ❌ Untested Modules

#### 🔴 High Priority (Business Logic / Core Services)

| Source File/Module | Type | Priority | Risk |
|-------------------|------|----------|------|
| `src/.../Services/TwitterService.cs` | Social Media Service | 🔴 High | Twitter API integration — posting, replying, liking tweets. Auth, rate limiting untested |
| `src/.../Services/FacebookService.cs` | Social Media Service | 🔴 High | Facebook API integration — posting to pages. Auth flow untested |
| `src/.../Services/RedditService.cs` | Social Media Service | 🔴 High | Reddit API integration — posting to subreddits. OAuth flow untested |
| `src/.../Services/PinterestService.cs` | Social Media Service | 🔴 High | Pinterest API integration — creating pins. Auth, board taxonomy untested |
| `src/.../Services/SeoGenerationService.cs` | Content Generation | 🔴 High | SEO metadata generation via AI — parsing, validation untested |
| `src/.../Services/SocialMediaPublisher.cs` | Orchestration | 🔴 High | Coordinates publishing across multiple platforms. Multi-service fan-out untested |
| `src/.../Services/TogetherAIImageGenerationService.cs` | AI Service | 🔴 High | Alternative image generation provider — API calls, response parsing untested |
| `src/.../Services/CosmosFactKeywordStore.cs` | Data Access | 🔴 High | Cosmos DB persistence — upserts, queries, increment operations untested |
| `src/.../Services/CosmosSocialMediaQueueStore.cs` | Data Access | 🔴 High | Cosmos DB queue — add/delete/query items untested |
| `src/.../Services/KeyVaultSecretProvider.cs` | Security | 🔴 High | Azure Key Vault secret retrieval — auth, caching, error handling untested |

#### 🟡 Medium Priority (Functions / Orchestrators)

| Source File/Module | Type | Priority | Risk |
|-------------------|------|----------|------|
| `src/.../Functions/CarFactsOrchestrator.cs` | Durable Orchestrator | 🟡 Medium | Main orchestration workflow — activity sequencing untested |
| `src/.../Functions/CarFactsTimerTrigger.cs` | Timer Trigger | 🟡 Medium | Entry point — timer binding and orchestrator start untested |
| `src/.../Functions/SocialMediaOrchestrator.cs` | Durable Orchestrator | 🟡 Medium | Social media generation workflow untested |
| `src/.../Functions/SocialMediaPostingTrigger.cs` | Timer Trigger | 🟡 Medium | Scheduled social media posting entry point untested |
| `src/.../Functions/ScheduledPostOrchestrator.cs` | Durable Orchestrator | 🟡 Medium | Scheduled post execution workflow untested |
| `src/.../Functions/ScheduledPostingOrchestrator.cs` | Durable Orchestrator | 🟡 Medium | Scheduled posting orchestration untested |
| `src/.../Functions/PinterestPostingOrchestrator.cs` | Durable Orchestrator | 🟡 Medium | Pinterest-specific posting flow untested |
| `src/.../Functions/PinterestPostingTrigger.cs` | Timer Trigger | 🟡 Medium | Pinterest posting trigger untested |
| `src/.../Functions/TweetReplyOrchestrator.cs` | Durable Orchestrator | 🟡 Medium | Tweet reply generation and posting flow untested |
| `src/.../Functions/TweetReplyTrigger.cs` | Timer Trigger | 🟡 Medium | Tweet reply trigger untested |
| `src/.../Functions/Activities/GenerateTweetFactsActivity.cs` | Activity | 🟡 Medium | Tweet fact generation activity untested |
| `src/.../Functions/Activities/GenerateTweetLinkActivity.cs` | Activity | 🟡 Medium | Tweet link post generation untested |
| `src/.../Functions/Activities/GenerateTweetLikeActivity.cs` | Activity | 🟡 Medium | Tweet like activity untested |
| `src/.../Functions/Activities/GenerateTweetReplyActivity.cs` | Activity | 🟡 Medium | Tweet reply generation untested |
| `src/.../Functions/Activities/GeneratePinContentActivity.cs` | Activity | 🟡 Medium | Pinterest pin content generation untested |
| `src/.../Functions/Activities/CreatePinterestPinActivity.cs` | Activity | 🟡 Medium | Pinterest pin creation untested |
| `src/.../Functions/Activities/SelectPinterestFactActivity.cs` | Activity | 🟡 Medium | Pinterest fact selection untested |
| `src/.../Functions/Activities/CreateWebStoryActivity.cs` | Activity | 🟡 Medium | Web story creation untested |
| `src/.../Functions/Activities/FindBacklinksActivity.cs` | Activity | 🟡 Medium | Backlink discovery untested |
| `src/.../Functions/Activities/GetPendingScheduledItemsActivity.cs` | Activity | 🟡 Medium | Queue query activity untested |
| `src/.../Functions/Activities/GetSocialMediaSettingsActivity.cs` | Activity | 🟡 Medium | Settings retrieval untested |
| `src/.../Functions/Activities/GetWebStoriesEnabledActivity.cs` | Activity | 🟡 Medium | Feature flag check untested |
| `src/.../Functions/Activities/IncrementSocialCountsActivity.cs` | Activity | 🟡 Medium | Counter increment untested |
| `src/.../Functions/Activities/StoreTweetReplyQueueActivity.cs` | Activity | 🟡 Medium | Tweet reply queue storage untested |
| `src/.../Functions/Activities/UpdatePinterestTrackingActivity.cs` | Activity | 🟡 Medium | Pinterest tracking update untested |

#### 🟢 Low Priority (Configuration / Models / Utilities)

| Source File/Module | Type | Priority | Risk |
|-------------------|------|----------|------|
| `src/.../Services/CachedImageGenerationService.cs` | Caching Decorator | 🟢 Low | Local-only caching wrapper — limited production risk |
| `src/.../Services/LocalSecretProvider.cs` | Dev Utility | 🟢 Low | Local config-based secrets — development-only |
| `src/.../Services/NullFactKeywordStore.cs` | Null Object | 🟢 Low | No-op implementation — trivial |
| `src/.../Services/NullSocialMediaQueueStore.cs` | Null Object | 🟢 Low | No-op implementation — trivial |
| `src/.../Helpers/SlugHelper.cs` | Utility | 🟢 Low | Anchor ID generation — tested indirectly via StoreFactKeywordsActivity |
| `src/.../Helpers/UsPostingScheduler.cs` | Utility | 🟢 Low | Time slot scheduling — complex logic but pure functions |
| `src/.../Prompts/PromptLoader.cs` | Utility | 🟢 Low | Embedded resource loader — simple I/O |
| `src/.../Configuration/AppSettings.cs` | Config POCOs | 🟢 Low | Settings classes — no logic |
| `src/.../Configuration/SecretNames.cs` | Constants | 🟢 Low | String constants — no logic |
| `src/.../Configuration/PinterestBoardTaxonomy.cs` | Config | 🟢 Low | Static board mapping — no logic |
| `src/.../Models/*.cs` (12 files) | Models/DTOs | 🟢 Low | Data classes — no business logic |
| `src/.../Program.cs` | Host Startup | 🟢 Low | DI registration — integration test candidate |
| `src/.../Services/Interfaces/*.cs` (11 files) | Interfaces | 🟢 Low | No implementation to test |

## Coverage Summary

- **Source modules** (non-interface, non-model .cs files): **55** total
- **Tested**: **16** (29%)
- **Untested**: **39** (71%)
- **Critical untested**: **10** files handling social media API auth/posting, Cosmos DB data access, Key Vault secrets, and AI service integration

> ⚠️ When excluding interfaces (11), models (12), config POCOs (3), and null objects (2): **27 substantive source files**, of which **16 are tested (59%)** and **11 are untested (41%)**.

## Test Anti-Patterns

| Pattern | Count | Details |
|---------|-------|---------|
| ❌ No assertions | **0** | All 58 tests use FluentAssertions (`.Should()`) or Moq `.Verify()` — no empty tests |
| ❌ Commented-out tests | **0** | No commented-out `[Fact]`, `[Theory]`, or `// test` blocks found |
| ❌ Flaky markers | **0** | No `Skip`, `[Trait("Category", "Flaky")]`, or retry attributes found |
| ❌ Excessive mocking (>5 deps) | **0** | Max mocked dependencies per test: 4 (`ExecuteScheduledPostActivity` tests mock `IFactKeywordStore`, `ISocialMediaQueueStore`, `ISocialMediaService`, `ITwitterService`) — within acceptable limits |
| ❌ Snapshot tests | **0** | No snapshot testing used |
| ⚠️ God test file | **1** | `ActivityTests.cs` (616 lines, 18 tests) covers 10+ different Activity classes in a single file using `#region` blocks. While organized, this conflates unrelated test subjects and makes test failure attribution harder |

### Positive Patterns Observed

| Pattern | Details |
|---------|---------|
| ✅ Meaningful test names | All tests follow `MethodName_Scenario_ExpectedBehavior` convention |
| ✅ Test data builder | Centralized `TestDataBuilder` provides consistent, realistic test data |
| ✅ Fake HTTP handler | `FakeHttpMessageHandler` enables request inspection (headers, URIs) without real HTTP calls |
| ✅ Arrange-Act-Assert | All tests clearly follow AAA pattern |
| ✅ Edge case coverage | Tests for rate limiting, empty results, API failures, markdown-wrapped JSON, special character encoding |
| ✅ Behavioral verification | Tests verify both return values and side effects (via `Moq.Verify`) |

## Test Infrastructure

| Aspect | Status | Details |
|--------|--------|---------|
| CI pipeline | ❌ **Not found** | `.github/workflows/` directory is empty. No CI/CD pipeline runs tests automatically |
| Coverage tool | ❌ **Not configured** | No Coverlet, ReportGenerator, or `<CollectCoverage>` in the test csproj |
| Test utilities | ✅ Present | `FakeHttpMessageHandler.cs` (HTTP stub), `TestDataBuilder.cs` (data factory) |
| Test data management | ✅ Present | `TestDataBuilder` provides consistent test data with configurable counts |
| Test isolation | ✅ Good | Each test constructs its own SUT and mocks. No shared mutable state between tests |
| `dotnet test` script | ❌ **Not configured** | No `Makefile`, build scripts, or helper scripts for running tests |

## Recommendations

1. **🔴 Add CI pipeline with test execution** — The `.github/workflows/` directory is empty. Create a GitHub Actions workflow that runs `dotnet test` on every PR and push. This is the single highest-impact improvement to prevent regressions.

2. **🔴 Test social media services** (`TwitterService`, `FacebookService`, `RedditService`, `PinterestService`) — These handle OAuth tokens, API rate limiting, and user-facing content posting. Use `FakeHttpMessageHandler` (already in the project) to test API call construction, auth headers, error responses, and rate limit handling without hitting real APIs.

3. **🔴 Test `SeoGenerationService`** — This service parses AI-generated SEO metadata. Test JSON parsing, validation of required fields, and handling of malformed AI responses (same pattern as `ContentGenerationServiceTests`).

4. **🟡 Test Cosmos DB stores** (`CosmosFactKeywordStore`, `CosmosSocialMediaQueueStore`) — These handle data persistence. Mock the `CosmosClient`/`Container` to test upsert logic, query construction, and error handling. Alternatively, add integration tests against the Cosmos DB emulator.

5. **🟡 Test `UsPostingScheduler` directly** — This class has complex scheduling logic (time windows, jitter, gap enforcement, clubbed slots). It's a pure function with no dependencies — ideal for thorough unit testing with `[Theory]` parameterized tests.

6. **🟡 Split `ActivityTests.cs` into per-activity test files** — The 616-line file covers 10+ activities. Split into `GenerateRawContentActivityTests.cs`, `ExecuteScheduledPostActivityTests.cs`, etc., to improve test discoverability and reduce merge conflicts.

7. **🟡 Add Durable Functions orchestrator tests** — `CarFactsOrchestrator`, `SocialMediaOrchestrator`, `ScheduledPostOrchestrator`, and other orchestrators coordinate the entire workflow but have zero test coverage. Use the Durable Task testing utilities to verify activity sequencing and error handling.

8. **🟢 Configure code coverage reporting** — Add Coverlet to the test project (`<PackageReference Include="coverlet.collector" />`) and generate coverage reports. Set a minimum threshold (e.g., 60%) to prevent coverage regression.

9. **🟢 Add `[Theory]` parameterized tests** — Currently all 58 tests are `[Fact]`. `ContentFormatterServiceTests` and `ContentGenerationServiceTests` would benefit from `[InlineData]` or `[MemberData]` to test multiple input variations more concisely.

10. **🟢 Test `KeyVaultSecretProvider`** — This handles Azure Key Vault secret retrieval for production. Mock `SecretClient` to verify secret caching, expiration handling, and error recovery.
