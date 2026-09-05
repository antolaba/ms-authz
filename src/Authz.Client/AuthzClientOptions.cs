namespace Authz.Client;

/// <summary>Options for <see cref="IAuthzClient"/> — bound from configuration section "Authz" or set in code.</summary>
public class AuthzClientOptions
{
    public const string SectionName = "Authz";

    /// <summary>Root URL of the consuming system's own ms-authz instance (MS-AUTHZ-SPEC.md §2 — never shared).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Sent as the X-Api-Key header on every request.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// How long a subject's effective-permission set is cached before the next call re-hits
    /// GET /me/permissions. Pull pattern (MS-AUTHZ-SPEC.md §8): "no se llama a OpenFGA por request de
    /// negocio". A role change can take up to this long to be reflected.
    /// </summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Tenant code used by <see cref="PermissionAuthorizationBehavior{TRequest,TResponse}"/> when
    /// <see cref="IAuthzRequestContextAccessor.TenantCode"/> is null. For single-tenant hosts: set it
    /// once, sync that same code into ms-authz, and the accessor only has to supply the subject.
    /// </summary>
    public string? DefaultTenantCode { get; set; }
}
