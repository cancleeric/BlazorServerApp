# LocalIdentityServer

**企業級 OAuth2/OIDC 認證伺服器** - .NET 8 + SQLite

## 🚀 30秒快速啟動

```bash
git clone <repo>
cd LocalIdentityServer
dotnet run
```

訪問：<http://localhost:5055>

## 🎯 企業級特性

✅ **安全** - PKCE、JWT RS256、BCrypt 密碼雜湊  
✅ **標準** - OAuth2/OIDC 完整支援  
✅ **整合** - 企業內部系統 SSO 認證  
✅ **彈性** - Repository Pattern、模組化設計  

## 📋 主要端點

- `/.well-known/openid-configuration` - OIDC 發現文件
- `/connect/authorize` - OAuth2 授權
- `/connect/token` - Token 發行  
- `/connect/userinfo` - 使用者資訊

## 🔧 生產部署

### 1. 環境配置

```json
{
  "IdentityServer": {
    "Issuer": "https://your-domain.com",
    "RequireHttps": true
  }
}
```

### 2. HTTPS 啟用

```bash
dotnet run --urls="https://localhost:5001"
```

### 3. 資料庫升級

- SQLite (開發) → PostgreSQL (生產)
- 自動 Migration 支援

## 📚 文件導航

- [企業升級計劃](ENTERPRISE_PLAN.md) - 完整技術路線圖
- [開發計劃](DEV_PLAN.md) - 詳細開發進度  
- [API 文件] - Swagger/OpenAPI (開發中)

## 💼 企業支援

**目標使用者**: 企業內部開發團隊  
**適用場景**: 內部系統 SSO、API 認證、微服務架構  
**技術支援**: 完整部署指南、故障排除、安全配置  

---

**快速、安全、企業級** - 為您的內部系統提供可靠的身份認證服務
