using Microsoft.Extensions.Logging;
using MsAuthz.Application.Common.Identifiers;
using MsAuthz.Application.Interfaces;

namespace MsAuthz.Application.Services;

public class EffectivePermissionsService(
    IOpenFgaGateway openFgaGateway,
    ICatalogRepository catalogRepository,
    ILogger<EffectivePermissionsService> logger) : IEffectivePermissionsService
{
    public async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(
        string tenantCode, string subjectId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Resolving effective permissions for {@Request}",
            new { TenantCode = tenantCode, SubjectId = subjectId });

        var user = OpenFgaIdentifiers.User(tenantCode, subjectId);
        var roleObjects = await openFgaGateway.ReadObjectsForUserAsync(
            user, OpenFgaIdentifiers.AssigneeRelation, OpenFgaIdentifiers.RoleType, cancellationToken);

        var assignedCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var roleObject in roleObjects)
        {
            if (OpenFgaIdentifiers.TryStripTenantRolePrefix(roleObject, tenantCode, out var roleCode))
                assignedCodes.Add(roleCode);
        }

        var catalogRoles = await catalogRepository.GetRolesAsync(cancellationToken);
        var permissionCodes = new SortedSet<string>(StringComparer.Ordinal);
        var unknownCodes = new List<string>();

        foreach (var code in assignedCodes)
        {
            var role = catalogRoles.FirstOrDefault(r => r.Code == code);
            if (role is null)
            {
                unknownCodes.Add(code);
                continue;
            }

            permissionCodes.UnionWith(role.PermissionCodes);
        }

        if (unknownCodes.Count > 0)
            logger.LogWarning(
                "Subject {SubjectId} in tenant {TenantCode} is assigned role(s) {UnknownCodes} that are not in the catalog; ignoring them",
                subjectId, tenantCode, unknownCodes);

        logger.LogInformation(
            "Resolved {PermissionCount} effective permission(s) from {RoleCount} role(s) for subject {SubjectId} in tenant {TenantCode}",
            permissionCodes.Count, assignedCodes.Count, subjectId, tenantCode);

        return permissionCodes.ToList();
    }
}
