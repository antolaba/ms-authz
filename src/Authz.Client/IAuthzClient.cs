namespace Authz.Client;

/// <summary>
/// Client for a system's own ms-authz instance (MS-AUTHZ-SPEC.md §8). Pull pattern: results are
/// cached with a short TTL, so business requests never wait on OpenFGA.
/// </summary>
public interface IAuthzClient
{
    /// <summary>
    /// Flat list of permission codes (e.g. "Sales.Write") the subject holds in the given tenant.
    /// Cached per (tenantCode, subjectId) for <see cref="AuthzClientOptions.CacheTtl"/>.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(
        string tenantCode, string subjectId, CancellationToken cancellationToken = default);
}
