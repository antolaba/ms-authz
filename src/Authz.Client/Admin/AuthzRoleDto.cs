namespace Authz.Client.Admin;

/// <summary>
/// Catalog role, with the permission codes it carries. Mirrors ms-authz's
/// <c>MsAuthz.Application.Dtos.RoleDto</c> (response shape of <c>GET /roles</c>) without depending on
/// that assembly — Authz.Client is a standalone package consumed by any .NET system
/// (MS-AUTHZ-SPEC.md §1, §8).
/// </summary>
public sealed record AuthzRoleDto(string Code, string Name, string? Description, IReadOnlyList<string> PermissionCodes);
