using Microsoft.Extensions.Logging;
using MsAuthz.Application.Common.Exceptions;
using MsAuthz.Application.Common.Identifiers;

namespace MsAuthz.Application.Services;

public class CatalogSyncService(
    ITenantProvisioningService tenantProvisioningService,
    ILogger<CatalogSyncService> logger) : ICatalogSyncService
{
    public async Task<IReadOnlyList<string>> SyncTenantsAsync(
        IReadOnlyList<string> tenantCodes, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting catalog sync with {@Request}", new { TenantCodes = tenantCodes });

        var distinctCodes = tenantCodes.Distinct(StringComparer.Ordinal).ToList();

        if (distinctCodes.Count == 0)
        {
            logger.LogWarning("Rejected catalog sync: no tenant code was supplied");
            throw new InvalidCatalogRequestException("At least one tenant code is required.");
        }

        foreach (var tenantCode in distinctCodes)
            OpenFgaIdentifiers.EnsureValidTenantCode(tenantCode);

        foreach (var tenantCode in distinctCodes)
            await tenantProvisioningService.ProvisionTenantAsync(tenantCode, cancellationToken);

        logger.LogInformation("Successfully synced catalog for {TenantCount} tenant(s)", distinctCodes.Count);

        return distinctCodes;
    }
}
