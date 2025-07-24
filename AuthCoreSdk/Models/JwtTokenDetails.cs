using System.Security.Claims;

namespace AuthCoreSdk.Models
{
    public class JwtTokenDetails
    {
        public string? Issuer { get; set; }
        public List<string>? Audiences { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public string? Subject { get; set; }
        public List<Claim>? Claims { get; set; }
    }
}
