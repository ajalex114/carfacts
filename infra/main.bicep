// ============================================================
// Car Facts Daily — Azure Infrastructure
// Replaces: infra/azuredeploy.json
// Deploy:   az deployment group create -g <rg> -f infra/main.bicep -p @infra/main.parameters.json
// ============================================================

@description('Prefix for all resource names (e.g. carfacts). Minimum 3 characters.')
@minLength(3)
param appNamePrefix string = 'carfacts'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Azure OpenAI resource endpoint URL.')
param azureOpenAIEndpoint string

@description('Azure OpenAI deployment name.')
param azureOpenAIDeploymentName string = 'gpt-4o-mini'

@description('WordPress.com site ID (e.g. yoursite.wordpress.com or numeric ID).')
param wordPressSiteId string

@description('CRON expression for the timer trigger (default: 6 AM UTC daily).')
param cronExpression string = '0 0 6 * * *'

@description('Public base URL for the live site.')
param siteBaseUrl string = 'https://carfactsdaily.com'

// ─── Resource name derivations ───────────────────────────────
var storageAccountName = 'st${replace(appNamePrefix, '-', '')}'
var blobStorageAccountName = 'stblob${replace(appNamePrefix, '-', '')}'
var functionAppName = 'func-${appNamePrefix}'
var appServicePlanName = 'asp-${appNamePrefix}'
var keyVaultName = 'kv-${appNamePrefix}'
var appInsightsName = 'ai-${appNamePrefix}'
var logAnalyticsName = 'log-${appNamePrefix}'
var cosmosAccountName = 'cosmos-${appNamePrefix}'
var staticWebAppName = 'swa-${appNamePrefix}'

// Built-in role definition IDs (stable across all tenants)
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
var cosmosBulkExecutorRoleId = '5bd9cd88-fe45-4216-938b-f97437e15450' // Cosmos DB Built-in Data Contributor
var cosmosReaderRoleId = '00000000-0000-0000-0000-000000000002'        // Cosmos DB Built-in Data Reader
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var storageBlobDataReaderRoleId = '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
var cognitiveServicesOpenAIUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad1285'

// ─── Log Analytics ────────────────────────────────────────────
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ─── App Insights ─────────────────────────────────────────────
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// ─── Storage Account (Functions internal — AzureWebJobsStorage) ──
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

// ─── Blob Storage Account (public: images + feeds) ────────────
resource blobStorageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: blobStorageAccountName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: blobStorageAccount
  name: 'default'
}

resource postImagesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'post-images'
  properties: {
    publicAccess: 'Blob'
  }
}

resource webFeedsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'web-feeds'
  properties: {
    publicAccess: 'Blob'
  }
}

// ─── Key Vault ─────────────────────────────────────────────────
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      name: 'standard'
      family: 'A'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

// ─── Cosmos DB ─────────────────────────────────────────────────
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: cosmosAccountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    capabilities: [{ name: 'EnableServerless' }]
    enableAutomaticFailover: false
    publicNetworkAccess: 'Enabled'
    disableKeyBasedMetadataWriteAccess: false
  }
}

resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmosAccount
  name: 'carfacts'
  properties: {
    resource: { id: 'carfacts' }
  }
}

resource factKeywordsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDatabase
  name: 'fact-keywords'
  properties: {
    resource: {
      id: 'fact-keywords'
      partitionKey: {
        paths: ['/keyword']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
      }
    }
  }
}

resource postsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDatabase
  name: 'posts'
  properties: {
    resource: {
      id: 'posts'
      partitionKey: {
        paths: ['/partitionKey']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [{ path: '/*' }]
        excludedPaths: [{ path: '/htmlContent/*' }]  // Don't index large HTML blob
      }
    }
  }
}

// ─── App Service Plan ──────────────────────────────────────────
resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: false
  }
}

// ─── Function App ──────────────────────────────────────────────
resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED'
          value: '1'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'KeyVault__VaultUri'
          value: keyVault.properties.vaultUri
        }
        // AI
        {
          name: 'AI__TextProvider'
          value: 'AzureOpenAI'
        }
        {
          name: 'AI__ImageProvider'
          value: 'StabilityAI'
        }
        {
          name: 'AI__AzureOpenAIEndpoint'
          value: azureOpenAIEndpoint
        }
        {
          name: 'AI__AzureOpenAIDeploymentName'
          value: azureOpenAIDeploymentName
        }
        {
          name: 'AI__AzureOpenAIApiVersion'
          value: '2025-01-01-preview'
        }
        // StabilityAI
        {
          name: 'StabilityAI__BaseUrl'
          value: 'https://api.stability.ai'
        }
        {
          name: 'StabilityAI__Model'
          value: 'stable-diffusion-xl-1024-v1-0'
        }
        {
          name: 'StabilityAI__Width'
          value: '1024'
        }
        {
          name: 'StabilityAI__Height'
          value: '1024'
        }
        {
          name: 'StabilityAI__Steps'
          value: '30'
        }
        {
          name: 'StabilityAI__CfgScale'
          value: '7'
        }
        // WordPress (kept for dual-publish cutover)
        {
          name: 'WordPress__SiteId'
          value: wordPressSiteId
        }
        {
          name: 'WordPress__PostStatus'
          value: 'publish'
        }
        // Cosmos DB
        {
          name: 'CosmosDb__AccountEndpoint'
          value: cosmosAccount.properties.documentEndpoint
        }
        {
          name: 'CosmosDb__DatabaseName'
          value: 'carfacts'
        }
        {
          name: 'CosmosDb__ContainerName'
          value: 'fact-keywords'
        }
        {
          name: 'CosmosDb__PostsContainerName'
          value: 'posts'
        }
        // Blob Storage (production — uses MI, not connection string)
        {
          name: 'BlobStorage__AccountName'
          value: blobStorageAccount.name
        }
        {
          name: 'BlobStorage__ImagesPublicBaseUrl'
          value: '${blobStorageAccount.properties.primaryEndpoints.blob}post-images'
        }
        {
          name: 'BlobStorage__ImagesContainerName'
          value: 'post-images'
        }
        {
          name: 'BlobStorage__WebFeedsContainerName'
          value: 'web-feeds'
        }
        // Schedule
        {
          name: 'Schedule__CronExpression'
          value: cronExpression
        }
        // Site
        {
          name: 'SiteBaseUrl'
          value: siteBaseUrl
        }
      ]
    }
  }
}

// ─── Static Web App ─────────────────────────────────────────────
resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    stagingEnvironmentPolicy: 'Enabled'
    allowConfigFileUpdates: true
    provider: 'GitHub'
    enterpriseGradeCdnStatus: 'Disabled'
  }
}

// ═══════════════════════════════════════════════════════════════
// Managed Identity Role Assignments
// ═══════════════════════════════════════════════════════════════

// func → Key Vault (Secrets User)
resource funcKvSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, functionApp.name, 'KvSecretsUser')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// func → Cosmos DB (Built-in Data Contributor)
resource funcCosmosContributor 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, functionApp.name, 'CosmosContributor')
  properties: {
    roleDefinitionId: '${cosmosAccount.id}/sqlRoleDefinitions/${cosmosBulkExecutorRoleId}'
    principalId: functionApp.identity.principalId
    scope: cosmosAccount.id
  }
}

// func → Blob Storage (Data Contributor — upload images, feeds)
resource funcBlobContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, functionApp.name, 'BlobContributor')
  scope: blobStorageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// func → Azure OpenAI (Cognitive Services OpenAI User)
// Note: scope is the resource group; narrow to the AOAI resource once its name is known.
resource funcOpenAIUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, functionApp.name, 'OpenAIUser')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesOpenAIUserRoleId)
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// swa → Cosmos DB (Built-in Data Reader)
resource swaCosmosReader 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, staticWebApp.name, 'CosmosReader')
  properties: {
    roleDefinitionId: '${cosmosAccount.id}/sqlRoleDefinitions/${cosmosReaderRoleId}'
    principalId: staticWebApp.identity.principalId
    scope: cosmosAccount.id
  }
}

// swa → Blob Storage (Data Reader — read images + feeds)
resource swaBlobReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, staticWebApp.name, 'BlobReader')
  scope: blobStorageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataReaderRoleId)
    principalId: staticWebApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ═══════════════════════════════════════════════════════════════
// Outputs
// ═══════════════════════════════════════════════════════════════
output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output staticWebAppName string = staticWebApp.name
output staticWebAppUrl string = 'https://${staticWebApp.properties.defaultHostname}'
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output cosmosAccountEndpoint string = cosmosAccount.properties.documentEndpoint
output blobStorageAccountName string = blobStorageAccount.name
output blobPublicEndpoint string = blobStorageAccount.properties.primaryEndpoints.blob
output appInsightsName string = appInsights.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
