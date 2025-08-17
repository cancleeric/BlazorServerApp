using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;

namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// QR Code 產生服務實作
/// 使用 QRCoder 套件產生 TOTP 設定用的 QR Code
/// </summary>
public class QrCodeService : IQrCodeService
{
    private readonly ILogger<QrCodeService> _logger;
    private readonly int _maxTextLength;
    private readonly int _minSize;
    private readonly int _maxSize;

    public QrCodeService(ILogger<QrCodeService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _maxTextLength = int.Parse(configuration["Mfa:QrCode:MaxTextLength"] ?? "2048");
        _minSize = int.Parse(configuration["Mfa:QrCode:MinSize"] ?? "64");
        _maxSize = int.Parse(configuration["Mfa:QrCode:MaxSize"] ?? "1024");
    }

    public byte[] GenerateQrCodeImage(string text, int size = 256)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be null or empty", nameof(text));

        if (!ValidateText(text))
            throw new ArgumentException("Text is too long or contains invalid characters", nameof(text));

        if (size < _minSize || size > _maxSize)
            throw new ArgumentException($"Size must be between {_minSize} and {_maxSize}", nameof(size));

        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(qrCodeData);
            
            var qrCodeBytes = qrCode.GetGraphic(size / 25); // 每個模組的像素大小
            
            _logger.LogDebug("Generated QR code image with size: {Size}x{Size}, data length: {Length}", 
                size, size, qrCodeBytes.Length);
            
            return qrCodeBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate QR code image");
            throw new InvalidOperationException("Failed to generate QR code image", ex);
        }
    }

    public string GenerateQrCodeBase64(string text, int size = 256)
    {
        try
        {
            var imageBytes = GenerateQrCodeImage(text, size);
            var base64String = Convert.ToBase64String(imageBytes);
            
            _logger.LogDebug("Generated QR code as Base64 string with length: {Length}", base64String.Length);
            
            return base64String;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate QR code as Base64");
            throw;
        }
    }

    public string GenerateQrCodeDataUri(string text, int size = 256)
    {
        try
        {
            var base64String = GenerateQrCodeBase64(text, size);
            var dataUri = $"data:image/png;base64,{base64String}";
            
            _logger.LogDebug("Generated QR code as data URI with length: {Length}", dataUri.Length);
            
            return dataUri;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate QR code as data URI");
            throw;
        }
    }

    public bool ValidateText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // 檢查文字長度
        if (text.Length > _maxTextLength)
            return false;

        // 檢查是否為有效的 URI 格式 (TOTP URI)
        if (text.StartsWith("otpauth://"))
        {
            try
            {
                var uri = new Uri(text);
                return uri.Scheme == "otpauth" && 
                       (uri.Host == "totp" || uri.Host == "hotp");
            }
            catch
            {
                return false;
            }
        }

        // 對於其他文字，檢查是否包含有效字符
        // QR Code 支援數字、字母、符號等
        try
        {
            // 嘗試建立 QR Code 來驗證
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
            return true;
        }
        catch
        {
            return false;
        }
    }
}