# 信用監控系統 (Credit Monitoring System)

## 專案概述

這是一個基於 **Blazor Server** 架構開發的信用監控系統，主要用於金融機構監控貸款帳戶的信用變化狀況。系統能夠即時追蹤客戶信用分數變化，並在信用惡化時發出警報，協助銀行業務人員及時採取風險控管措施。

## 🎯 專案目的

- **信用風險監控**：即時監控貸款帳戶的信用分數變化
- **預警機制**：當客戶信用狀況出現惡化時自動發出警報
- **案件管理**：統一管理信用惡化案件和相關問題傳票
- **保證人追蹤**：監控保證人的保證責任和信用狀況
- **業務決策支援**：提供完整的信用資料供風險評估使用

## 🏗️ 系統架構

```
CreditMonitoring/
├── CreditMonitoring.Web/          # Blazor Server 前端應用
├── CreditMonitoring.Api/          # Web API 後端服務
└── CreditMonitoring.Common/       # 共用模型和介面
```

### 主要組件

1. **前端 (Blazor Server)**
   - 信用監控儀表板
   - 惡化案件管理
   - 帳戶詳細資訊檢視
   - 案件詳細分析

2. **後端 (Web API)**
   - RESTful API 服務
   - 資料存取層
   - 業務邏輯處理

3. **共用程式庫**
   - 資料模型定義
   - 服務介面
   - 常用工具類別

## 📊 核心功能

### 1. 信用監控儀表板

- 顯示所有需要關注的貸款帳戶
- 信用分數變化趨勢
- 警報等級分類顯示
- 即時監控狀態

### 2. 惡化案件管理

- 信用惡化案件列表
- 案件嚴重程度分級
- 相關問題傳票追蹤
- 保證人資訊整合

### 3. 帳戶詳細資訊

- 完整的客戶貸款資訊
- 歷史信用分數記錄
- 擔保品清單
- 付款記錄

### 4. 風險警報系統

- 自動信用惡化偵測
- 多級警報機制
- 即時通知功能

## 💾 資料模型

### 主要實體

- **LoanAccount (貸款帳戶)**
  - 帳戶基本資訊
  - 貸款條件
  - 信用分數

- **CreditAlert (信用警報)**
  - 警報類型和描述
  - 信用分數變化
  - 嚴重程度等級

- **Guarantor (保證人)**
  - 保證人基本資料
  - 聯絡資訊
  - 保證關係

- **Voucher (傳票)**
  - 問題傳票編號
  - 相關金額
  - 處理狀態

- **Collateral (擔保品)**
  - 擔保品類型
  - 估值資訊
  - 設定位置

## 🔐 安全性功能

- **多重認證支援**：支援多種 OpenID Connect 提供者
- **授權控制**：基於角色的存取控制
- **身分驗證**：整合 Microsoft Identity 平台
- **會話管理**：安全的 Cookie 認證機制

## 🚀 技術架構

### 前端技術

- **Blazor Server**: .NET 8.0
- **Bootstrap**: 響應式UI框架
- **SignalR**: 即時通訊

### 後端技術

- **ASP.NET Core**: .NET 8.0
- **Entity Framework Core**: 資料存取
- **RESTful API**: HTTP/JSON 通訊

### 認證與授權

- **Microsoft Identity Web**
- **OpenID Connect**
- **Cookie Authentication**

## 📋 系統需求

### 開發環境

- **.NET SDK**: 8.0 或以上版本
- **Visual Studio 2022** 或 **VS Code**
- **SQL Server** 或相容的資料庫

### 執行環境

- **Windows/Linux/macOS**
- **.NET 8.0 Runtime**
- **Web Server**: IIS/Kestrel/Nginx

## 🛠️ 安裝與部署

### 1. 複製專案

```bash
git clone [repository-url]
cd BlazorServerApp
```

### 2. 還原套件

```bash
dotnet restore
```

### 3. 設定資料庫連線

編輯 `appsettings.json` 檔案：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Your-Database-Connection-String"
  },
  "ApiSettings": {
    "BaseUrl": "https://localhost:7001"
  }
}
```

### 4. 執行資料庫遷移

```bash
dotnet ef database update --project CreditMonitoring.Api
```

### 5. 啟動應用程式

#### 啟動 API 服務

```bash
cd CreditMonitoring.Api
dotnet run
```

#### 啟動 Web 應用程式

```bash
cd CreditMonitoring.Web
dotnet run
```

## 🔧 開發指南

### 專案結構說明

```
CreditMonitoring.Web/
├── Pages/                      # Blazor 頁面
│   ├── CreditMonitoring.razor  # 信用監控儀表板
│   ├── DeterioratedCases.razor # 惡化案件管理
│   ├── AccountDetails.razor    # 帳戶詳細資訊
│   └── CaseDetails.razor       # 案件詳細分析
├── Services/                   # 服務層
├── Models/                     # 視圖模型
├── Shared/                     # 共用組件
└── Extensions/                 # 擴展方法

CreditMonitoring.Api/
├── Controllers/                # API 控制器
├── Services/                   # 業務服務
├── Data/                       # 資料存取層
└── Program.cs                  # 應用程式進入點

CreditMonitoring.Common/
├── Models/                     # 共用資料模型
└── Interfaces/                 # 服務介面定義
```

### 新增功能步驟

1. 在 `Common` 專案中定義資料模型
2. 在 `Api` 專案中實作業務邏輯
3. 在 `Web` 專案中建立用戶介面
4. 更新相關的服務介面和依賴注入

## 📝 API 文件

### 主要端點

- `GET /api/creditmonitoring/accounts` - 取得所有帳戶
- `GET /api/creditmonitoring/accounts/{id}` - 取得特定帳戶
- `GET /api/creditmonitoring/accounts/{id}/alerts` - 取得帳戶警報
- `POST /api/creditmonitoring/accounts/{id}/monitoring` - 開始監控

## 🤝 貢獻指南

1. Fork 此專案
2. 建立功能分支 (`git checkout -b feature/amazing-feature`)
3. 提交變更 (`git commit -m 'Add some amazing feature'`)
4. 推送到分支 (`git push origin feature/amazing-feature`)
5. 開啟 Pull Request

## 📄 授權條款

此專案採用 [MIT License](LICENSE) 授權。

## 📞 聯絡資訊

如有任何問題或建議，請聯絡開發團隊。

---

**注意**: 此系統包含敏感的金融資料，請確保在生產環境中採用適當的安全措施和資料保護機制。
