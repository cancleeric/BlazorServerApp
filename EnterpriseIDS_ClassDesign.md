# Enterprise Identity Server (IDS) 核心類別架構設計

專案 ID: `d4ec1dab-a127-4c9b-97ec-30d388c6ff73`
專案名稱: `EnterpriseIdentityServer`

## 🏗️ 核心架構概覽

基於 SOLID 原則與企業級需求，設計以下模組化類別架構：

```
EnterpriseIDS/
├── Core/                           # 核心業務邏輯
│   ├── Entities/                   # 領域實體
│   ├── Interfaces/                 # 核心介面定義
│   ├── Services/                   # 業務服務
│   └── ValueObjects/               # 值對象
├── Infrastructure/                 # 基礎設施層
│   ├── Data/                       # 資料存取
│   ├── Security/                   # 安全機制
│   ├── Caching/                    # 快取策略
│   └── Messaging/                  # 訊息處理
├── Application/                    # 應用服務層
│   ├── Commands/                   # 命令模式
│   ├── Queries/                    # 查詢模式
│   ├── Handlers/                   # 處理器
│   └── DTOs/                       # 資料傳輸對象
├── Presentation/                   # 表現層
│   ├── Controllers/                # API 控制器
│   ├── Endpoints/                  # 端點定義
│   ├── Middleware/                 # 中介軟體
│   └── ViewModels/                 # 視圖模型
└── Shared/                         # 共享組件
    ├── Constants/                  # 常數定義
    ├── Extensions/                 # 擴展方法
    └── Utilities/                  # 工具類別
```

## 📋 核心類別設計

### 1. Core/Entities (領域實體)

#### 1.1 身份與用戶管理
```csharp
// 使用者實體 (強化版)
public class User : BaseEntity, IAuditable, ISoftDeletable
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public UserStatus Status { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? TenantId { get; set; }  // 多租戶支援
    
    // 導航屬性
    public virtual ICollection<UserRole> UserRoles { get; set; }
    public virtual ICollection<UserClaim> UserClaims { get; set; }
    public virtual ICollection<UserLogin> UserLogins { get; set; }
    public virtual Tenant? Tenant { get; set; }
}

// 租戶實體 (多租戶架構)
public class Tenant : BaseEntity, IAuditable
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Domain { get; set; }
    public TenantStatus Status { get; set; }
    public TenantSettings Settings { get; set; }
    
    // 導航屬性
    public virtual ICollection<User> Users { get; set; }
    public virtual ICollection<Client> Clients { get; set; }
}

// 角色實體 (RBAC)
public class Role : BaseEntity, IAuditable
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string? Description { get; set; }
    public string? TenantId { get; set; }
    
    // 導航屬性
    public virtual ICollection<RolePermission> RolePermissions { get; set; }
    public virtual ICollection<UserRole> UserRoles { get; set; }
}

// 權限實體
public class Permission : BaseEntity
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Category { get; set; }
    public string? Description { get; set; }
    
    // 導航屬性
    public virtual ICollection<RolePermission> RolePermissions { get; set; }
}
```

#### 1.2 OAuth2/OIDC 核心實體
```csharp
// 客戶端實體 (OAuth2 Client)
public class Client : BaseEntity, IAuditable, ITenantScoped
{
    public string ClientId { get; set; }
    public string ClientName { get; set; }
    public string? ClientSecret { get; set; }
    public ClientType Type { get; set; }
    public bool RequirePkce { get; set; }
    public bool RequireConsent { get; set; }
    public int AccessTokenLifetime { get; set; }
    public int RefreshTokenLifetime { get; set; }
    public bool AllowOfflineAccess { get; set; }
    public string? TenantId { get; set; }
    
    // 導航屬性
    public virtual ICollection<ClientRedirectUri> RedirectUris { get; set; }
    public virtual ICollection<ClientScope> AllowedScopes { get; set; }
    public virtual ICollection<ClientGrantType> AllowedGrantTypes { get; set; }
    public virtual Tenant? Tenant { get; set; }
}

// 授權碼實體
public class AuthorizationCode : BaseEntity, IExpirable
{
    public string Code { get; set; }
    public string ClientId { get; set; }
    public string UserId { get; set; }
    public string RedirectUri { get; set; }
    public string CodeChallenge { get; set; }
    public string CodeChallengeMethod { get; set; }
    public string RequestedScopes { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    
    // 導航屬性
    public virtual Client Client { get; set; }
    public virtual User User { get; set; }
}

// Refresh Token 實體
public class RefreshToken : BaseEntity, IExpirable
{
    public string Token { get; set; }
    public string ClientId { get; set; }
    public string UserId { get; set; }
    public string Scopes { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public string? ReplacedByToken { get; set; }
    
    // 導航屬性
    public virtual Client Client { get; set; }
    public virtual User User { get; set; }
}

// 持久化金鑰實體 (企業級金鑰管理)
public class PersistedKey : BaseEntity
{
    public string KeyId { get; set; }
    public string Algorithm { get; set; }
    public string KeyData { get; set; }  // 加密存儲
    public KeyType Type { get; set; }
    public KeyStatus Status { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
```

#### 1.3 審計與安全實體
```csharp
// 審計日誌實體
public class AuditLog : BaseEntity
{
    public string? UserId { get; set; }
    public string? TenantId { get; set; }
    public string EventType { get; set; }
    public string EventDescription { get; set; }
    public string? ResourceId { get; set; }
    public string? ResourceType { get; set; }
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
    public AuditResult Result { get; set; }
    public string? ErrorMessage { get; set; }
    public string? AdditionalData { get; set; }  // JSON
    
    // 導航屬性
    public virtual User? User { get; set; }
    public virtual Tenant? Tenant { get; set; }
}

// 安全事件實體
public class SecurityEvent : BaseEntity
{
    public string? UserId { get; set; }
    public SecurityEventType EventType { get; set; }
    public SecurityLevel Level { get; set; }
    public string Description { get; set; }
    public string IpAddress { get; set; }
    public string? Location { get; set; }
    public bool IsResolved { get; set; }
    public string? ResolutionNotes { get; set; }
    
    // 導航屬性
    public virtual User? User { get; set; }
}
```

### 2. Core/Interfaces (核心介面)

#### 2.1 Repository 介面
```csharp
// 基礎 Repository 介面
public interface IRepository<TEntity, TKey> where TEntity : BaseEntity
{
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(TKey id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default);
}

// 使用者 Repository 介面
public interface IUserRepository : IRepository<User, string>
{
    Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> IsUserNameTakenAsync(string userName, string? excludeUserId = null, CancellationToken cancellationToken = default);
    Task<bool> IsEmailTakenAsync(string email, string? excludeUserId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetByTenantAsync(string tenantId, CancellationToken cancellationToken = default);
    Task IncrementFailedLoginAttemptsAsync(string userId, CancellationToken cancellationToken = default);
    Task ResetFailedLoginAttemptsAsync(string userId, CancellationToken cancellationToken = default);
    Task LockUserAsync(string userId, DateTime until, CancellationToken cancellationToken = default);
}

// 多租戶 Repository 介面
public interface ITenantRepository : IRepository<Tenant, string>
{
    Task<Tenant?> GetByDomainAsync(string domain, CancellationToken cancellationToken = default);
    Task<bool> IsDomainTakenAsync(string domain, string? excludeTenantId = null, CancellationToken cancellationToken = default);
}

// OAuth2 相關 Repository 介面
public interface IClientRepository : IRepository<Client, string>
{
    Task<Client?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);
    Task<bool> IsClientIdTakenAsync(string clientId, string? excludeId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<Client>> GetByTenantAsync(string tenantId, CancellationToken cancellationToken = default);
}

public interface IAuthorizationCodeRepository : IRepository<AuthorizationCode, string>
{
    Task<AuthorizationCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task MarkAsUsedAsync(string id, CancellationToken cancellationToken = default);
    Task CleanupExpiredAsync(CancellationToken cancellationToken = default);
}

public interface IRefreshTokenRepository : IRepository<RefreshToken, string>
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<IEnumerable<RefreshToken>> GetByUserAsync(string userId, CancellationToken cancellationToken = default);
    Task RevokeAsync(string tokenId, string? replacedByToken = null, CancellationToken cancellationToken = default);
    Task RevokeAllByUserAsync(string userId, CancellationToken cancellationToken = default);
    Task CleanupExpiredAsync(CancellationToken cancellationToken = default);
}
```

#### 2.2 服務介面
```csharp
// 認證服務介面
public interface IAuthenticationService
{
    Task<AuthenticationResult> AuthenticateAsync(string userName, string password, string? tenantId = null);
    Task<AuthenticationResult> AuthenticateAsync(string userName, string password, string totpCode, string? tenantId = null);
    Task<bool> ValidatePasswordAsync(string userId, string password);
    Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
    Task<bool> ResetPasswordAsync(string userId, string newPassword);
    Task<bool> EnableTwoFactorAsync(string userId);
    Task<bool> DisableTwoFactorAsync(string userId);
}

// Token 服務介面 (增強版)
public interface ITokenService
{
    Task<TokenResponse> CreateTokensAsync(TokenRequest request);
    Task<TokenValidationResult> ValidateAccessTokenAsync(string token);
    Task<TokenValidationResult> ValidateIdTokenAsync(string token);
    Task<RefreshTokenResult> RefreshTokenAsync(string refreshToken, string clientId);
    Task RevokeTokenAsync(string token, string tokenTypeHint = "");
    Task RevokeAllUserTokensAsync(string userId);
    Task<JwksDocument> GetJwksAsync();
}

// 授權服務介面
public interface IAuthorizationService
{
    Task<AuthorizationResponse> AuthorizeAsync(AuthorizationRequest request);
    Task<ConsentResponse> ProcessConsentAsync(ConsentRequest request);
    Task<bool> ValidateRedirectUriAsync(string clientId, string redirectUri);
    Task<bool> ValidateScopeAsync(string clientId, IEnumerable<string> requestedScopes);
    Task<AuthorizationCode> CreateAuthorizationCodeAsync(AuthorizationCodeRequest request);
    Task<AuthorizationCode?> ConsumeAuthorizationCodeAsync(string code, string clientId);
}

// 多租戶服務介面
public interface ITenantService
{
    Task<Tenant?> GetCurrentTenantAsync();
    Task<Tenant?> GetTenantByDomainAsync(string domain);
    Task<TenantContext> ResolveTenantAsync(HttpContext context);
    Task<bool> IsTenantActiveAsync(string tenantId);
    Task<TenantSettings> GetTenantSettingsAsync(string tenantId);
}

// 密鑰管理服務介面
public interface IKeyManagementService
{
    Task<SecurityKey> GetCurrentSigningKeyAsync();
    Task<SecurityKey> GetKeyByIdAsync(string keyId);
    Task<IEnumerable<SecurityKey>> GetValidationKeysAsync();
    Task<string> CreateNewKeyAsync(KeyType type = KeyType.RSA);
    Task RotateKeysAsync();
    Task<bool> RevokeKeyAsync(string keyId);
}

// 審計服務介面
public interface IAuditService
{
    Task LogEventAsync(AuditEvent auditEvent);
    Task LogSecurityEventAsync(SecurityEvent securityEvent);
    Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(string userId, int take = 100);
    Task<IEnumerable<SecurityEvent>> GetSecurityEventsAsync(SecurityLevel? minLevel = null, int take = 100);
}
```

### 3. Core/Services (業務服務)

#### 3.1 核心業務服務
```csharp
// 企業級認證服務
public class EnterpriseAuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantService _tenantService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITotpService _totpService;
    private readonly IAuditService _auditService;
    private readonly ISecurityPolicyService _securityPolicyService;

    // 實作認證邏輯，包含多租戶、MFA、帳戶鎖定等企業級功能
}

// 增強版 Token 服務
public class EnterpriseTokenService : ITokenService
{
    private readonly IKeyManagementService _keyManagementService;
    private readonly IClientRepository _clientRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IConfiguration _configuration;
    private readonly ICacheService _cacheService;

    // 實作 JWT 創建、驗證、刷新等邏輯
}

// 多租戶服務
public class TenantService : ITenantService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICacheService _cacheService;

    // 實作租戶解析、設定管理等功能
}
```

### 4. Application Layer (應用服務層)

#### 4.1 Commands (命令模式)
```csharp
// 用戶管理命令
public record CreateUserCommand(
    string UserName,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? TenantId = null
) : IRequest<CreateUserResult>;

public record UpdateUserCommand(
    string UserId,
    string? Email = null,
    string? FirstName = null,
    string? LastName = null,
    UserStatus? Status = null
) : IRequest<UpdateUserResult>;

// OAuth2 授權命令
public record CreateAuthorizationCodeCommand(
    string ClientId,
    string UserId,
    string RedirectUri,
    string CodeChallenge,
    string CodeChallengeMethod,
    IEnumerable<string> Scopes
) : IRequest<CreateAuthorizationCodeResult>;

public record ExchangeAuthorizationCodeCommand(
    string Code,
    string ClientId,
    string? ClientSecret,
    string RedirectUri,
    string CodeVerifier
) : IRequest<TokenResponse>;
```

#### 4.2 Queries (查詢模式)
```csharp
// 用戶查詢
public record GetUserByIdQuery(string UserId) : IRequest<User?>;
public record GetUsersByTenantQuery(string TenantId, int Page = 1, int PageSize = 20) : IRequest<PagedResult<User>>;

// 客戶端查詢
public record GetClientByIdQuery(string ClientId) : IRequest<Client?>;
public record GetClientsByTenantQuery(string TenantId) : IRequest<IEnumerable<Client>>;

// 審計查詢
public record GetAuditLogsQuery(
    string? UserId = null,
    string? TenantId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 50
) : IRequest<PagedResult<AuditLog>>;
```

### 5. Infrastructure Layer (基礎設施層)

#### 5.1 資料存取實作
```csharp
// Entity Framework DbContext
public class EnterpriseIdsDbContext : DbContext, ITenantAware
{
    public string? CurrentTenantId { get; set; }
    
    public DbSet<User> Users { get; set; }
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<Client> Clients { get; set; }
    public DbSet<AuthorizationCode> AuthorizationCodes { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<PersistedKey> PersistedKeys { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<SecurityEvent> SecurityEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // 配置實體映射
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EnterpriseIdsDbContext).Assembly);
        
        // 多租戶全域篩選器
        modelBuilder.Entity<User>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Client>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }
}

// Repository 基底實作
public abstract class BaseRepository<TEntity, TKey> : IRepository<TEntity, TKey> 
    where TEntity : BaseEntity
{
    protected readonly EnterpriseIdsDbContext _context;
    protected readonly DbSet<TEntity> _dbSet;

    protected BaseRepository(EnterpriseIdsDbContext context)
    {
        _context = context;
        _dbSet = context.Set<TEntity>();
    }

    // 實作基礎 CRUD 操作
}
```

#### 5.2 安全機制實作
```csharp
// 密鑰管理服務
public class KeyManagementService : IKeyManagementService
{
    private readonly IPersistedKeyRepository _keyRepository;
    private readonly IKeyVaultService _keyVaultService;
    private readonly ICacheService _cacheService;

    // 實作企業級密鑰管理，支援 HSM/Key Vault
}

// 安全策略服務
public class SecurityPolicyService : ISecurityPolicyService
{
    private readonly ITenantService _tenantService;
    private readonly IConfiguration _configuration;

    public Task<PasswordPolicy> GetPasswordPolicyAsync(string? tenantId = null);
    public Task<LockoutPolicy> GetLockoutPolicyAsync(string? tenantId = null);
    public Task<bool> IsPasswordCompliantAsync(string password, PasswordPolicy policy);
}
```

## 🔧 設計原則與特色

### SOLID 原則實作
- **單一責任原則 (SRP)**: 每個類別專注單一職責
- **開放封閉原則 (OCP)**: 透過介面與抽象類別支援擴展
- **里氏替換原則 (LSP)**: 所有實作都可以替換其介面
- **介面隔離原則 (ISP)**: 專用的小型介面設計
- **依賴反轉原則 (DIP)**: 依賴注入與抽象依賴

### 企業級特色
- **多租戶架構**: 完整的租戶隔離支援
- **RBAC 權限控制**: 細粒度的角色權限管理
- **審計與日誌**: 完整的操作追蹤
- **安全強化**: 密鑰管理、帳戶鎖定、MFA 支援
- **高可用性設計**: 支援叢集部署與負載平衡

### 可擴展性設計
- **CQRS 模式**: 命令查詢責任分離
- **Repository Pattern**: 資料存取抽象
- **Domain-Driven Design**: 領域驅動設計
- **Event Sourcing**: 支援事件溯源 (可選)

這個設計將為您提供一個堅實的企業級 Identity Server 基礎，可以支援大規模、高安全性的認證需求。