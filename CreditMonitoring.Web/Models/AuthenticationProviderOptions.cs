namespace CreditMonitoring.Web.Models;

public class AuthenticationProviderOptions
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Icon { get; set; }
    public string Authority { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string[] Scopes { get; set; }
    public string ResponseType { get; set; } = "code";
    public bool Enabled { get; set; } = true;
}

public class AuthenticationConfig
{
    public Dictionary<string, AuthenticationProviderOptions> Providers { get; set; }
}