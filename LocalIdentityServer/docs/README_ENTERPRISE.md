# LocalIdentityServer - 企業級內部認證伺服器

**基於 .NET 8 + SQLite 的企業級 OAuth2/OIDC 認證伺服器，專為內部系統整合設計**

## 🎯 企業級特性

✅ **生產就緒** - 完整的錯誤處理、日誌記錄、安全配置  
✅ **內部整合** - 支援企業內部系統的 SSO 認證  
✅ **高安全性** - PKCE、JWT RS256、密碼雜湊、權限控制  
✅ **可擴展** - Repository Pattern、SOLID 原則、模組化設計  
✅ **監控友好** - 結構化日誌、健康檢查、性能指標  

## 🚀 快速部署

### 1. 系統需求

- .NET 8.0 Runtime
- SQLite 3.x (內建)
- Linux/Windows Server

### 2. 生產部署

```bash
# 1. 複製到伺服器
git clone <your-repo>
cd LocalIdentityServer

# 2. 生產配置
cp appsettings.json appsettings.Production.json
# 編輯 appsettings.Production.json 設定

# 3. 建立資料庫
dotnet ef database update

# 4. 啟動服務
dotnet run --environment=Production --urls="https://localhost:5001"
```

## 🔐 安全配置

### JWT 金鑰管理

```json
{
  "IdentityServer": {
    "SigningKey": {
      "Type": "RSA",
      "KeySize": 2048,
      "PersistKeys": true,
      "KeyStorePath": "/app/keys"
    }
  }
}
```

### HTTPS 配置 (必要)

```json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://*:5001",
        "Certificate": {
          "Path": "/app/cert.pfx",
          "Password": "your-cert-password"
        }
      }
    }
  }
}
```

## 📋 企業級端點

| 端點 | 用途 | 狀態 |
|------|------|------|
| `/.well-known/openid-configuration` | OIDC Discovery | ✅ |
| `/connect/authorize` | 授權端點 | ✅ |
| `/connect/token` | Token 發行 | ✅ |
| `/connect/userinfo` | 使用者資訊 | ✅ |
| `/health` | 健康檢查 | 🔄 待實作 |
| `/admin` | 管理介面 | 🔄 待實作 |

## 🏗️ 企業整合範例

### ASP.NET Core Web App

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://your-identity-server.company.com";
        options.Audience = "your-api-resource";
        options.RequireHttpsMetadata = true;
    });
```

### JavaScript SPA

```javascript
const config = {
    authority: 'https://your-identity-server.company.com',
    client_id: 'spa-client',
    redirect_uri: 'https://your-app.company.com/callback',
    response_type: 'code',
    scope: 'openid profile email api.read'
};
```

## 🔧 生產級配置項目

### 1. 資料庫升級 (建議)

- 從 SQLite 升級到 PostgreSQL/SQL Server
- 配置連接池和故障轉移

### 2. 高可用性

- 負載均衡器配置
- 多實例部署
- 健康檢查端點

### 3. 監控整合

- Serilog + ELK Stack
- Application Insights
- Prometheus + Grafana

### 4. 安全強化

- Certificate-based 客戶端驗證
- Rate Limiting
- IP 白名單

## 📊 預設配置

### 內建使用者 (僅供初始測試)

- **admin** / **Password123!** (系統管理員)
- **user** / **Password123!** (一般使用者)

### 內建客戶端

- **enterprise-web** - 企業內部 Web 應用
- **enterprise-api** - 企業內部 API 服務

> ⚠️ **生產部署前必須更換預設憑證！**

## 🛡️ 安全建議

1. **更換所有預設密碼和金鑰**
2. **啟用 HTTPS 並使用有效證書**
3. **定期輪替 JWT 簽章金鑰**
4. **設定適當的 CORS 政策**
5. **實作日誌監控和警報**

## 📈 企業級功能路線圖

### Phase 1 (當前)

- ✅ OAuth2/OIDC 核心功能
- ✅ 資料持久化
- ✅ 基本安全措施

### Phase 2 (規劃中)

- 🔄 管理員介面
- 🔄 使用者自助服務
- 🔄 多租戶支援
- 🔄 SAML 2.0 支援

### Phase 3 (未來)

- 📋 Active Directory 整合
- 📋 多因素驗證 (MFA)
- 📋 稽核日誌
- 📋 API 管理

## 📞 企業支援

- **內部部署**: 完整的部署文件和腳本
- **技術支援**: 詳細的故障排除指南
- **安全諮詢**: 企業級安全配置建議
- **客製化**: 依企業需求調整功能

---

**LocalIdentityServer** - 為企業內部應用提供可靠、安全的身份認證服務
