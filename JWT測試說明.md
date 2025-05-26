# 🔐 JWT 功能測試說明

## 📋 測試前準備

### 1. 啟動應用程式
```bash
# 啟動 API
cd CreditMonitoring.Api
dotnet run

# 啟動 Web 應用 (另一個終端)
cd CreditMonitoring.Web  
dotnet run
```

### 2. 測試用戶帳號
```csharp
// 預設測試用戶（定義在 AuthenticationService.cs）
用戶名: admin
密碼: admin123
角色: Admin, CreditOfficer

用戶名: officer
密碼: officer123  
角色: CreditOfficer

用戶名: manager
密碼: manager123
角色: Manager, CreditOfficer
```

## 🧪 API 端點測試

### 1. 用戶登入
```http
POST /api/auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "admin123",
  "bankCode": "001",
  "branchCode": "0001"
}
```

**期望回應:**
```json
{
  "success": true,
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "tokenType": "Bearer",
  "expiresIn": 3600,
  "expiresAt": "2024-12-26T11:00:00Z",
  "userInfo": {
    "userId": "U001",
    "username": "admin",
    "userType": "Admin",
    "bankCode": "001",
    "branchCode": "0001",
    "roles": ["Admin", "CreditOfficer"]
  }
}
```

### 2. 驗證令牌
```http
POST /api/auth/validate
Content-Type: application/json

{
  "token": "你的_JWT_令牌"
}
```

### 3. 獲取用戶信息
```http
GET /api/auth/me
Authorization: Bearer 你的_JWT_令牌
```

### 4. 刷新令牌
```http
POST /api/auth/refresh
Authorization: Bearer 你的_JWT_令牌
```

### 5. 用戶登出
```http
POST /api/auth/logout
Authorization: Bearer 你的_JWT_令牌
```

## 🌐 Blazor 前端測試

### 1. JWT 登入頁面
訪問: `https://localhost:5001/jwt-login`

### 2. 測試登入流程
1. 輸入用戶名: `admin`
2. 輸入密碼: `admin123`
3. 點擊登入按鈕
4. 檢查是否成功重定向

### 3. 檢查認證狀態
```razor
@page "/test-auth"
@using Microsoft.AspNetCore.Components.Authorization
@inject AuthenticationStateProvider AuthStateProvider

<h3>認證狀態測試</h3>

<AuthorizeView>
    <Authorized>
        <p>✅ 用戶已認證: @context.User.Identity.Name</p>
        <p>角色: @string.Join(", ", context.User.Claims.Where(c => c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role").Select(c => c.Value))</p>
    </Authorized>
    <NotAuthorized>
        <p>❌ 用戶未認證</p>
    </NotAuthorized>
</AuthorizeView>
```

## 🔍 問題排查

### 1. 常見錯誤
- **401 Unauthorized**: 檢查令牌是否有效或已過期
- **400 Bad Request**: 檢查請求格式是否正確
- **500 Internal Server Error**: 檢查伺服器日誌

### 2. 日誌查看
```bash
# API 日誌會顯示詳細的認證流程
# 注意查看 ILogger 輸出的信息
```

### 3. JWT 令牌解析
使用 [jwt.io](https://jwt.io) 網站解析令牌內容，檢查：
- Header: 算法和類型
- Payload: 用戶聲明信息
- Signature: 簽名驗證

## ✅ 功能驗證清單

- [ ] 用戶可以成功登入
- [ ] JWT 令牌正確生成
- [ ] 令牌包含正確的用戶信息
- [ ] API 端點正確驗證令牌
- [ ] 前端可以儲存和使用令牌
- [ ] 用戶可以成功登出
- [ ] 令牌刷新功能正常
- [ ] 過期令牌被正確拒絕
- [ ] 角色基礎授權正常運作

## 🔧 進一步優化建議

1. **令牌黑名單**: 實現令牌撤銷機制
2. **Refresh Token**: 實現更安全的令牌刷新
3. **HTTPS 強制**: 生產環境強制使用 HTTPS
4. **CORS 配置**: 正確配置跨域訪問
5. **錯誤處理**: 完善錯誤訊息和處理機制 