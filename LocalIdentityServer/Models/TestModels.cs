namespace LocalIdentityServer.Models;

public record TestUser(
    string Id, 
    string UserName, 
    string Email, 
    string Password, 
    string[] Roles)
{
    public string? FullName { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Department { get; init; }
    public string? JobTitle { get; init; }
    public List<TestClaim> Claims { get; init; } = new();
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool EmailVerified { get; init; } = true;
    public bool PhoneNumberVerified { get; init; } = false;
    public string? Picture { get; init; }
    public string? Website { get; init; }
    public string? Gender { get; init; }
    public string? BirthDate { get; init; }
    public string? ZoneInfo { get; init; }
    public string? Locale { get; init; }
    public TestAddress? Address { get; init; }
    public string? GivenName { get; init; }
    public string? FamilyName { get; init; }
}

public record TestClaim(string Type, string Value);

public record TestAddress(
    string? Formatted,
    string? StreetAddress,
    string? Locality,
    string? Region,
    string? PostalCode,
    string? Country);

public record TestClient(string ClientId, string ClientName, string ClientSecret, string RedirectUri, string[] AllowedScopes);
