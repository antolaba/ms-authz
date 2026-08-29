using Microsoft.Extensions.Logging;
using MsAuthz.Application.Common.Identifiers;
using MsAuthz.Application.Interfaces;

namespace MsAuthz.Application.Services;

public class TenantProvisioningService(
    ICatalogRepository catalogRepository,
    ITenantRegistry tenantRegistry,
    IOpenFgaGateway openFgaGateway,
    ILogger<TenantProvisioningService> logger) : ITenantProvisioningService
{
    public async Task ProvisionTenantAsync(string tenantCode, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting tenant provisioning with request: {@Request}", new { TenantCode = tenantCode });

        // Idempotent: RegisterAsync is a no-op for a tenant code already known.
        await tenantRegistry.RegisterAsync(tenantCode, cancellationToken);

        var roles = await catalogRepository.GetRolesAsync(cancellationToken);

        var tuples = roles
            .SelectMany(role => role.PermissionCodes.Select(permissionCode => new OpenFgaTupleKey(
                OpenFgaIdentifiers.RoleAssigneeUserset(tenantCode, role.Code),
                OpenFgaIdentifiers.GrantedRelation,
                OpenFgaIdentifiers.Permission(tenantCode, permissionCode))))
            .ToList();

        if (tuples.Count > 0)
        {
            // Idempotent write: a tuple that already exists (e.g. re-running provisioning, or
            // catalog/sync re-expanding this same tenant) is silently skipped by the gateway.
            await openFgaGateway.WriteTuplesAsync(tuples, cancellationToken);
        }

        logger.LogInformation(
            "Successfully provisioned tenant {TenantCode}: expanded {RoleCount} role(s) into {TupleCount} role->permission tuple(s)",
            tenantCode, roles.Count, tuples.Count);
    }
}
