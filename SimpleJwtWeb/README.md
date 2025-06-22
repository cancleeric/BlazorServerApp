# SimpleJwtWeb - Blazor Web App with JWT Authentication

一個使用 Blazor Web App 技術和 JWT 驗證的現代化範例應用程式，展示如何在 .NET 8 中實現完整的身份驗證和授權系統。

## 🚀 專案特色

- **Blazor Web App** - 使用 .NET 8 最新的 Blazor Web App 模板
- **JWT Authentication** - 完整的 JWT token 驗證實現
- **Interactive Auto Mode** - 支援 Server 和 WebAssembly 混合渲染
- **響應式 UI** - 使用 Bootstrap 5 建立的現代化界面
- **自動 Token 管理** - 包含 token 刷新和自動登出功能
- **角色基礎存取控制** - 支援不同使用者角色的權限管理
- **LocalStorage 持久化** - 安全的 token 儲存機制

## 📋 系統需求

- .NET 8.0 SDK 或更高版本
- Visual Studio 2022 或 VS Code
- SimpleJwtApi 後端服務（用於 JWT token 驗證）

## 🛠️ 安裝與設定

### 1. 克隆專案
```bash
git clone [repository-url]
cd BlazorServerApp/SimpleJwtWeb
```

### 2. 還原 NuGet 套件
```bash
dotnet restore
```

### 3. 設定後端 API URL
編輯 `appsettings.json` 檔案，設定正確的 API 基礎 URL：

```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:7001"
  }
}
```

### 4. 確保後端服務運行
確保 SimpleJwtApi 專案正在運行在指定的 URL 上。

### 5. 執行應用程式
```bash
dotnet run
```

應用程式將在 `https://localhost:5001` 上啟動。

## 🏗️ 專案架構

```
SimpleJwtWeb/
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor      # 主要版面配置
│   │   └── NavMenu.razor         # 導航選單
│   └── Pages/
│       ├── Home.razor           # 首頁
│       ├── Login.razor          # 登入頁面
│       ├── ApiDemo.razor        # API 示範頁面
│       ├── Profile.razor        # 使用者資料頁面
│       └── About.razor          # 關於頁面
├── Models/
│   ├── LoginRequest.cs          # 登入請求模型
│   └── LoginResponse.cs         # 登入回應模型
├── Services/
│   ├── IApiService.cs           # API 服務介面
│   ├── ApiService.cs            # API 服務實現
│   ├── IAuthService.cs          # 驗證服務介面
│   └── AuthService.cs           # 驗證服務實現
└── wwwroot/
    ├── app.css                  # 自訂樣式
    └── bootstrap/               # Bootstrap 5 樣式檔案
```

## 🔐 驗證流程

### 登入流程
1. 使用者在登入頁面輸入憑證
2. 應用程式向後端 API 發送登入請求
3. 成功後接收 JWT token 和使用者資訊
4. Token 儲存在 LocalStorage 中
5. 設定 HTTP 客戶端的 Authorization 標頭
6. 導航到首頁

### Token 管理
- **自動添加**: 所有 API 請求自動包含 Authorization 標頭
- **過期檢查**: 定期檢查 token 是否過期
- **自動登出**: Token 過期時自動清除並重導向至登入頁面
- **安全儲存**: 使用 LocalStorage 安全儲存 token

## 📱 功能頁面

### 🏠 首頁 (Home)
- 歡迎訊息和專案介紹
- 快速導航連結
- 使用者狀態顯示

### 🔑 登入頁面 (Login)
- 使用者名稱和密碼輸入
- 記住登入狀態選項
- 錯誤訊息顯示
- 響應式設計

### 🛠️ API 示範 (ApiDemo)
- 公開 API 端點測試
- 受保護的 API 端點測試
- 管理員專用 API 端點測試
- 使用者專用 API 端點測試
- 即時回應顯示

### 👤 使用者資料 (Profile)
- 當前使用者資訊顯示
- JWT token 詳細資訊
- 角色和聲明展示
- Token 過期時間顯示

### ℹ️ 關於頁面 (About)
- 專案技術堆疊介紹
- 架構說明
- 開發團隊資訊

## 🔧 服務層

### AuthService
```csharp
public interface IAuthService
{
    Task<bool> LoginAsync(string username, string password);
    Task LogoutAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<string?> GetTokenAsync();
    Task<string?> GetUsernameAsync();
    Task<IEnumerable<string>> GetRolesAsync();
    event EventHandler<bool> AuthenticationStateChanged;
}
```

### ApiService
```csharp
public interface IApiService
{
    Task<string> GetAsync(string endpoint);
    Task<T?> GetAsync<T>(string endpoint);
    Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data);
}
```

## 🎨 UI 元件

### 導航選單功能
- **響應式設計**: 支援桌面和行動裝置
- **角色基礎顯示**: 根據使用者角色顯示不同選單項目
- **登入狀態指示**: 清楚顯示當前登入狀態
- **快速登出**: 一鍵登出功能

### 樣式特色
- **Bootstrap 5**: 現代化的 UI 框架
- **FontAwesome 圖示**: 豐富的圖示庫
- **自訂主題**: 專案專屬的色彩主題
- **響應式布局**: 適應各種螢幕尺寸

## 🧪 測試功能

### 可用的測試端點：
- `GET /api/test/public` - 公開端點
- `GET /api/test/protected` - 需要驗證的端點  
- `GET /api/test/admin` - 需要管理員角色
- `GET /api/test/user` - 需要使用者角色

### 測試帳號：
- **管理員**: `admin` / `admin123`
- **使用者**: `user` / `user123`

## 🔒 安全性功能

- **JWT Token 驗證**: 使用業界標準的 JWT tokens
- **角色基礎授權**: 支援多層級權限控制
- **自動 Token 清理**: 防止過期 token 累積
- **HTTPS 強制**: 生產環境強制使用 HTTPS
- **XSS 防護**: 內建跨站腳本攻擊防護

## 🚀 部署

### 開發環境
```bash
dotnet run --environment Development
```

### 生產環境
```bash
dotnet publish -c Release
```

## 📄 相依套件

- **Microsoft.AspNetCore.Authentication.JwtBearer** - JWT 驗證
- **System.IdentityModel.Tokens.Jwt** - JWT token 處理
- **Microsoft.AspNetCore.Components.WebAssembly** - Blazor WebAssembly 支援

## 🤝 貢獻指南

1. Fork 此專案
2. 建立功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交變更 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 開啟 Pull Request

## 📝 授權條款

此專案採用 MIT 授權條款 - 詳見 [LICENSE](LICENSE) 檔案

## 📞 聯絡資訊

如有任何問題或建議，請透過以下方式聯絡：

- 專案問題: [GitHub Issues](link-to-issues)
- 電子郵件: [your-email@example.com](mailto:your-email@example.com)

---

**注意**: 這是一個示範專案，主要用於展示 Blazor Web App 與 JWT 驗證的整合。在生產環境中使用前，請確保進行適當的安全性審核和測試。
