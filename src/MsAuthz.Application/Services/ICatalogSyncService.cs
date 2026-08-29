namespace MsAuthz.Application.Services;

/// <summary>
/// Re-expands the catalog into tuples for every tenant ms-authz knows about — backs
/// POST /catalog/sync, to be run after a catalog change (MS-AUTHZ-SPEC.md §5, §7). Idempotent, same
/// operational shape as `migrate-all-tenant.sh` in the wider workspace.
/// </summary>
public interface ICatalogSyncService
{
    /// <returns>The tenant codes that were (re-)synced.</returns>
    Task<IReadOnlyList<string>> SyncAllTenantsAsync(CancellationToken cancellationToken = default);
}
