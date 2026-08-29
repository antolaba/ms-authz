using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MsAuthz.Api.Authentication;

/// <summary>
/// ⚠️ SECURITY BOUNDARY (MS-AUTHZ-SPEC.md §7): this is the ONLY check ms-authz performs on a caller.
/// It authenticates the CALLER (a backend), not the end user. It does NOT validate any JWT and does
/// NOT verify the keycloak user id / tenant code the caller passes in — ms-authz trusts that whoever
/// holds this API key already validated the end user's JWT in their own pipeline before asking here.
///
/// Consequence: ms-authz must never be reachable from outside the private network of the system that
/// owns it. There is no public-facing deployment of this service, ever — same boundary as ms-filestore.
/// </summary>
public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder)
    : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationDefaults.HeaderName, out var providedKeys))
        {
            return Task.FromResult(AuthenticateResult.Fail(
                $"Missing '{ApiKeyAuthenticationDefaults.HeaderName}' header."));
        }

        var providedKey = providedKeys.ToString();
        var expectedKey = Options.ApiKey;

        if (string.IsNullOrEmpty(expectedKey) || !FixedTimeEquals(providedKey, expectedKey))
        {
            Logger.LogWarning("Rejected request with an invalid API key from {RemoteIpAddress}",
                Context.Connection.RemoteIpAddress);
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var identity = new ClaimsIdentity(ApiKeyAuthenticationDefaults.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationDefaults.SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>Constant-time comparison so response timing can't be used to brute-force the key.</summary>
    private static bool FixedTimeEquals(string a, string b)
    {
        var bytesA = System.Text.Encoding.UTF8.GetBytes(a);
        var bytesB = System.Text.Encoding.UTF8.GetBytes(b);

        if (bytesA.Length != bytesB.Length)
        {
            // Still compare against something of the caller-provided length to avoid a trivial
            // early-return timing signal on length itself.
            _ = System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(bytesA, bytesA);
            return false;
        }

        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }
}
