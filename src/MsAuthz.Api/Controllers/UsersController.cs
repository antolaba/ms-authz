using Microsoft.AspNetCore.Mvc;
using MsAuthz.Api.Contracts;
using MsAuthz.Application.Dtos;
using MsAuthz.Application.Services;

namespace MsAuthz.Api.Controllers;

/// <summary>
/// Role assignment for a user within a tenant. "{id}" is the keycloak user id — see the security
/// boundary note on ApiKeyAuthenticationHandler: ms-authz does not verify it exists in Keycloak.
/// </summary>
[ApiController]
[Route("users/{id}/roles")]
public class UsersController(IUserRoleService userRoleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AssignedRoleDto>>> GetUserRoles(
        string id, [FromQuery] string tenant, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenant))
            return BadRequest("Query parameter 'tenant' is required.");

        var roles = await userRoleService.GetUserRolesAsync(tenant, id, cancellationToken);
        return Ok(roles);
    }

    [HttpPut]
    public async Task<ActionResult<IReadOnlyList<AssignedRoleDto>>> SetUserRoles(
        string id, [FromQuery] string tenant, [FromBody] SetUserRolesRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenant))
            return BadRequest("Query parameter 'tenant' is required.");

        var roles = await userRoleService.SetUserRolesAsync(tenant, id, request.RoleCodes, cancellationToken);
        return Ok(roles);
    }
}
