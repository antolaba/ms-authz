namespace MsAuthz.Application.Services;

/// <summary>
/// Re-expands the catalog into tuples for a caller-supplied list of tenants — backs
/// POST /catalog/sync, to be run after a catalog change (MS-AUTHZ-SPEC.md §5, §7). Idempotent, same
/// operational shape as `migrate-all-tenant.sh` in the wider workspace. ms-authz has no source of
/// truth for which tenants exist — the caller passes the list on every sync.
/// </summary>
public interface ICatalogSyncService
{
    /// <returns>The distinct tenant codes that were synced.</returns>
    Task<IReadOnlyList<string>> SyncTenantsAsync(IReadOnlyList<string> tenantCodes, CancellationToken cancellationToken = default);
}
