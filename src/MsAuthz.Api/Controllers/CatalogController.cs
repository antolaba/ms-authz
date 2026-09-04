using Microsoft.AspNetCore.Mvc;
using MsAuthz.Api.Contracts;
using MsAuthz.Application.Services;

namespace MsAuthz.Api.Controllers;

/// <summary>Catalog re-expansion job, to run after a role/permission change (MS-AUTHZ-SPEC.md §5, §7).</summary>
[ApiController]
[Route("catalog")]
public class CatalogController(ICatalogSyncService catalogSyncService) : ControllerBase
{
    /// <summary>Idempotent — re-expands the current catalog into tuples for every tenant code given.</summary>
    [HttpPost("sync")]
    public async Task<ActionResult<IReadOnlyList<string>>> Sync(
        [FromBody] SyncCatalogRequest request, CancellationToken cancellationToken)
    {
        if (request.TenantCodes is not { Count: > 0 })
            return BadRequest("'tenantCodes' is required.");

        var syncedTenants = await catalogSyncService.SyncTenantsAsync(request.TenantCodes, cancellationToken);
        return Ok(syncedTenants);
    }
}
