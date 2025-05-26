# 🔐 最簡單的 JWT Web API 範本

## 📖 學習目標

這個範本專門用於學習 JWT 在 ASP.NET Core Web API 中的基本應用：

1. **JWT 生成** - 如何創建包含用戶信息的 JWT 令牌
2. **JWT 驗證** - 如何驗證令牌的有效性
3. **API 保護** - 如何使用 JWT 保護 API 端點
4. **角色授權** - 如何實現基於角色的訪問控制

## 🚀 快速開始

### 1. 運行專案
```bash
cd SimpleJwtApi
dotnet run
```

### 2. 訪問 Swagger 文檔
打開瀏覽器訪問：`https://localhost:7001/swagger`

### 3. 測試 API

#### 第一步：測試公開端點
```http
GET https://localhost:7001/api/auth/public
```

#### 第二步：用戶登入
```http
POST https://localhost:7001/api/auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "123456"
}
```

**回應範例：**
```json
{
  "success": true,
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "message": "登入成功",
  "expiresAt": "2024-12-26T12:00:00Z",
  "user": {
    "id": "1",
    "username": "admin",
    "email": "admin@example.com",
    "roles": ["Admin", "User"]
  }
}
```

#### 第三步：使用 JWT 訪問受保護端點
```http
GET https://localhost:7001/api/auth/me
Authorization: Bearer [你的JWT令牌]
```

#### 第四步：測試管理員專用端點
```http
GET https://localhost:7001/api/auth/admin-only
Authorization: Bearer [管理員JWT令牌]
```

## 👥 測試帳號

| 用戶名 | 密碼   | 角色        | 說明       |
|--------|--------|-------------|------------|
| admin  | 123456 | Admin, User | 管理員帳號 |
| user   | 123456 | User        | 一般用戶   |

## 🔍 核心概念解析

### 1. JWT 結構
JWT 由三部分組成，用 `.` 分隔：
```
Header.Payload.Signature
```

- **Header**: 包含算法和令牌類型
- **Payload**: 包含聲明 (Claims)，即用戶數據
- **Signature**: 用於驗證令牌未被篡改

### 2. 關鍵文件說明

#### `JwtService.cs` - JWT 核心邏輯
- `GenerateToken()`: 生成 JWT 令牌
- `ValidateToken()`: 驗證令牌有效性
- `GetUserInfoFromToken()`: 從令牌提取用戶信息

#### `AuthController.cs` - API 端點
- `POST /login`: 用戶登入，返回 JWT
- `GET /me`: 需要 JWT 的受保護端點
- `GET /admin-only`: 需要管理員角色的端點
- `GET /public`: 公開端點，無需認證

#### `Program.cs` - 中間件配置
- JWT 認證配置
- 中間件執行順序

### 3. 重要配置參數

```json
{
  "Jwt": {
    "SecretKey": "簽名密鑰，必須足夠長",
    "Issuer": "JWT 發行者",
    "Audience": "JWT 受眾",
    "ExpirationMinutes": 60
  }
}
```

## 🛡️ 安全注意事項

1. **密鑰安全**: `SecretKey` 應該使用強隨機字符串，至少 256 位
2. **HTTPS**: 生產環境必須使用 HTTPS
3. **密碼加密**: 實際應用中密碼應該使用 bcrypt 等方式加密
4. **令牌過期**: 設置合理的過期時間
5. **敏感信息**: 不要在 JWT 中存儲敏感信息

## 📝 學習練習

### 初級練習
1. 修改用戶資料，添加新的測試用戶
2. 調整令牌過期時間
3. 添加新的用戶角色

### 中級練習
1. 實現令牌刷新功能
2. 添加密碼加密/驗證
3. 實現令牌黑名單機制

### 高級練習
1. 整合真實資料庫
2. 實現多因素認證
3. 添加 API 限流功能

## 🔧 擴展建議

1. **資料庫整合**: 使用 Entity Framework 連接資料庫
2. **密碼安全**: 實現 bcrypt 密碼加密
3. **日誌記錄**: 添加結構化日誌
4. **單元測試**: 編寫 JWT 服務的單元測試
5. **Blazor 整合**: 創建 Blazor 前端消費 API

## 🐛 常見問題

### Q: JWT 令牌無效？
A: 檢查：
- 令牌格式是否正確 (Bearer + 空格 + 令牌)
- 令牌是否已過期
- 密鑰配置是否一致

### Q: 401 Unauthorized 錯誤？
A: 檢查：
- 是否已添加 `[Authorize]` 屬性
- Authentication 中間件是否正確配置
- JWT 配置參數是否正確

### Q: 如何解析 JWT 內容？
A: 可以使用 [jwt.io](https://jwt.io) 網站解析令牌內容（僅用於調試）

## 📚 相關資源

- [JWT 官方文檔](https://jwt.io/)
- [ASP.NET Core 認證文檔](https://docs.microsoft.com/aspnet/core/security/authentication/)
- [JWT 最佳實踐](https://auth0.com/blog/a-look-at-the-latest-draft-for-jwt-bcp/)

---

這個範本專為學習設計，包含詳細註釋和說明。建議按順序學習每個文件，理解 JWT 的核心概念！🎓 