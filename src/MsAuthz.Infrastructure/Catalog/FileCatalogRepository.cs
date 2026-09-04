using MsAuthz.Application.Interfaces;

namespace MsAuthz.Infrastructure.Catalog;

/// <summary>
/// Serves the catalog from an in-memory <see cref="CatalogSnapshot"/> loaded once at startup by
/// <see cref="CatalogFileLoader"/> — no I/O on any call. Registered as a singleton (see
/// DependencyInjection.AddInfrastructure).
/// </summary>
public class FileCatalogRepository(CatalogSnapshot snapshot) : ICatalogRepository
{
    public Task<IReadOnlyList<CatalogRole>> GetRolesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(snapshot.Roles);

    public Task<IReadOnlyList<CatalogPermission>> GetPermissionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(snapshot.Permissions);

    public Task<IReadOnlyList<string>> GetUnknownRoleCodesAsync(
        IEnumerable<string> roleCodes, CancellationToken cancellationToken = default)
    {
        var known = snapshot.Roles.Select(r => r.Code).ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<string> unknown = roleCodes
            .Distinct(StringComparer.Ordinal)
            .Where(code => !known.Contains(code))
            .ToList();

        return Task.FromResult(unknown);
    }
}
