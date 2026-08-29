using MsAuthz.Application.Interfaces;

namespace MsAuthz.UnitTests.TestDoubles;

public class FakeCatalogRepository : ICatalogRepository
{
    private readonly List<CatalogRole> _roles = [];

    public FakeCatalogRepository WithRole(string code, string name, params string[] permissionCodes)
    {
        _roles.Add(new CatalogRole(code, name, null, permissionCodes));
        return this;
    }

    public Task<IReadOnlyList<CatalogRole>> GetRolesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CatalogRole>>(_roles);

    public Task<IReadOnlyList<CatalogPermission>> GetPermissionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CatalogPermission>>(
            _roles.SelectMany(r => r.PermissionCodes).Distinct()
                .Select(code => new CatalogPermission(code, "Module", null))
                .ToList());

    public Task<IReadOnlyList<string>> GetUnknownRoleCodesAsync(IEnumerable<string> roleCodes, CancellationToken cancellationToken = default)
    {
        var known = _roles.Select(r => r.Code).ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<string> unknown = roleCodes.Distinct(StringComparer.Ordinal).Where(c => !known.Contains(c)).ToList();
        return Task.FromResult(unknown);
    }
}
