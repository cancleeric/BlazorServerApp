using BlazorAuthSdk.Authentication;
using BlazorAuthSdk.Services;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Register BlazorAuthSdk services
builder.Services.AddHttpClient<IAuthClientService, AuthClientService>();
builder.Services.AddScoped<IAuthClientService, AuthClientService>();
builder.Services.AddScoped<IAuthService, BlazorAuthSdk.Services.AuthService>();

// Register the custom AuthenticationStateProvider from BlazorAuthSdk
builder.Services.AddScoped<AuthenticationStateProvider, JwtAuthenticationStateProvider>();

// Add AuthorizationCore for Blazor's built-in authorization features
builder.Services.AddAuthorizationCore();

// Add IConfiguration for BlazorAuthSdk services
builder.Services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// IMPORTANT: UseAuthentication must be before UseAuthorization
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();