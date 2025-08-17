using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;
using LocalIdentityServer.Data;
using LocalIdentityServer.Data.Entities;
using LocalIdentityServer.Models.Requests;
using LocalIdentityServer.Models.Responses;
using LocalIdentityServer.Services.MFA;

namespace LocalIdentityServer.Tests.Integration;

public class MfaIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public MfaIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // 移除現有的 DbContext 註冊
                var dbContextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<LocalIdentityDbContext>));
                if (dbContextDescriptor != null)
                {
                    services.Remove(dbContextDescriptor);
                }
                
                // 移除通用 DbContextOptions 描述符
                var dbContextOptionsDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions));
                if (dbContextOptionsDescriptor != null)
                {
                    services.Remove(dbContextOptionsDescriptor);
                }

                // 移除 DbContext 自身的註冊
                var contextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(LocalIdentityDbContext));
                if (contextDescriptor != null)
                {
                    services.Remove(contextDescriptor);
                }

                // 使用 In-Memory 資料庫進行測試
                services.AddDbContext<LocalIdentityDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
                });
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task SetupTotp_WithValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var userId = "test-user-1";
        await SeedTestUserAsync(userId);

        var request = new SetupTotpRequest
        {
            UserId = userId
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/mfa/setup/totp", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SetupTotpResponse>();
        
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.SecretKey));
        Assert.False(string.IsNullOrEmpty(result.QrCodeUri));
        Assert.True(result.QrCodeUri.Contains("otpauth://totp/"));
    }

    [Fact]
    public async Task VerifyTotp_WithValidCode_ShouldReturnSuccess()
    {
        // Arrange
        var userId = "test-user-2";
        await SeedTestUserAsync(userId);

        // 先設置 TOTP
        var setupRequest = new SetupTotpRequest { UserId = userId };
        var setupResponse = await _client.PostAsJsonAsync("/api/mfa/setup/totp", setupRequest);
        var setupResult = await setupResponse.Content.ReadFromJsonAsync<SetupTotpResponse>();

        // 生成有效的 TOTP 代碼
        using var scope = _factory.Services.CreateScope();
        var totpService = scope.ServiceProvider.GetRequiredService<ITotpService>();
        var validCode = totpService.GetCurrentTotp(setupResult!.SecretKey);

        var verifyRequest = new VerifyTotpRequest
        {
            UserId = userId,
            Code = validCode
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/mfa/verify/totp", verifyRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<VerifyTotpResponse>();
        
        Assert.NotNull(result);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task VerifyTotp_WithInvalidCode_ShouldReturnFailure()
    {
        // Arrange
        var userId = "test-user-3";
        await SeedTestUserAsync(userId);

        // 先設置 TOTP
        var setupRequest = new SetupTotpRequest { UserId = userId };
        await _client.PostAsJsonAsync("/api/mfa/setup/totp", setupRequest);

        var verifyRequest = new VerifyTotpRequest
        {
            UserId = userId,
            Code = "invalid"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/mfa/verify/totp", verifyRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<VerifyTotpResponse>();
        
        Assert.NotNull(result);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task GenerateBackupCodes_WithValidRequest_ShouldReturnCodes()
    {
        // Arrange
        var userId = "test-user-4";
        await SeedTestUserAsync(userId);

        var request = new GenerateBackupCodesRequest
        {
            UserId = userId
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/mfa/backup-codes/generate", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GenerateBackupCodesResponse>();
        
        Assert.NotNull(result);
        Assert.NotEmpty(result.BackupCodes);
        Assert.Equal(10, result.BackupCodes.Count); // 預設生成 10 組備援代碼
        Assert.All(result.BackupCodes, code => Assert.Equal(8, code.Length)); // 每組代碼長度為 8
    }

    [Fact]
    public async Task VerifyBackupCode_WithValidCode_ShouldReturnSuccess()
    {
        // Arrange
        var userId = "test-user-5";
        await SeedTestUserAsync(userId);

        // 先生成備援代碼
        var generateRequest = new GenerateBackupCodesRequest { UserId = userId };
        var generateResponse = await _client.PostAsJsonAsync("/api/mfa/backup-codes/generate", generateRequest);
        var generateResult = await generateResponse.Content.ReadFromJsonAsync<GenerateBackupCodesResponse>();

        var backupCode = generateResult!.BackupCodes.First();
        var verifyRequest = new VerifyBackupCodeRequest
        {
            UserId = userId,
            Code = backupCode
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/mfa/verify/backup-code", verifyRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<VerifyBackupCodeResponse>();
        
        Assert.NotNull(result);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task VerifyBackupCode_WithUsedCode_ShouldReturnFailure()
    {
        // Arrange
        var userId = "test-user-6";
        await SeedTestUserAsync(userId);

        // 先生成備援代碼
        var generateRequest = new GenerateBackupCodesRequest { UserId = userId };
        var generateResponse = await _client.PostAsJsonAsync("/api/mfa/backup-codes/generate", generateRequest);
        var generateResult = await generateResponse.Content.ReadFromJsonAsync<GenerateBackupCodesResponse>();

        var backupCode = generateResult!.BackupCodes.First();
        var verifyRequest = new VerifyBackupCodeRequest
        {
            UserId = userId,
            Code = backupCode
        };

        // 第一次驗證 (應該成功)
        await _client.PostAsJsonAsync("/api/mfa/verify/backup-code", verifyRequest);

        // Act - 第二次使用相同代碼 (應該失敗)
        var response = await _client.PostAsJsonAsync("/api/mfa/verify/backup-code", verifyRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<VerifyBackupCodeResponse>();
        
        Assert.NotNull(result);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task GetMfaStatus_WithConfiguredMfa_ShouldReturnCorrectStatus()
    {
        // Arrange
        var userId = "test-user-7";
        await SeedTestUserAsync(userId);

        // 設置 TOTP
        var setupRequest = new SetupTotpRequest { UserId = userId };
        await _client.PostAsJsonAsync("/api/mfa/setup/totp", setupRequest);

        // Act
        var response = await _client.GetAsync($"/api/mfa/status/{userId}");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<MfaStatusResponse>();
        
        Assert.NotNull(result);
        Assert.True(result.IsTotpEnabled);
        Assert.False(result.IsSmsEnabled);
        Assert.False(result.IsEmailEnabled);
    }

    [Fact]
    public async Task SetupSms_WithValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var userId = "test-user-8";
        await SeedTestUserAsync(userId);

        var request = new SetupSmsRequest
        {
            UserId = userId,
            PhoneNumber = "+1234567890"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/mfa/setup/sms", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SetupSmsResponse>();
        
        Assert.NotNull(result);
        Assert.True(result.IsSetupSuccessful);
    }

    [Fact]
    public async Task DisableMfa_WithValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var userId = "test-user-9";
        await SeedTestUserAsync(userId);

        // 先設置 TOTP
        var setupRequest = new SetupTotpRequest { UserId = userId };
        await _client.PostAsJsonAsync("/api/mfa/setup/totp", setupRequest);

        var disableRequest = new DisableMfaRequest
        {
            UserId = userId,
            Method = "totp"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/mfa/disable", disableRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DisableMfaResponse>();
        
        Assert.NotNull(result);
        Assert.True(result.IsDisabled);
    }

    [Fact]
    public async Task RateLimiting_WithTooManyRequests_ShouldReturnTooManyRequests()
    {
        // Arrange
        var userId = "test-user-10";
        await SeedTestUserAsync(userId);

        // 先設置 TOTP
        var setupRequest = new SetupTotpRequest { UserId = userId };
        await _client.PostAsJsonAsync("/api/mfa/setup/totp", setupRequest);

        var verifyRequest = new VerifyTotpRequest
        {
            UserId = userId,
            Code = "invalid"
        };

        // Act - 發送大量無效請求觸發 rate limiting
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(_client.PostAsJsonAsync("/api/mfa/verify/totp", verifyRequest));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert - 應該有一些請求被 rate limiting 阻擋
        var rateLimitedResponses = responses.Where(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        Assert.NotEmpty(rateLimitedResponses);
    }

    private async Task SeedTestUserAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LocalIdentityDbContext>();

        // 檢查用戶是否已存在
        var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (existingUser == null)
        {
            var user = new UserEntity
            {
                Id = userId,
                UserName = $"testuser_{userId}",
                Email = $"test_{userId}@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}