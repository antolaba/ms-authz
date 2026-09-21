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

    /// <summary>
    /// Evicts the cached permission set for (tenantCode, subjectId). Call this right after changing
    /// that subject's role assignments.
    ///
    /// The cache is an in-process IMemoryCache: if the host runs more than one instance, this only
    /// affects the instance that handled the call — the others keep serving the old permission set
    /// until their own <see cref="AuthzClientOptions.CacheTtl"/> expires, with no error to signal it.
    /// </summary>
    Task InvalidateAsync(string tenantCode, string subjectId, CancellationToken cancellationToken = default);
}
