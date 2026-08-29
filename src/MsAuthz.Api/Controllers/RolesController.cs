using Microsoft.AspNetCore.Mvc;
using MsAuthz.Application.Dtos;
using MsAuthz.Application.Services;

namespace MsAuthz.Api.Controllers;

/// <summary>Read-only catalog, for the future role-administration screen (MS-AUTHZ-SPEC.md §12 step 8).</summary>
[ApiController]
[Route("roles")]
public class RolesController(ICatalogService catalogService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await catalogService.GetRolesAsync(cancellationToken);
        return Ok(roles);
    }
}
