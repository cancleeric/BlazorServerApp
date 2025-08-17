using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace EnterpriseIDS.Core.Entities;

/// <summary>
/// 租戶配置實體 - 儲存租戶的各種配置設定
/// </summary>
public class TenantConfiguration : BaseEntity
{
    /// <summary>
    /// 租戶 ID
    /// </summary>
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>
    /// 租戶導航屬性
    /// </summary>
    public virtual Tenant? Tenant { get; set; }

    /// <summary>
    /// 配置鍵值
    /// </summary>
    [Required]
    [StringLength(200)]
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>
    /// 配置值
    /// </summary>
    public string? ConfigValue { get; set; }

    /// <summary>
    /// 配置類型（string, int, bool, json, etc.）
    /// </summary>
    [Required]
    [StringLength(50)]
    public string ConfigType { get; set; } = "string";

    /// <summary>
    /// 配置分類
    /// </summary>
    [StringLength(100)]
    public string? Category { get; set; }

    /// <summary>
    /// 配置描述
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// 是否為敏感配置（如密碼、金鑰等）
    /// </summary>
    public bool IsSensitive { get; set; } = false;

    /// <summary>
    /// 是否可被子租戶繼承
    /// </summary>
    public bool IsInheritable { get; set; } = true;

    /// <summary>
    /// 是否為必需配置
    /// </summary>
    public bool IsRequired { get; set; } = false;

    /// <summary>
    /// 是否唯讀
    /// </summary>
    public bool IsReadOnly { get; set; } = false;

    /// <summary>
    /// 驗證規則（JSON 格式）
    /// </summary>
    public string? ValidationRules { get; set; }

    /// <summary>
    /// 允許的值列表（JSON 格式）
    /// </summary>
    public string? AllowedValues { get; set; }

    /// <summary>
    /// 預設值
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// 排序順序
    /// </summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// 配置版本
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 生效日期
    /// </summary>
    public DateTime? EffectiveDate { get; set; }

    /// <summary>
    /// 過期日期
    /// </summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// 標籤（JSON 格式）
    /// </summary>
    public string? TagsJson { get; set; }

    /// <summary>
    /// 標籤列表
    /// </summary>
    public List<string> Tags
    {
        get => string.IsNullOrEmpty(TagsJson)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(TagsJson) ?? new List<string>();
        set => TagsJson = JsonSerializer.Serialize(value);
    }

    // 業務邏輯方法

    /// <summary>
    /// 獲取類型化的配置值
    /// </summary>
    /// <typeparam name="T">目標類型</typeparam>
    /// <returns>轉換後的值</returns>
    public T? GetTypedValue<T>()
    {
        if (string.IsNullOrEmpty(ConfigValue))
            return default(T);

        try
        {
            return ConfigType.ToLowerInvariant() switch
            {
                "string" => (T)(object)ConfigValue,
                "int" => (T)(object)int.Parse(ConfigValue),
                "long" => (T)(object)long.Parse(ConfigValue),
                "double" => (T)(object)double.Parse(ConfigValue),
                "decimal" => (T)(object)decimal.Parse(ConfigValue),
                "bool" => (T)(object)bool.Parse(ConfigValue),
                "datetime" => (T)(object)DateTime.Parse(ConfigValue),
                "json" => JsonSerializer.Deserialize<T>(ConfigValue),
                _ => JsonSerializer.Deserialize<T>(ConfigValue)
            };
        }
        catch
        {
            return default(T);
        }
    }

    /// <summary>
    /// 設定類型化的配置值
    /// </summary>
    /// <typeparam name="T">值的類型</typeparam>
    /// <param name="value">要設定的值</param>
    public void SetTypedValue<T>(T value)
    {
        if (value == null)
        {
            ConfigValue = null;
            return;
        }

        ConfigValue = ConfigType.ToLowerInvariant() switch
        {
            "string" => value.ToString(),
            "int" or "long" or "double" or "decimal" or "bool" or "datetime" => value.ToString(),
            "json" => JsonSerializer.Serialize(value),
            _ => JsonSerializer.Serialize(value)
        };
    }

    /// <summary>
    /// 檢查配置是否有效
    /// </summary>
    public bool IsValid()
    {
        // 檢查必需配置是否有值
        if (IsRequired && string.IsNullOrEmpty(ConfigValue))
            return false;

        // 檢查是否在有效期內
        var now = DateTime.UtcNow;
        if (EffectiveDate.HasValue && EffectiveDate.Value > now)
            return false;

        if (ExpiryDate.HasValue && ExpiryDate.Value < now)
            return false;

        return true;
    }

    /// <summary>
    /// 驗證配置值
    /// </summary>
    /// <param name="value">要驗證的值</param>
    /// <returns>驗證結果</returns>
    public (bool IsValid, string? ErrorMessage) ValidateValue(string? value)
    {
        // 檢查必需配置
        if (IsRequired && string.IsNullOrEmpty(value))
            return (false, "此配置為必需項目");

        // 檢查允許的值
        if (!string.IsNullOrEmpty(AllowedValues))
        {
            try
            {
                var allowedList = JsonSerializer.Deserialize<List<string>>(AllowedValues);
                if (allowedList != null && !allowedList.Contains(value ?? string.Empty))
                    return (false, $"值必須是以下之一: {string.Join(", ", allowedList)}");
            }
            catch
            {
                // 忽略 JSON 解析錯誤
            }
        }

        // 檢查類型轉換
        if (!string.IsNullOrEmpty(value))
        {
            try
            {
                switch (ConfigType.ToLowerInvariant())
                {
                    case "int":
                        int.Parse(value);
                        break;
                    case "long":
                        long.Parse(value);
                        break;
                    case "double":
                        double.Parse(value);
                        break;
                    case "decimal":
                        decimal.Parse(value);
                        break;
                    case "bool":
                        bool.Parse(value);
                        break;
                    case "datetime":
                        DateTime.Parse(value);
                        break;
                    case "json":
                        JsonDocument.Parse(value);
                        break;
                }
            }
            catch (Exception ex)
            {
                return (false, $"值格式不正確: {ex.Message}");
            }
        }

        // 檢查驗證規則
        if (!string.IsNullOrEmpty(ValidationRules))
        {
            try
            {
                var rules = JsonSerializer.Deserialize<Dictionary<string, object>>(ValidationRules);
                if (rules != null)
                {
                    var validationResult = ValidateAgainstRules(value, rules);
                    if (!validationResult.IsValid)
                        return validationResult;
                }
            }
            catch
            {
                // 忽略 JSON 解析錯誤
            }
        }

        return (true, null);
    }

    /// <summary>
    /// 根據規則驗證值
    /// </summary>
    private (bool IsValid, string? ErrorMessage) ValidateAgainstRules(string? value, Dictionary<string, object> rules)
    {
        if (string.IsNullOrEmpty(value))
            return (true, null);

        // 最小長度檢查
        if (rules.ContainsKey("minLength") && int.TryParse(rules["minLength"].ToString(), out int minLen))
        {
            if (value.Length < minLen)
                return (false, $"長度不能少於 {minLen} 個字元");
        }

        // 最大長度檢查
        if (rules.ContainsKey("maxLength") && int.TryParse(rules["maxLength"].ToString(), out int maxLen))
        {
            if (value.Length > maxLen)
                return (false, $"長度不能超過 {maxLen} 個字元");
        }

        // 最小值檢查（數字類型）
        if (rules.ContainsKey("minValue") && double.TryParse(value, out double numValue) && 
            double.TryParse(rules["minValue"].ToString(), out double minValue))
        {
            if (numValue < minValue)
                return (false, $"值不能小於 {minValue}");
        }

        // 最大值檢查（數字類型）
        if (rules.ContainsKey("maxValue") && double.TryParse(value, out double numValue2) && 
            double.TryParse(rules["maxValue"].ToString(), out double maxValue))
        {
            if (numValue2 > maxValue)
                return (false, $"值不能大於 {maxValue}");
        }

        // 正則表達式檢查
        if (rules.ContainsKey("pattern"))
        {
            try
            {
                var pattern = rules["pattern"].ToString();
                if (!string.IsNullOrEmpty(pattern) && !System.Text.RegularExpressions.Regex.IsMatch(value, pattern))
                    return (false, "值格式不符合要求");
            }
            catch
            {
                // 忽略正則表達式錯誤
            }
        }

        return (true, null);
    }

    /// <summary>
    /// 複製配置（用於繼承）
    /// </summary>
    public TenantConfiguration Clone(Guid newTenantId)
    {
        return new TenantConfiguration
        {
            TenantId = newTenantId,
            ConfigKey = ConfigKey,
            ConfigValue = ConfigValue,
            ConfigType = ConfigType,
            Category = Category,
            Description = Description,
            IsSensitive = IsSensitive,
            IsInheritable = IsInheritable,
            IsRequired = IsRequired,
            IsReadOnly = IsReadOnly,
            ValidationRules = ValidationRules,
            AllowedValues = AllowedValues,
            DefaultValue = DefaultValue,
            SortOrder = SortOrder,
            Version = 1, // 新版本從 1 開始
            EffectiveDate = EffectiveDate,
            ExpiryDate = ExpiryDate,
            TagsJson = TagsJson,
            CreatedAt = DateTime.UtcNow,
            CreatedById = CreatedById
        };
    }
}