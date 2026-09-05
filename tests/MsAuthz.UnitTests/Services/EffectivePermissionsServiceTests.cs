using FluentAssertions;
using MsAuthz.Application.Common.Identifiers;
using MsAuthz.Application.Services;
using MsAuthz.UnitTests.TestDoubles;

namespace MsAuthz.UnitTests.Services;

/// <summary>
/// Tenant isolation on GET /me/permissions. The user object carries the tenant, so a subject with
/// roles in two tenants is two different OpenFGA users and ListObjects for one of them can only reach
/// that tenant's tuples. The prefix filter in the service stays as defence in depth against anything
/// malformed in the store (MS-AUTHZ-SPEC.md §15).
/// </summary>
public class EffectivePermissionsServiceTests
{
    [Fact]
    public async Task GetEffectivePermissionsAsync_returns_only_the_permissions_of_the_tenant_asked_for()
    {
        var gateway = new FakeOpenFgaGateway();
        SeedRoleWithPermissions(gateway, "jurol", "8f3c1a94-user", "vendedor", "Sales.Read", "Sales.Write");
        SeedRoleWithPermissions(gateway, "otraempresa", "8f3c1a94-user", "comprador", "Purchasing.ApproveOrder");

        var sut = new EffectivePermissionsService(gateway, TestLogger.For<EffectivePermissionsService>());

        var result = await sut.GetEffectivePermissionsAsync("jurol", "8f3c1a94-user");

        result.Should().BeEquivalentTo(["Sales.Read", "Sales.Write"]);
        result.Should().NotContain("Purchasing.ApproveOrder");
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_returns_only_the_other_tenants_permissions_when_asked_for_it()
    {
        var gateway = new FakeOpenFgaGateway();
        SeedRoleWithPermissions(gateway, "jurol", "8f3c1a94-user", "vendedor", "Sales.Read", "Sales.Write");
        SeedRoleWithPermissions(gateway, "otraempresa", "8f3c1a94-user", "comprador", "Purchasing.ApproveOrder");

        var sut = new EffectivePermissionsService(gateway, TestLogger.For<EffectivePermissionsService>());

        var result = await sut.GetEffectivePermissionsAsync("otraempresa", "8f3c1a94-user");

        result.Should().BeEquivalentTo(["Purchasing.ApproveOrder"]);
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_returns_empty_for_a_tenant_the_subject_has_no_role_in()
    {
        var gateway = new FakeOpenFgaGateway();
        SeedRoleWithPermissions(gateway, "jurol", "8f3c1a94-user", "vendedor", "Sales.Read");

        var sut = new EffectivePermissionsService(gateway, TestLogger.For<EffectivePermissionsService>());

        var result = await sut.GetEffectivePermissionsAsync("unrelatedtenant", "8f3c1a94-user");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_unions_permissions_from_two_roles_in_the_same_tenant()
    {
        var gateway = new FakeOpenFgaGateway();
        SeedRoleWithPermissions(gateway, "jurol", "8f3c1a94-user", "vendedor", "Sales.Read", "Sales.Write");
        SeedRoleWithPermissions(gateway, "jurol", "8f3c1a94-user", "cajero", "Cashier.Open", "Cashier.Close");

        var sut = new EffectivePermissionsService(gateway, TestLogger.For<EffectivePermissionsService>());

        var result = await sut.GetEffectivePermissionsAsync("jurol", "8f3c1a94-user");

        result.Should().BeEquivalentTo(["Sales.Read", "Sales.Write", "Cashier.Open", "Cashier.Close"]);
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_drops_a_permission_reached_through_a_malformed_cross_tenant_assignment()
    {
        var gateway = new FakeOpenFgaGateway();
        SeedRoleWithPermissions(gateway, "jurol", "8f3c1a94-user", "vendedor", "Sales.Read");
        gateway.Seed(
            OpenFgaIdentifiers.User("jurol", "8f3c1a94-user"),
            OpenFgaIdentifiers.AssigneeRelation,
            OpenFgaIdentifiers.Role("otraempresa", "comprador"));
        gateway.Seed(
            OpenFgaIdentifiers.RoleAssigneeUserset("otraempresa", "comprador"),
            OpenFgaIdentifiers.GrantedRelation,
            OpenFgaIdentifiers.Permission("otraempresa", "Purchasing.ApproveOrder"));

        var sut = new EffectivePermissionsService(gateway, TestLogger.For<EffectivePermissionsService>());

        var result = await sut.GetEffectivePermissionsAsync("jurol", "8f3c1a94-user");

        result.Should().BeEquivalentTo(["Sales.Read"]);
    }

    private static void SeedRoleWithPermissions(
        FakeOpenFgaGateway gateway, string tenant, string subjectId, string roleCode, params string[] permissionCodes)
    {
        gateway.Seed(
            OpenFgaIdentifiers.User(tenant, subjectId),
            OpenFgaIdentifiers.AssigneeRelation,
            OpenFgaIdentifiers.Role(tenant, roleCode));

        foreach (var permissionCode in permissionCodes)
        {
            gateway.Seed(
                OpenFgaIdentifiers.RoleAssigneeUserset(tenant, roleCode),
                OpenFgaIdentifiers.GrantedRelation,
                OpenFgaIdentifiers.Permission(tenant, permissionCode));
        }
    }
}
