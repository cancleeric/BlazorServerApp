// Azure 資源部署範本 (Bicep)
// 這個範本將創建信貸監控系統所需的所有 Azure 資源

@description('資源群組的位置')
param location string = resourceGroup().location

@description('環境名稱 (dev, staging, prod)')
param environment string = 'dev'

@description('應用程式名稱前綴')
param appNamePrefix string = 'creditmonitoring'

// === 變數定義 ===
var uniqueSuffix = uniqueString(resourceGroup().id)
var appServicePlanName = '${appNamePrefix}-asp-${environment}'
var webAppName = '${appNamePrefix}-web-${environment}-${uniqueSuffix}'
var apiAppName = '${appNamePrefix}-api-${environment}-${uniqueSuffix}'
var sqlServerName = '${appNamePrefix}-sql-${environment}-${uniqueSuffix}'
var sqlDatabaseName = '${appNamePrefix}-db-${environment}'
var keyVaultName = '${appNamePrefix}-kv-${environment}-${uniqueSuffix}'
var appInsightsName = '${appNamePrefix}-ai-${environment}'
var serviceBusName = '${appNamePrefix}-sb-${environment}-${uniqueSuffix}'
var functionAppName = '${appNamePrefix}-func-${environment}-${uniqueSuffix}'
var storageAccountName = '${appNamePrefix}st${environment}${uniqueSuffix}'

// === App Service Plan ===
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: environment == 'prod' ? 'P1v3' : 'B1'
  }
  properties: {
    reserved: false
  }
}

// === Azure SQL Server ===
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: 'creditadmin'
    administratorLoginPassword: 'TempPassword123!' // 實際部署時應從 Key Vault 取得
    version: '12.0'
    publicNetworkAccess: 'Enabled'
  }
  
  resource firewallRule 'firewallRules@2023-05-01-preview' = {
    name: 'AllowAzureServices'
    properties: {
      startIpAddress: '0.0.0.0'
      endIpAddress: '0.0.0.0'
    }
  }
}

// === Azure SQL Database ===
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: environment == 'prod' ? 'S2' : 'S0'
  }
  properties: {
    collation: 'Chinese_Taiwan_Stroke_CI_AS'
  }
}

// === Key Vault ===
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: webApp.identity.principalId
        permissions: {
          secrets: ['get', 'list']
        }
      }
      {
        tenantId: subscription().tenantId
        objectId: apiApp.identity.principalId
        permissions: {
          secrets: ['get', 'list']
        }
      }
    ]
    enabledForTemplateDeployment: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

// === Application Insights ===
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Request_Source: 'rest'
  }
}

// === Service Bus ===
resource serviceBus 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {}
  
  resource creditAlertsQueue 'queues@2022-10-01-preview' = {
    name: 'credit-alerts'
    properties: {
      maxSizeInMegabytes: 1024
      defaultMessageTimeToLive: 'P14D'
    }
  }
  
  resource notificationsQueue 'queues@2022-10-01-preview' = {
    name: 'notifications'
    properties: {
      maxSizeInMegabytes: 1024
      defaultMessageTimeToLive: 'P14D'
    }
  }
}

// === Storage Account for Functions ===
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
  }
}

// === Function App ===
resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
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
          name: 'ServiceBusConnection'
          value: listKeys('${serviceBus.id}/AuthorizationRules/RootManageSharedAccessKey', serviceBus.apiVersion).primaryConnectionString
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
      ]
    }
  }
}

// === Web App (Blazor Frontend) ===
resource webApp 'Microsoft.Web/sites@2023-01-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      appSettings: [
        {
          name: 'ApiSettings__BaseUrl'
          value: 'https://${apiAppName}.azurewebsites.net'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'KeyVaultName'
          value: keyVaultName
        }
        {
          name: 'AzureAd__TenantId'
          value: subscription().tenantId
        }
        {
          name: 'AzureAd__ClientId'
          value: 'YOUR_AAD_CLIENT_ID' // 需要手動設定
        }
      ]
    }
  }
}

// === API App ===
resource apiApp 'Microsoft.Web/sites@2023-01-01' = {
  name: apiAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      appSettings: [
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: 'Server=tcp:${sqlServer.name}.database.windows.net,1433;Initial Catalog=${sqlDatabaseName};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'KeyVaultName'
          value: keyVaultName
        }
        {
          name: 'ServiceBusConnection'
          value: listKeys('${serviceBus.id}/AuthorizationRules/RootManageSharedAccessKey', serviceBus.apiVersion).primaryConnectionString
        }
        {
          name: 'AzureAd__TenantId'
          value: subscription().tenantId
        }
      ]
    }
  }
}

// === Key Vault 密鑰 ===
resource jwtSecretKey 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'JwtSecretKey'
  properties: {
    value: 'YourVerySecureJwtSecretKeyForProduction123456789ABCDEF'
  }
}

resource sqlConnectionString 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'SqlConnectionString'
  properties: {
    value: 'Server=tcp:${sqlServer.name}.database.windows.net,1433;Initial Catalog=${sqlDatabaseName};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
  }
}

// === 輸出 ===
output webAppUrl string = 'https://${webApp.properties.hostNames[0]}'
output apiAppUrl string = 'https://${apiApp.properties.hostNames[0]}'
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output keyVaultUri string = keyVault.properties.vaultUri
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey
output serviceBusNamespace string = serviceBus.properties.serviceBusEndpoint
