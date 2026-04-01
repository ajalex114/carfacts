# CarFacts — Deployment Guide

Step-by-step instructions to deploy the Daily Car Facts Azure Functions app from scratch.

---

## Prerequisites

Before starting, ensure you have:

1. **Azure CLI** installed and logged in
   ```bash
   az --version        # Confirm installed (2.50+ recommended)
   az login            # Login to your Azure account
   az account show     # Verify correct subscription
   ```

2. **Azure Functions Core Tools v4**
   ```bash
   func --version      # Should show 4.x
   ```
   Install: https://learn.microsoft.com/azure/azure-functions/functions-run-local

3. **.NET 8 SDK**
   ```bash
   dotnet --version    # Should show 8.x
   ```

4. **API Keys ready** (you'll need these during deployment):
   - Azure OpenAI API key + endpoint URL
   - Stability AI API key ([platform.stability.ai](https://platform.stability.ai/))
   - WordPress username + application password

---

## Step 1: Get Your API Credentials

### Azure OpenAI
1. Go to [Azure Portal](https://portal.azure.com) → **Azure OpenAI** resource
2. Navigate to **Keys and Endpoint**
3. Copy **KEY 1** and **Endpoint**
4. Under **Model deployments**, note your deployment name (default: `gpt-4o-mini`)

### Stability AI
1. Go to [platform.stability.ai](https://platform.stability.ai/)
2. Navigate to **API Keys**
3. Create or copy your API key

### WordPress Application Password
1. Login to your WordPress admin panel
2. Go to **Users → Profile**
3. Scroll to **Application Passwords**
4. Enter a name (e.g., `carfacts-bot`) and click **Add New Application Password**
5. Copy the generated password (shown only once)

---

## Step 2: Deploy Infrastructure (ARM Template)

### Choose your region and create the resource group

```bash
# Pick a region (common options: eastus, westus2, westeurope, centralindia)
az group create \
  --name rg-carfacts \
  --location centralindia
```

### Deploy all Azure resources

From the project root directory:

```bash
az deployment group create \
  --resource-group rg-carfacts \
  --template-file infra/azuredeploy.json \
  --parameters \
    azureOpenAIApiKey="<your-azure-openai-key>" \
    stabilityAIApiKey="<your-stability-ai-key>" \
    wordPressUsername="<your-wp-username>" \
    wordPressAppPassword="<your-wp-app-password>" \
    azureOpenAIEndpoint="https://<your-resource>.openai.azure.com/" \
    wordPressSiteUrl="https://<your-site>.com"
```

**Optional parameters** (have sensible defaults):

| Parameter | Default | Description |
|-----------|---------|-------------|
| `appNamePrefix` | `carfacts` | Prefix for all resource names |
| `location` | Resource group location | Azure region |
| `azureOpenAIDeploymentName` | `gpt-4o-mini` | Your model deployment name |
| `cronExpression` | `0 0 6 * * *` | Schedule (default: 6 AM UTC daily) |

### What gets created

| Resource | Name | SKU / Tier |
|----------|------|------------|
| Storage Account | `stcarfacts` | Standard LRS |
| App Service Plan | `asp-carfacts` | Consumption (Y1 — serverless) |
| Function App | `func-carfacts` | .NET 8 isolated, system-assigned identity |
| Key Vault | `kv-carfacts` | Standard, RBAC-enabled |
| Application Insights | `ai-carfacts` | Web |
| Log Analytics Workspace | `log-carfacts` | PerGB2018, 30-day retention |
| RBAC Role Assignment | — | Key Vault Secrets User → Function App |

The ARM template also automatically:
- Stores all 4 secrets in Key Vault
- Configures the Function App's system-assigned managed identity
- Grants the Function App **Key Vault Secrets User** RBAC role
- Wires up Application Insights connection string

### Verify deployment

```bash
# Check the deployment succeeded
az deployment group show \
  --resource-group rg-carfacts \
  --name azuredeploy \
  --query properties.provisioningState

# List created resources
az resource list \
  --resource-group rg-carfacts \
  --output table
```

---

## Step 3: Build and Publish the Function App

```bash
# Navigate to the function project
cd src/CarFacts.Functions

# Restore and build
dotnet restore
dotnet build --configuration Release

# Publish to Azure
func azure functionapp publish func-carfacts
```

You should see output ending with:
```
Deployment completed successfully.
```

### Verify the function is registered

```bash
az functionapp function list \
  --name func-carfacts \
  --resource-group rg-carfacts \
  --output table
```

You should see `DailyCarFactsFunction` listed.

---

## Step 4: Verify It Works

### Trigger a manual test run

```bash
# Invoke the timer function manually
az functionapp function invoke \
  --name func-carfacts \
  --resource-group rg-carfacts \
  --function-name DailyCarFactsFunction
```

Alternatively, in the **Azure Portal**:
1. Go to **func-carfacts** → **Functions** → **DailyCarFactsFunction**
2. Click **Code + Test** → **Test/Run**
3. Click **Run**

### Check the logs

```bash
# Stream live logs
func azure functionapp logstream func-carfacts
```

Or view in Application Insights:
1. Go to **ai-carfacts** in Azure Portal
2. Click **Transaction search**
3. Filter by the last hour
4. Look for traces like:
   - `Car Facts pipeline started for March 21`
   - `Step 1/4: Generating content via Azure OpenAI`
   - `Step 2/4: Generating 5 images via Stability AI`
   - `✅ Published: <title> → <url>`

---

## Step 5: Verify the WordPress Post

1. Go to your WordPress site's admin panel
2. Navigate to **Posts**
3. You should see a new post with:
   - An AI-generated catchy title
   - 5 historical car facts with images
   - Schema.org structured data
   - Yoast SEO metadata filled in

---

## Post-Deployment Configuration

### Change the schedule

The default is 6 AM UTC daily. To change:

```bash
az functionapp config appsettings set \
  --name func-carfacts \
  --resource-group rg-carfacts \
  --settings "Schedule__CronExpression=0 0 8 * * *"
```

Common CRON expressions (Azure Functions uses 6-field NCrontab):
| Expression | Schedule |
|------------|----------|
| `0 0 6 * * *` | Daily at 6:00 AM UTC |
| `0 30 7 * * *` | Daily at 7:30 AM UTC |
| `0 0 6 * * 1-5` | Weekdays only at 6:00 AM |
| `0 0 */6 * * *` | Every 6 hours |

### Switch to draft mode

To review posts before they go live:

```bash
az functionapp config appsettings set \
  --name func-carfacts \
  --resource-group rg-carfacts \
  --settings "WordPress__PostStatus=draft"
```

### Update a secret

```bash
az keyvault secret set \
  --vault-name kv-carfacts \
  --name "StabilityAI-ApiKey" \
  --value "<new-key>"
```

No function restart needed — secrets are fetched at runtime.

---

## Local Development

For running locally before deploying:

### 1. Configure local settings

Edit `src/CarFacts.Functions/local.settings.json`:
- Set `KeyVault__VaultUri` to your Key Vault URI
- Set `AzureOpenAI__Endpoint` to your OpenAI endpoint
- Set `WordPress__SiteUrl` to your WordPress URL
- Set `APPLICATIONINSIGHTS_CONNECTION_STRING` (optional for local)

### 2. Authenticate to Key Vault locally

```bash
az login
```

`DefaultAzureCredential` will use your Azure CLI login to access Key Vault secrets during local development.

### 3. Run

```bash
cd src/CarFacts.Functions
func start
```

The timer won't fire immediately. To test, change the CRON to trigger sooner or use an HTTP test endpoint.

---

## Monitoring & Telemetry

### Application Insights Dashboard

1. Go to **ai-carfacts** in Azure Portal
2. Useful views:
   - **Live Metrics** — real-time execution data
   - **Failures** — any errors in the pipeline
   - **Performance** — execution duration per step
   - **Transaction search** — detailed traces per run

### Key Metrics to Watch

| Metric | Where | Healthy Value |
|--------|-------|---------------|
| Function success rate | App Insights → Failures | 100% |
| Execution duration | App Insights → Performance | < 120 seconds |
| Key Vault access | Key Vault → Metrics | No 403 errors |
| Daily invocations | App Insights → Usage | 1/day |

### Set Up Alerts (optional)

```bash
# Alert on function failures
az monitor metrics alert create \
  --name "carfacts-failure-alert" \
  --resource-group rg-carfacts \
  --scopes "/subscriptions/<sub-id>/resourceGroups/rg-carfacts/providers/Microsoft.Web/sites/func-carfacts" \
  --condition "count requests/failed > 0" \
  --window-size 1h \
  --evaluation-frequency 15m \
  --description "CarFacts function execution failed"
```

---

## Costs

| Resource | Estimated Monthly Cost |
|----------|----------------------|
| Azure Functions (Consumption) | ₹0 (free tier: 1M executions + 400K GB-s) |
| Azure OpenAI (GPT-4o-mini, ~30 calls) | ₹12–15 |
| Stability AI (150 images/month) | ₹37–40 |
| Key Vault (120 secret reads/month) | ₹0 |
| Application Insights (< 5 GB) | ₹0 (free tier) |
| Storage Account (minimal) | ₹1–2 |
| **Total** | **~₹50–57/month** |

---

## Teardown

To remove all resources:

```bash
az group delete --name rg-carfacts --yes --no-wait
```

This deletes everything in the resource group. Key Vault enters soft-delete for 7 days and can be purged:

```bash
az keyvault purge --name kv-carfacts --location centralindia
```

---

## Troubleshooting

| Problem | Cause | Fix |
|---------|-------|-----|
| Function not triggering | Wrong CRON expression | Verify `Schedule__CronExpression` in app settings |
| 403 from Key Vault | Missing RBAC role | Grant function's identity **Key Vault Secrets User** role |
| AI returns invalid JSON | Temperature too high | Lower `AzureOpenAI__Temperature` from 0.85 to 0.7 |
| Image generation fails | Invalid Stability AI key / rate limit | Check key in Key Vault; reduce to 3 images if rate-limited |
| WordPress upload fails | Bad credentials or URL | Verify `WordPress-Username` and `WordPress-AppPassword` in KV |
| Post missing SEO metadata | Yoast SEO not installed | Install Yoast SEO plugin on your WordPress site |
