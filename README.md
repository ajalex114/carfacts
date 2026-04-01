# CarFacts — Daily Car Facts to WordPress

An Azure Functions app that automatically publishes daily automotive history blog posts to WordPress with AI-generated content, images, and full SEO/GEO optimization.

## What It Does

Every day at 6 AM UTC, the function:

1. **Generates 5 car facts** for "on this day in history" via Azure OpenAI (GPT-4o-mini)
2. **Creates catchy titles** and SEO metadata (meta description, keywords, GEO summary)
3. **Generates 5 images** via Stability AI (SDXL 1024×1024)
4. **Publishes to WordPress** with schema markup, table of contents, FAQ section, and Yoast SEO fields

## Architecture

```
Timer Trigger (6 AM UTC)
    │
    ├── ContentGenerationService  →  Azure OpenAI (single call for facts + SEO)
    ├── ImageGenerationService    →  Stability AI (5 images in parallel)
    ├── ContentFormatterService   →  Build SEO/GEO HTML
    └── WordPressService          →  Upload images + create post
```

All secrets are stored in **Azure Key Vault** and accessed via Managed Identity.  
Telemetry is tracked via **Application Insights**.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- Azure subscription
- Azure OpenAI resource with a GPT-4o-mini deployment
- Stability AI API key ([platform.stability.ai](https://platform.stability.ai/))
- WordPress site with Application Passwords enabled

## Local Development

### 1. Restore and build

```bash
cd src/CarFacts.Functions
dotnet restore
dotnet build
```

### 2. Configure local settings

Edit `src/CarFacts.Functions/local.settings.json` and fill in:

| Setting | Description |
|---------|-------------|
| `KeyVault__VaultUri` | Your Key Vault URI |
| `AzureOpenAI__Endpoint` | Azure OpenAI resource endpoint |
| `AzureOpenAI__DeploymentName` | Deployment name (default: `gpt-4o-mini`) |
| `WordPress__SiteUrl` | Your WordPress site URL |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | App Insights connection string |

### 3. Add secrets to Key Vault

```bash
az keyvault secret set --vault-name kv-carfacts --name AzureOpenAI-ApiKey --value "YOUR_KEY"
az keyvault secret set --vault-name kv-carfacts --name StabilityAI-ApiKey --value "YOUR_KEY"
az keyvault secret set --vault-name kv-carfacts --name WordPress-Username --value "YOUR_USER"
az keyvault secret set --vault-name kv-carfacts --name WordPress-AppPassword --value "YOUR_APP_PWD"
```

### 4. Run locally

```bash
func start
```

## Deploy to Azure

### Option 1: ARM Template (recommended)

```bash
# Create resource group
az group create --name rg-carfacts --location eastus

# Deploy infrastructure
az deployment group create \
  --resource-group rg-carfacts \
  --template-file infra/azuredeploy.json \
  --parameters \
    azureOpenAIApiKey="YOUR_KEY" \
    stabilityAIApiKey="YOUR_KEY" \
    wordPressUsername="YOUR_USER" \
    wordPressAppPassword="YOUR_APP_PWD" \
    azureOpenAIEndpoint="https://YOUR_RESOURCE.openai.azure.com/" \
    wordPressSiteUrl="https://YOUR_SITE.com"

# Publish function code
cd src/CarFacts.Functions
func azure functionapp publish func-carfacts
```

### Option 2: Manual

1. Create resources manually in Azure Portal
2. Configure app settings to match `local.settings.json`
3. Grant the Function App's Managed Identity the **Key Vault Secrets User** role
4. Deploy via `func azure functionapp publish <name>`

## Resources Deployed (ARM Template)

| Resource | Name | Purpose |
|----------|------|---------|
| Resource Group | `rg-carfacts` | Container for all resources |
| Storage Account | `stcarfacts` | Azure Functions runtime storage |
| App Service Plan | `asp-carfacts` | Consumption plan (serverless) |
| Function App | `func-carfacts` | The application (system-assigned identity) |
| Key Vault | `kv-carfacts` | Stores all API keys and credentials |
| Application Insights | `ai-carfacts` | Telemetry and monitoring |
| Log Analytics Workspace | `log-carfacts` | Backing store for App Insights |

## Key Vault Secrets

| Secret Name | Description |
|-------------|-------------|
| `AzureOpenAI-ApiKey` | Azure OpenAI API key |
| `StabilityAI-ApiKey` | Stability AI API key |
| `WordPress-Username` | WordPress username |
| `WordPress-AppPassword` | WordPress application password |

## Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| `Schedule:CronExpression` | `0 0 6 * * *` | Daily at 6 AM UTC |
| `AzureOpenAI:Temperature` | `0.85` | AI creativity (0.0–1.0) |
| `WordPress:PostStatus` | `publish` | Set to `draft` to review first |
| `StabilityAI:Steps` | `30` | Image quality (more = better but slower) |

## Project Structure

```
carfacts/
├── src/CarFacts.Functions/
│   ├── Functions/              Timer-triggered entry point
│   ├── Models/                 DTOs (CarFact, CarFactsResponse, etc.)
│   ├── Services/               Business logic
│   │   └── Interfaces/         Abstractions (SOLID: Dependency Inversion)
│   ├── Configuration/          Strongly-typed settings + secret names
│   ├── Program.cs              DI composition root
│   └── host.json               Azure Functions host config
├── infra/
│   └── azuredeploy.json        ARM template
├── PreWork/                    Original n8n workflow documentation
└── README.md
```

## Cost Estimate

| Item | Monthly Cost |
|------|-------------|
| Azure Functions (Consumption) | Free tier (1M executions/month) |
| Azure OpenAI (GPT-4o-mini) | ~₹12-15 |
| Stability AI (5 images/day) | ~₹37-40 |
| Key Vault | ~₹0 (low usage) |
| App Insights | Free tier (5 GB/month) |
| **Total** | **~₹50-55/month** |
