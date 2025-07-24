using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 1. 添加基本服務
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. 配置 JWT 認證 - 這是最重要的部分！
// 注意：這裡的 JWT 配置必須與 AuthenticationServer 發行 JWT 的配置一致
var jwtKey = builder.Configuration["Jwt:SecretKey"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLongForJWT"; // 與 AuthenticationServer 的 Jwt:Key 相同
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "AuthenticationServer"; // 與 AuthenticationServer 的 Jwt:Issuer 相同
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SimpleJwtWeb"; // 與 AuthenticationServer 的 Jwt:Audience 相同

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

// 3. 添加授權服務
builder.Services.AddAuthorization();

var app = builder.Build();

// 4. 配置 HTTP 請求管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 5. 重要：認證和授權中間件的順序很重要！
app.UseAuthentication(); // 先認證
app.UseAuthorization();  // 後授權

app.MapControllers();

Console.WriteLine("🚀 簡單 JWT API 已啟動！ (作為資源伺服器)");
Console.WriteLine("📖 Swagger 文檔: https://localhost:7001/swagger");

app.Run();
