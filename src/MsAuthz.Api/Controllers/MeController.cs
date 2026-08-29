using Microsoft.AspNetCore.Mvc;
using MsAuthz.Application.Services;

namespace MsAuthz.Api.Controllers;

/// <summary>
/// The only endpoint on the hot path (MS-AUTHZ-SPEC.md §1, §7). Callers pass the keycloak user id and
/// tenant code directly — ms-authz does not verify either against Keycloak (see the security-boundary
/// note on ApiKeyAuthenticationHandler).
/// </summary>
[ApiController]
[Route("me")]
public class MeController(IEffectivePermissionsService effectivePermissionsService) : ControllerBase
{
    /// <summary>Flat list of permission codes the subject holds in the given tenant.</summary>
    [HttpGet("permissions")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetPermissions(
        [FromQuery] string tenant, [FromQuery] string subject, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenant))
            return BadRequest("Query parameter 'tenant' is required.");

        if (string.IsNullOrWhiteSpace(subject))
            return BadRequest("Query parameter 'subject' is required.");

        var permissions = await effectivePermissionsService.GetEffectivePermissionsAsync(tenant, subject, cancellationToken);
        return Ok(permissions);
    }
}
