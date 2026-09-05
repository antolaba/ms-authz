using FluentAssertions;
using MsAuthz.Application.Common.Exceptions;
using MsAuthz.Application.Common.Identifiers;
using MsAuthz.Application.Services;
using MsAuthz.UnitTests.TestDoubles;

namespace MsAuthz.UnitTests.Services;

public class UserRoleServiceTests
{
    [Fact]
    public async Task SetUserRolesAsync_writes_assignee_tuples_for_new_roles()
    {
        var catalog = new FakeCatalogRepository().WithRole("vendedor", "Vendedor").WithRole("cajero", "Cajero");
        var gateway = new FakeOpenFgaGateway();
        var sut = new UserRoleService(gateway, catalog, TestLogger.For<UserRoleService>());

        var result = await sut.SetUserRolesAsync("jurol", "user-1", ["vendedor", "cajero"]);

        result.Select(r => r.Code).Should().BeEquivalentTo(["vendedor", "cajero"]);
        gateway.Tuples.Should().Contain((
            OpenFgaIdentifiers.User("jurol", "user-1"), OpenFgaIdentifiers.AssigneeRelation, OpenFgaIdentifiers.Role("jurol", "vendedor")));
        gateway.Tuples.Should().Contain((
            OpenFgaIdentifiers.User("jurol", "user-1"), OpenFgaIdentifiers.AssigneeRelation, OpenFgaIdentifiers.Role("jurol", "cajero")));
    }

    [Fact]
    public async Task SetUserRolesAsync_removes_a_role_no_longer_in_the_desired_set()
    {
        var catalog = new FakeCatalogRepository().WithRole("vendedor", "Vendedor").WithRole("cajero", "Cajero");
        var gateway = new FakeOpenFgaGateway();
        var sut = new UserRoleService(gateway, catalog, TestLogger.For<UserRoleService>());

        await sut.SetUserRolesAsync("jurol", "user-1", ["vendedor", "cajero"]);
        await sut.SetUserRolesAsync("jurol", "user-1", ["vendedor"]);

        gateway.Tuples.Should().NotContain((
            OpenFgaIdentifiers.User("jurol", "user-1"), OpenFgaIdentifiers.AssigneeRelation, OpenFgaIdentifiers.Role("jurol", "cajero")));
        gateway.Tuples.Should().Contain((
            OpenFgaIdentifiers.User("jurol", "user-1"), OpenFgaIdentifiers.AssigneeRelation, OpenFgaIdentifiers.Role("jurol", "vendedor")));
    }

    [Fact]
    public async Task SetUserRolesAsync_does_not_touch_the_same_users_roles_in_another_tenant()
    {
        var catalog = new FakeCatalogRepository().WithRole("vendedor", "Vendedor");
        var gateway = new FakeOpenFgaGateway();
        var sut = new UserRoleService(gateway, catalog, TestLogger.For<UserRoleService>());

        await sut.SetUserRolesAsync("jurol", "user-1", ["vendedor"]);
        await sut.SetUserRolesAsync("otraempresa", "user-1", []);

        gateway.Tuples.Should().Contain((
            OpenFgaIdentifiers.User("jurol", "user-1"), OpenFgaIdentifiers.AssigneeRelation, OpenFgaIdentifiers.Role("jurol", "vendedor")),
            "clearing roles in one tenant must not remove the same user's role in a different tenant");
    }

    [Fact]
    public async Task SetUserRolesAsync_throws_for_an_unknown_role_code()
    {
        var catalog = new FakeCatalogRepository().WithRole("vendedor", "Vendedor");
        var gateway = new FakeOpenFgaGateway();
        var sut = new UserRoleService(gateway, catalog, TestLogger.For<UserRoleService>());

        var act = () => sut.SetUserRolesAsync("jurol", "user-1", ["not-a-real-role"]);

        await act.Should().ThrowAsync<InvalidCatalogRequestException>();
        gateway.Tuples.Should().BeEmpty("no tuple should be written when validation fails");
    }

    [Fact]
    public async Task GetUserRolesAsync_returns_empty_for_a_user_with_no_roles()
    {
        var catalog = new FakeCatalogRepository().WithRole("vendedor", "Vendedor");
        var gateway = new FakeOpenFgaGateway();
        var sut = new UserRoleService(gateway, catalog, TestLogger.For<UserRoleService>());

        var result = await sut.GetUserRolesAsync("jurol", "user-1");

        result.Should().BeEmpty();
    }
}
