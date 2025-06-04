# 信貸監控系統 Azure 部署指南

## 🚀 Azure 部署步驟

### 1. 先決條件

- Azure 訂閱帳戶
- Azure CLI 已安裝並登入
- .NET 8.0 SDK
- Azure Functions Core Tools v4

### 2. 資源部署

#### 2.1 使用 Bicep 範本部署基礎架構

```powershell
# 1. 登入 Azure
az login

# 2. 設定預設訂閱
az account set --subscription "YOUR_SUBSCRIPTION_ID"

# 3. 建立資源群組
az group create --name "rg-creditmonitoring-prod" --location "East Asia"

# 4. 部署 Bicep 範本
az deployment group create `
  --resource-group "rg-creditmonitoring-prod" `
  --template-file "Azure/azure-resources.bicep" `
  --parameters environment=prod appNamePrefix=creditmonitoring
```

#### 2.2 設定 Azure AD 應用程式註冊

```powershell
# 1. 建立應用程式註冊
az ad app create --display-name "CreditMonitoring-Web" `
  --web-redirect-uris "https://your-web-app.azurewebsites.net/signin-oidc" `
  --web-home-page-url "https://your-web-app.azurewebsites.net"

# 2. 取得應用程式 ID 和租戶 ID
az ad app list --display-name "CreditMonitoring-Web" --query "[0].{appId:appId,objectId:objectId}"
```

### 3. 設定 Key Vault 密鑰

```powershell
# 設定必要的密鑰
az keyvault secret set --vault-name "your-keyvault-name" --name "JwtSecretKey" --value "YOUR_JWT_SECRET_KEY"
az keyvault secret set --vault-name "your-keyvault-name" --name "AzureAdClientSecret" --value "YOUR_AAD_CLIENT_SECRET"
az keyvault secret set --vault-name "your-keyvault-name" --name "SqlConnectionString" --value "YOUR_SQL_CONNECTION_STRING"
```

### 4. 應用程式部署

#### 4.1 部署 API 應用程式

```powershell
# 1. 建置專案
dotnet publish CreditMonitoring.Api/CreditMonitoring.Api.csproj -c Release -o ./publish/api

# 2. 部署到 Azure App Service
az webapp deployment source config-zip `
  --resource-group "rg-creditmonitoring-prod" `
  --name "your-api-app-name" `
  --src "./publish/api.zip"
```

#### 4.2 部署 Web 應用程式

```powershell
# 1. 建置專案
dotnet publish CreditMonitoring.Web/CreditMonitoring.Web.csproj -c Release -o ./publish/web

# 2. 部署到 Azure App Service
az webapp deployment source config-zip `
  --resource-group "rg-creditmonitoring-prod" `
  --name "your-web-app-name" `
  --src "./publish/web.zip"
```

#### 4.3 部署 Azure Functions

```powershell
# 1. 建置 Functions 專案
dotnet publish CreditMonitoring.Functions/CreditMonitoring.Functions.csproj -c Release -o ./publish/functions

# 2. 部署到 Azure Functions
func azure functionapp publish "your-function-app-name" --csharp
```

### 5. 資料庫設定

#### 5.1 執行資料庫遷移

```powershell
# 1. 更新連接字串到生產資料庫
# 2. 執行 Entity Framework 遷移
dotnet ef database update --project CreditMonitoring.Api --connection "YOUR_PRODUCTION_CONNECTION_STRING"
```

#### 5.2 初始化種子資料

```powershell
# 可以透過 API 端點或直接 SQL 腳本來初始化測試資料
```

### 6. 設定應用程式設定

#### 6.1 API App Service 設定

```powershell
# 設定應用程式設定
az webapp config appsettings set --resource-group "rg-creditmonitoring-prod" --name "your-api-app-name" --settings `
  "ASPNETCORE_ENVIRONMENT=Production" `
  "KeyVaultName=your-keyvault-name" `
  "APPLICATIONINSIGHTS_CONNECTION_STRING=your-app-insights-connection-string"
```

#### 6.2 Web App Service 設定

```powershell
# 設定應用程式設定
az webapp config appsettings set --resource-group "rg-creditmonitoring-prod" --name "your-web-app-name" --settings `
  "ASPNETCORE_ENVIRONMENT=Production" `
  "KeyVaultName=your-keyvault-name" `
  "ApiSettings__BaseUrl=https://your-api-app-name.azurewebsites.net" `
  "AzureAd__TenantId=your-tenant-id" `
  "AzureAd__ClientId=your-client-id"
```

### 7. 啟用 Managed Identity

```powershell
# 為 App Services 啟用系統分配的受控識別
az webapp identity assign --resource-group "rg-creditmonitoring-prod" --name "your-api-app-name"
az webapp identity assign --resource-group "rg-creditmonitoring-prod" --name "your-web-app-name"
az functionapp identity assign --resource-group "rg-creditmonitoring-prod" --name "your-function-app-name"
```

### 8. 設定 Key Vault 存取原則

```powershell
# 取得應用程式的 Principal ID
$apiPrincipalId = az webapp identity show --resource-group "rg-creditmonitoring-prod" --name "your-api-app-name" --query principalId -o tsv
$webPrincipalId = az webapp identity show --resource-group "rg-creditmonitoring-prod" --name "your-web-app-name" --query principalId -o tsv
$funcPrincipalId = az functionapp identity show --resource-group "rg-creditmonitoring-prod" --name "your-function-app-name" --query principalId -o tsv

# 設定 Key Vault 存取原則
az keyvault set-policy --name "your-keyvault-name" --object-id $apiPrincipalId --secret-permissions get list
az keyvault set-policy --name "your-keyvault-name" --object-id $webPrincipalId --secret-permissions get list
az keyvault set-policy --name "your-keyvault-name" --object-id $funcPrincipalId --secret-permissions get list
```

### 9. 監控設定

#### 9.1 設定 Application Insights 警報

```powershell
# 建立回應時間警報
az monitor metrics alert create `
  --name "High Response Time" `
  --resource-group "rg-creditmonitoring-prod" `
  --target-resource-id "/subscriptions/your-subscription-id/resourceGroups/rg-creditmonitoring-prod/providers/Microsoft.Web/sites/your-api-app-name" `
  --condition "avg requests/duration > 1000" `
  --description "API response time is too high"

# 建立錯誤率警報
az monitor metrics alert create `
  --name "High Error Rate" `
  --resource-group "rg-creditmonitoring-prod" `
  --target-resource-id "/subscriptions/your-subscription-id/resourceGroups/rg-creditmonitoring-prod/providers/Microsoft.Web/sites/your-api-app-name" `
  --condition "avg requests/failed > 5" `
  --description "API error rate is too high"
```

### 10. 安全性設定

#### 10.1 設定網路限制（可選）

```powershell
# 限制 API 只能被 Web App 存取
az webapp config access-restriction add `
  --resource-group "rg-creditmonitoring-prod" `
  --name "your-api-app-name" `
  --rule-name "WebAppOnly" `
  --priority 100 `
  --service-tag "AzureCloud"
```

#### 10.2 啟用 HTTPS Only

```powershell
# 強制 HTTPS
az webapp update --resource-group "rg-creditmonitoring-prod" --name "your-api-app-name" --https-only true
az webapp update --resource-group "rg-creditmonitoring-prod" --name "your-web-app-name" --https-only true
```

## 🔧 驗證部署

### 1. 健康檢查

```powershell
# 檢查 API 健康狀態
curl https://your-api-app-name.azurewebsites.net/health

# 檢查 Web 應用程式
curl https://your-web-app-name.azurewebsites.net
```

### 2. 功能測試

1. 訪問 Web 應用程式
2. 進行 Azure AD 登入
3. 測試信貸監控功能
4. 驗證即時通知功能

### 3. 監控檢查

1. 檢查 Application Insights 資料
2. 驗證 Service Bus 訊息處理
3. 確認 Key Vault 存取正常

## 📝 後續維護

### 定期任務

1. 檢查和更新 SSL 憑證
2. 監控 Azure 成本
3. 審查安全性設定
4. 備份重要資料
5. 更新依賴項和安全修補程式

### 效能最佳化

1. 啟用 CDN（如需要）
2. 設定自動縮放規則
3. 最佳化資料庫查詢
4. 監控並調整 Service Bus 設定

## 🚨 疑難排解

### 常見問題

1. **Key Vault 存取被拒絕**
   - 檢查 Managed Identity 是否已啟用
   - 確認 Key Vault 存取原則設定正確

2. **資料庫連接失敗**
   - 檢查防火牆規則
   - 確認連接字串正確
   - 驗證 Managed Identity 對 SQL Server 的權限

3. **Service Bus 訊息處理失敗**
   - 檢查連接字串
   - 確認佇列存在
   - 驗證 Functions 權限

4. **SignalR 連接問題**
   - 檢查 CORS 設定
   - 確認 WebSocket 支援已啟用
   - 驗證認證設定
