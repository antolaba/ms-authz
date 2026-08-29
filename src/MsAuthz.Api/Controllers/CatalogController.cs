using Microsoft.AspNetCore.Mvc;
using MsAuthz.Application.Services;

namespace MsAuthz.Api.Controllers;

/// <summary>Catalog re-expansion job, to run after a role/permission change (MS-AUTHZ-SPEC.md §5, §7).</summary>
[ApiController]
[Route("catalog")]
public class CatalogController(ICatalogSyncService catalogSyncService) : ControllerBase
{
    /// <summary>Idempotent — re-expands the current catalog into tuples for every known tenant.</summary>
    [HttpPost("sync")]
    public async Task<ActionResult<IReadOnlyList<string>>> Sync(CancellationToken cancellationToken)
    {
        var syncedTenants = await catalogSyncService.SyncAllTenantsAsync(cancellationToken);
        return Ok(syncedTenants);
    }
}
