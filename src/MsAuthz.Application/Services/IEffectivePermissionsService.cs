namespace MsAuthz.Application.Services;

/// <summary>Backs GET /me/permissions — the only endpoint on the hot path (MS-AUTHZ-SPEC.md §1, §7).</summary>
public interface IEffectivePermissionsService
{
    /// <summary>
    /// Flat, deduplicated, tenant-filtered list of permission codes (e.g. "Sales.Write") the subject
    /// holds in <paramref name="tenantCode"/>, resolved through OpenFGA's ListObjects.
    /// </summary>
    Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(
        string tenantCode, string subjectId, CancellationToken cancellationToken = default);
}
