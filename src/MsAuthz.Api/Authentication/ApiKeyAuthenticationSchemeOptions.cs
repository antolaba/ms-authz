using Microsoft.AspNetCore.Authentication;

namespace MsAuthz.Api.Authentication;

public class ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    /// <summary>The expected API key value. Bound from configuration ("Security:ApiKey"), see Program.cs.</summary>
    public string ApiKey { get; set; } = string.Empty;
}
