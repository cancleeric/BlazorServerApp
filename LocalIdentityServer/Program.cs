using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using LocalIdentityServer.Services;
using LocalIdentityServer.Services.KeyVault;
using LocalIdentityServer.Stores;
using LocalIdentityServer.Models;
using LocalIdentityServer.Data;
using LocalIdentityServer.Data.Repositories;
using LocalIdentityServer.Endpoints;
using LocalIdentityServer.Middleware;
using LocalIdentityServer.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// InMemory Users & Clients (DEMO) - 將逐步遷移至資料庫
var users = new List<TestUser>
{
    new("user_1", "alice", "alice@example.com", "password", new[]{"Admin"}),
    new("user_2", "bob", "bob@example.com", "password", new[]{"User"})
};
var clients = new List<TestClient>
{
    new("demo_client","Demo Client","secret","https://localhost:5003/callback", new[]{"openid","profile","api.read"})
};

// 配置資料庫 - SQLite (遵循依賴反轉原則)
builder.Services.AddDbContext<LocalIdentityDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? 
                     "Data Source=localidentity.db"));

// Repository Pattern 註冊 (遵循依賴反轉原則)
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IAuthorizationCodeRepository, AuthorizationCodeRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IPersistedKeyRepository, PersistedKeyRepository>();
builder.Services.AddScoped<ITokenBlacklistRepository, TokenBlacklistRepository>();

// MFA Repository 註冊
builder.Services.AddScoped<LocalIdentityServer.Data.Repositories.IMfaRepository, LocalIdentityServer.Data.Repositories.MfaRepository>();
builder.Services.AddScoped<LocalIdentityServer.Data.Repositories.IMfaBackupCodeRepository, LocalIdentityServer.Data.Repositories.MfaBackupCodeRepository>();
builder.Services.AddScoped<LocalIdentityServer.Data.Repositories.IMfaAuditRepository, LocalIdentityServer.Data.Repositories.MfaAuditRepository>();

// Services 註冊
builder.Services.AddScoped<ITokenService, DefaultTokenService>();
builder.Services.AddScoped<IErrorService, DefaultErrorService>();
builder.Services.AddScoped<ITokenIntrospectionService, TokenIntrospectionService>();
builder.Services.AddScoped<ITokenRevocationService, TokenRevocationService>();
builder.Services.AddScoped<IEndSessionService, EndSessionService>();

// Refresh Token 輪替服務
builder.Services.AddScoped<IRefreshTokenRotationService, RefreshTokenRotationService>();

// MFA Services 註冊
builder.Services.AddScoped<LocalIdentityServer.Services.MFA.ITotpService, LocalIdentityServer.Services.MFA.TotpService>();
builder.Services.AddScoped<LocalIdentityServer.Services.MFA.IQrCodeService, LocalIdentityServer.Services.MFA.QrCodeService>();
builder.Services.AddScoped<LocalIdentityServer.Services.MFA.ISmsService, LocalIdentityServer.Services.MFA.SmsService>();
builder.Services.AddScoped<LocalIdentityServer.Services.MFA.IEmailService, LocalIdentityServer.Services.MFA.EmailService>();
builder.Services.AddScoped<LocalIdentityServer.Services.MFA.IMfaEncryptionService, LocalIdentityServer.Services.MFA.MfaEncryptionService>();
builder.Services.AddScoped<LocalIdentityServer.Services.MFA.IMfaAuditService, LocalIdentityServer.Services.MFA.MfaAuditService>();
builder.Services.AddScoped<LocalIdentityServer.Services.MFA.IMfaBackupCodeService, LocalIdentityServer.Services.MFA.MfaBackupCodeService>();
builder.Services.AddScoped<LocalIdentityServer.Services.MFA.IMfaService, LocalIdentityServer.Services.MFA.MfaService>();
builder.Services.AddScoped<LocalIdentityServer.Services.MFA.IOtpDeliveryService, LocalIdentityServer.Services.MFA.OtpDeliveryService>();
builder.Services.AddScoped<LocalIdentityServer.Services.MFA.IMfaBruteForceProtectionService, LocalIdentityServer.Services.MFA.MfaBruteForceProtectionService>();

// Rate Limiting 服務 (使用內存快取避免 Redis 依賴)
builder.Services.AddRateLimiting(builder.Configuration, useRedis: false);

// Controllers for MFA APIs
builder.Services.AddControllers();

// 安全標頭與 HTTPS 強化服務 (企業級安全配置)
builder.Services.AddSecurityServices(builder.Configuration);
builder.Services.AddSecurityHealthChecks(builder.Configuration);

// 企業級金鑰管理服務 (遵循依賴反轉原則)
builder.Services.Configure<KeyManagementOptions>(
    builder.Configuration.GetSection(KeyManagementOptions.SectionName));

// Data Protection for key encryption
builder.Services.AddDataProtection();

builder.Services.AddScoped<IKeyDataProtectionService, KeyDataProtectionService>();
builder.Services.AddScoped<IKeyManagementService, DefaultKeyManagementService>();

// Key Vault 支援 (可選) - 暫時停用以專注測試核心金鑰管理功能
// builder.Services.Configure<AzureKeyVaultOptions>(
//     builder.Configuration.GetSection(AzureKeyVaultOptions.SectionName));
// 註冊 Key Vault 服務 (根據配置啟用)
// var azureKeyVaultOptions = builder.Configuration.GetSection(AzureKeyVaultOptions.SectionName).Get<AzureKeyVaultOptions>();
// if (azureKeyVaultOptions?.Enabled == true)
// {
//     builder.Services.AddScoped<IExternalKeyVault, AzureKeyVaultService>();
// }

// 自動金鑰輪替背景服務
builder.Services.AddHostedService<KeyRotationBackgroundService>();

// InMemory stores (暫時保留，將逐步遷移至 Repository)
builder.Services.AddSingleton<IAuthorizationCodeStore, InMemoryAuthorizationCodeStore>();
builder.Services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
builder.Services.AddSingleton(users);
builder.Services.AddSingleton(clients);

// RSA key 配置 (生產環境應使用持久化金鑰)
using var rsa = RSA.Create(2048);
var key = new RsaSecurityKey(rsa) { KeyId = Guid.NewGuid().ToString("N") };
var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

builder.Services.AddSingleton(signingCredentials);
builder.Services.AddSingleton<RsaSecurityKey>(key);

// 身份驗證配置
builder.Services.AddAuthentication()
    .AddCookie("auth", opt =>
    {
        opt.LoginPath = "/login";
        opt.Cookie.Name = "idsrv.session";
        opt.SlidingExpiration = true;
    })
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "https://localhost:5055",
            ValidAudience = "api_resource_a",
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromSeconds(5)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// 確保資料庫已建立
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<LocalIdentityDbContext>();
    context.Database.EnsureCreated();
    
    // 初始化憑證監控
    app.Services.InitializeCertificateMonitoring(builder.Configuration);
}

// 安全中間件管道 (企業級 HTTPS 與安全標頭強化)
app.UseSecurityConfiguration(app.Environment);

app.UseStaticFiles();

// Rate Limiting 中間件 (在認證之前執行)
app.UseRateLimiting();

// MFA 專用 Rate Limiting 中間件
app.UseMfaRateLimit();

// 配置端點 - 遵循單一責任原則 (SRP)
app.MapDiscoveryEndpoints();
app.MapAuthenticationEndpoints();
app.MapAuthorizationEndpoints();
app.MapUserInfoEndpoints();
app.MapIntrospectionEndpoints();
app.MapRevocationEndpoints();
app.MapEndSessionEndpoints();

// MFA API Controllers
app.MapControllers();

// 錯誤頁面端點
app.MapGet("/error", (HttpContext ctx) =>
{
    ctx.Response.ContentType = "text/html";
    return ctx.Response.SendFileAsync("wwwroot/error.html");
});

app.Run();

// 讓 Program 類別對測試專案可見
public partial class Program { }
