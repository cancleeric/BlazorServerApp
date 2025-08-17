# .NET 現代化應用程式解決方案集合

歡迎來到此 .NET 解決方案集合！這個 Repository 是一個全面的範例，旨在展示如何使用 .NET 8 建構從企業級應用到特定功能示範的各種現代化應用程式。

## 📜 專案狀態

根據最新的[專案完成報告](./PROJECT_COMPLETION_REPORT.md)，主要專案的狀態如下：

- **信貸監控系統 (Credit Monitoring System)**: ✅ **開發完成**。Azure 雲端服務整合已完成，系統可完整編譯。
- **JWT 認證示範 (SimpleJwtWeb & SimpleJwtApi)**: ✅ **完全完成並已通過測試**。這是一個功能完整的 Blazor Web App + JWT API 認證範例。

---

## 📂 專案概覽

此 Repository 包含多個專案，涵蓋了微服務、身份驗證、前端技術和雲端部署等領域。

| 專案名稱 | 類型 | 技術棧 | 說明 |
| :--- | :--- | :--- | :--- |
| **CreditMonitoring.Web** | Blazor Server | .NET 8, SignalR | **核心應用**。提供信貸監控的儀表板、案件管理和報表功能。 |
| **CreditMonitoring.Api** | Web API | .NET 8, EF Core | 為 `CreditMonitoring.Web` 提供後端 RESTful API 服務。 |
| **AuthenticationServer** | Web API | .NET 8, JWT | 一個獨立的、輕量級的 JWT **身份驗證伺服器**。 |
| **AuthCoreSdk** | 類別庫 | .NET 8 | 提供核心認證服務，包含使用者服務、JWT 生成和控制器邏輯。 |
| **BlazorAuthSdk** | 類別庫 | .NET 8 | Blazor 客戶端認證 SDK，封裝了 JWT 狀態管理和客戶端服務。 |
| **SimpleJwtApi** | Web API | .NET 8, JWT | 一個**示範用 API**，展示如何使用 JWT 保護端點。 |
| **SimpleJwtServerBlazor** | Blazor Server | .NET 8, JWT | 一個**示範用前端**，展示如何整合 JWT 認證並與受保護的 API 互動。 |
| **SimpleCookieWeb** | Blazor Web App | .NET 8 | 展示傳統 Cookie 身份驗證的 Blazor Web App。 |
| **LocalIdentityServer** | IdentityServer | .NET 8, SQLite | 一個輕量級的本地 OAuth2/OIDC 認證伺服器，用於開發測試。 |
| **MyIdentityServerWithAspNetIdentity** | IdentityServer | .NET 8, Duende IdentityServer | 使用 ASP.NET Identity 的完整 IdentityServer 實作。 |
| **BlazorWebAppWithAuthCoreSdk** | Blazor Web App | .NET 8 | 整合 `AuthCoreSdk` 進行身份驗證的 Blazor Web App。 |
| **BlazorServerPureDemo** | Blazor Server | .NET 8 | 一個純粹的 Blazor Server 專案範本。 |
| **BlazorWebAppPureDemo** | Blazor Web App | .NET 8 | 一個純粹的 Blazor Web App 專案範本。 |
| **CreditMonitoring.Functions** | Azure Functions | .NET 8 | 包含用於處理信用警報的 Azure Functions。 |
| **CreditMonitoring.Common** | 類別庫 | .NET 8 | 包含所有專案共用的資料模型、介面和服務。 |
| **CreditMonitoring.Tests** | xUnit | .NET 8 | 包含信貸監控系統的整合測試。 |
| **Azure** | IaC | Bicep, SQL | 包含 Azure 基礎設施部署腳本和資料庫遷移腳本。 |

---

## 🔐 安全性與身份驗證

本解決方案的核心是基於 **JSON Web Token (JWT)** 的現代身份驗證架構，同時也提供了其他方案作為比較。

- **集中式認證**: `AuthenticationServer` 和 `LocalIdentityServer` 提供了兩種不同複雜度的 JWT 身份驗證服務。
- **無狀態與可擴展**: JWT 的無狀態特性使其非常適合微服務和分散式架構。
- **設計文件**: 我們為 JWT 的設計和實施準備了詳細的文件，涵蓋了從單體到大規模微服務架構的策略。
  - [**JWT 微服務身份驗證設計**](./JWT_Authentication_Design_Microservices.md)
  - [**大規模微服務 JWT 策略**](./Large_Scale_JWT_Authentication_Strategy.md)

### 多種認證實作範例

這個 Repository 展示了多種身份驗證方法，您可以根據需求選擇最適合的方案：

- **JWT (JSON Web Token)**:
  - `SimpleJwtApi` + `SimpleJwtServerBlazor`: 一個完整的 JWT 客戶端與伺服器實作。
  - `AuthenticationServer` + `AuthCoreSdk` + `BlazorAuthSdk`: 一個更模組化的 JWT 認證架構，將核心邏輯封裝在 SDK 中。
- **Cookie-based Authentication**:
  - `SimpleCookieWeb`: 一個使用傳統 Cookie 進行身份驗證的 Blazor Web App。
- **IdentityServer (OAuth2 / OIDC)**:
  - `LocalIdentityServer`: 一個輕量級的本地開發用 IdentityServer。
  - `MyIdentityServerWithAspNetIdentity`: 一個使用 Duende IdentityServer 和 ASP.NET Identity 的完整 OpenID Connect 和 OAuth 2.0 解決方案。

---

## 🚀 快速開始：JWT 認證示範

這是一個快速指南，讓您可以在本機運行 `SimpleJwtApi` 和 `SimpleJwtServerBlazor`，體驗完整的 JWT 認證流程。

### 1. 啟動後端 API (`SimpleJwtApi`)

首先，開啟一個終端機，進入 `SimpleJwtApi` 目錄並啟動它。

```bash
cd SimpleJwtApi
dotnet run
```
API 將會運行在 `https://localhost:7001`。您可以透過 `https://localhost:7001/swagger` 查看 API 文件。

### 2. 啟動前端應用程式 (`SimpleJwtServerBlazor`)

接著，開啟**另一個**終端機，進入 `SimpleJwtServerBlazor` 目錄並啟動它。

```bash
cd SimpleJwtServerBlazor
dotnet run
```
Web 應用程式將會運行在 `https://localhost:7229` (或指定的其他埠口)。

### 3. 測試認證流程

1.  在瀏覽器中開啟前端應用的 URL。
2.  點擊導覽列上的 **"Login"**。
3.  使用以下任一組憑證登入：
    - **管理員**: `admin` / `123456`
    - **一般使用者**: `user` / `123456`
4.  登入成功後，您將被重新導向到首頁，並能看到個人化的歡迎訊息。
5.  您可以嘗試存取不同的受保護頁面來測試授權機制。

---

## 🏦 信貸監控系統 (Credit Monitoring System)

這是本解決方案的核心企業級應用。

### 🎯 專案目的

- **信用風險監控**：即時監控貸款帳戶的信用分數變化。
- **預警機制**：當客戶信用狀況出現惡化時自動發出警報。
- **案件管理**：統一管理信用惡化案件和相關問題傳票。
- **業務決策支援**：提供完整的信用資料供風險評估使用。

### 🛠️ 安裝與部署

#### 1. 還原 .NET 套件

在解決方案根目錄執行：
```bash
dotnet restore
```

#### 2. 設定資料庫連線

編輯 `CreditMonitoring.Api/appsettings.json`，設定您的資料庫連線字串。

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Your-Database-Connection-String"
  }
}
```

#### 3. 執行資料庫遷移

```bash
dotnet ef database update --project CreditMonitoring.Api
```

#### 4. 啟動應用程式

您需要同時啟動 API 和 Web 應用程式。

- **啟動 API 服務**:
  ```bash
  cd CreditMonitoring.Api
  dotnet run
  ```
- **啟動 Web 應用程式**:
  ```bash
  cd CreditMonitoring.Web
  dotnet run
  ```

---

## 🤝 貢獻指南

歡迎對本專案做出貢獻！請遵循以下步驟：

1.  Fork 此專案。
2.  建立您的功能分支 (`git checkout -b feature/AmazingFeature`)。
3.  提交您的變更 (`git commit -m 'Add some AmazingFeature'`)。
4.  將您的分支推送到遠端 (`git push origin feature/AmazingFeature`)。
5.  開啟一個 Pull Request。

## 📄 授權條款

此專案採用 MIT 授權條款。

---

**注意**: `CreditMonitoring` 系統設計用於處理敏感的金融資料，請確保在生產環境中採用適當的安全措施和資料保護機制。
