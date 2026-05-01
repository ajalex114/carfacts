# Subscription Migration Runbook

**Purpose:** Move Car Facts Daily from one Azure subscription (or tenant/account) to another with zero downtime and zero reconnection to the old environment.

**Principle:** Everything connects via Managed Identity. No secrets embed subscription-specific resource names. After migration, nothing points back to the old subscription.

---

## Prerequisites

- [ ] Azure CLI installed and logged in (`az login`)
- [ ] AzCopy 10+ installed (`azcopy --version`)
- [ ] PowerShell 7+ (`pwsh --version`)
- [ ] Access to both source and target subscriptions
- [ ] Domain registrar access (to update DNS CNAME)
- [ ] GitHub repository access (to update SWA deployment token)

---

## Phase 1 — Provision New Environment

```bash
# 1. Create resource group in target subscription
az account set --subscription <TARGET_SUBSCRIPTION_ID>
az group create --name rg-carfacts --location eastus

# 2. Deploy Bicep
az deployment group create \
  --resource-group rg-carfacts \
  --template-file infra/main.bicep \
  --parameters \
      appNamePrefix=carfacts \
      azureOpenAIEndpoint=<AOAI_ENDPOINT> \
      wordPressSiteId=<WP_SITE_ID>
```

**Outputs to note:**
- `keyVaultName` — for secret migration
- `functionAppName` — for deployment
- `staticWebAppName` — for GitHub Action token
- `blobStorageAccountName` — for AzCopy

---

## Phase 2 — Migrate Key Vault Secrets

```powershell
.\scripts\migrate-keyvault-secrets.ps1 `
    -SourceVaultName       "kv-carfacts" `
    -TargetVaultName       "kv-carfacts" `
    -SourceSubscriptionId  "<OLD_SUB_ID>" `
    -TargetSubscriptionId  "<NEW_SUB_ID>"
```

Verify all secrets present in target vault:
```bash
az keyvault secret list --vault-name kv-carfacts --subscription <NEW_SUB_ID> --output table
```

---

## Phase 3 — Migrate Cosmos DB Data

```powershell
.\scripts\migrate-data.ps1 `
    -SourceCosmosAccount   "cosmos-carfacts" `
    -TargetCosmosAccount   "cosmos-carfacts" `
    -SourceBlobAccount     "stblobcarfacts" `
    -TargetBlobAccount     "stblobcarfacts" `
    -SourceSubscriptionId  "<OLD_SUB_ID>" `
    -TargetSubscriptionId  "<NEW_SUB_ID>"
```

**Verify Cosmos DB:**
```bash
az cosmosdb sql container show \
  --account-name cosmos-carfacts \
  --database-name carfacts \
  --name posts \
  --subscription <NEW_SUB_ID>
```

---

## Phase 4 — Migrate Blob Storage (Images + Feeds)

The `migrate-data.ps1` script handles this via AzCopy. Verify:

```bash
# Count blobs in source
az storage blob list \
  --account-name stblobcarfacts --container-name post-images \
  --subscription <OLD_SUB_ID> --auth-mode login --output tsv | wc -l

# Count blobs in target (should match)
az storage blob list \
  --account-name stblobcarfacts --container-name post-images \
  --subscription <NEW_SUB_ID> --auth-mode login --output tsv | wc -l
```

---

## Phase 5 — Deploy Function App

```bash
az account set --subscription <NEW_SUB_ID>
cd src/CarFacts.Functions
dotnet publish -c Release -o ./publish
cd publish && zip -r function.zip . && cd ..
az functionapp deployment source config-zip \
  --name func-carfacts \
  --resource-group rg-carfacts \
  --src publish/function.zip
```

---

## Phase 6 — Deploy Static Web App

1. Get the new SWA deployment token:
   ```bash
   az staticwebapp secrets list --name swa-carfacts --subscription <NEW_SUB_ID> \
     --query properties.apiKey -o tsv
   ```
2. Update the GitHub repository secret `AZURE_STATIC_WEB_APPS_API_TOKEN` with the new token.
3. Trigger a deployment by pushing a commit or re-running the GitHub Action.

---

## Phase 7 — Smoke Tests (Pre-DNS)

Use the SWA's default hostname (before DNS cutover) to verify:

| Test | Expected |
|------|----------|
| `curl https://<swa-hostname>.azurestaticapps.net/` | Home page HTML, 200 |
| `curl https://<swa-hostname>.azurestaticapps.net/archive` | Archive page HTML, 200 |
| `curl https://<swa-hostname>.azurestaticapps.net/feed/` | RSS XML, `application/rss+xml` |
| `curl https://<swa-hostname>.azurestaticapps.net/sitemap_index.xml` | Sitemap XML, 200 |
| `curl https://<swa-hostname>.azurestaticapps.net/robots.txt` | Contains `Sitemap:` line |
| `curl https://<swa-hostname>.azurestaticapps.net/ads.txt` | Contains `google.com` |

---

## Phase 8 — DNS Cutover (Zero Downtime)

### Before cutting over

1. Set DNS TTL to **60 seconds** on the `carfactsdaily.com` record — at least 24 hours in advance.
2. Add the custom domain to the new SWA:
   ```bash
   az staticwebapp hostname set --name swa-carfacts --hostname carfactsdaily.com \
     --subscription <NEW_SUB_ID>
   ```
3. Note the CNAME value provided by the command output.

### Cutover

1. At your registrar / DNS provider, update the CNAME record:
   - **Old:** points to old SWA or WordPress CNAME
   - **New:** points to `<new-swa-name>.azurestaticapps.net`
2. Wait 60 seconds (TTL). Verify DNS propagation:
   ```bash
   nslookup carfactsdaily.com
   ```
3. Verify HTTPS:
   ```bash
   curl -I https://carfactsdaily.com/
   ```

### If something goes wrong

Revert DNS back to old CNAME within 60 seconds — the old site is still live.

---

## Phase 9 — Post-Cutover Verification

- [ ] `https://carfactsdaily.com/` loads correctly
- [ ] `https://carfactsdaily.com/archive` loads correctly
- [ ] A post detail page loads (e.g. a recent date/slug URL)
- [ ] `https://carfactsdaily.com/feed/` returns valid RSS
- [ ] `https://carfactsdaily.com/sitemap_index.xml` returns valid XML
- [ ] Google Search Console: submit new sitemap URL
- [ ] Bing Webmaster Tools: submit new sitemap URL
- [ ] Verify AdSense is loading (check browser devtools Network tab)
- [ ] Verify GA4 is firing (check GA4 Realtime report)

---

## Phase 10 — Decommission Old Environment

Wait **30 days** before decommissioning to allow:
- Search engine recrawl
- Analytics data continuity
- Any in-flight email/social links to resolve

After 30 days:

```bash
az account set --subscription <OLD_SUB_ID>

# Delete resource group (removes ALL resources in it)
az group delete --name rg-carfacts --yes --no-wait

# Or selectively:
az functionapp delete --name func-carfacts --resource-group rg-carfacts
az staticwebapp delete --name swa-carfacts --resource-group rg-carfacts
# Keep Key Vault in soft-delete for 7 more days (auto-purges)
```

---

## Appendix — Environment Variable Mapping (Old → New)

All app settings are set by `infra/main.bicep` automatically. The only values that change cross-subscription are:

| Setting | Source |
|---------|--------|
| `CosmosDb__AccountEndpoint` | New Cosmos DB endpoint (set by Bicep output) |
| `BlobStorage__AccountName` | New Blob account name (set by Bicep) |
| `KeyVault__VaultUri` | New Key Vault URI (set by Bicep) |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | New App Insights connection string |

No secrets are hardcoded. Managed Identity handles all auth — no connection strings change after deploy.
