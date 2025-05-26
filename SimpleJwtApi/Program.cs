using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SimpleJwtApi.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. 添加基本服務
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. 註冊 JWT 服務
builder.Services.AddScoped<JwtService>();

// 3. 配置 JWT 認證 - 這是最重要的部分！
var jwtKey = builder.Configuration["Jwt:SecretKey"] ?? "MyVerySecretKeyForJwtTokenThatIsAtLeast256Bits";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SimpleJwtApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SimpleJwtApiUsers";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // 驗證設定
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// 4. 添加授權服務
builder.Services.AddAuthorization();

var app = builder.Build();

// 5. 配置 HTTP 請求管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 6. 重要：認證和授權中間件的順序很重要！
app.UseAuthentication(); // 先認證
app.UseAuthorization();  // 後授權

app.MapControllers();

Console.WriteLine("🚀 簡單 JWT API 已啟動！");
Console.WriteLine("📖 Swagger 文檔: https://localhost:7001/swagger");
Console.WriteLine("🔐 測試登入: POST /api/auth/login");
Console.WriteLine("👤 測試用戶: admin/123456 或 user/123456");

app.Run();