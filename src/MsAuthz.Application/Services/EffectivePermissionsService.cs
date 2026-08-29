using Microsoft.Extensions.Logging;
using MsAuthz.Application.Common.Identifiers;
using MsAuthz.Application.Interfaces;

namespace MsAuthz.Application.Services;

public class EffectivePermissionsService(
    IOpenFgaGateway openFgaGateway,
    ILogger<EffectivePermissionsService> logger) : IEffectivePermissionsService
{
    public async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(
        string tenantCode, string subjectId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Resolving effective permissions for {@Request}",
            new { TenantCode = tenantCode, SubjectId = subjectId });

        var user = OpenFgaIdentifiers.User(subjectId);

        // ListObjects returns permission objects across every tenant the subject has a role in — not
        // just tenantCode. Verified empirically (MS-AUTHZ-SPEC.md §4). We MUST filter by the
        // "permission:<tenantCode>|" prefix below, or permissions leak across tenants.
        var objects = await openFgaGateway.ListObjectsAsync(
            user, OpenFgaIdentifiers.GrantedRelation, OpenFgaIdentifiers.PermissionType, cancellationToken);

        var permissionCodes = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var permissionObject in objects)
        {
            if (OpenFgaIdentifiers.TryStripTenantPrefix(permissionObject, tenantCode, out var permissionCode))
                permissionCodes.Add(permissionCode);
        }

        logger.LogInformation(
            "Resolved {PermissionCount} effective permission(s) for subject {SubjectId} in tenant {TenantCode} " +
            "(OpenFGA returned {RawObjectCount} object(s) across all tenants before filtering)",
            permissionCodes.Count, subjectId, tenantCode, objects.Count);

        return permissionCodes.ToList();
    }
}
