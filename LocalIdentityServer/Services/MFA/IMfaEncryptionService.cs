namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// MFA 加密服務介面 - 遵循介面隔離原則 (ISP)
/// 負責加密和解密 MFA 相關的敏感資料
/// </summary>
public interface IMfaEncryptionService
{
    /// <summary>
    /// 加密 TOTP 密鑰
    /// </summary>
    /// <param name="secret">明文密鑰</param>
    /// <param name="userId">使用者ID (用作額外的加密上下文)</param>
    /// <returns>加密後的密鑰</returns>
    string EncryptSecret(string secret, string userId);

    /// <summary>
    /// 解密 TOTP 密鑰
    /// </summary>
    /// <param name="encryptedSecret">加密的密鑰</param>
    /// <param name="userId">使用者ID</param>
    /// <returns>明文密鑰</returns>
    string DecryptSecret(string encryptedSecret, string userId);

    /// <summary>
    /// 加密敏感資料 (如備用碼、設定等)
    /// </summary>
    /// <param name="data">要加密的資料</param>
    /// <param name="purpose">加密目的 (用於隔離不同用途的加密)</param>
    /// <returns>加密後的資料</returns>
    string EncryptData(string data, string purpose = "general");

    /// <summary>
    /// 解密敏感資料
    /// </summary>
    /// <param name="encryptedData">加密的資料</param>
    /// <param name="purpose">加密目的</param>
    /// <returns>明文資料</returns>
    string DecryptData(string encryptedData, string purpose = "general");

    /// <summary>
    /// 產生安全的隨機字串
    /// </summary>
    /// <param name="length">字串長度</param>
    /// <param name="includeSpecialChars">是否包含特殊字符</param>
    /// <returns>隨機字串</returns>
    string GenerateSecureRandomString(int length, bool includeSpecialChars = false);

    /// <summary>
    /// 雜湊密碼或敏感資料 (不可逆)
    /// </summary>
    /// <param name="data">要雜湊的資料</param>
    /// <param name="salt">鹽值 (可選)</param>
    /// <returns>雜湊值</returns>
    string HashData(string data, string? salt = null);

    /// <summary>
    /// 驗證雜湊值
    /// </summary>
    /// <param name="data">原始資料</param>
    /// <param name="hash">雜湊值</param>
    /// <param name="salt">鹽值 (可選)</param>
    /// <returns>是否匹配</returns>
    bool VerifyHash(string data, string hash, string? salt = null);

    /// <summary>
    /// 重新加密資料 (用於金鑰輪替)
    /// </summary>
    /// <param name="oldEncryptedData">舊的加密資料</param>
    /// <param name="purpose">加密目的</param>
    /// <returns>重新加密的資料</returns>
    string ReencryptData(string oldEncryptedData, string purpose = "general");

    /// <summary>
    /// 檢查加密資料的完整性
    /// </summary>
    /// <param name="encryptedData">加密的資料</param>
    /// <param name="purpose">加密目的</param>
    /// <returns>是否完整</returns>
    bool ValidateIntegrity(string encryptedData, string purpose = "general");
}