namespace Authz.Client.Admin;

/// <summary>
/// Administration operations against a system's own ms-authz instance: reading the readable role
/// catalog and reading/replacing a user's role assignments in a tenant — the backend side of a
/// role-administration screen.
///
/// Deliberately a separate interface from <see cref="IAuthzClient"/>, and deliberately uncached:
/// <see cref="IAuthzClient.GetEffectivePermissionsAsync"/> is the hot-path pull-cache (§8, 5 min TTL)
/// that business requests hit on every call. These are low-volume administration operations — an
/// admin screen listing/loading a page of users, or writing a role change — where a cache would only
/// risk serving stale reads right after an assignment, with no meaningful load to save. Every call
/// here hits ms-authz directly.
/// </summary>
public interface IAuthzAdminClient
{
    /// <summary>The full, system-wide role catalog (not tenant-scoped — MS-AUTHZ-SPEC.md §5: "catálogo
    /// de roles fijo por sistema"). Backs the role-administration screen's role list.</summary>
    Task<IReadOnlyList<AuthzRoleDto>> GetRoleCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>Roles currently assigned to <paramref name="userId"/> within <paramref name="tenantCode"/>.</summary>
    Task<IReadOnlyList<AuthzAssignedRoleDto>> GetUserRolesAsync(
        string tenantCode, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the user's entire role set in the tenant with exactly <paramref name="roleCodes"/> —
    /// a replace-set, not an append: roles not in the list are revoked, roles in the list not
    /// currently held are granted, the rest are left untouched. Throws
    /// <see cref="Exceptions.AuthzInvalidRoleCodesException"/> if any code is not in the catalog.
    /// </summary>
    Task<IReadOnlyList<AuthzAssignedRoleDto>> ReplaceUserRolesAsync(
        string tenantCode, string userId, IReadOnlyList<string> roleCodes, CancellationToken cancellationToken = default);
}
