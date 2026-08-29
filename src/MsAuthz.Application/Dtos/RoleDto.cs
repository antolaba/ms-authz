namespace MsAuthz.Application.Dtos;

/// <summary>Catalog role, with the permission codes it carries — response shape for GET /roles.</summary>
public sealed record RoleDto(string Code, string Name, string? Description, IReadOnlyList<string> PermissionCodes);

/// <summary>Catalog permission — part of the response shape for GET /roles.</summary>
public sealed record PermissionDto(string Code, string Module, string? Description);

/// <summary>A role assigned to a user in a tenant — response shape for GET/PUT /users/{id}/roles.</summary>
public sealed record AssignedRoleDto(string Code, string Name);
