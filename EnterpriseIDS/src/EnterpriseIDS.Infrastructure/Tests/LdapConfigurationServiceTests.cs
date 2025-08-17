using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection;
using Moq;
using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Infrastructure.Services;

namespace EnterpriseIDS.Infrastructure.Tests;

/// <summary>
/// LDAP 配置服務單元測試
/// </summary>
public class LdapConfigurationServiceTests
{
    private readonly Mock<ILogger<LdapConfigurationService>> _mockLogger;
    private readonly Mock<ILdapConfigurationRepository> _mockRepository;
    private readonly Mock<IDataProtectionProvider> _mockDataProtectionProvider;
    private readonly Mock<IDataProtector> _mockDataProtector;
    private readonly Mock<ITenantContextService> _mockTenantContextService;
    private readonly LdapConfigurationService _configService;

    public LdapConfigurationServiceTests()
    {
        _mockLogger = new Mock<ILogger<LdapConfigurationService>>();
        _mockRepository = new Mock<ILdapConfigurationRepository>();
        _mockDataProtectionProvider = new Mock<IDataProtectionProvider>();
        _mockDataProtector = new Mock<IDataProtector>();
        _mockTenantContextService = new Mock<ITenantContextService>();

        _mockDataProtectionProvider
            .Setup(p => p.CreateProtector("LdapConfiguration.SensitiveData"))
            .Returns(_mockDataProtector.Object);

        _configService = new LdapConfigurationService(
            _mockLogger.Object,
            _mockRepository.Object,
            _mockDataProtectionProvider.Object,
            _mockTenantContextService.Object);
    }

    /// <summary>
    /// 測試取得配置功能
    /// </summary>
    public async Task<bool> TestGetConfigurationAsync()
    {
        try
        {
            var tenantId = Guid.NewGuid();
            var configuration = new LdapConfiguration
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Test Config",
                ServiceAccountPassword = "encrypted_password"
            };

            _mockRepository
                .Setup(r => r.GetByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(configuration);

            _mockDataProtector
                .Setup(p => p.Unprotect("encrypted_password"))
                .Returns("decrypted_password");

            var result = await _configService.GetConfigurationAsync(tenantId);

            if (result == null)
                throw new Exception("應該返回配置");

            if (result.Id != configuration.Id)
                throw new Exception("配置 ID 不匹配");

            // 驗證敏感資料被解密
            if (result.ServiceAccountPassword != "decrypted_password")
                throw new Exception("密碼解密失敗");

            Console.WriteLine("✅ TestGetConfigurationAsync - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestGetConfigurationAsync - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試儲存配置功能
    /// </summary>
    public async Task<bool> TestSaveConfigurationAsync()
    {
        try
        {
            var tenantId = Guid.NewGuid();
            var configuration = new LdapConfiguration
            {
                Id = Guid.Empty, // 新配置
                TenantId = Guid.Empty,
                Name = "New Config",
                ServerUrl = "ldap://test.com",
                BaseDn = "DC=test,DC=com",
                Port = 389,
                AuthenticationType = LdapAuthenticationType.Simple,
                ServiceAccountDn = "CN=service,DC=test,DC=com",
                ServiceAccountPassword = "plain_password"
            };

            _mockTenantContextService
                .Setup(s => s.GetCurrentTenantId())
                .Returns(tenantId);

            _mockDataProtector
                .Setup(p => p.Protect("plain_password"))
                .Returns("encrypted_password");

            _mockRepository
                .Setup(r => r.CreateAsync(It.IsAny<LdapConfiguration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LdapConfiguration config, CancellationToken token) => config);

            var result = await _configService.SaveConfigurationAsync(configuration);

            if (result == null)
                throw new Exception("應該返回儲存的配置");

            if (result.TenantId != tenantId)
                throw new Exception("租戶 ID 應該自動設定");

            if (result.Id == Guid.Empty)
                throw new Exception("新配置應該生成 ID");

            // 驗證敏感資料被解密返回
            if (result.ServiceAccountPassword != "plain_password")
                throw new Exception("返回的配置應該包含解密的密碼");

            Console.WriteLine("✅ TestSaveConfigurationAsync - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestSaveConfigurationAsync - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試配置驗證功能
    /// </summary>
    public async Task<bool> TestConfigurationValidation()
    {
        try
        {
            // 測試無效配置 - 缺少必要欄位
            var invalidConfig = new LdapConfiguration
            {
                Name = "", // 必要欄位為空
                ServerUrl = "ldap://test.com",
                BaseDn = "DC=test,DC=com"
            };

            try
            {
                await _configService.SaveConfigurationAsync(invalidConfig);
                throw new Exception("無效配置應該拋出異常");
            }
            catch (ArgumentException)
            {
                // 預期的異常
            }

            Console.WriteLine("✅ TestConfigurationValidation - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestConfigurationValidation - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試刪除配置功能
    /// </summary>
    public async Task<bool> TestDeleteConfigurationAsync()
    {
        try
        {
            var configId = Guid.NewGuid();

            _mockRepository
                .Setup(r => r.DeleteAsync(configId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await _configService.DeleteConfigurationAsync(configId);

            if (!result)
                throw new Exception("刪除應該成功");

            Console.WriteLine("✅ TestDeleteConfigurationAsync - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestDeleteConfigurationAsync - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試加密解密功能
    /// </summary>
    public async Task<bool> TestEncryptDecryptSensitiveDataAsync()
    {
        try
        {
            var plainText = "sensitive_data";
            var encryptedText = "encrypted_data";

            _mockDataProtector
                .Setup(p => p.Protect(plainText))
                .Returns(encryptedText);

            _mockDataProtector
                .Setup(p => p.Unprotect(encryptedText))
                .Returns(plainText);

            // 測試加密
            var encrypted = await _configService.EncryptSensitiveDataAsync(plainText);
            if (encrypted != encryptedText)
                throw new Exception("加密結果不正確");

            // 測試解密
            var decrypted = await _configService.DecryptSensitiveDataAsync(encryptedText);
            if (decrypted != plainText)
                throw new Exception("解密結果不正確");

            // 測試空字串
            var emptyEncrypted = await _configService.EncryptSensitiveDataAsync("");
            if (emptyEncrypted != "")
                throw new Exception("空字串加密應該返回空字串");

            var emptyDecrypted = await _configService.DecryptSensitiveDataAsync("");
            if (emptyDecrypted != "")
                throw new Exception("空字串解密應該返回空字串");

            Console.WriteLine("✅ TestEncryptDecryptSensitiveDataAsync - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestEncryptDecryptSensitiveDataAsync - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 測試取得啟用配置功能
    /// </summary>
    public async Task<bool> TestGetEnabledConfigurationsAsync()
    {
        try
        {
            var configurations = new List<LdapConfiguration>
            {
                new LdapConfiguration
                {
                    Id = Guid.NewGuid(),
                    Name = "Config 1",
                    IsEnabled = true,
                    ServiceAccountPassword = "encrypted1"
                },
                new LdapConfiguration
                {
                    Id = Guid.NewGuid(),
                    Name = "Config 2",
                    IsEnabled = true,
                    ServiceAccountPassword = "encrypted2"
                }
            };

            _mockRepository
                .Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(configurations);

            _mockDataProtector
                .Setup(p => p.Unprotect("encrypted1"))
                .Returns("decrypted1");

            _mockDataProtector
                .Setup(p => p.Unprotect("encrypted2"))
                .Returns("decrypted2");

            var result = await _configService.GetEnabledConfigurationsAsync();

            if (result.Count() != 2)
                throw new Exception("應該返回 2 個配置");

            var resultList = result.ToList();
            if (resultList[0].ServiceAccountPassword != "decrypted1" ||
                resultList[1].ServiceAccountPassword != "decrypted2")
                throw new Exception("密碼解密失敗");

            Console.WriteLine("✅ TestGetEnabledConfigurationsAsync - PASSED");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TestGetEnabledConfigurationsAsync - FAILED: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 執行所有 LDAP 配置服務測試
    /// </summary>
    public static async Task<bool> RunAllTestsAsync()
    {
        Console.WriteLine("🧪 Running LDAP Configuration Service Tests");
        Console.WriteLine("=" + new string('=', 45));

        var testInstance = new LdapConfigurationServiceTests();
        var tests = new Func<Task<bool>>[]
        {
            testInstance.TestGetConfigurationAsync,
            testInstance.TestSaveConfigurationAsync,
            testInstance.TestConfigurationValidation,
            testInstance.TestDeleteConfigurationAsync,
            testInstance.TestEncryptDecryptSensitiveDataAsync,
            testInstance.TestGetEnabledConfigurationsAsync
        };

        var passedTests = 0;
        var totalTests = tests.Length;

        foreach (var test in tests)
        {
            try
            {
                if (await test())
                    passedTests++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test execution failed: {ex.Message}");
            }
        }

        Console.WriteLine($"\n📊 LDAP Configuration Service Test Results: {passedTests}/{totalTests} tests passed");
        return passedTests == totalTests;
    }
}