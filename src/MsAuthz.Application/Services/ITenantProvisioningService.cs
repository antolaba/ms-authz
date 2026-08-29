namespace MsAuthz.Application.Services;

/// <summary>
/// Expands the entire catalog into per-tenant OpenFGA tuples — backs POST /tenants
/// (MS-AUTHZ-SPEC.md §5, "Las tuplas rol→permiso se materializan por tenant"). Idempotent: safe to
/// call more than once for the same tenant.
/// </summary>
public interface ITenantProvisioningService
{
    Task ProvisionTenantAsync(string tenantCode, CancellationToken cancellationToken = default);
}
