using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Core.ValueObjects;
using EnterpriseIDS.Infrastructure.Caching;

namespace EnterpriseIDS.Infrastructure.Tests;

/// <summary>
/// 簡單的 Redis 會話測試程式
/// </summary>
public static class TestRedisSession
{
    public static async Task RunTestAsync()
    {
        Console.WriteLine("=== Redis 分散式會話管理測試 ===");
        
        // 建立記憶體快取進行測試
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        
        // 建立日誌工廠
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });
        
        var sessionLogger = loggerFactory.CreateLogger<RedisDistributedSessionService>();
        var cacheLogger = loggerFactory.CreateLogger<RedisTenantContextCache>();
        
        // 建立服務實例
        var sessionService = new RedisDistributedSessionService(distributedCache, sessionLogger);
        var tenantCache = new RedisTenantContextCache(distributedCache, cacheLogger);
        
        Console.WriteLine("1. 測試分散式會話管理...");
        await TestSessionManagement(sessionService);
        
        Console.WriteLine("\n2. 測試租戶上下文快取...");
        await TestTenantContextCache(tenantCache);
        
        Console.WriteLine("\n=== 測試完成 ===");
    }
    
    private static async Task TestSessionManagement(IDistributedSessionService sessionService)
    {
        try
        {
            var sessionId = "test_session_" + Guid.NewGuid().ToString("N")[..8];
            var userId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var metadata = new Dictionary<string, object>
            {
                { "browser", "Chrome" },
                { "ip", "192.168.1.100" }
            };
            
            Console.WriteLine($"  建立會話: {sessionId}");
            await sessionService.CreateSessionAsync(sessionId, userId, tenantId, metadata);
            
            Console.WriteLine($"  取得會話資訊...");
            var session = await sessionService.GetSessionAsync(sessionId);
            
            if (session != null)
            {
                Console.WriteLine($"    ✓ 會話 ID: {session.SessionId}");
                Console.WriteLine($"    ✓ 使用者 ID: {session.UserId}");
                Console.WriteLine($"    ✓ 租戶 ID: {session.TenantId}");
                Console.WriteLine($"    ✓ 建立時間: {session.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"    ✓ 是否活躍: {session.IsActive}");
                Console.WriteLine($"    ✓ 元資料數量: {session.Metadata.Count}");
            }
            else
            {
                Console.WriteLine("    ✗ 會話資訊為空");
                return;
            }
            
            Console.WriteLine($"  設定會話資料...");
            await sessionService.SetSessionDataAsync(sessionId, "user_preferences", new { theme = "dark", language = "zh-TW" });
            
            Console.WriteLine($"  取得會話資料...");
            var preferences = await sessionService.GetSessionDataAsync<object>(sessionId, "user_preferences");
            
            if (preferences != null)
            {
                Console.WriteLine($"    ✓ 會話資料已設定和取得");
            }
            else
            {
                Console.WriteLine("    ✗ 會話資料取得失敗");
            }
            
            Console.WriteLine($"  刷新會話...");
            await sessionService.RefreshSessionAsync(sessionId);
            
            var refreshedSession = await sessionService.GetSessionAsync(sessionId);
            if (refreshedSession != null && refreshedSession.LastActivityAt > session.LastActivityAt)
            {
                Console.WriteLine($"    ✓ 會話已成功刷新");
            }
            else
            {
                Console.WriteLine("    ✗ 會話刷新失敗");
            }
            
            Console.WriteLine($"  取得使用者會話列表...");
            var userSessions = await sessionService.GetUserSessionsAsync(userId);
            Console.WriteLine($"    ✓ 使用者會話數量: {userSessions.Count()}");
            
            Console.WriteLine($"  銷毀會話...");
            await sessionService.DestroySessionAsync(sessionId);
            
            var destroyedSession = await sessionService.GetSessionAsync(sessionId);
            if (destroyedSession == null)
            {
                Console.WriteLine($"    ✓ 會話已成功銷毀");
            }
            else
            {
                Console.WriteLine("    ✗ 會話銷毀失敗");
            }
            
            Console.WriteLine("  ✓ 分散式會話管理測試完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ 分散式會話管理測試失敗: {ex.Message}");
        }
    }
    
    private static async Task TestTenantContextCache(ITenantContextCache tenantCache)
    {
        try
        {
            var tenantId = Guid.NewGuid();
            var cacheKey = $"tenant_{tenantId}";
            
            var tenantContext = new TenantContext
            {
                TenantId = tenantId,
                Name = "測試租戶",
                DisplayName = "測試租戶顯示名稱",
                IsActive = true,
                Theme = "enterprise",
                Settings = new Dictionary<string, object>
                {
                    { "maxUsers", 500 },
                    { "features", new[] { "sso", "mfa", "audit" } }
                }
            };
            
            Console.WriteLine($"  設定租戶上下文快取...");
            await tenantCache.SetAsync(cacheKey, tenantContext);
            
            Console.WriteLine($"  取得租戶上下文快取...");
            var retrievedContext = await tenantCache.GetAsync(cacheKey);
            
            if (retrievedContext != null)
            {
                Console.WriteLine($"    ✓ 租戶 ID: {retrievedContext.TenantId}");
                Console.WriteLine($"    ✓ 租戶名稱: {retrievedContext.Name}");
                Console.WriteLine($"    ✓ 顯示名稱: {retrievedContext.DisplayName}");
                Console.WriteLine($"    ✓ 是否啟用: {retrievedContext.IsActive}");
                Console.WriteLine($"    ✓ 主題: {retrievedContext.Theme}");
                Console.WriteLine($"    ✓ 設定數量: {retrievedContext.Settings.Count}");
            }
            else
            {
                Console.WriteLine("    ✗ 租戶上下文快取為空");
                return;
            }
            
            Console.WriteLine($"  檢查快取存在性...");
            var exists = await tenantCache.ExistsAsync(cacheKey);
            if (exists)
            {
                Console.WriteLine($"    ✓ 快取項目存在");
            }
            else
            {
                Console.WriteLine("    ✗ 快取項目不存在");
            }
            
            Console.WriteLine($"  延長快取過期時間...");
            await tenantCache.RefreshAsync(cacheKey, TimeSpan.FromHours(2));
            Console.WriteLine($"    ✓ 快取過期時間已延長");
            
            Console.WriteLine($"  測試批次操作...");
            var batchItems = new Dictionary<string, TenantContext>
            {
                { "batch_1", new TenantContext { TenantId = Guid.NewGuid(), Name = "批次租戶1", IsActive = true } },
                { "batch_2", new TenantContext { TenantId = Guid.NewGuid(), Name = "批次租戶2", IsActive = true } }
            };
            
            await tenantCache.SetManyAsync(batchItems);
            var batchResults = await tenantCache.GetManyAsync(batchItems.Keys);
            
            Console.WriteLine($"    ✓ 批次設定: {batchItems.Count} 個項目");
            Console.WriteLine($"    ✓ 批次取得: {batchResults.Count} 個項目");
            
            Console.WriteLine($"  移除快取...");
            await tenantCache.RemoveAsync(cacheKey);
            
            var removedContext = await tenantCache.GetAsync(cacheKey);
            if (removedContext == null)
            {
                Console.WriteLine($"    ✓ 快取已成功移除");
            }
            else
            {
                Console.WriteLine("    ✗ 快取移除失敗");
            }
            
            Console.WriteLine("  ✓ 租戶上下文快取測試完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ 租戶上下文快取測試失敗: {ex.Message}");
        }
    }
}