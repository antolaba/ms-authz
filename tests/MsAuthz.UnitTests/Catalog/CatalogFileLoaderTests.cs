using FluentAssertions;
using MsAuthz.Infrastructure.Catalog;

namespace MsAuthz.UnitTests.Catalog;

public class CatalogFileLoaderTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    [Fact]
    public void Load_returns_roles_and_permissions_ordered_by_code()
    {
        var path = WriteCatalog("""
        {
          "permissions": [
            { "code": "Sales.Write", "module": "Sales", "description": "Create and edit sales" },
            { "code": "Sales.Read", "module": "Sales", "description": "View sales" }
          ],
          "roles": [
            { "code": "seller", "name": "Seller", "description": null, "permissions": ["Sales.Write", "Sales.Read"] }
          ]
        }
        """);

        var snapshot = CatalogFileLoader.Load(path);

        snapshot.Permissions.Select(p => p.Code).Should().Equal("Sales.Read", "Sales.Write");
        snapshot.Roles.Should().ContainSingle();
        snapshot.Roles[0].PermissionCodes.Should().Equal("Sales.Read", "Sales.Write");
    }

    [Fact]
    public async Task Load_populates_a_repository_whose_GetUnknownRoleCodesAsync_reflects_the_loaded_roles()
    {
        var path = WriteCatalog("""
        {
          "permissions": [
            { "code": "Sales.Read", "module": "Sales", "description": null }
          ],
          "roles": [
            { "code": "seller", "name": "Seller", "description": null, "permissions": ["Sales.Read"] }
          ]
        }
        """);

        var repository = new FileCatalogRepository(CatalogFileLoader.Load(path));

        var unknown = await repository.GetUnknownRoleCodesAsync(["seller", "not-a-role"]);

        unknown.Should().BeEquivalentTo(["not-a-role"]);
    }

    [Fact]
    public void Load_expands_a_wildcard_role_to_every_permission_in_the_catalog()
    {
        var path = WriteCatalog("""
        {
          "permissions": [
            { "code": "Sales.Read", "module": "Sales", "description": null },
            { "code": "Sales.Write", "module": "Sales", "description": null }
          ],
          "roles": [
            { "code": "admin", "name": "Administrator", "description": null, "permissions": ["*"] }
          ]
        }
        """);

        var snapshot = CatalogFileLoader.Load(path);

        snapshot.Roles[0].PermissionCodes.Should().Equal("Sales.Read", "Sales.Write");
    }

    [Fact]
    public void Load_throws_when_a_role_references_a_permission_that_does_not_exist()
    {
        var path = WriteCatalog("""
        {
          "permissions": [
            { "code": "Sales.Read", "module": "Sales", "description": null }
          ],
          "roles": [
            { "code": "seller", "name": "Seller", "description": null, "permissions": ["Sales.Bogus"] }
          ]
        }
        """);

        var act = () => CatalogFileLoader.Load(path);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Sales.Bogus*");
    }

    [Fact]
    public void Load_throws_for_a_duplicate_permission_code()
    {
        var path = WriteCatalog("""
        {
          "permissions": [
            { "code": "Sales.Read", "module": "Sales", "description": null },
            { "code": "Sales.Read", "module": "Sales", "description": "duplicate" }
          ],
          "roles": []
        }
        """);

        var act = () => CatalogFileLoader.Load(path);

        act.Should().Throw<InvalidOperationException>().WithMessage("*duplicate*Sales.Read*");
    }

    [Fact]
    public void Load_throws_for_a_permission_code_containing_the_tenant_separator()
    {
        var path = WriteCatalog("""
        {
          "permissions": [
            { "code": "Sales|Read", "module": "Sales", "description": null }
          ],
          "roles": []
        }
        """);

        var act = () => CatalogFileLoader.Load(path);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Sales|Read*");
    }

    [Fact]
    public void Load_throws_with_the_path_when_the_file_does_not_exist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");

        var act = () => CatalogFileLoader.Load(path);

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{path}*");
    }

    private string WriteCatalog(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(path, json);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
