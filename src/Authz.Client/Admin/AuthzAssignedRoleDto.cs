namespace Authz.Client.Admin;

/// <summary>
/// A role assigned to a user in a tenant. Mirrors ms-authz's
/// <c>MsAuthz.Application.Dtos.AssignedRoleDto</c> (response shape of <c>GET/PUT /users/{id}/roles</c>)
/// without depending on that assembly — see <see cref="AuthzRoleDto"/> for why.
/// </summary>
public sealed record AuthzAssignedRoleDto(string Code, string Name);
