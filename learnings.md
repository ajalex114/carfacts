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

## Quick Reference: Environment Detection

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
