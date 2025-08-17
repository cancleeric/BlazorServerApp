using Microsoft.EntityFrameworkCore;
using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;
using System.Linq.Expressions;

namespace EnterpriseIDS.Infrastructure.Data.Repositories;

/// <summary>
/// 租戶配置儲存庫實作
/// </summary>
public class TenantConfigurationRepository : BaseRepository<TenantConfiguration>, ITenantConfigurationRepository
{
    public TenantConfigurationRepository(EnterpriseIdentityDbContext context) : base(context)
    {
    }

    /// <summary>
    /// 取得租戶的特定配置
    /// </summary>
    public async Task<TenantConfiguration?> GetByKeyAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(configKey))
            return null;

        return await _dbSet
            .FirstOrDefaultAsync(tc => tc.TenantId == tenantId && tc.ConfigKey == configKey, cancellationToken);
    }

    /// <summary>
    /// 取得租戶的所有配置
    /// </summary>
    public async Task<IEnumerable<TenantConfiguration>> GetAllForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(tc => tc.TenantId == tenantId)
            .OrderBy(tc => tc.Category)
            .ThenBy(tc => tc.SortOrder)
            .ThenBy(tc => tc.ConfigKey)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 取得租戶的分類配置
    /// </summary>
    public async Task<IEnumerable<TenantConfiguration>> GetByCategoryAsync(Guid tenantId, string category, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(tc => tc.TenantId == tenantId && tc.Category == category)
            .OrderBy(tc => tc.SortOrder)
            .ThenBy(tc => tc.ConfigKey)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 批次新增配置
    /// </summary>
    public async Task<IEnumerable<TenantConfiguration>> AddRangeAsync(IEnumerable<TenantConfiguration> configurations, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddRangeAsync(configurations, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return configurations;
    }

    /// <summary>
    /// 刪除租戶的特定配置
    /// </summary>
    public async Task DeleteByKeyAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default)
    {
        var configuration = await GetByKeyAsync(tenantId, configKey, cancellationToken);
        if (configuration != null)
        {
            _dbSet.Remove(configuration);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 刪除租戶的所有配置
    /// </summary>
    public async Task DeleteAllForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var configurations = await _dbSet
            .Where(tc => tc.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (configurations.Any())
        {
            _dbSet.RemoveRange(configurations);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 檢查配置是否存在
    /// </summary>
    public async Task<bool> ExistsAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AnyAsync(tc => tc.TenantId == tenantId && tc.ConfigKey == configKey, cancellationToken);
    }

    /// <summary>
    /// 取得配置歷史
    /// </summary>
    public async Task<IEnumerable<TenantConfiguration>> GetHistoryAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(tc => tc.TenantId == tenantId && tc.ConfigKey == configKey)
            .OrderByDescending(tc => tc.Version)
            .ThenByDescending(tc => tc.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 取得有效配置（考慮生效日期和過期日期）
    /// </summary>
    public async Task<IEnumerable<TenantConfiguration>> GetEffectiveConfigurationsAsync(Guid tenantId, DateTime? effectiveDate = null, CancellationToken cancellationToken = default)
    {
        var checkDate = effectiveDate ?? DateTime.UtcNow;

        return await _dbSet
            .Where(tc => tc.TenantId == tenantId)
            .Where(tc => tc.EffectiveDate == null || tc.EffectiveDate <= checkDate)
            .Where(tc => tc.ExpiryDate == null || tc.ExpiryDate > checkDate)
            .OrderBy(tc => tc.Category)
            .ThenBy(tc => tc.SortOrder)
            .ThenBy(tc => tc.ConfigKey)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 複製配置到其他租戶
    /// </summary>
    public async Task CopyConfigurationsAsync(Guid sourceTenantId, Guid targetTenantId, string[]? configKeys = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(tc => tc.TenantId == sourceTenantId);

        if (configKeys != null && configKeys.Length > 0)
        {
            query = query.Where(tc => configKeys.Contains(tc.ConfigKey));
        }

        var sourceConfigurations = await query.ToListAsync(cancellationToken);

        // 檢查目標租戶已存在的配置
        var existingKeys = await _dbSet
            .Where(tc => tc.TenantId == targetTenantId)
            .Select(tc => tc.ConfigKey)
            .ToListAsync(cancellationToken);

        var configurationsToAdd = new List<TenantConfiguration>();

        foreach (var sourceConfig in sourceConfigurations)
        {
            // 跳過已存在的配置
            if (existingKeys.Contains(sourceConfig.ConfigKey))
                continue;

            // 建立新的配置實例
            var newConfig = sourceConfig.Clone(targetTenantId);
            configurationsToAdd.Add(newConfig);
        }

        if (configurationsToAdd.Any())
        {
            await AddRangeAsync(configurationsToAdd, cancellationToken);
        }
    }

    /// <summary>
    /// 批次更新配置
    /// </summary>
    public async Task BatchUpdateAsync(IEnumerable<TenantConfiguration> configurations, CancellationToken cancellationToken = default)
    {
        foreach (var config in configurations)
        {
            _dbSet.Update(config);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 搜尋配置
    /// </summary>
    public async Task<IEnumerable<TenantConfiguration>> SearchAsync(string searchTerm, Guid? tenantId = null, string? category = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        // 租戶篩選
        if (tenantId.HasValue)
        {
            query = query.Where(tc => tc.TenantId == tenantId.Value);
        }

        // 分類篩選
        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(tc => tc.Category == category);
        }

        // 搜尋條件
        if (!string.IsNullOrEmpty(searchTerm))
        {
            var term = searchTerm.ToLowerInvariant();
            query = query.Where(tc =>
                tc.ConfigKey.ToLowerInvariant().Contains(term) ||
                (tc.Description != null && tc.Description.ToLowerInvariant().Contains(term)) ||
                (tc.ConfigValue != null && tc.ConfigValue.ToLowerInvariant().Contains(term)));
        }

        return await query
            .OrderBy(tc => tc.Category)
            .ThenBy(tc => tc.ConfigKey)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 取得配置統計資訊
    /// </summary>
    public async Task<ConfigurationStatistics> GetStatisticsAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (tenantId.HasValue)
        {
            query = query.Where(tc => tc.TenantId == tenantId.Value);
        }

        var statistics = new ConfigurationStatistics();

        // 總配置數
        statistics.TotalConfigurations = await query.CountAsync(cancellationToken);

        // 按分類統計
        var categoryGroups = await query
            .Where(tc => tc.Category != null)
            .GroupBy(tc => tc.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        statistics.ConfigurationsByCategory = categoryGroups
            .ToDictionary(g => g.Category!, g => g.Count);

        // 按類型統計
        var typeGroups = await query
            .GroupBy(tc => tc.ConfigType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        statistics.ConfigurationsByType = typeGroups
            .ToDictionary(g => g.Type, g => g.Count);

        // 敏感配置數量
        statistics.SensitiveConfigurations = await query
            .CountAsync(tc => tc.IsSensitive, cancellationToken);

        // 必需配置數量
        statistics.RequiredConfigurations = await query
            .CountAsync(tc => tc.IsRequired, cancellationToken);

        // 唯讀配置數量
        statistics.ReadOnlyConfigurations = await query
            .CountAsync(tc => tc.IsReadOnly, cancellationToken);

        // 有過期日期的配置數量
        statistics.ExpirableConfigurations = await query
            .CountAsync(tc => tc.ExpiryDate != null, cancellationToken);

        return statistics;
    }

    /// <summary>
    /// 取得過期的配置
    /// </summary>
    public async Task<IEnumerable<TenantConfiguration>> GetExpiredConfigurationsAsync(DateTime? asOfDate = null, CancellationToken cancellationToken = default)
    {
        var checkDate = asOfDate ?? DateTime.UtcNow;

        return await _dbSet
            .Where(tc => tc.ExpiryDate != null && tc.ExpiryDate <= checkDate)
            .OrderBy(tc => tc.ExpiryDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 取得即將過期的配置
    /// </summary>
    public async Task<IEnumerable<TenantConfiguration>> GetExpiringConfigurationsAsync(int daysAhead = 7, CancellationToken cancellationToken = default)
    {
        var checkDate = DateTime.UtcNow.AddDays(daysAhead);

        return await _dbSet
            .Where(tc => tc.ExpiryDate != null && 
                        tc.ExpiryDate > DateTime.UtcNow && 
                        tc.ExpiryDate <= checkDate)
            .OrderBy(tc => tc.ExpiryDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 清理過期的配置
    /// </summary>
    public async Task<int> CleanupExpiredConfigurationsAsync(DateTime? asOfDate = null, CancellationToken cancellationToken = default)
    {
        var expiredConfigurations = await GetExpiredConfigurationsAsync(asOfDate, cancellationToken);
        
        if (expiredConfigurations.Any())
        {
            _dbSet.RemoveRange(expiredConfigurations);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return expiredConfigurations.Count();
    }
}

/// <summary>
/// 配置統計資訊
/// </summary>
public class ConfigurationStatistics
{
    /// <summary>
    /// 總配置數
    /// </summary>
    public int TotalConfigurations { get; set; }

    /// <summary>
    /// 按分類統計的配置數
    /// </summary>
    public Dictionary<string, int> ConfigurationsByCategory { get; set; } = new();

    /// <summary>
    /// 按類型統計的配置數
    /// </summary>
    public Dictionary<string, int> ConfigurationsByType { get; set; } = new();

    /// <summary>
    /// 敏感配置數量
    /// </summary>
    public int SensitiveConfigurations { get; set; }

    /// <summary>
    /// 必需配置數量
    /// </summary>
    public int RequiredConfigurations { get; set; }

    /// <summary>
    /// 唯讀配置數量
    /// </summary>
    public int ReadOnlyConfigurations { get; set; }

    /// <summary>
    /// 有過期日期的配置數量
    /// </summary>
    public int ExpirableConfigurations { get; set; }

    /// <summary>
    /// 計算敏感配置比例
    /// </summary>
    public double SensitiveRate => TotalConfigurations > 0 ? (double)SensitiveConfigurations / TotalConfigurations * 100 : 0;

    /// <summary>
    /// 計算必需配置比例
    /// </summary>
    public double RequiredRate => TotalConfigurations > 0 ? (double)RequiredConfigurations / TotalConfigurations * 100 : 0;
}