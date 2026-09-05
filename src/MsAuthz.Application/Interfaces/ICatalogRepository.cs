namespace MsAuthz.Application.Interfaces;

/// <summary>A catalog role with its effective permission codes (own plus those of included roles).</summary>
public sealed record CatalogRole(string Code, string Name, string? Description, IReadOnlyList<string> PermissionCodes);

/// <summary>A catalog permission.</summary>
public sealed record CatalogPermission(string Code, string Module, string? Description);

/// <summary>
/// Read access to the ms-authz catalog (roles, permissions, role→permission). Backed by a JSON file
/// loaded once at startup and served from memory. The catalog is global to the system (not
/// per-tenant); tenant scoping only exists on role assignments in OpenFGA.
/// </summary>
public interface ICatalogRepository
{
    Task<IReadOnlyList<CatalogRole>> GetRolesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogPermission>> GetPermissionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Codes of every role in the catalog that does NOT exist — used to validate PUT /users/{id}/roles.</summary>
    Task<IReadOnlyList<string>> GetUnknownRoleCodesAsync(IEnumerable<string> roleCodes, CancellationToken cancellationToken = default);
}
