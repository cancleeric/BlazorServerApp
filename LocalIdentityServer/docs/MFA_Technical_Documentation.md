# MFA (Multi-Factor Authentication) 技術文檔

## 概述

LocalIdentityServer 實現了企業級的多因子認證 (MFA) 系統，支援多種認證方法，包括：

- **TOTP (Time-based One-Time Password)** - 基於時間的一次性密碼，支援 Google Authenticator、Microsoft Authenticator 等應用程式
- **SMS OTP** - 簡訊一次性密碼，透過 Twilio API 發送
- **Email OTP** - 電子郵件一次性密碼，透過 SendGrid API 發送
- **備援代碼 (Backup Codes)** - 可以在主要 MFA 方法不可用時使用的一次性備援代碼

## 系統架構

### 核心元件

#### 1. 資料層 (Data Layer)

**實體模型:**
- `UserMfaEntity` - 存儲使用者的 MFA 方法配置
- `MfaBackupCodeEntity` - 存儲備援代碼
- `MfaAuditLogEntity` - 存儲 MFA 相關的審計日誌

**Repository 模式:**
- `IMfaRepository` / `MfaRepository` - MFA 方法的資料存取
- `IMfaBackupCodeRepository` / `MfaBackupCodeRepository` - 備援代碼的資料存取
- `IMfaAuditRepository` / `MfaAuditRepository` - 審計日誌的資料存取

#### 2. 服務層 (Service Layer)

**核心服務:**
- `ITotpService` / `TotpService` - TOTP 生成與驗證服務
- `IQrCodeService` / `QrCodeService` - QR Code 生成服務
- `ISmsService` / `SmsService` - SMS 發送服務
- `IEmailService` / `EmailService` - 電子郵件發送服務
- `IMfaBackupCodeService` / `MfaBackupCodeService` - 備援代碼管理服務

**支援服務:**
- `IMfaEncryptionService` / `MfaEncryptionService` - MFA 相關資料加密
- `IMfaAuditService` / `MfaAuditService` - MFA 審計日誌服務
- `IMfaBruteForceProtectionService` / `MfaBruteForceProtectionService` - 暴力破解防護
- `IOtpDeliveryService` / `OtpDeliveryService` - OTP 傳送協調服務

#### 3. API 層 (API Layer)

**控制器:**
- `MfaController` - 一般 MFA 管理 API
- `MfaAdminController` - 管理員專用 MFA API

#### 4. 安全與中間件 (Security & Middleware)

**中間件:**
- `MfaRateLimitingMiddleware` - MFA 專用的頻率限制中間件

**安全特性:**
- Rate Limiting - 防止暴力破解攻擊
- 失敗計數與帳戶鎖定
- 加密存儲敏感資料
- 完整的審計日誌

## 技術規範

### TOTP 實現

- **標準**: RFC 6238 (TOTP: Time-Based One-Time Password Algorithm)
- **套件**: OtpNet 1.4.0
- **設定**:
  - 時間窗口: 30 秒
  - 代碼長度: 6 位數
  - 雜湊演算法: SHA-1
  - 容錯範圍: ±1 個時間窗口 (前後 30 秒)

### QR Code 生成

- **套件**: QRCoder 1.6.0
- **格式**: otpauth:// URI
- **影像格式**: PNG (Base64 編碼)

### SMS 整合

- **服務提供商**: Twilio
- **套件**: Twilio 7.6.0
- **設定**: 透過 `appsettings.json` 的 `Mfa:Twilio` 區段

### Email 整合

- **服務提供商**: SendGrid
- **套件**: SendGrid 9.29.3
- **設定**: 透過 `appsettings.json` 的 `Mfa:SendGrid` 區段

### 資料加密

- **方法**: ASP.NET Core Data Protection
- **用途**: 加密 TOTP 密鑰、OTP 代碼等敏感資料

### 資料庫支援

- **主要**: SQLite (開發與測試)
- **生產**: 可擴展至 PostgreSQL、SQL Server
- **ORM**: Entity Framework Core 9.0.8

## 配置設定

### appsettings.json 設定範例

```json
{
  "Mfa": {
    "Issuer": "LocalIdentityServer",
    "Totp": {
      "TimeStepSeconds": 30,
      "Digits": 6,
      "HashMode": "Sha1"
    },
    "QrCode": {
      "MaxTextLength": 2048,
      "MinSize": 64,
      "MaxSize": 1024
    },
    "BackupCodes": {
      "Length": 8,
      "ExpirationDays": 90
    },
    "Otp": {
      "Length": 6,
      "ExpirationMinutes": 5
    },
    "BruteForce": {
      "MaxFailedAttempts": 5,
      "LockoutDurationMinutes": 15
    },
    "Twilio": {
      "AccountSid": "your_twilio_account_sid",
      "AuthToken": "your_twilio_auth_token",
      "FromNumber": "+1234567890"
    },
    "SendGrid": {
      "ApiKey": "your_sendgrid_api_key"
    },
    "Email": {
      "FromEmail": "noreply@localidentityserver.com",
      "FromName": "LocalIdentityServer"
    },
    "RateLimiting": {
      "MfaVerificationPerIpPerMinute": 10,
      "MfaVerificationPerUserPerMinute": 5,
      "MfaSetupPerUserPerHour": 3
    },
    "Audit": {
      "RetentionDays": 90,
      "EnableGeoLocation": false,
      "EnableDeviceFingerprinting": false
    }
  }
}
```

## API 端點

### MFA 狀態查詢

```http
GET /api/mfa/status
Authorization: Bearer {token}
```

### TOTP 設定

```http
POST /api/mfa/setup/totp
Authorization: Bearer {token}
Content-Type: application/json

{
  "deviceName": "iPhone 12"
}
```

### TOTP 確認

```http
POST /api/mfa/setup/totp/confirm
Authorization: Bearer {token}
Content-Type: application/json

{
  "secret": "base32_encoded_secret",
  "setupToken": "setup_token",
  "code": "123456",
  "deviceName": "iPhone 12"
}
```

### MFA 驗證

```http
POST /api/mfa/verify
Authorization: Bearer {token}
Content-Type: application/json

{
  "method": "TOTP",
  "code": "123456"
}
```

### 備援代碼生成

```http
POST /api/mfa/backup-codes/generate
Authorization: Bearer {token}
```

## 安全考量

### 1. 暴力破解防護

- 失敗次數限制 (預設: 5 次)
- 帳戶鎖定機制 (預設: 15 分鐘)
- IP 級別的 Rate Limiting

### 2. 重放攻擊防護

- TOTP 代碼一次性使用檢查
- 時間窗口驗證
- 備援代碼使用後立即失效

### 3. 資料保護

- TOTP 密鑰使用 Data Protection 加密
- 備援代碼使用 BCrypt 雜湊
- 審計日誌包含風險評分

### 4. 會話安全

- MFA 驗證狀態與 OAuth2 流程整合
- 支援 PKCE (Proof Key for Code Exchange)
- JWT Token 包含 MFA 驗證聲明

## 效能最佳化

### 1. 快取策略

- TOTP 驗證結果短期快取 (30 秒)
- 失敗計數使用 Redis (可選)
- QR Code 圖像快取

### 2. 資料庫最佳化

- 適當的索引策略
- 審計日誌分區 (生產環境)
- 過期資料清理機制

### 3. 外部服務優化

- SMS/Email 服務的錯誤重試
- 非同步訊息發送
- 服務健康狀態監控

## 監控與日誌

### 審計事件類型

- `setup_totp` - TOTP 設定
- `verify_totp` - TOTP 驗證
- `setup_sms` - SMS 設定
- `verify_sms` - SMS 驗證
- `setup_email` - Email 設定
- `verify_email` - Email 驗證
- `generate_backup_codes` - 備援代碼生成
- `use_backup_code` - 備援代碼使用
- `disable_mfa` - MFA 停用
- `account_locked` - 帳戶鎖定
- `security_violation` - 安全違規

### 風險評分計算

風險評分 (0-100) 基於以下因素：
- 失敗嘗試次數
- 地理位置異常
- 設備指紋變化
- 時間模式異常
- IP 信譽

## 部署注意事項

### 生產環境準備

1. **金鑰管理**
   - 使用 Azure Key Vault 或 HSM
   - 定期金鑰輪替
   - 備份與恢復策略

2. **外部服務設定**
   - Twilio 帳戶與電話號碼驗證
   - SendGrid 網域驗證與 SPF/DKIM 設定
   - 服務限額與計費監控

3. **資料庫配置**
   - 連線池設定
   - 備份策略
   - 效能監控

4. **安全設定**
   - HTTPS 強制執行
   - 安全標頭配置
   - CSP (Content Security Policy) 設定

### 擴展性規劃

- 水平擴展支援 (Redis 快取)
- 負載平衡配置
- CDN 整合 (靜態資源)
- 微服務架構遷移路徑

## 故障排除

### 常見問題

1. **TOTP 時間同步問題**
   - 檢查伺服器時間設定
   - 調整時間容錯範圍
   - 使用 NTP 同步

2. **SMS/Email 發送失敗**
   - 檢查 API 金鑰配置
   - 驗證服務提供商狀態
   - 查看錯誤日誌

3. **資料庫連線問題**
   - 檢查連線字串
   - 驗證 Migration 狀態
   - 監控連線池使用情況

### 診斷工具

- MFA 狀態檢查端點
- 審計日誌查詢 API
- 健康檢查端點
- 效能計數器

## 開發指南

### 擴展新的 MFA 方法

1. 實現對應的服務介面
2. 新增資料庫實體 (如需要)
3. 更新 API 控制器
4. 新增相關測試
5. 更新文檔

### 自訂驗證邏輯

可以透過實現 `IMfaService` 介面來自訂 MFA 驗證流程，支援：
- 風險評估整合
- 多重驗證要求
- 條件式 MFA 觸發

### 測試策略

- 單元測試 (所有服務層)
- 整合測試 (API 端點)
- 端到端測試 (完整流程)
- 效能測試 (高負載情境)
- 安全測試 (滲透測試)