using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using LocalIdentityServer.Services;
using LocalIdentityServer.Stores;
using LocalIdentityServer.Models;
using LocalIdentityServer.Data;
using LocalIdentityServer.Data.Repositories;
using LocalIdentityServer.Endpoints;
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

// Services 註冊
builder.Services.AddScoped<ITokenService, DefaultTokenService>();
builder.Services.AddScoped<IErrorService, DefaultErrorService>();

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
}

app.UseStaticFiles();

// 配置端點 - 遵循單一責任原則 (SRP)
app.MapDiscoveryEndpoints();
// app.MapAuthenticationEndpoints(); // TODO: 修復AuthenticationEndpoints.cs後啟用
app.MapAuthorizationEndpoints();
app.MapUserInfoEndpoints();

// 錯誤頁面端點
app.MapGet("/error", (HttpContext ctx) =>
{
    ctx.Response.ContentType = "text/html";
    return ctx.Response.SendFileAsync("wwwroot/error.html");
});

app.Run();
