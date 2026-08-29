using Microsoft.Extensions.Logging;
using MsAuthz.Application.Dtos;
using MsAuthz.Application.Interfaces;

namespace MsAuthz.Application.Services;

public class CatalogService(
    ICatalogRepository catalogRepository,
    ILogger<CatalogService> logger) : ICatalogService
{
    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Loading role catalog");

        var roles = await catalogRepository.GetRolesAsync(cancellationToken);

        logger.LogInformation("Loaded {RoleCount} role(s) from the catalog", roles.Count);

        return roles
            .Select(r => new RoleDto(r.Code, r.Name, r.Description, r.PermissionCodes))
            .ToList();
    }
}
