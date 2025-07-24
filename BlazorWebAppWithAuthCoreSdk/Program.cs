using BlazorWebAppWithAuthCoreSdk.Components;
using BlazorWebAppWithAuthCoreSdk.Services; // Keep this for IApiService
using BlazorAuthSdk.Services; // Add this using statement
using BlazorAuthSdk.Authentication; // Add this using statement
using Microsoft.AspNetCore.Components.Authorization; // Add this using statement

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// 註冊 HTTP 客戶端和自定義服務
builder.Services.AddHttpClient<IApiService, ApiService>();

// Register BlazorAuthSdk services
builder.Services.AddScoped<IAuthService, BlazorAuthSdk.Services.AuthService>(); // Register AuthService from SDK
builder.Services.AddHttpClient<BlazorAuthSdk.Services.IAuthClientService, BlazorAuthSdk.Services.AuthClientService>(); // Register AuthClientService from SDK
builder.Services.AddScoped<AuthenticationStateProvider, JwtAuthenticationStateProvider>(); // Register the custom AuthenticationStateProvider from SDK

// Add AuthorizationCore for Blazor's built-in authorization features
builder.Services.AddAuthorizationCore();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode();

app.Run();
