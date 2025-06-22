using System.ComponentModel.DataAnnotations;

namespace SimpleJwtWeb.Models;

public class LoginRequest
{
    [Required(ErrorMessage = "請輸入用戶名")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入密碼")]
    public string Password { get; set; } = string.Empty;
}
