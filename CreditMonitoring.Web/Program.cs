using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Components.Authorization;
using CreditMonitoring.Web.Extensions;
using CreditMonitoring.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// 添加認證配置
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies"; // 設定預設的認證方案為 Cookies
    options.DefaultChallengeScheme = "oidc"; // 設定預設的挑戰方案為 OpenID Connect
})
.AddCookie("Cookies") // 添加 Cookies 認證方案
.AddMultipleOpenIdConnect(builder.Configuration); // 添加多個 OpenID Connect 認證方案

// 添加授權
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAuthenticatedUser", policy =>
        policy.RequireAuthenticatedUser()); // 添加一個需要認證用戶的授權策略
});

// 添加服務到容器
builder.Services.AddRazorPages(); // 添加 Razor Pages 服務
builder.Services.AddServerSideBlazor(); // 添加 Server-Side Blazor 服務
builder.Services.AddHttpContextAccessor(); // 添加 HttpContextAccessor 服務
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>(); // 添加自訂的 AuthenticationStateProvider 服務
builder.Services.AddHttpClient<ICreditMonitoringService, CreditMonitoringService>(); // 添加 HttpClient 服務

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

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();