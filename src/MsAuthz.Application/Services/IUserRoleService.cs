using MsAuthz.Application.Dtos;

namespace MsAuthz.Application.Services;

/// <summary>
/// Assignment of catalog roles to a user within a tenant — backs GET/PUT /users/{id}/roles
/// (MS-AUTHZ-SPEC.md §4, "Tuplas" — writes/deletes `assignee` tuples).
/// </summary>
public interface IUserRoleService
{
    Task<IReadOnlyList<AssignedRoleDto>> GetUserRolesAsync(
        string tenantCode, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the user's role set in the tenant with exactly <paramref name="roleCodes"/>: writes
    /// the newly-added assignee tuples, deletes the ones no longer present, leaves the rest untouched.
    /// Throws <see cref="Common.Exceptions.InvalidCatalogRequestException"/> if any code is not in the catalog.
    /// </summary>
    Task<IReadOnlyList<AssignedRoleDto>> SetUserRolesAsync(
        string tenantCode, string userId, IReadOnlyList<string> roleCodes, CancellationToken cancellationToken = default);
}
