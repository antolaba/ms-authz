using Microsoft.Extensions.Logging;
using MsAuthz.Application.Interfaces;

namespace MsAuthz.Application.Services;

public class CatalogSyncService(
    ITenantRegistry tenantRegistry,
    ITenantProvisioningService tenantProvisioningService,
    ILogger<CatalogSyncService> logger) : ICatalogSyncService
{
    public async Task<IReadOnlyList<string>> SyncAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting catalog sync across all known tenants");

        var tenantCodes = await tenantRegistry.GetAllTenantCodesAsync(cancellationToken);

        // Re-running provisioning is exactly the re-expansion this endpoint promises: it writes
        // every role->permission tuple the catalog now says should exist, and is idempotent for
        // tuples that already do (MS-AUTHZ-SPEC.md §5).
        foreach (var tenantCode in tenantCodes)
            await tenantProvisioningService.ProvisionTenantAsync(tenantCode, cancellationToken);

        logger.LogInformation("Successfully synced catalog for {TenantCount} tenant(s)", tenantCodes.Count);

        return tenantCodes;
    }
}
