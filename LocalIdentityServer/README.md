# LocalIdentityServer

# LocalIdentityServer

**企業級 OAuth2/OIDC 認證伺服器** - 基於 .NET 8 + SQLite，專為企業內部系統 SSO 認證設計。

## 🎯 主要功能

### 🏢 企業級特性

✅ **高安全性**: JWT RS256 簽章、PKCE 支援、BCrypt 密碼雜湊  
✅ **高可靠性**: EF Core + 資料庫持久化、Repository Pattern 架構  
✅ **可擴展性**: 模組化設計、依賴注入、SOLID 原則  
✅ **易維護性**: 完整錯誤處理、結構化日誌、清晰程式碼結構  

### 🔐 OAuth2/OIDC 核心功能

✅ **Authorization Code Flow**: 含 PKCE 安全增強  
✅ **OpenID Connect**: ID Token、UserInfo、Discovery 完整支援  
✅ **多種授權模式**: Client Credentials、Password Grant  
✅ **Token 管理**: JWT Access Token、ID Token、Refresh Token  
✅ **安全機制**: 一次性授權碼、權限範圍驗證、Token 輪替  

### 🏗️ 技術架構

✅ **現代技術棧**: ASP.NET Core 8.0、Entity Framework Core 9.0.8  
✅ **企業級資料庫**: SQLite (可升級至 PostgreSQL/SQL Server)  
✅ **設計模式**: Repository Pattern、Dependency Injection  
✅ **API 設計**: RESTful、OpenAPI 文件、端點分離  

## 🚀 快速啟動

### 系統需求

- .NET 8.0 SDK
- SQLite (已內建)

### 啟動步驟

1. 複製專案並還原套件：

```bash
git clone <repository-url>
cd LocalIdentityServer
dotnet restore
```

1. 建立資料庫 (自動執行)：

```bash
dotnet ef database update
```

1. 啟動伺服器：

```bash
dotnet run
```

1. 訪問伺服器：<http://localhost:5055>

### 內建測試資料

**測試用戶**：

- 用戶名：`alice` / 密碼：`password` (Admin 角色)
- 用戶名：`bob` / 密碼：`password` (User 角色)

**測試客戶端**：

- Client ID：`demo_client`
- Client Secret：`secret`
- Redirect URI：`https://localhost:5003/callback`

## 📋 主要端點

| 端點 | 功能 | 狀態 |
|------|------|------|
| `/.well-known/openid-configuration` | OIDC Discovery 文件 | ✅ 運行中 |
| `/.well-known/jwks.json` | JSON Web Key Set | ✅ 運行中 |
| `/connect/authorize` | OAuth2 授權端點 | ✅ 運行中 |
| `/connect/userinfo` | OIDC UserInfo 端點 | ✅ 運行中 |
| `/connect/token` | Token 發行端點 | 🔄 準備啟用 |
| `/login` | 使用者登入頁面 | ✅ 運行中 |

## 🏗️ 技術架構

### 核心技術棧

- **ASP.NET Core 8.0**: Minimal API 架構
- **Entity Framework Core 9.0.8**: ORM 與資料庫管理
- **SQLite**: 本地資料庫存儲
- **JWT**: JSON Web Token 實作
- **BCrypt.Net**: 密碼雜湊處理

### 設計模式

- **Repository Pattern**: 資料存取層抽象
- **Dependency Injection**: 依賴注入管理
- **SOLID 原則**: 面向物件設計原則
- **端點分離**: 模組化的 API 端點設計

### 專案結構

```text
LocalIdentityServer/
├── Data/
│   ├── Entities/          # EF Core 實體模型
│   ├── Repositories/      # Repository 介面與實作
│   └── LocalIdentityDbContext.cs
├── Endpoints/             # 模組化端點
│   ├── DiscoveryEndpoints.cs
│   ├── AuthorizationEndpoints.cs
│   ├── UserInfoEndpoints.cs
│   └── AuthenticationEndpoints.cs
├── Services/              # 核心服務
├── Models/                # 資料模型
└── localidentity.db       # SQLite 資料庫
```

## 🔐 安全特性

### 已實作

- ✅ **PKCE 支援**: 授權碼流程安全強化
- ✅ **JWT 簽章**: RSA 2048 位元金鑰
- ✅ **密碼雜湊**: BCrypt 安全儲存
- ✅ **一次性授權碼**: 防止重複使用
- ✅ **Scope 驗證**: 權限範圍控制

### 計劃強化

- 🔄 **Refresh Token Rotation**: 防止重放攻擊
- 🔄 **Rate Limiting**: 防止暴力破解
- 🔄 **金鑰持久化**: SQLite 金鑰存儲

## 📚 OAuth2/OIDC 流程範例

### 授權碼流程

1. **客戶端重導向至授權端點**:

```text
GET /connect/authorize?
    response_type=code&
    client_id=demo_client&
    redirect_uri=https://localhost:5003/callback&
    scope=openid profile&
    code_challenge=xyz&
    code_challenge_method=S256
```

1. **使用者登入並授權**
1. **取得授權碼並兌換 Token**:

```bash
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=authorization_code&
code=abc123&
client_id=demo_client&
client_secret=secret&
redirect_uri=https://localhost:5003/callback&
code_verifier=xyz
```

## 🧪 測試與開發

### 使用 Postman

專案包含 Postman Collection：`SimpleJwtApi.postman_collection.json`

### cURL 範例

```bash
# 取得 Discovery 文件
curl http://localhost:5055/.well-known/openid-configuration

# 取得 JWKS
curl http://localhost:5055/.well-known/jwks.json
```

## 🚧 開發狀態

### 已完成 (Milestone 1)

- ✅ EF Core + SQLite 完整遷移
- ✅ Repository Pattern 實作
- ✅ 端點模組化設計
- ✅ 基本 OAuth2/OIDC 功能
- ✅ 伺服器成功部署

### 進行中 (Milestone 2)

- 🔄 Token 端點完善與啟用
- 🔄 金鑰持久化實作
- 🔄 安全性強化

### 計劃中

- 📋 授權同意頁面 (Consent)
- 📋 登出功能實作
- 📋 單元測試與整合測試

## ⚠️ 使用注意事項

> **重要提醒**: 此專案為教學與開發測試用途，不建議直接用於生產環境。正式環境請考慮使用：
>
> - **IdentityServer**: .NET 生態成熟方案
> - **Keycloak**: 開源身份管理平台
> - **Auth0 / Azure AD B2C**: 雲端 SaaS 服務

### 安全考量

- 預設使用 HTTP (開發環境)，生產環境需啟用 HTTPS
- 金鑰目前存於記憶體，生產環境需實作金鑰持久化
- 密碼明文顯示於測試資料，生產環境需移除

## 📞 支援與貢獻

- 詳細開發計劃：參見 `DEV_PLAN.md`
- 問題回報：使用 GitHub Issues
- 功能建議：歡迎提交 Pull Request

## 🏗️ 技術架構

### 核心技術棧

- **ASP.NET Core 8.0**: Minimal API 架構
- **Entity Framework Core 9.0.8**: ORM 與資料庫管理
- **SQLite**: 本地資料庫存儲
- **JWT**: JSON Web Token 實作
- **BCrypt.Net**: 密碼雜湊處理

### 設計模式

- **Repository Pattern**: 資料存取層抽象
- **Dependency Injection**: 依賴注入管理
- **SOLID 原則**: 面向物件設計原則
- **端點分離**: 模組化的 API 端點設計

### 專案結構

```text
LocalIdentityServer/
├── Data/
│   ├── Entities/          # EF Core 實體模型
│   ├── Repositories/      # Repository 介面與實作
│   └── LocalIdentityDbContext.cs
├── Endpoints/             # 模組化端點
│   ├── DiscoveryEndpoints.cs
│   ├── AuthorizationEndpoints.cs
│   ├── UserInfoEndpoints.cs
│   └── AuthenticationEndpoints.cs
├── Services/              # 核心服務
├── Models/                # 資料模型
└── localidentity.db       # SQLite 資料庫
```

## 🔐 安全特性

### 已實作

- ✅ **PKCE 支援**: 授權碼流程安全強化
- ✅ **JWT 簽章**: RSA 2048 位元金鑰
- ✅ **密碼雜湊**: BCrypt 安全儲存
- ✅ **一次性授權碼**: 防止重複使用
- ✅ **Scope 驗證**: 權限範圍控制

### 計劃強化

- 🔄 **Refresh Token Rotation**: 防止重放攻擊
- 🔄 **Rate Limiting**: 防止暴力破解
- 🔄 **金鑰持久化**: SQLite 金鑰存儲

## 📚 OAuth2/OIDC 流程範例

### 授權碼流程

1. **客戶端重導向至授權端點**:

```text
GET /connect/authorize?
    response_type=code&
    client_id=demo_client&
    redirect_uri=https://localhost:5003/callback&
    scope=openid profile&
    code_challenge=xyz&
    code_challenge_method=S256
```

1. **使用者登入並授權**
1. **取得授權碼並兌換 Token**:

```bash
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=authorization_code&
code=abc123&
client_id=demo_client&
client_secret=secret&
redirect_uri=https://localhost:5003/callback&
code_verifier=xyz
```

## 🧪 測試與開發

### 使用 Postman

專案包含 Postman Collection：`SimpleJwtApi.postman_collection.json`

### cURL 範例

```bash
# 取得 Discovery 文件
curl http://localhost:5055/.well-known/openid-configuration

# 取得 JWKS
curl http://localhost:5055/.well-known/jwks.json
```

## 🚧 開發狀態

### 已完成 (Milestone 1)

- ✅ EF Core + SQLite 完整遷移
- ✅ Repository Pattern 實作
- ✅ 端點模組化設計
- ✅ 基本 OAuth2/OIDC 功能
- ✅ 伺服器成功部署

### 進行中 (Milestone 2)

- 🔄 Token 端點完善與啟用
- 🔄 金鑰持久化實作
- 🔄 安全性強化

### 計劃中

- 📋 授權同意頁面 (Consent)
- 📋 登出功能實作
- 📋 單元測試與整合測試

## ⚠️ 使用注意事項

> **重要提醒**: 此專案為教學與開發測試用途，不建議直接用於生產環境。正式環境請考慮使用：
>
> - **IdentityServer**: .NET 生態成熟方案
> - **Keycloak**: 開源身份管理平台
> - **Auth0 / Azure AD B2C**: 雲端 SaaS 服務

### 安全考量

- 預設使用 HTTP (開發環境)，生產環境需啟用 HTTPS
- 金鑰目前存於記憶體，生產環境需實作金鑰持久化
- 密碼明文顯示於測試資料，生產環境需移除

## 📞 支援與貢獻

- 詳細開發計劃：參見 `DEV_PLAN.md`
- 問題回報：使用 GitHub Issues
- 功能建議：歡迎提交 Pull Request
