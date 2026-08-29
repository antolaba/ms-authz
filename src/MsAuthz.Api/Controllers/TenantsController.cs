using Microsoft.AspNetCore.Mvc;
using MsAuthz.Api.Contracts;
using MsAuthz.Application.Services;

namespace MsAuthz.Api.Controllers;

/// <summary>Tenant provisioning: expands the whole catalog into tuples for a new tenant (MS-AUTHZ-SPEC.md §5, §11).</summary>
[ApiController]
[Route("tenants")]
public class TenantsController(ITenantProvisioningService tenantProvisioningService) : ControllerBase
{
    /// <summary>Idempotent — safe to call again for a tenant already provisioned.</summary>
    [HttpPost]
    public async Task<IActionResult> ProvisionTenant(
        [FromBody] ProvisionTenantRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantCode))
            return BadRequest("'tenantCode' is required.");

        await tenantProvisioningService.ProvisionTenantAsync(request.TenantCode, cancellationToken);
        return NoContent();
    }
}
