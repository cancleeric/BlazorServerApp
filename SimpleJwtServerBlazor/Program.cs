

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(); // 新增 ServerSide Blazor 支援
// 新增：註冊 HttpClient
builder.Services.AddHttpClient();
// 新增：註冊 JwtAuthenticationStateProvider
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, SimpleJwtServerBlazor.Services.JwtAuthenticationStateProvider>();
builder.Services.AddScoped<SimpleJwtServerBlazor.Services.JwtAuthenticationStateProvider>();
// 新增：Blazor Server Authentication
builder.Services.AddAuthorizationCore();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();
app.MapBlazorHub(); // 新增 Blazor Hub
app.MapFallbackToPage("/_Host"); // 新增 fallback

app.Run();
