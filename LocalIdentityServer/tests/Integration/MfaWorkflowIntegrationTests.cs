using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using Xunit;
using LocalIdentityServer.Data;
using LocalIdentityServer.Data.Entities;
using LocalIdentityServer.Models.Requests;
using LocalIdentityServer.Models.Responses;
using LocalIdentityServer.Services.MFA;

namespace LocalIdentityServer.Tests.Integration;

/// <summary>
/// 測試完整的 MFA 工作流程，包括設置、驗證和管理
/// </summary>
public class MfaWorkflowIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public MfaWorkflowIntegrationTests(WebApplicationFactory<Program> factory)
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
                    options.UseInMemoryDatabase("WorkflowTestDb_" + Guid.NewGuid());
                });
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CompleteTotp_Workflow_ShouldWork()
    {
        // Arrange
        var userId = "workflow-user-1";
        await SeedTestUserAsync(userId);

        // Step 1: 檢查初始 MFA 狀態 (應該沒有啟用)
        var initialStatusResponse = await _client.GetAsync($"/api/mfa/status/{userId}");
        initialStatusResponse.EnsureSuccessStatusCode();
        var initialStatus = await initialStatusResponse.Content.ReadFromJsonAsync<MfaStatusResponse>();
        
        Assert.NotNull(initialStatus);
        Assert.False(initialStatus.IsTotpEnabled);

        // Step 2: 設置 TOTP
        var setupRequest = new SetupTotpRequest { UserId = userId };
        var setupResponse = await _client.PostAsJsonAsync("/api/mfa/setup/totp", setupRequest);
        setupResponse.EnsureSuccessStatusCode();
        var setupResult = await setupResponse.Content.ReadFromJsonAsync<SetupTotpResponse>();
        
        Assert.NotNull(setupResult);
        Assert.False(string.IsNullOrEmpty(setupResult.SecretKey));

        // Step 3: 驗證設置是否成功
        var statusAfterSetupResponse = await _client.GetAsync($"/api/mfa/status/{userId}");
        statusAfterSetupResponse.EnsureSuccessStatusCode();
        var statusAfterSetup = await statusAfterSetupResponse.Content.ReadFromJsonAsync<MfaStatusResponse>();
        
        Assert.NotNull(statusAfterSetup);
        Assert.True(statusAfterSetup.IsTotpEnabled);

        // Step 4: 生成並驗證 TOTP 代碼
        using var scope = _factory.Services.CreateScope();
        var totpService = scope.ServiceProvider.GetRequiredService<ITotpService>();
        var validCode = totpService.GetCurrentTotp(setupResult.SecretKey);

        var verifyRequest = new VerifyTotpRequest
        {
            UserId = userId,
            Code = validCode
        };

        var verifyResponse = await _client.PostAsJsonAsync("/api/mfa/verify/totp", verifyRequest);
        verifyResponse.EnsureSuccessStatusCode();
        var verifyResult = await verifyResponse.Content.ReadFromJsonAsync<VerifyTotpResponse>();
        
        Assert.NotNull(verifyResult);
        Assert.True(verifyResult.IsValid);

        // Step 5: 停用 TOTP
        var disableRequest = new DisableMfaRequest
        {
            UserId = userId,
            Method = "totp"
        };

        var disableResponse = await _client.PostAsJsonAsync("/api/mfa/disable", disableRequest);
        disableResponse.EnsureSuccessStatusCode();
        var disableResult = await disableResponse.Content.ReadFromJsonAsync<DisableMfaResponse>();
        
        Assert.NotNull(disableResult);
        Assert.True(disableResult.IsDisabled);

        // Step 6: 確認停用後的狀態
        var finalStatusResponse = await _client.GetAsync($"/api/mfa/status/{userId}");
        finalStatusResponse.EnsureSuccessStatusCode();
        var finalStatus = await finalStatusResponse.Content.ReadFromJsonAsync<MfaStatusResponse>();
        
        Assert.NotNull(finalStatus);
        Assert.False(finalStatus.IsTotpEnabled);
    }

    [Fact]
    public async Task CompleteBackupCodes_Workflow_ShouldWork()
    {
        // Arrange
        var userId = "workflow-user-2";
        await SeedTestUserAsync(userId);

        // Step 1: 生成備援代碼
        var generateRequest = new GenerateBackupCodesRequest { UserId = userId };
        var generateResponse = await _client.PostAsJsonAsync("/api/mfa/backup-codes/generate", generateRequest);
        generateResponse.EnsureSuccessStatusCode();
        var generateResult = await generateResponse.Content.ReadFromJsonAsync<GenerateBackupCodesResponse>();
        
        Assert.NotNull(generateResult);
        Assert.NotEmpty(generateResult.BackupCodes);
        var originalBackupCodes = generateResult.BackupCodes.ToList();

        // Step 2: 使用一個備援代碼
        var firstCode = originalBackupCodes.First();
        var verifyRequest = new VerifyBackupCodeRequest
        {
            UserId = userId,
            Code = firstCode
        };

        var verifyResponse = await _client.PostAsJsonAsync("/api/mfa/verify/backup-code", verifyRequest);
        verifyResponse.EnsureSuccessStatusCode();
        var verifyResult = await verifyResponse.Content.ReadFromJsonAsync<VerifyBackupCodeResponse>();
        
        Assert.NotNull(verifyResult);
        Assert.True(verifyResult.IsValid);

        // Step 3: 嘗試重複使用相同代碼 (應該失敗)
        var retryResponse = await _client.PostAsJsonAsync("/api/mfa/verify/backup-code", verifyRequest);
        retryResponse.EnsureSuccessStatusCode();
        var retryResult = await retryResponse.Content.ReadFromJsonAsync<VerifyBackupCodeResponse>();
        
        Assert.NotNull(retryResult);
        Assert.False(retryResult.IsValid);

        // Step 4: 重新生成備援代碼
        var regenerateResponse = await _client.PostAsJsonAsync("/api/mfa/backup-codes/generate", generateRequest);
        regenerateResponse.EnsureSuccessStatusCode();
        var regenerateResult = await regenerateResponse.Content.ReadFromJsonAsync<GenerateBackupCodesResponse>();
        
        Assert.NotNull(regenerateResult);
        Assert.NotEmpty(regenerateResult.BackupCodes);
        
        // 確認新代碼與舊代碼不同
        var newBackupCodes = regenerateResult.BackupCodes.ToList();
        Assert.NotEqual(originalBackupCodes, newBackupCodes);

        // Step 5: 使用新的備援代碼應該成功
        var newCode = newBackupCodes.First();
        var newVerifyRequest = new VerifyBackupCodeRequest
        {
            UserId = userId,
            Code = newCode
        };

        var newVerifyResponse = await _client.PostAsJsonAsync("/api/mfa/verify/backup-code", newVerifyRequest);
        newVerifyResponse.EnsureSuccessStatusCode();
        var newVerifyResult = await newVerifyResponse.Content.ReadFromJsonAsync<VerifyBackupCodeResponse>();
        
        Assert.NotNull(newVerifyResult);
        Assert.True(newVerifyResult.IsValid);
    }

    [Fact]
    public async Task MultiMethod_MfaSetup_ShouldWork()
    {
        // Arrange
        var userId = "workflow-user-3";
        await SeedTestUserAsync(userId);

        // Step 1: 設置 TOTP
        var totpSetupRequest = new SetupTotpRequest { UserId = userId };
        var totpSetupResponse = await _client.PostAsJsonAsync("/api/mfa/setup/totp", totpSetupRequest);
        totpSetupResponse.EnsureSuccessStatusCode();

        // Step 2: 設置 SMS
        var smsSetupRequest = new SetupSmsRequest
        {
            UserId = userId,
            PhoneNumber = "+1234567890"
        };
        var smsSetupResponse = await _client.PostAsJsonAsync("/api/mfa/setup/sms", smsSetupRequest);
        smsSetupResponse.EnsureSuccessStatusCode();

        // Step 3: 檢查狀態 (兩種方法都應該啟用)
        var statusResponse = await _client.GetAsync($"/api/mfa/status/{userId}");
        statusResponse.EnsureSuccessStatusCode();
        var status = await statusResponse.Content.ReadFromJsonAsync<MfaStatusResponse>();
        
        Assert.NotNull(status);
        Assert.True(status.IsTotpEnabled);
        Assert.True(status.IsSmsEnabled);

        // Step 4: 停用 TOTP (SMS 應該仍然啟用)
        var disableTotpRequest = new DisableMfaRequest
        {
            UserId = userId,
            Method = "totp"
        };
        var disableTotpResponse = await _client.PostAsJsonAsync("/api/mfa/disable", disableTotpRequest);
        disableTotpResponse.EnsureSuccessStatusCode();

        // Step 5: 檢查部分停用後的狀態
        var partialStatusResponse = await _client.GetAsync($"/api/mfa/status/{userId}");
        partialStatusResponse.EnsureSuccessStatusCode();
        var partialStatus = await partialStatusResponse.Content.ReadFromJsonAsync<MfaStatusResponse>();
        
        Assert.NotNull(partialStatus);
        Assert.False(partialStatus.IsTotpEnabled);
        Assert.True(partialStatus.IsSmsEnabled);

        // Step 6: 停用 SMS
        var disableSmsRequest = new DisableMfaRequest
        {
            UserId = userId,
            Method = "sms"
        };
        var disableSmsResponse = await _client.PostAsJsonAsync("/api/mfa/disable", disableSmsRequest);
        disableSmsResponse.EnsureSuccessStatusCode();

        // Step 7: 檢查完全停用後的狀態
        var finalStatusResponse = await _client.GetAsync($"/api/mfa/status/{userId}");
        finalStatusResponse.EnsureSuccessStatusCode();
        var finalStatus = await finalStatusResponse.Content.ReadFromJsonAsync<MfaStatusResponse>();
        
        Assert.NotNull(finalStatus);
        Assert.False(finalStatus.IsTotpEnabled);
        Assert.False(finalStatus.IsSmsEnabled);
    }

    [Fact]
    public async Task MfaAudit_ShouldLogAllOperations()
    {
        // Arrange
        var userId = "workflow-user-4";
        await SeedTestUserAsync(userId);

        // Step 1: 執行各種 MFA 操作
        var setupRequest = new SetupTotpRequest { UserId = userId };
        await _client.PostAsJsonAsync("/api/mfa/setup/totp", setupRequest);

        var generateBackupRequest = new GenerateBackupCodesRequest { UserId = userId };
        await _client.PostAsJsonAsync("/api/mfa/backup-codes/generate", generateBackupRequest);

        var disableRequest = new DisableMfaRequest { UserId = userId, Method = "totp" };
        await _client.PostAsJsonAsync("/api/mfa/disable", disableRequest);

        // Step 2: 檢查審計日誌
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LocalIdentityDbContext>();
        
        var auditLogs = await context.MfaAuditLogs
            .Where(log => log.UserId == userId)
            .OrderBy(log => log.CreatedAt)
            .ToListAsync();

        // Assert - 應該有多個審計記錄
        Assert.NotEmpty(auditLogs);
        
        // 檢查是否包含所有操作類型
        var operationTypes = auditLogs.Select(log => log.EventType).ToList();
        Assert.Contains("setup_totp", operationTypes);
        Assert.Contains("generate_backup_codes", operationTypes);
        Assert.Contains("disable_mfa", operationTypes);

        // 檢查審計記錄的完整性
        Assert.All(auditLogs, log =>
        {
            Assert.Equal(userId, log.UserId);
            Assert.False(string.IsNullOrEmpty(log.EventType));
            Assert.True(log.CreatedAt > DateTime.MinValue);
        });
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