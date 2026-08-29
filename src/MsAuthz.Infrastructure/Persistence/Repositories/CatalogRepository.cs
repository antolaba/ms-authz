using Microsoft.EntityFrameworkCore;
using MsAuthz.Application.Interfaces;

namespace MsAuthz.Infrastructure.Persistence.Repositories;

public class CatalogRepository(AuthzDbContext context) : ICatalogRepository
{
    public async Task<IReadOnlyList<CatalogRole>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await context.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.Code)
            .ToListAsync(cancellationToken);

        return roles
            .Select(r => new CatalogRole(
                r.Code,
                r.Name,
                r.Description,
                r.RolePermissions
                    .Select(rp => rp.Permission.Code)
                    .OrderBy(code => code, StringComparer.Ordinal)
                    .ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<CatalogPermission>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        return await context.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Code)
            .Select(p => new CatalogPermission(p.Code, p.Module, p.Description))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetUnknownRoleCodesAsync(
        IEnumerable<string> roleCodes, CancellationToken cancellationToken = default)
    {
        var requested = roleCodes.Distinct(StringComparer.Ordinal).ToList();
        if (requested.Count == 0)
            return [];

        var known = await context.Roles
            .AsNoTracking()
            .Where(r => requested.Contains(r.Code))
            .Select(r => r.Code)
            .ToListAsync(cancellationToken);

        return requested.Except(known, StringComparer.Ordinal).ToList();
    }
}
