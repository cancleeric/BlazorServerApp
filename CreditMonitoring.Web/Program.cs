using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Components.Authorization;
using CreditMonitoring.Web.Extensions;
using CreditMonitoring.Web.Services;
using CreditMonitoring.Web.Interfaces;
using CreditMonitoring.Common.Interfaces;
using CreditMonitoring.Common.Services;

var builder = WebApplication.CreateBuilder(args);

// 設定API基礎URL
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5000";

// 註冊HttpClient用於API呼叫
builder.Services.AddHttpClient("CreditMonitoringApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// 註冊JWT相關服務
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();

// 設定多重身份認證
if (builder.Environment.IsDevelopment())
{
    // 開發環境：支援Cookie和JWT雙重認證
    builder.Services.AddAuthentication("Cookies")
        .AddCookie("Cookies", options =>
        {
            options.LoginPath = "/login";
            options.LogoutPath = "/logout";
        });
    
    // 註冊混合身份認證狀態提供者
    builder.Services.AddScoped<AuthenticationStateProvider>(provider =>
    {
        // 可以根據需要選擇使用哪種認證方式
        // 這裡預設使用JWT認證
        return provider.GetRequiredService<JwtAuthenticationStateProvider>();
    });
}
else
{
    // 生產環境：使用OpenID Connect + JWT混合認證
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "Cookies";
        options.DefaultChallengeScheme = "oidc";
    })
    .AddCookie("Cookies")
    .AddMultipleOpenIdConnect(builder.Configuration);
    
    // 生產環境使用原有的CustomAuthenticationStateProvider
    builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
}

// 添加授權策略
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAuthenticatedUser", policy =>
        policy.RequireAuthenticatedUser());
    
    // JWT角色基礎授權策略
    options.AddPolicy("RequireAdmin", policy => 
        policy.RequireRole("Admin"));
    options.AddPolicy("RequireCreditOfficer", policy => 
        policy.RequireRole("CreditOfficer", "Manager", "Admin"));
    options.AddPolicy("RequireManager", policy => 
        policy.RequireRole("Manager", "Admin"));
    options.AddPolicy("RequireAuditor", policy => 
        policy.RequireRole("Auditor", "Admin"));
});

// 添加服務到容器
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddHttpClient<IWebCreditMonitoringService, CreditMonitoringService>();
builder.Services.AddScoped<ICreditMonitoringService>(provider => 
    provider.GetRequiredService<IWebCreditMonitoringService>());

var app = builder.Build();

// 配置 HTTP 請求管道
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 添加認證和授權中間件
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();