namespace MsAuthz.Application.Services;

/// <summary>
/// Expands the entire catalog into OpenFGA tuples for one tenant. Only called by
/// <see cref="ICatalogSyncService"/>, which backs POST /catalog/sync. Idempotent: safe to call more
/// than once for the same tenant.
/// </summary>
public interface ITenantProvisioningService
{
    Task ProvisionTenantAsync(string tenantCode, CancellationToken cancellationToken = default);
}
