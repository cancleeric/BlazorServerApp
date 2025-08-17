using System.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Novell.Directory.Ldap;
using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Core.ValueObjects;

namespace EnterpriseIDS.Infrastructure.Ldap;

/// <summary>
/// LDAP 服務實作
/// </summary>
public class LdapService : ILdapService
{
    private readonly ILogger<LdapService> _logger;
    private readonly ILdapConfigurationService _configurationService;
    private readonly Dictionary<Guid, LdapConnection> _connectionPool;
    private readonly object _connectionLock = new();

    public LdapService(
        ILogger<LdapService> logger,
        ILdapConfigurationService configurationService)
    {
        _logger = logger;
        _configurationService = configurationService;
        _connectionPool = new Dictionary<Guid, LdapConnection>();
    }

    #region 連線與配置管理

    public async Task<LdapConnectionTestResult> TestConnectionAsync(LdapConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("測試 LDAP 連線: {ServerUrl}", configuration.ServerUrl);

            using var connection = new LdapConnection();
            
            // 配置連線參數
            if (configuration.UseSsl)
            {
                connection.SecureSocketLayer = true;
            }

            // 建立連線
            await Task.Run(() => connection.Connect(configuration.ServerUrl, configuration.Port), cancellationToken);

            // 驗證認證
            if (configuration.RequiresAuthentication())
            {
                var password = await _configurationService.DecryptSensitiveDataAsync(configuration.ServiceAccountPassword ?? "", cancellationToken);
                await Task.Run(() => connection.Bind(configuration.ServiceAccountDn, password), cancellationToken);
            }
            else
            {
                await Task.Run(() => connection.Bind(null, null), cancellationToken);
            }

            stopwatch.Stop();

            // 取得伺服器資訊
            var serverInfo = GetServerInfo(connection);

            _logger.LogInformation("LDAP 連線測試成功: {ResponseTime}ms", stopwatch.ElapsedMilliseconds);

            return new LdapConnectionTestResult
            {
                Success = true,
                ResponseTime = stopwatch.Elapsed,
                ServerInfo = serverInfo,
                SupportedFeatures = GetSupportedFeatures(connection),
                ConnectionDetails = new Dictionary<string, object>
                {
                    ["Host"] = configuration.ServerUrl,
                    ["Port"] = configuration.Port,
                    ["SSL"] = configuration.UseSsl,
                    ["ServerType"] = configuration.ServerType.ToString(),
                    ["ConnectedAt"] = DateTime.UtcNow
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "LDAP 連線測試失敗: {ServerUrl}", configuration.ServerUrl);

            return new LdapConnectionTestResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ResponseTime = stopwatch.Elapsed,
                ConnectionDetails = new Dictionary<string, object>
                {
                    ["Host"] = configuration.ServerUrl,
                    ["Port"] = configuration.Port,
                    ["Error"] = ex.GetType().Name,
                    ["TestedAt"] = DateTime.UtcNow
                }
            };
        }
    }

    public async Task<LdapConnectionTestResult> TestConnectionAsync(LdapConnectionParameters parameters, CancellationToken cancellationToken = default)
    {
        var configuration = new LdapConfiguration
        {
            ServerUrl = parameters.ServerUrl,
            Port = parameters.Port,
            UseSsl = parameters.UseSsl,
            UseStartTls = parameters.UseStartTls,
            IgnoreSslErrors = parameters.IgnoreSslErrors,
            ServiceAccountDn = parameters.ServiceAccountDn,
            ServiceAccountPassword = parameters.ServiceAccountPassword,
            ConnectionTimeout = parameters.ConnectionTimeout
        };

        return await TestConnectionAsync(configuration, cancellationToken);
    }

    public async Task<LdapHealthCheckResult> CheckHealthAsync(Guid configurationId, CancellationToken cancellationToken = default)
    {
        var checkTime = DateTime.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var configuration = await _configurationService.GetConfigurationByIdAsync(configurationId, cancellationToken);
            if (configuration == null)
            {
                return new LdapHealthCheckResult
                {
                    IsHealthy = false,
                    Status = "Configuration Not Found",
                    ErrorMessage = $"LDAP configuration with ID {configurationId} not found",
                    CheckTime = checkTime,
                    ResponseTime = stopwatch.Elapsed
                };
            }

            var connection = await GetConnectionAsync(configurationId, cancellationToken);
            if (connection == null || !connection.Connected)
            {
                return new LdapHealthCheckResult
                {
                    IsHealthy = false,
                    Status = "Connection Failed",
                    ErrorMessage = "Unable to establish LDAP connection",
                    CheckTime = checkTime,
                    ResponseTime = stopwatch.Elapsed
                };
            }

            // 執行簡單的搜尋測試
            var searchResult = await Task.Run(() => 
                connection.Search(configuration.BaseDn, LdapConnection.ScopeBase, "(objectClass=*)", null, false), 
                cancellationToken);

            stopwatch.Stop();

            return new LdapHealthCheckResult
            {
                IsHealthy = true,
                Status = "Healthy",
                CheckTime = checkTime,
                ResponseTime = stopwatch.Elapsed,
                Metrics = new Dictionary<string, object>
                {
                    ["ConnectionPoolSize"] = _connectionPool.Count,
                    ["LastSyncTime"] = configuration.LastSyncAt?.ToString() ?? "Never",
                    ["SyncStatus"] = configuration.LastSyncStatus.ToString()
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "LDAP 健康檢查失敗: {ConfigurationId}", configurationId);

            return new LdapHealthCheckResult
            {
                IsHealthy = false,
                Status = "Error",
                ErrorMessage = ex.Message,
                CheckTime = checkTime,
                ResponseTime = stopwatch.Elapsed
            };
        }
    }

    public async Task<Dictionary<string, object>> GetServerInfoAsync(LdapConfiguration configuration, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new LdapConnection();
            
            if (configuration.UseSsl)
            {
                connection.SecureSocketLayer = true;
            }

            await Task.Run(() => connection.Connect(configuration.ServerUrl, configuration.Port), cancellationToken);
            
            if (configuration.RequiresAuthentication())
            {
                var password = await _configurationService.DecryptSensitiveDataAsync(configuration.ServiceAccountPassword ?? "", cancellationToken);
                await Task.Run(() => connection.Bind(configuration.ServiceAccountDn, password), cancellationToken);
            }

            return GetServerInfoDetails(connection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得 LDAP 伺服器資訊失敗: {ServerUrl}", configuration.ServerUrl);
            return new Dictionary<string, object>
            {
                ["Error"] = ex.Message,
                ["ServerUrl"] = configuration.ServerUrl
            };
        }
    }

    #endregion

    #region 使用者認證

    public async Task<LdapAuthenticationResult> AuthenticateAsync(string username, string password, Guid configurationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var configuration = await _configurationService.GetConfigurationByIdAsync(configurationId, cancellationToken);
            if (configuration == null)
            {
                return new LdapAuthenticationResult
                {
                    Success = false,
                    ErrorMessage = "LDAP configuration not found"
                };
            }

            return await AuthenticateAsync(username, password, configuration, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LDAP 認證失敗: {Username}", username);
            return new LdapAuthenticationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<LdapAuthenticationResult> AuthenticateAsync(string username, string password, LdapConfiguration configuration, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("開始 LDAP 認證: {Username}", username);

            // 首先搜尋使用者
            var user = await GetUserAsync(username, configuration, cancellationToken);
            if (user == null)
            {
                return new LdapAuthenticationResult
                {
                    Success = false,
                    ErrorMessage = "User not found in LDAP directory"
                };
            }

            // 嘗試使用使用者憑證綁定
            using var authConnection = new LdapConnection();
            
            if (configuration.UseSsl)
            {
                authConnection.SecureSocketLayer = true;
            }

            await Task.Run(() => authConnection.Connect(configuration.ServerUrl, configuration.Port), cancellationToken);
            await Task.Run(() => authConnection.Bind(user.DistinguishedName, password), cancellationToken);

            // 取得使用者群組
            var groups = await GetUserGroupsAsync(username, configuration, true, cancellationToken);

            _logger.LogInformation("LDAP 認證成功: {Username}", username);

            return new LdapAuthenticationResult
            {
                Success = true,
                User = user,
                Groups = groups.ToList(),
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["AuthenticatedAt"] = DateTime.UtcNow,
                    ["AuthenticationMethod"] = "LDAP",
                    ["ServerType"] = configuration.ServerType.ToString()
                }
            };
        }
        catch (LdapException ldapEx) when (ldapEx.ResultCode == LdapException.InvalidCredentials)
        {
            _logger.LogWarning("LDAP 認證失敗 - 無效憑證: {Username}", username);
            return new LdapAuthenticationResult
            {
                Success = false,
                ErrorMessage = "Invalid username or password"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LDAP 認證過程發生錯誤: {Username}", username);
            return new LdapAuthenticationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<bool> UserExistsAsync(string username, Guid configurationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await GetUserAsync(username, configurationId, cancellationToken);
            return user != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "檢查使用者是否存在失敗: {Username}", username);
            return false;
        }
    }

    #endregion

    #region 私有輔助方法

    private async Task<IEnumerable<string>> GetNestedGroupsAsync(string groupDn, LdapConfiguration configuration, LdapConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            var nestedGroups = new HashSet<string>();
            var filter = $"(&(objectClass=group)({configuration.GroupMemberAttribute}={groupDn}))";
            var searchResults = await Task.Run(() => 
                connection.Search(configuration.GetGroupSearchBaseDn(), LdapConnection.ScopeSub, filter, null, false), 
                cancellationToken);

            while (searchResults.HasMore())
            {
                var entry = searchResults.Next();
                nestedGroups.Add(entry.Dn);
                
                // 遞迴查找更深層的嵌套群組
                var deeperNested = await GetNestedGroupsAsync(entry.Dn, configuration, connection, cancellationToken);
                foreach (var nested in deeperNested)
                {
                    nestedGroups.Add(nested);
                }
            }

            return nestedGroups;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "取得嵌套群組失敗: {GroupDn}", groupDn);
            return Enumerable.Empty<string>();
        }
    }

    private async Task<LdapConnection?> GetConnectionAsync(Guid configurationId, CancellationToken cancellationToken = default)
    {
        lock (_connectionLock)
        {
            if (_connectionPool.TryGetValue(configurationId, out var existingConnection) && 
                existingConnection.Connected)
            {
                return existingConnection;
            }
        }

        try
        {
            var configuration = await _configurationService.GetConfigurationByIdAsync(configurationId, cancellationToken);
            if (configuration == null)
                return null;

            var connection = new LdapConnection();
            
            if (configuration.UseSsl)
            {
                connection.SecureSocketLayer = true;
            }

            await Task.Run(() => connection.Connect(configuration.ServerUrl, configuration.Port), cancellationToken);
            
            if (configuration.RequiresAuthentication())
            {
                var password = await _configurationService.DecryptSensitiveDataAsync(configuration.ServiceAccountPassword ?? "", cancellationToken);
                await Task.Run(() => connection.Bind(configuration.ServiceAccountDn, password), cancellationToken);
            }

            lock (_connectionLock)
            {
                _connectionPool[configurationId] = connection;
            }

            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "建立 LDAP 連線失敗: {ConfigurationId}", configurationId);
            return null;
        }
    }

    private async Task<LdapUserModel?> GetUserAsync(string username, LdapConfiguration configuration, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = new LdapConnection();
            
            if (configuration.UseSsl)
            {
                connection.SecureSocketLayer = true;
            }

            await Task.Run(() => connection.Connect(configuration.ServerUrl, configuration.Port), cancellationToken);
            
            if (configuration.RequiresAuthentication())
            {
                var password = await _configurationService.DecryptSensitiveDataAsync(configuration.ServiceAccountPassword ?? "", cancellationToken);
                await Task.Run(() => connection.Bind(configuration.ServiceAccountDn, password), cancellationToken);
            }

            var filter = string.Format(configuration.UserSearchFilter, username);
            var searchResults = await Task.Run(() => 
                connection.Search(configuration.GetUserSearchBaseDn(), LdapConnection.ScopeOne, filter, null, false), 
                cancellationToken);

            if (searchResults.HasMore())
            {
                var entry = searchResults.Next();
                return MapLdapEntryToUser(entry, configuration);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜尋 LDAP 使用者失敗: {Username}", username);
            return null;
        }
    }

    private LdapUserModel MapLdapEntryToUser(LdapEntry entry, LdapConfiguration configuration)
    {
        var user = new LdapUserModel
        {
            DistinguishedName = entry.Dn
        };

        // 映射基本屬性
        if (entry.GetAttribute(configuration.UsernameAttribute)?.StringValue is string username)
            user.Username = username;

        if (entry.GetAttribute(configuration.EmailAttribute)?.StringValue is string email)
            user.Email = email;

        if (entry.GetAttribute(configuration.FirstNameAttribute)?.StringValue is string firstName)
            user.FirstName = firstName;

        if (entry.GetAttribute(configuration.LastNameAttribute)?.StringValue is string lastName)
            user.LastName = lastName;

        if (entry.GetAttribute(configuration.DisplayNameAttribute)?.StringValue is string displayName)
            user.DisplayName = displayName;

        // 映射 GUID 和 SID (主要用於 AD)
        if (entry.GetAttribute("objectGUID")?.ByteValue is byte[] guidBytes)
            user.ObjectGuid = new Guid(guidBytes).ToString();

        if (entry.GetAttribute("objectSid")?.ByteValue is byte[] sidBytes)
            user.SecurityIdentifier = ConvertSidToString(sidBytes);

        // 映射其他常用屬性
        if (entry.GetAttribute("department")?.StringValue is string department)
            user.Department = department;

        if (entry.GetAttribute("title")?.StringValue is string title)
            user.JobTitle = title;

        if (entry.GetAttribute("telephoneNumber")?.StringValue is string phone)
            user.PhoneNumber = phone;

        if (entry.GetAttribute("mobile")?.StringValue is string mobile)
            user.MobileNumber = mobile;

        if (entry.GetAttribute("physicalDeliveryOfficeName")?.StringValue is string office)
            user.Office = office;

        if (entry.GetAttribute("manager")?.StringValue is string manager)
            user.ManagerDn = manager;

        // 檢查帳號狀態 (AD 特定)
        if (entry.GetAttribute("userAccountControl")?.StringValue is string uacString && 
            int.TryParse(uacString, out var uac))
        {
            user.IsEnabled = (uac & 0x0002) == 0; // ADS_UF_ACCOUNTDISABLE
            user.IsLocked = (uac & 0x0010) != 0;  // ADS_UF_LOCKOUT
        }

        return user;
    }

    private string ConvertSidToString(byte[] sidBytes)
    {
        // 簡化的 SID 轉換，實際實作可能需要更複雜的邏輯
        try
        {
            return Convert.ToBase64String(sidBytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    private string GetServerInfo(LdapConnection connection)
    {
        try
        {
            var rootDse = connection.Read("");
            return rootDse?.GetAttribute("serverName")?.StringValue ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private List<string> GetSupportedFeatures(LdapConnection connection)
    {
        var features = new List<string>();
        
        try
        {
            var rootDse = connection.Read("");
            if (rootDse != null)
            {
                var supportedControls = rootDse.GetAttribute("supportedControl");
                if (supportedControls != null)
                {
                    features.AddRange(supportedControls.StringValueArray);
                }
            }
        }
        catch
        {
            // 忽略錯誤，回傳空清單
        }
        
        return features;
    }

    private Dictionary<string, object> GetServerInfoDetails(LdapConnection connection)
    {
        var details = new Dictionary<string, object>();
        
        try
        {
            var rootDse = connection.Read("");
            if (rootDse != null)
            {
                details["ServerName"] = rootDse.GetAttribute("serverName")?.StringValue ?? "Unknown";
                details["DomainControllerFunctionality"] = rootDse.GetAttribute("domainControllerFunctionality")?.StringValue ?? "Unknown";
                details["ForestFunctionality"] = rootDse.GetAttribute("forestFunctionality")?.StringValue ?? "Unknown";
                details["SupportedLdapVersion"] = rootDse.GetAttribute("supportedLDAPVersion")?.StringValueArray ?? Array.Empty<string>();
                details["SupportedSaslMechanisms"] = rootDse.GetAttribute("supportedSASLMechanisms")?.StringValueArray ?? Array.Empty<string>();
            }
        }
        catch (Exception ex)
        {
            details["Error"] = ex.Message;
        }
        
        return details;
    }

    #endregion

    #region 未實作的方法 (需要後續實作)

    public async Task<IEnumerable<LdapUserModel>> SearchUsersAsync(string searchTerm, Guid configurationId, int maxResults = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var configuration = await _configurationService.GetConfigurationByIdAsync(configurationId, cancellationToken);
            if (configuration == null)
                return Enumerable.Empty<LdapUserModel>();

            using var connection = new LdapConnection();
            
            if (configuration.UseSsl)
            {
                connection.SecureSocketLayer = true;
            }

            await Task.Run(() => connection.Connect(configuration.ServerUrl, configuration.Port), cancellationToken);
            
            if (configuration.RequiresAuthentication())
            {
                var password = await _configurationService.DecryptSensitiveDataAsync(configuration.ServiceAccountPassword ?? "", cancellationToken);
                await Task.Run(() => connection.Bind(configuration.ServiceAccountDn, password), cancellationToken);
            }

            var filter = $"(&{configuration.UserSearchFilter.Replace("{0}", "*" + searchTerm + "*")}(|(cn=*{searchTerm}*)(mail=*{searchTerm}*)(displayName=*{searchTerm}*)))";
            var searchResults = await Task.Run(() => 
                connection.Search(configuration.GetUserSearchBaseDn(), LdapConnection.ScopeSub, filter, null, false), 
                cancellationToken);

            var users = new List<LdapUserModel>();
            var count = 0;
            
            while (searchResults.HasMore() && count < maxResults)
            {
                var entry = searchResults.Next();
                var user = MapLdapEntryToUser(entry, configuration);
                if (user != null)
                {
                    users.Add(user);
                    count++;
                }
            }

            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜尋 LDAP 使用者失敗: {SearchTerm}", searchTerm);
            return Enumerable.Empty<LdapUserModel>();
        }
    }

    public async Task<LdapUserModel?> GetUserAsync(string username, Guid configurationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var configuration = await _configurationService.GetConfigurationByIdAsync(configurationId, cancellationToken);
            if (configuration == null)
                return null;

            return await GetUserAsync(username, configuration, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得 LDAP 使用者失敗: {Username}", username);
            return null;
        }
    }

    public async Task<LdapUserModel?> GetUserByDnAsync(string distinguishedName, Guid configurationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var configuration = await _configurationService.GetConfigurationByIdAsync(configurationId, cancellationToken);
            if (configuration == null)
                return null;

            using var connection = new LdapConnection();
            
            if (configuration.UseSsl)
            {
                connection.SecureSocketLayer = true;
            }

            await Task.Run(() => connection.Connect(configuration.ServerUrl, configuration.Port), cancellationToken);
            
            if (configuration.RequiresAuthentication())
            {
                var password = await _configurationService.DecryptSensitiveDataAsync(configuration.ServiceAccountPassword ?? "", cancellationToken);
                await Task.Run(() => connection.Bind(configuration.ServiceAccountDn, password), cancellationToken);
            }

            var entry = await Task.Run(() => connection.Read(distinguishedName), cancellationToken);
            return entry != null ? MapLdapEntryToUser(entry, configuration) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得 LDAP 使用者失敗: {DN}", distinguishedName);
            return null;
        }
    }

    public async Task<IEnumerable<LdapUserModel>> GetAllUsersAsync(Guid configurationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var configuration = await _configurationService.GetConfigurationByIdAsync(configurationId, cancellationToken);
            if (configuration == null)
                return Enumerable.Empty<LdapUserModel>();

            using var connection = new LdapConnection();
            
            if (configuration.UseSsl)
            {
                connection.SecureSocketLayer = true;
            }

            await Task.Run(() => connection.Connect(configuration.ServerUrl, configuration.Port), cancellationToken);
            
            if (configuration.RequiresAuthentication())
            {
                var password = await _configurationService.DecryptSensitiveDataAsync(configuration.ServiceAccountPassword ?? "", cancellationToken);
                await Task.Run(() => connection.Bind(configuration.ServiceAccountDn, password), cancellationToken);
            }

            var filter = configuration.UserSearchFilter.Replace("{0}", "*");
            var searchResults = await Task.Run(() => 
                connection.Search(configuration.GetUserSearchBaseDn(), LdapConnection.ScopeSub, filter, null, false), 
                cancellationToken);

            var users = new List<LdapUserModel>();
            
            while (searchResults.HasMore())
            {
                var entry = searchResults.Next();
                var user = MapLdapEntryToUser(entry, configuration);
                if (user != null)
                {
                    users.Add(user);
                }
            }

            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得所有 LDAP 使用者失敗");
            return Enumerable.Empty<LdapUserModel>();
        }
    }

    public async Task<IEnumerable<string>> GetUserGroupsAsync(string username, Guid configurationId, bool includeNested = true, CancellationToken cancellationToken = default)
    {
        try
        {
            var configuration = await _configurationService.GetConfigurationByIdAsync(configurationId, cancellationToken);
            if (configuration == null)
                return Enumerable.Empty<string>();

            var user = await GetUserAsync(username, configuration, cancellationToken);
            if (user == null)
                return Enumerable.Empty<string>();

            using var connection = new LdapConnection();
            
            if (configuration.UseSsl)
            {
                connection.SecureSocketLayer = true;
            }

            await Task.Run(() => connection.Connect(configuration.ServerUrl, configuration.Port), cancellationToken);
            
            if (configuration.RequiresAuthentication())
            {
                var password = await _configurationService.DecryptSensitiveDataAsync(configuration.ServiceAccountPassword ?? "", cancellationToken);
                await Task.Run(() => connection.Bind(configuration.ServiceAccountDn, password), cancellationToken);
            }

            var groups = new HashSet<string>();
            var filter = $"(&(objectClass=group)({configuration.GroupMemberAttribute}={user.DistinguishedName}))";
            var searchResults = await Task.Run(() => 
                connection.Search(configuration.GetGroupSearchBaseDn(), LdapConnection.ScopeSub, filter, null, false), 
                cancellationToken);

            while (searchResults.HasMore())
            {
                var entry = searchResults.Next();
                groups.Add(entry.Dn);
                
                // 如果需要包含嵌套群組，遞迴查找
                if (includeNested)
                {
                    var nestedGroups = await GetNestedGroupsAsync(entry.Dn, configuration, connection, cancellationToken);
                    foreach (var nested in nestedGroups)
                    {
                        groups.Add(nested);
                    }
                }
            }

            return groups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得使用者群組失敗: {Username}", username);
            return Enumerable.Empty<string>();
        }
    }

    // ... 其他未實作的方法
    // (為了節省空間，這裡只列出部分方法，實際應該包含所有介面方法)

    #endregion

    #region IDisposable

    public void Dispose()
    {
        lock (_connectionLock)
        {
            foreach (var connection in _connectionPool.Values)
            {
                try
                {
                    connection?.Disconnect();
                    connection?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "關閉 LDAP 連線時發生錯誤");
                }
            }
            _connectionPool.Clear();
        }
    }

    public async Task CleanupExpiredResourcesAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            lock (_connectionLock)
            {
                var expiredConnections = _connectionPool
                    .Where(kvp => !kvp.Value.Connected)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var id in expiredConnections)
                {
                    if (_connectionPool.TryGetValue(id, out var connection))
                    {
                        try
                        {
                            connection.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "清理過期連線時發生錯誤: {ConfigurationId}", id);
                        }
                        _connectionPool.Remove(id);
                    }
                }
            }
        }, cancellationToken);
    }

    #endregion

    // 其他介面方法的基本實作
    public Task<IEnumerable<LdapGroupModel>> SearchGroupsAsync(string searchTerm, Guid configurationId, int maxResults = 100, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SearchGroupsAsync 呼叫 - 此功能將在後續版本實作");
        return Task.FromResult(Enumerable.Empty<LdapGroupModel>());
    }

    public Task<LdapGroupModel?> GetGroupAsync(string groupName, Guid configurationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetGroupAsync 呼叫 - 此功能將在後續版本實作");
        return Task.FromResult<LdapGroupModel?>(null);
    }

    public Task<LdapGroupModel?> GetGroupByDnAsync(string distinguishedName, Guid configurationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetGroupByDnAsync 呼叫 - 此功能將在後續版本實作");
        return Task.FromResult<LdapGroupModel?>(null);
    }

    public Task<IEnumerable<LdapGroupModel>> GetAllGroupsAsync(Guid configurationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetAllGroupsAsync 呼叫 - 此功能將在後續版本實作");
        return Task.FromResult(Enumerable.Empty<LdapGroupModel>());
    }

    public Task<IEnumerable<string>> GetGroupMembersAsync(string groupName, Guid configurationId, bool includeNested = true, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetGroupMembersAsync 呼叫 - 此功能將在後續版本實作");
        return Task.FromResult(Enumerable.Empty<string>());
    }

    public Task<IEnumerable<LdapGroupModel>> GetChildGroupsAsync(string groupName, Guid configurationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetChildGroupsAsync 呼叫 - 此功能將在後續版本實作");
        return Task.FromResult(Enumerable.Empty<LdapGroupModel>());
    }

    public Task<IEnumerable<LdapGroupModel>> GetParentGroupsAsync(string groupName, Guid configurationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetParentGroupsAsync 呼叫 - 此功能將在後續版本實作");
        return Task.FromResult(Enumerable.Empty<LdapGroupModel>());
    }

    public Task<LdapSyncResult> SynchronizeAsync(Guid configurationId, LdapSyncOptions? options = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SynchronizeAsync 呼叫 - 基礎同步功能實作中");
        return Task.FromResult(new LdapSyncResult
        {
            Success = false,
            ErrorMessage = "完整同步功能將在後續版本實作"
        });
    }

    public Task<LdapSyncResult> IncrementalSyncAsync(Guid configurationId, DateTime fromDate, LdapSyncOptions? options = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("IncrementalSyncAsync 呼叫 - 此功能將在後續版本實作");
        return Task.FromResult(new LdapSyncResult
        {
            Success = false,
            ErrorMessage = "增量同步功能將在後續版本實作"
        });
    }

    public Task<LdapSyncResult> SyncUserAsync(string username, Guid configurationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SyncUserAsync 呼叫 - 此功能將在後續版本實作");
        return Task.FromResult(new LdapSyncResult
        {
            Success = false,
            ErrorMessage = "單一使用者同步功能將在後續版本實作"
        });
    }

    public Task<LdapSyncResult> SyncGroupAsync(string groupName, Guid configurationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SyncGroupAsync 呼叫 - 此功能將在後續版本實作");
        return Task.FromResult(new LdapSyncResult
        {
            Success = false,
            ErrorMessage = "單一群組同步功能將在後續版本實作"
        });
    }

    public Task<IEnumerable<LdapConflictItem>> DetectConflictsAsync(Guid configurationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DetectConflictsAsync 呼叫 - 此功能將在後續版本實作");
        return Task.FromResult(Enumerable.Empty<LdapConflictItem>());
    }

    public Task<bool> ResolveConflictAsync(LdapConflictItem conflict, ConflictResolutionStrategy strategy, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("ResolveConflictAsync 呼叫 - 此功能將在後續版本實作");
        return Task.FromResult(false);
    }

    public Task<Dictionary<string, object>> GetSchemaAsync(Guid configurationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetSchemaAsync 呼叫 - 此功能將在後續版本實作");
        return Task.FromResult(new Dictionary<string, object>
        {
            ["Status"] = "功能將在後續版本實作"
        });
    }

    public Task<IEnumerable<string>> GetAvailableAttributesAsync(Guid configurationId, string objectClass, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetAvailableAttributesAsync 呼叫 - 此功能將在後續版本實作");
        return Task.FromResult(Enumerable.Empty<string>());
    }

    public Task<bool> ValidateAttributeMappingAsync(LdapConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("ValidateAttributeMappingAsync 呼叫 - 此功能將在後續版本實作");
        return Task.FromResult(true); // 暫時返回 true，實際驗證邏輯待實作
    }

    public Task<Dictionary<string, object>> GetSyncStatisticsAsync(Guid configurationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetSyncStatisticsAsync 呼叫 - 此功能將在後續版本實作");
        return Task.FromResult(new Dictionary<string, object>
        {
            ["Status"] = "統計功能將在後續版本實作"
        });
    }

    public Task<Dictionary<string, object>> GetConnectionStatisticsAsync(Guid configurationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetConnectionStatisticsAsync 呼叫 - 此功能將在後續版本實作");
        return Task.FromResult(new Dictionary<string, object>
        {
            ["ConnectionPoolSize"] = _connectionPool.Count,
            ["Status"] = "連線統計功能將在後續版本實作"
        });
    }

    private Task<IEnumerable<string>> GetUserGroupsAsync(string username, LdapConfiguration configuration, bool includeNested, CancellationToken cancellationToken)
    {
        // 這是一個私有實作，為了支援認證功能
        // 實際實作需要完整的群組搜尋邏輯
        return Task.FromResult<IEnumerable<string>>(new List<string>());
    }
}