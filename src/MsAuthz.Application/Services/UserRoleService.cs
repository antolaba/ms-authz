using Microsoft.Extensions.Logging;
using MsAuthz.Application.Common.Exceptions;
using MsAuthz.Application.Common.Identifiers;
using MsAuthz.Application.Dtos;
using MsAuthz.Application.Interfaces;

namespace MsAuthz.Application.Services;

public class UserRoleService(
    IOpenFgaGateway openFgaGateway,
    ICatalogRepository catalogRepository,
    ILogger<UserRoleService> logger) : IUserRoleService
{
    public async Task<IReadOnlyList<AssignedRoleDto>> GetUserRolesAsync(
        string tenantCode, string userId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Loading role assignments for {@Request}", new { TenantCode = tenantCode, UserId = userId });

        var roleCodes = await GetAssignedRoleCodesAsync(tenantCode, userId, cancellationToken);
        var result = await ToAssignedRoleDtosAsync(roleCodes, cancellationToken);

        logger.LogInformation(
            "User {UserId} has {RoleCount} role(s) assigned in tenant {TenantCode}",
            userId, result.Count, tenantCode);

        return result;
    }

    public async Task<IReadOnlyList<AssignedRoleDto>> SetUserRolesAsync(
        string tenantCode, string userId, IReadOnlyList<string> roleCodes, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Starting role assignment update for {@Request}",
            new { TenantCode = tenantCode, UserId = userId, RoleCodes = roleCodes });

        var desiredCodes = roleCodes.Distinct(StringComparer.Ordinal).ToList();

        var unknownCodes = await catalogRepository.GetUnknownRoleCodesAsync(desiredCodes, cancellationToken);
        if (unknownCodes.Count > 0)
        {
            logger.LogWarning(
                "Rejected role assignment for user {UserId} in tenant {TenantCode}: unknown role code(s) {UnknownCodes}",
                userId, tenantCode, unknownCodes);
            throw new InvalidCatalogRequestException(
                $"Unknown role code(s): {string.Join(", ", unknownCodes)}");
        }

        var user = OpenFgaIdentifiers.User(userId);
        var currentCodes = await GetAssignedRoleCodesAsync(tenantCode, userId, cancellationToken);

        var toAdd = desiredCodes.Except(currentCodes, StringComparer.Ordinal).ToList();
        var toRemove = currentCodes.Except(desiredCodes, StringComparer.Ordinal).ToList();

        if (toRemove.Count > 0)
        {
            var deletes = toRemove
                .Select(code => new OpenFgaTupleKey(user, OpenFgaIdentifiers.AssigneeRelation, OpenFgaIdentifiers.Role(tenantCode, code)))
                .ToList();
            await openFgaGateway.DeleteTuplesAsync(deletes, cancellationToken);
        }

        if (toAdd.Count > 0)
        {
            var writes = toAdd
                .Select(code => new OpenFgaTupleKey(user, OpenFgaIdentifiers.AssigneeRelation, OpenFgaIdentifiers.Role(tenantCode, code)))
                .ToList();
            await openFgaGateway.WriteTuplesAsync(writes, cancellationToken);
        }

        logger.LogInformation(
            "Updated role assignments for user {UserId} in tenant {TenantCode}: added {AddedCount}, removed {RemovedCount}",
            userId, tenantCode, toAdd.Count, toRemove.Count);

        return await ToAssignedRoleDtosAsync(desiredCodes, cancellationToken);
    }

    private async Task<List<string>> GetAssignedRoleCodesAsync(
        string tenantCode, string userId, CancellationToken cancellationToken)
    {
        var user = OpenFgaIdentifiers.User(userId);
        var objects = await openFgaGateway.ReadObjectsForUserAsync(
            user, OpenFgaIdentifiers.AssigneeRelation, OpenFgaIdentifiers.RoleType, cancellationToken);

        var codes = new List<string>();
        foreach (var roleObject in objects)
        {
            if (OpenFgaIdentifiers.TryStripTenantRolePrefix(roleObject, tenantCode, out var roleCode))
                codes.Add(roleCode);
        }

        return codes;
    }

    private async Task<IReadOnlyList<AssignedRoleDto>> ToAssignedRoleDtosAsync(
        IReadOnlyList<string> roleCodes, CancellationToken cancellationToken)
    {
        if (roleCodes.Count == 0)
            return [];

        var catalogRoles = await catalogRepository.GetRolesAsync(cancellationToken);
        var byCode = catalogRoles.ToDictionary(r => r.Code, StringComparer.Ordinal);

        return roleCodes
            .Select(code => byCode.TryGetValue(code, out var role)
                ? new AssignedRoleDto(role.Code, role.Name)
                : new AssignedRoleDto(code, code)) // tuple survived even if the catalog entry was later deleted
            .OrderBy(r => r.Code, StringComparer.Ordinal)
            .ToList();
    }
}
