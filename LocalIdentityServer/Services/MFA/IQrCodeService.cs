namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// QR Code 產生服務介面
/// 負責產生 TOTP 設定用的 QR Code
/// </summary>
public interface IQrCodeService
{
    /// <summary>
    /// 產生 QR Code 圖片 (PNG 格式)
    /// </summary>
    /// <param name="text">QR Code 內容 (通常是 TOTP URI)</param>
    /// <param name="size">QR Code 大小 (像素)</param>
    /// <returns>PNG 圖片的 byte 陣列</returns>
    byte[] GenerateQrCodeImage(string text, int size = 256);

    /// <summary>
    /// 產生 QR Code 的 Base64 字串
    /// </summary>
    /// <param name="text">QR Code 內容</param>
    /// <param name="size">QR Code 大小 (像素)</param>
    /// <returns>Base64 編碼的圖片字串</returns>
    string GenerateQrCodeBase64(string text, int size = 256);

    /// <summary>
    /// 產生 QR Code 的 Data URI (用於 HTML img src)
    /// </summary>
    /// <param name="text">QR Code 內容</param>
    /// <param name="size">QR Code 大小 (像素)</param>
    /// <returns>data:image/png;base64,... 格式的 URI</returns>
    string GenerateQrCodeDataUri(string text, int size = 256);

    /// <summary>
    /// 驗證文字是否適合產生 QR Code
    /// </summary>
    /// <param name="text">要驗證的文字</param>
    /// <returns>是否適合產生 QR Code</returns>
    bool ValidateText(string text);
}