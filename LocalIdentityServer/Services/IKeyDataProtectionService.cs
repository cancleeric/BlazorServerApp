namespace LocalIdentityServer.Services;

/// <summary>
/// 金鑰資料保護服務介面 - 遵循依賴反轉原則 (DIP)
/// 負責金鑰資料的加密與解密
/// </summary>
public interface IKeyDataProtectionService
{
    /// <summary>
    /// 加密金鑰資料
    /// </summary>
    /// <param name="keyData">明文金鑰資料</param>
    /// <returns>加密後的金鑰資料</returns>
    string ProtectKeyData(string keyData);

    /// <summary>
    /// 解密金鑰資料
    /// </summary>
    /// <param name="encryptedKeyData">加密的金鑰資料</param>
    /// <returns>明文金鑰資料</returns>
    string UnprotectKeyData(string encryptedKeyData);

    /// <summary>
    /// 驗證加密資料是否有效
    /// </summary>
    /// <param name="encryptedKeyData">加密的金鑰資料</param>
    /// <returns>是否有效</returns>
    bool IsValidEncryptedData(string encryptedKeyData);
}