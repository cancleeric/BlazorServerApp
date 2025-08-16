# LocalIdentityServer 開發計劃

> 目標：打造一個本地可用、教學導向、符合 OAuth2 + OpenID Connect 核心概念的輕量身份與授權伺服器，支援 Access Token / ID Token 發行、Authorization Code Flow (含 PKCE)、Refresh Token、UserInfo、Discovery 與 JWKS。並保留擴充空間（Consent、外部登入、管理介面）。

---

## 1. 目前進度摘要 (Current Status - Milestone 1 已完成)

| 項目 | 狀態 | 備註 |
|------|------|------|
| Users / Clients InMemory | ✅ 完成 | 已建立 Entity 模型，並行使用中 |
| JWT 簽章 (RSA 2048) | ✅ 完成 | 已實作，規劃持久化至 SQLite |
| Discovery (`/.well-known/openid-configuration`) | ✅ 完成 | 模組化分離至 DiscoveryEndpoints.cs |
| JWKS (`/.well-known/jwks.json`) | ✅ 完成 | 模組化分離至 DiscoveryEndpoints.cs |
| Password Grant | ✅ 完成 | 僅示範，不建議生產 |
| Authorization Code Flow | ✅ 完成 | 含 PKCE、Nonce，模組化至 AuthorizationEndpoints.cs |
| `/connect/token` 多種 grant | 🔄 實作中 | AuthenticationEndpoints.cs 已建立，需啟用 |
| Refresh Token | ✅ 完成 | 基本功能，規劃加入 rotation |
| ID Token | ✅ 完成 | 基本 claims (sub,name,email,nonce) |
| `/connect/userinfo` | ✅ 完成 | 模組化分離至 UserInfoEndpoints.cs |
| Login (表單) | ✅ 完成 | Cookie Session `auth` |
| 錯誤處理統一 | ✅ 完成 | ErrorService + 錯誤頁面 |
| **EF Core + SQLite** | ✅ **完成** | **已建立 DbContext、實體模型、Repository Pattern** |
| **資料庫遷移** | ✅ **完成** | **InitialCreate 遷移已套用，SQLite 資料庫建立** |
| **Repository Pattern** | ✅ **完成** | **遵循 SOLID 原則，已實作介面與實作類別** |
| **端點模組化** | ✅ **完成** | **遵循 SRP，分離至 Endpoints/ 目錄** |
| **伺服器部署** | ✅ **完成** | **成功運行於 <http://localhost:5055>** |
| Logout | 未實作 | 規劃 `/connect/logout` |
| 授權同意(Consent) | 未實作 | 規劃互動頁面 |
| 金鑰持久化 | 未實作 | 規劃存入 SQLite |
| 測試策略 | 未建立 | 將加入單元 + 整合測試 |

---

## 2. 系統概觀

```text
Browser ──> /connect/authorize (驗證登入→產出授權碼)
   │                │
   │<─redirect(code)│
Client App ──(POST /connect/token)──> Tokens(Access/ID/Refresh)
   │
   ├─> 呼叫 API / Resource Server (附帶 Access Token)
   └─> (可用 refresh_token 續期)
```

---

## 3. 主要資料結構 (EF Core + SQLite) - 已完成實作

| 實體 | 欄位重點 | 實作狀態 |
|------|----------|----------|
| UserEntity | Id, UserName, Email, Roles, PasswordHash | ✅ **已完成**，包含密碼雜湊支援 |
| ClientEntity | ClientId, Secret, RedirectUri, AllowedScopes | ✅ **已完成**，支援 PKCE 設定 |
| AuthorizationCodeEntity | Code, ClientId, RedirectUri, Scope, Subject, Nonce, CodeChallenge, Exp | ✅ **已完成**，包含 PKCE 支援 |
| RefreshTokenEntity | Token, Subject, Scope, ExpiresAt, ClientId | ✅ **已完成**，準備 Rotation 機制 |
| PersistedKeyEntity | Id, Created, KeyId, Algorithm, Data | ✅ **已完成**，金鑰持久化模型 |

### 已實作的 Repository Pattern (遵循 SOLID 原則)

- **IUserRepository / UserRepository**: 使用者管理，支援密碼驗證
- **IClientRepository / ClientRepository**: OAuth 客戶端管理，支援 PKCE 驗證
- **IAuthorizationCodeRepository / AuthorizationCodeRepository**: 授權碼管理，支援一次性使用
- **IRefreshTokenRepository / RefreshTokenRepository**: 刷新令牌管理
- **IPersistedKeyRepository / PersistedKeyRepository**: 金鑰持久化 (準備實作)

### 已建立的端點模組化 (遵循 SRP 原則)

- **DiscoveryEndpoints.cs**: OIDC Discovery 文件和 JWKS 端點
- **AuthorizationEndpoints.cs**: OAuth2/OIDC 授權流程端點
- **UserInfoEndpoints.cs**: OIDC UserInfo 端點
- **AuthenticationEndpoints.cs**: Token 端點 (準備啟用)

### 資料庫設計原則 (遵循開發指導原則)

- **單一責任原則 (SRP)**: 每個實體專注單一職責 ✅ 已實作
- **開放封閉原則 (OCP)**: 使用 Repository Pattern 便於擴展 ✅ 已實作
- **依賴反轉原則 (DIP)**: 使用介面抽象資料存取層 ✅ 已實作
- **適當的索引設計**: 提升查詢效能 ✅ 已設計
- **外鍵約束**: 確保資料完整性 ✅ 已設計

---

## 4. 開發階段規劃 (Milestones)

## 4. 開發階段規劃 (Milestones) - 已完成 Milestone 1

### ✅ Milestone 1：EF Core + SQLite 遷移 (已完成)

- [x] 統一錯誤輸出格式 (RFC 6749)
- [x] 加入錯誤導向頁 (error, error_description)
- [x] Authorization Code 僅允許一次使用 (TakeAsync 移除後不可再用)
- [x] **安裝 EF Core + SQLite 套件**
- [x] **建立 DbContext 和實體模型**
- [x] **實作 Repository Pattern (遵循 SOLID 原則)**
- [x] **資料庫遷移腳本**
- [x] **Password Hashing (取代明文密碼)**
- [x] **更新所有 Store 使用 EF Core**
- [x] **端點模組化分離** (DiscoveryEndpoints, AuthorizationEndpoints, UserInfoEndpoints)
- [x] **伺服器成功部署** (運行於 <http://localhost:5055>)
- [ ] **金鑰持久化至 SQLite** (模型已建立，待實作)
- [ ] 補充單元測試：Code 生成 / Token Claims

**Milestone 1 成就總結**：

- 完整的 EF Core + SQLite 架構
- Repository Pattern 實作，遵循 SOLID 原則
- 端點模組化，提升可維護性
- 成功的資料庫遷移和部署

### 🔄 Milestone 2：安全性強化與 Token 端點完善 (當前重點)

- [ ] **啟用 AuthenticationEndpoints** (修復 Token 端點)
- [ ] **金鑰持久化至 SQLite** (使用 PersistedKeyEntity)
- [ ] **完全遷移至 Repository Pattern** (移除 InMemory Stores)
- [ ] Refresh Token Rotation + 重放防護
- [ ] 增加 State 驗證紀錄（避免 CSRF）
- [ ] Nonce 儲存與過期清理 (背景服務)
- [ ] 加入 Content-Security-Policy / SameSite / Secure Cookie
- [ ] **實作密碼雜湊驗證 (PasswordHasher)**
- [ ] **加入 Rate Limiting**
- [ ] **SQL Injection 防護驗證**
- [ ] 補充單元測試：Repository Pattern / Token 生成

### Milestone 3：使用者體驗與同意頁

- [ ] `/connect/authorize` 增加 Consent Page (Scope 勾選)
- [ ] 可設定 `Client.RequireConsent`
- [ ] 記住使用者已授權的 Client/Scope
- [ ] UI 改為 Razor Pages or Minimal + HTML Template 分離

### Milestone 4：登出與 Session 管理

- [ ] `/connect/logout` (前端 GET + POST) + Cookie SignOut
- [ ] 支援 post_logout_redirect_uri
- [ ] 增加 Session 資訊頁 `/session` (診斷用)

### Milestone 5：測試與工具

- [ ] Postman Collection 匯出與文件化
- [ ] 自動化整合測試：
  - 授權碼流程 (含 PKCE)
  - Refresh Token 續期
  - Client Credentials
  - UserInfo 保護驗證
- [ ] 負面測試：無效 client、錯誤 redirect_uri、過期 code、PKCE 不符

### Milestone 6：擴充功能

- [ ] 動態 Client 註冊 (POST /connect/register)
- [ ] JTI + Token 撤銷 (黑名單)
- [ ] 多 Audience 支援 (`aud` 陣列)
- [ ] 自訂 Claims Mapper (Profile Service)

---

## 5. Endpoint 詳細規格

| Endpoint | 需求 | 現狀 |
|----------|------|------|
| `/connect/authorize` | 支援 code、PKCE、nonce | ✅ 已完成，模組化至 AuthorizationEndpoints.cs |
| `/connect/token` | grant_type=password/authorization_code/refresh_token/client_credentials | 🔄 AuthenticationEndpoints.cs 已建立，需啟用 |
| `/connect/userinfo` | Bearer 驗證、回傳 OIDC claims | ✅ 已完成，模組化至 UserInfoEndpoints.cs |
| `/login` | 表單登入 | ✅ 已完成，支援 Cookie 認證 |
| `/logout` | (待實作) | 📋 規劃中，單點登出行為定義 |
| `/.well-known/openid-configuration` | Discovery | ✅ 已完成，模組化至 DiscoveryEndpoints.cs |
| `/.well-known/jwks.json` | 公鑰 | ✅ 已完成，模組化至 DiscoveryEndpoints.cs |

---

## 6. Token 與 Claims 策略

| 類型 | TTL | 內容 | 注意 |
|------|-----|------|------|
| Access Token | 30m | sub, username, email, role, scope | 未來可壓縮 (縮減 claim) |
| ID Token | 30m | sub, name, email, nonce | 加入 at_hash / c_hash (可選) |
| Refresh Token | 8h | subject, scope, client | Rotation 防重放 |

---

## 7. 安全性待辦 (Security To-Do)

- [ ] 改用 `PasswordHasher` + 雜湊密碼
- [ ] 強制 HTTPS / HSTS
- [ ] 加入 Rate Limit (授權碼 / token endpoint)
- [ ] 清理過期 code / refresh token 的背景工作 (HostedService)
- [ ] 支援金鑰輪替 (kid 切換 + 舊 key 保留驗證期)

---

## 8. 測試策略

| 類型 | 案例 | 工具 |
|------|------|------|
| 單元測試 | Code 生成、PKCE 驗證、Nonce 儲存 | xUnit |
| 整合測試 | 授權碼流程端到端 | WebApplicationFactory |
| 安全測試 | 無效 client、過期 code、重複使用 code | 自動化腳本 |
| 邊界測試 | Scope 空集合、過長 redirect_uri | 自訂案例 |

---

## 9. 風險與應對

| 風險 | 說明 | 應對 |
|------|------|------|
| 記憶體儲存易遺失 | 重啟即失效 | 改 DB / Redis |
| 無金鑰持久化 | Token 驗證失敗 | 建立金鑰檔案儲存 |
| Refresh 可被重用 | 無 rotation | 實作 rotation + 廢止舊 token |
| 缺少 CSRF 防護 | /login 表單攻擊 | Anti-forgery token |
| 缺少 Consent | User 無感授權 | 加 UI + 記錄授權歷史 |

---

## 15. Roadmap (已更新時間規劃)

| 階段 | 重點 | 狀態 |
|------|------|------|
| ✅ Milestone 1 | EF Core + Repository Pattern + 端點模組化 + 伺服器部署 | **已完成** |
| 🔄 Milestone 2 | Token 端點完善 + 安全性強化 + 金鑰持久化 | **進行中** |
| 📋 Milestone 3 | Consent / Logout / 改善 UX | 規劃中 |
| 📋 Milestone 4 | 測試全面化 + 動態 Client 註冊 PoC | 規劃中 |

---

## 11. 可替換/整合方向 (若改採成熟方案)

| 產品 | 優點 | 遷移策略 |
|------|------|----------|
| IdentityServer | .NET 友好、標準實作 | 先對齊資料模型 (Client / Scope) |
| Keycloak | 完整 IAM、管理介面 | 以 OIDC 設定導入，保留本地 API |
| Auth0 / Azure AD B2C | SaaS 快速 | 抽象 TokenService 界面 |

---

## 12. 待建立/重構的類別 (Refactor List)

| 類別/服務 | 用途 | 優先 |
|-----------|------|------|
| `ITokenService` | 產生 access/id/refresh | 高 |
| `ICodeStore` | 授權碼儲存介面 | 高 |
| `IKeyStore` | 金鑰載入/輪替 | 中 |
| `IClientStore` | Client 查詢 | 中 |
| `IUserStore` | 使用者查詢/驗證 | 中 |
| `IRefreshTokenStore` | Refresh 管理 | 高 |
| `ICurrentTimeProvider` | 測試可抽換 | 低 |

---

## 13. 開發原則

1. 端點回應遵循 RFC 6749 / OIDC 規範欄位
2. 拒絕資訊洩漏（不回傳多餘錯誤細節）
3. 模組化：Token / Code / Store 分離
4. 可測試性：抽換時間、儲存、簽章
5. 清晰日誌：授權碼簽發、Token 兌換、Refresh 使用

---

## 14. 下一步建議 (Immediate TODO - Milestone 2 進行中)

### 當前優先任務 (完成 Token 端點與安全強化)

- [ ] **啟用 AuthenticationEndpoints.cs** (修復 Program.cs 中的 Token 端點註冊)
- [ ] **實作金鑰持久化** (使用已建立的 PersistedKeyEntity)
- [ ] **完全遷移至 Repository** (移除剩餘的 InMemory Stores)
- [ ] **建立種子資料機制** (資料庫初始化時自動建立測試用戶和客戶端)
- [ ] **實作 Refresh Token Rotation**
- [ ] **加入密碼雜湊服務** (升級現有的密碼處理)

### 已完成成就 (Milestone 1)

- ✅ **EF Core + SQLite 完整架構**：包含實體模型、DbContext、遷移
- ✅ **Repository Pattern 實作**：遵循 SOLID 原則的介面與實作
- ✅ **端點模組化**：符合單一責任原則的檔案分離
- ✅ **資料庫建立與遷移**：InitialCreate 成功套用
- ✅ **伺服器部署**：成功運行於 <http://localhost:5055>
- ✅ **基本 OAuth2/OIDC 功能**：Discovery、JWKS、Authorization、UserInfo

### 遵循開發指導原則 (持續進行)

- **SOLID 原則**: Repository Pattern 實作依賴反轉 ✅ 已實作
- **錯誤處理**: 統一資料庫例外處理機制 ✅ 已建立
- **空值檢查**: 嚴格的參數驗證 🔄 持續改善
- **模組化設計**: 清晰的分層架構 ✅ 已實作
- **可測試性**: 使用介面抽象便於單元測試 ✅ 架構已建立

### 後續任務

- [ ] 統一錯誤輸出格式（建立 Error Helper）✅ 已完成
- [ ] 實作 `/error` 顯示授權錯誤頁並於 /connect/authorize 使用 ✅ 已完成  
- [ ] 實作 `/connect/logout` (Cookie SignOut + 重新導向)
- [ ] 將 Program.cs 內邏輯拆分成擴充方法 (builder.Services.AddLocalIdentityServer())
- [ ] 新增最基本單元測試專案 `LocalIdentityServer.Tests`

---
若同意以上計劃，可指示我要開始 Milestone 1 的重構與抽離程式碼。若需調整優先順序或加入新需求，請告知。
