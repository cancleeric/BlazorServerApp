# MFA API 參考文檔

## 概述

LocalIdentityServer MFA API 提供完整的多因子認證管理功能。所有 API 端點都需要有效的 Bearer Token 認證，並遵循 RESTful 設計原則。

## 基本資訊

- **Base URL**: `{server}/api/mfa`
- **認證方式**: Bearer Token (JWT)
- **內容類型**: `application/json`
- **字元編碼**: UTF-8

## 錯誤處理

### 標準 HTTP 狀態碼

- `200 OK` - 請求成功
- `400 Bad Request` - 請求參數錯誤
- `401 Unauthorized` - 認證失敗或 Token 無效
- `403 Forbidden` - 權限不足
- `404 Not Found` - 資源不存在
- `409 Conflict` - 資源衝突 (例如重複設定)
- `429 Too Many Requests` - 請求頻率超過限制
- `500 Internal Server Error` - 伺服器內部錯誤

### 錯誤回應格式

```json
{
  "error": "error_code",
  "message": "Human readable error message",
  "details": {
    "field": "specific_error_detail"
  },
  "timestamp": "2024-01-01T00:00:00Z"
}
```

## 認證

所有 API 請求都需要在 Header 中包含有效的 Bearer Token：

```http
Authorization: Bearer {your_jwt_token}
```

## API 端點

### 1. 取得 MFA 狀態

取得當前使用者的 MFA 配置狀態。

**端點**: `GET /api/mfa/status`

**請求範例**:
```http
GET /api/mfa/status
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**成功回應** (200 OK):
```json
{
  "isEnabled": true,
  "requiresMfa": false,
  "primaryMethod": "TOTP",
  "enabledMethods": [
    {
      "id": "mfa_123",
      "method": "TOTP",
      "deviceName": "iPhone 12",
      "isEnabled": true,
      "isPrimary": true,
      "lastUsedAt": "2024-01-01T12:00:00Z",
      "isLocked": false,
      "maskedTarget": null
    },
    {
      "id": "mfa_124",
      "method": "SMS",
      "deviceName": "Phone",
      "isEnabled": true,
      "isPrimary": false,
      "lastUsedAt": "2023-12-15T08:30:00Z",
      "isLocked": false,
      "maskedTarget": "+886*****5678"
    }
  ],
  "backupCodesAvailable": 8,
  "backupCodesTotal": 10,
  "isTotpEnabled": true,
  "isSmsEnabled": true,
  "isEmailEnabled": false
}
```

### 2. TOTP 設定

#### 2.1 開始 TOTP 設定

開始設定 TOTP 認證，系統會生成密鑰和 QR Code。

**端點**: `POST /api/mfa/setup/totp`

**請求範例**:
```http
POST /api/mfa/setup/totp
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "deviceName": "iPhone 12",
  "accountName": "john.doe@example.com"
}
```

**成功回應** (200 OK):
```json
{
  "setupToken": "setup_abc123xyz",
  "secret": "JBSWY3DPEHPK3PXP",
  "qrCodeUri": "otpauth://totp/LocalIdentityServer:john.doe@example.com?secret=JBSWY3DPEHPK3PXP&issuer=LocalIdentityServer",
  "qrCodeImage": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAA...",
  "manualEntryKey": "JBSW Y3DP EHPK 3PXP",
  "instructions": "請使用 Google Authenticator 或其他 TOTP 應用程式掃描 QR Code，然後輸入 6 位數驗證碼確認設定。"
}
```

#### 2.2 確認 TOTP 設定

使用 TOTP 應用程式生成的代碼確認設定。

**端點**: `POST /api/mfa/setup/totp/confirm`

**請求範例**:
```http
POST /api/mfa/setup/totp/confirm
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "secret": "JBSWY3DPEHPK3PXP",
  "setupToken": "setup_abc123xyz",
  "code": "123456",
  "deviceName": "iPhone 12"
}
```

**成功回應** (200 OK):
```json
{
  "success": true,
  "backupCodes": [
    "12345678",
    "87654321",
    "11223344",
    "44332211",
    "55667788",
    "88776655",
    "99881122",
    "22118899",
    "33445566",
    "66554433"
  ],
  "message": "TOTP 設定成功",
  "completedAt": "2024-01-01T12:00:00Z"
}
```

### 3. SMS 設定

設定 SMS 作為 MFA 方法。

**端點**: `POST /api/mfa/setup/sms`

**請求範例**:
```http
POST /api/mfa/setup/sms
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "phoneNumber": "+886912345678",
  "deviceName": "Personal Phone"
}
```

**成功回應** (200 OK):
```json
{
  "success": true,
  "message": "SMS 設定成功",
  "verificationId": "sms_verify_123",
  "maskedTarget": "+886*****5678",
  "expiresAt": "2024-01-01T12:05:00Z"
}
```

### 4. Email 設定

設定 Email 作為 MFA 方法。

**端點**: `POST /api/mfa/setup/email`

**請求範例**:
```http
POST /api/mfa/setup/email
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "emailAddress": "john.doe@example.com",
  "deviceName": "Work Email"
}
```

**成功回應** (200 OK):
```json
{
  "success": true,
  "message": "Email 設定成功",
  "verificationId": "email_verify_123",
  "maskedTarget": "j***@example.com",
  "expiresAt": "2024-01-01T12:05:00Z"
}
```

### 5. MFA 驗證

#### 5.1 發起 MFA 挑戰

對於需要傳送代碼的方法 (SMS/Email)，先發起挑戰。

**端點**: `POST /api/mfa/challenge`

**請求範例**:
```http
POST /api/mfa/challenge
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "method": "SMS",
  "clientId": "demo_client",
  "sessionId": "session_123"
}
```

**成功回應** (200 OK):
```json
{
  "success": true,
  "challengeId": "challenge_abc123",
  "expiresAt": "2024-01-01T12:05:00Z",
  "maskedTarget": "+886*****5678",
  "message": "驗證碼已發送到您的手機",
  "generatedAt": "2024-01-01T12:00:00Z"
}
```

#### 5.2 執行 MFA 驗證

使用收到的代碼進行驗證。

**端點**: `POST /api/mfa/verify`

**請求範例**:
```http
POST /api/mfa/verify
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "method": "TOTP",
  "code": "123456",
  "clientId": "demo_client",
  "sessionId": "session_123",
  "deviceFingerprint": "fp_abc123",
  "geoLocation": "{\"country\":\"TW\",\"city\":\"Taipei\"}"
}
```

**成功回應** (200 OK):
```json
{
  "isValid": true,
  "isLocked": false,
  "remainingAttempts": 5,
  "lockedUntil": null,
  "riskScore": 10,
  "message": "驗證成功",
  "verifiedAt": "2024-01-01T12:00:00Z"
}
```

**失敗回應** (200 OK):
```json
{
  "isValid": false,
  "isLocked": false,
  "remainingAttempts": 3,
  "lockedUntil": null,
  "riskScore": 45,
  "message": "驗證碼錯誤",
  "verifiedAt": "2024-01-01T12:00:00Z"
}
```

### 6. 備援代碼管理

#### 6.1 生成備援代碼

生成新的備援代碼，舊代碼會立即失效。

**端點**: `POST /api/mfa/backup-codes/generate`

**請求範例**:
```http
POST /api/mfa/backup-codes/generate
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**成功回應** (200 OK):
```json
{
  "success": true,
  "backupCodes": [
    "12345678",
    "87654321",
    "11223344",
    "44332211",
    "55667788",
    "88776655",
    "99881122",
    "22118899",
    "33445566",
    "66554433"
  ],
  "expiresAt": "2024-04-01T00:00:00Z",
  "message": "備援代碼已生成",
  "generatedAt": "2024-01-01T12:00:00Z",
  "batchId": "batch_abc123"
}
```

#### 6.2 查看備援代碼狀態

查看當前備援代碼的使用狀況。

**端點**: `GET /api/mfa/backup-codes/status`

**請求範例**:
```http
GET /api/mfa/backup-codes/status
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**成功回應** (200 OK):
```json
{
  "totalGenerated": 10,
  "availableCodes": 8,
  "usedCodes": 2,
  "lastGeneratedAt": "2024-01-01T00:00:00Z",
  "lastUsedAt": "2024-01-15T08:30:00Z",
  "expiresAt": "2024-04-01T00:00:00Z",
  "hasExpiredCodes": false
}
```

### 7. MFA 管理

#### 7.1 停用 MFA 方法

停用特定的 MFA 方法。

**端點**: `POST /api/mfa/disable`

**請求範例**:
```http
POST /api/mfa/disable
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "method": "SMS",
  "confirmationCode": "123456"
}
```

**成功回應** (200 OK):
```json
{
  "success": true,
  "message": "SMS MFA 已停用",
  "hasOtherMethods": true,
  "disabledAt": "2024-01-01T12:00:00Z"
}
```

#### 7.2 變更主要方法

設定主要的 MFA 方法。

**端點**: `PUT /api/mfa/primary-method`

**請求範例**:
```http
PUT /api/mfa/primary-method
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "methodId": "mfa_123",
  "confirmationCode": "123456"
}
```

**成功回應** (200 OK):
```json
{
  "success": true,
  "message": "主要 MFA 方法已更新",
  "newPrimaryMethod": "TOTP"
}
```

### 8. 審計日誌

#### 8.1 查看 MFA 審計日誌

查看使用者的 MFA 活動記錄。

**端點**: `GET /api/mfa/audit-logs`

**查詢參數**:
- `page` (int, optional): 頁碼，預設 1
- `pageSize` (int, optional): 每頁數量，預設 20，最大 100
- `fromDate` (string, optional): 開始日期 (ISO 8601)
- `toDate` (string, optional): 結束日期 (ISO 8601)
- `eventType` (string, optional): 事件類型篩選
- `method` (string, optional): MFA 方法篩選

**請求範例**:
```http
GET /api/mfa/audit-logs?page=1&pageSize=10&eventType=verify_totp
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**成功回應** (200 OK):
```json
{
  "logs": [
    {
      "eventType": "verify_totp",
      "method": "TOTP",
      "result": "Success",
      "description": "TOTP 驗證成功",
      "ipAddress": "192.168.1.100",
      "createdAt": "2024-01-01T12:00:00Z",
      "failureReason": null,
      "riskScore": 10
    },
    {
      "eventType": "setup_sms",
      "method": "SMS",
      "result": "Success",
      "description": "SMS MFA 設定完成",
      "ipAddress": "192.168.1.100",
      "createdAt": "2024-01-01T10:00:00Z",
      "failureReason": null,
      "riskScore": 5
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 10,
    "totalCount": 25,
    "totalPages": 3
  }
}
```

## 管理員 API

管理員 API 提供額外的管理功能，需要管理員權限。

### 基本資訊

- **Base URL**: `{server}/api/mfa-admin`
- **認證方式**: Bearer Token (需要管理員權限)

### 1. 重設使用者 MFA

管理員可以重設指定使用者的 MFA 設定。

**端點**: `POST /api/mfa-admin/reset`

**請求範例**:
```http
POST /api/mfa-admin/reset
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "userId": "user_123",
  "reason": "使用者丟失手機，需要重設 MFA"
}
```

**成功回應** (200 OK):
```json
{
  "success": true,
  "message": "使用者 MFA 已重設",
  "userId": "user_123",
  "resetAt": "2024-01-01T12:00:00Z"
}
```

### 2. 解鎖使用者 MFA

管理員可以解鎖被鎖定的使用者 MFA。

**端點**: `POST /api/mfa-admin/unlock`

**請求範例**:
```http
POST /api/mfa-admin/unlock
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "userId": "user_123",
  "method": "TOTP"
}
```

**成功回應** (200 OK):
```json
{
  "success": true,
  "message": "使用者 MFA 已解鎖",
  "userId": "user_123",
  "method": "TOTP",
  "unlockedAt": "2024-01-01T12:00:00Z"
}
```

### 3. 查看系統 MFA 統計

查看系統的 MFA 使用統計。

**端點**: `GET /api/mfa-admin/statistics`

**請求範例**:
```http
GET /api/mfa-admin/statistics
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**成功回應** (200 OK):
```json
{
  "totalUsers": 1000,
  "mfaEnabledUsers": 850,
  "mfaAdoptionRate": 85.0,
  "methodDistribution": {
    "TOTP": 650,
    "SMS": 300,
    "Email": 150
  },
  "securityIncidents": {
    "lockedAccounts": 5,
    "failedAttempts24h": 125
  },
  "reportGeneratedAt": "2024-01-01T12:00:00Z"
}
```

## Rate Limiting

為了防止濫用，API 實施了以下 Rate Limiting 限制：

### 預設限制

- **MFA 驗證**: 每分鐘每 IP 10 次，每分鐘每使用者 5 次
- **MFA 設定**: 每小時每使用者 3 次
- **挑戰發送**: 每分鐘每使用者 3 次
- **一般 API**: 每分鐘每 IP 60 次

### Rate Limit Headers

當接近或超過限制時，回應會包含以下 Headers：

```http
X-RateLimit-Limit: 10
X-RateLimit-Remaining: 2
X-RateLimit-Reset: 1640995200
Retry-After: 60
```

### 超過限制的回應

**狀態碼**: 429 Too Many Requests

```json
{
  "error": "rate_limit_exceeded",
  "message": "API 請求頻率超過限制，請稍後再試",
  "retryAfter": 60,
  "timestamp": "2024-01-01T12:00:00Z"
}
```

## SDK 範例

### JavaScript/TypeScript

```javascript
class MfaApiClient {
  constructor(baseUrl, token) {
    this.baseUrl = baseUrl;
    this.token = token;
  }

  async getMfaStatus() {
    const response = await fetch(`${this.baseUrl}/api/mfa/status`, {
      headers: {
        'Authorization': `Bearer ${this.token}`,
        'Content-Type': 'application/json'
      }
    });
    return response.json();
  }

  async setupTotp(deviceName) {
    const response = await fetch(`${this.baseUrl}/api/mfa/setup/totp`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${this.token}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ deviceName })
    });
    return response.json();
  }

  async verifyMfa(method, code) {
    const response = await fetch(`${this.baseUrl}/api/mfa/verify`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${this.token}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ method, code })
    });
    return response.json();
  }
}

// 使用範例
const client = new MfaApiClient('https://api.example.com', 'your_token');

// 取得 MFA 狀態
const status = await client.getMfaStatus();
console.log('MFA Status:', status);

// 設定 TOTP
const totpSetup = await client.setupTotp('iPhone 12');
console.log('TOTP Setup:', totpSetup);

// 驗證 MFA
const verification = await client.verifyMfa('TOTP', '123456');
console.log('Verification Result:', verification.isValid);
```

### C#

```csharp
public class MfaApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public MfaApiClient(string baseUrl, string token)
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<MfaStatusResponse> GetMfaStatusAsync()
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/api/mfa/status");
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MfaStatusResponse>(json);
    }

    public async Task<TotpSetupResponse> SetupTotpAsync(string deviceName)
    {
        var request = new { deviceName };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/mfa/setup/totp", content);
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TotpSetupResponse>(responseJson);
    }

    public async Task<MfaVerifyResponse> VerifyMfaAsync(string method, string code)
    {
        var request = new { method, code };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/mfa/verify", content);
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MfaVerifyResponse>(responseJson);
    }
}
```

## 測試環境

### 測試用戶

在測試環境中，您可以使用以下預設用戶：

```json
{
  "testUsers": [
    {
      "username": "alice",
      "password": "password",
      "email": "alice@example.com",
      "role": "Admin"
    },
    {
      "username": "bob", 
      "password": "password",
      "email": "bob@example.com",
      "role": "User"
    }
  ]
}
```

### 測試端點

測試環境提供額外的端點用於測試：

- `POST /api/mfa/test/generate-totp` - 為測試用戶生成 TOTP 代碼
- `POST /api/mfa/test/send-sms` - 模擬發送 SMS (不實際發送)
- `GET /api/mfa/test/health` - 健康檢查端點

---

**注意**: 本文檔涵蓋了 LocalIdentityServer MFA API 的主要功能。如需更詳細的資訊或支援，請參考技術文檔或聯繫開發團隊。