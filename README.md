# .NET 應用程式解決方案集合

歡迎來到此 .NET 解決方案集合！這個 Repository 包含多個專案，旨在展示從企業級應用到特定功能示範的各種 .NET 技術應用。

## 📜 專案狀態

根據最新的[專案完成報告](./PROJECT_COMPLETION_REPORT.md)，主要專案的狀態如下：

- **信貸監控系統 (Credit Monitoring System)**: ✅ **開發完成**。Azure 雲端服務整合已完成，系統可完整編譯。
- **JWT 認證示範 (SimpleJwtWeb & SimpleJwtApi)**: ✅ **完全完成並已通過測試**。這是一個功能完整的 Blazor Web App + JWT API 認證範例。

---

## 📂 專案概覽

此 Repository 包含以下主要專案：

| 專案名稱 | 類型 | 技術棧 | 說明 |
| :--- | :--- | :--- | :--- |
| **CreditMonitoring.Web** | Blazor Server | .NET 8, SignalR | **核心應用**。提供信貸監控的儀表板、案件管理和報表功能。 |
| **CreditMonitoring.Api** | Web API | .NET 8, EF Core | 為 `CreditMonitoring.Web` 提供後端 RESTful API 服務。 |
| **AuthenticationServer** | Web API | .NET 8 | 一個獨立的、輕量級的 JWT **身份驗證伺服器**。 |
| **SimpleJwtApi** | Web API | .NET 8 | 一個**示範用 API**，展示如何使用 JWT 保護端點。 |
| **SimpleJwtWeb** | Blazor Web App | .NET 8, JWT | 一個**示範用前端**，展示如何整合 JWT 認證並與受保護的 API 互動。 |
| **CreditMonitoring.Functions** | Azure Functions | .NET 8 | 包含用於處理信用警報的 Azure Functions。 |
| **CreditMonitoring.Common** | 類別庫 | .NET 8 | 包含所有專案共用的資料模型、介面和服務。 |

---

## 🔐 安全性與身份驗證

本解決方案的核心是基於 **JSON Web Token (JWT)** 的現代身份驗證架構。

- **集中式認證**: `AuthenticationServer` 和 `SimpleJwtApi` 提供了兩種 JWT 身份驗證服務的實現範例。
- **無狀態與可擴展**: JWT 的無狀態特性使其非常適合微服務和分散式架構。
- **設計文件**: 我們為 JWT 的設計和實施準備了詳細的文件，涵蓋了從單體到大規模微服務架構的策略。
  - [**JWT 微服務身份驗證設計**](./JWT_Authentication_Design_Microservices.md)
  - [**大規模微服務 JWT 策略**](./Large_Scale_JWT_Authentication_Strategy.md)

---

## 🚀 快速開始：JWT 認證示範

這是一個快速指南，讓您可以在本機運行 `SimpleJwtApi` 和 `SimpleJwtWeb`，體驗完整的 JWT 認證流程。

### 1. 啟動後端 API (`SimpleJwtApi`)

首先，開啟一個終端機，進入 `SimpleJwtApi` 目錄並啟動它。

```bash
cd SimpleJwtApi
dotnet run
```
API 將會運行在 `http://localhost:5000` (或 `https://localhost:5001`)。您可以透過 `http://localhost:5000/swagger` 查看 API 文件。

### 2. 啟動前端應用程式 (`SimpleJwtWeb`)

接著，開啟**另一個**終端機，進入 `SimpleJwtWeb` 目錄並啟動它。

```bash
cd SimpleJwtWeb
dotnet run
```
Web 應用程式將會運行在 `http://localhost:5155` (或指定的其他埠口)。

### 3. 測試認證流程

1.  在瀏覽器中開啟 `http://localhost:5155`。
2.  點擊導覽列上的 **"Login"**。
3.  使用以下任一組憑證登入：
    - **管理員**: `admin` / `123456`
    - **一般使用者**: `user` / `123456`
4.  登入成功後，您將被重新導向到顯示您個人資訊的頁面。
5.  您可以嘗試存取不同的受保護頁面：
    - **Public Info**: 無需登入即可存取。
    - **My Profile**: 登入後即可存取。
    - **Admin Only**: 僅限管理員 (`admin`) 存取。

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

在專案根目錄執行：
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

此專案採用 [MIT License](LICENSE) 授權。

---

**注意**: `CreditMonitoring` 系統設計用於處理敏感的金融資料，請確保在生產環境中採用適當的安全措施和資料保護機制。