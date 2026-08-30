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

        try
        {
            OpenFgaIdentifiers.EnsureValidTenantCode(tenantCode);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(
                "Rejected tenant provisioning for tenant code {TenantCode}: {Reason}", tenantCode, ex.Message);
            throw;
        }

        // Idempotent: RegisterAsync is a no-op for a tenant code already known.
        await tenantRegistry.RegisterAsync(tenantCode, cancellationToken);

        var roles = await catalogRepository.GetRolesAsync(cancellationToken);

        var (toAdd, toRemove) = await DiffTuplesAsync(tenantCode, roles, cancellationToken);

        if (toRemove.Count > 0)
            await openFgaGateway.DeleteTuplesAsync(toRemove, cancellationToken);

        if (toAdd.Count > 0)
            await openFgaGateway.WriteTuplesAsync(toAdd, cancellationToken);

        logger.LogInformation(
            "Successfully provisioned tenant {TenantCode}: {RoleCount} role(s) expanded, {AddedCount} tuple(s) added, {RemovedCount} tuple(s) removed",
            tenantCode, roles.Count, toAdd.Count, toRemove.Count);
    }

    private async Task<(List<OpenFgaTupleKey> ToAdd, List<OpenFgaTupleKey> ToRemove)> DiffTuplesAsync(
        string tenantCode, IReadOnlyList<CatalogRole> roles, CancellationToken cancellationToken)
    {
        var toAdd = new List<OpenFgaTupleKey>();
        var toRemove = new List<OpenFgaTupleKey>();

        foreach (var role in roles)
        {
            var assignee = OpenFgaIdentifiers.RoleAssigneeUserset(tenantCode, role.Code);
            var desiredCodes = role.PermissionCodes.ToHashSet(StringComparer.Ordinal);

            var grantedObjects = await openFgaGateway.ReadObjectsForUserAsync(
                assignee, OpenFgaIdentifiers.GrantedRelation, OpenFgaIdentifiers.PermissionType, cancellationToken);

            var currentCodes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var permissionObject in grantedObjects)
            {
                if (OpenFgaIdentifiers.TryStripTenantPrefix(permissionObject, tenantCode, out var code))
                    currentCodes.Add(code);
            }

            foreach (var code in desiredCodes.Except(currentCodes, StringComparer.Ordinal))
                toAdd.Add(new OpenFgaTupleKey(assignee, OpenFgaIdentifiers.GrantedRelation, OpenFgaIdentifiers.Permission(tenantCode, code)));

            foreach (var code in currentCodes.Except(desiredCodes, StringComparer.Ordinal))
                toRemove.Add(new OpenFgaTupleKey(assignee, OpenFgaIdentifiers.GrantedRelation, OpenFgaIdentifiers.Permission(tenantCode, code)));
        }

        return (toAdd, toRemove);
    }
}
