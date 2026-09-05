namespace MsAuthz.Application.Services;

/// <summary>Backs GET /me/permissions — the only endpoint on the hot path.</summary>
public interface IEffectivePermissionsService
{
    /// <summary>
    /// Flat, deduplicated list of permission codes (e.g. "Sales.Write") the subject holds in
    /// <paramref name="tenantCode"/>: the union of the catalog permissions of every role assigned to
    /// the subject in that tenant.
    /// </summary>
    Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(
        string tenantCode, string subjectId, CancellationToken cancellationToken = default);
}
