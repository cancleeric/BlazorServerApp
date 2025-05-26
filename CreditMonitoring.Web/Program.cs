using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Components.Authorization;
using CreditMonitoring.Web.Extensions;
using CreditMonitoring.Web.Services;
using CreditMonitoring.Web.Interfaces;
using CreditMonitoring.Common.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// 在開發環境中使用簡化的認證設置
if (builder.Environment.IsDevelopment())
{
    // 開發環境使用簡單的 cookie 認證
    builder.Services.AddAuthentication("Cookies")
        .AddCookie("Cookies", options =>
        {
            options.LoginPath = "/login";
            options.LogoutPath = "/logout";
        });
}
else
{
    // 生產環境使用完整的 OpenID Connect 認證
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "Cookies";
        options.DefaultChallengeScheme = "oidc";
    })
    .AddCookie("Cookies")
    .AddMultipleOpenIdConnect(builder.Configuration);
}

// 添加授權
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAuthenticatedUser", policy =>
        policy.RequireAuthenticatedUser());
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