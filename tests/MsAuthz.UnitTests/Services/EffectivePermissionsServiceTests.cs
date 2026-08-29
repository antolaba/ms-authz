using FluentAssertions;
using MsAuthz.Application.Common.Identifiers;
using MsAuthz.Application.Services;
using MsAuthz.UnitTests.TestDoubles;

namespace MsAuthz.UnitTests.Services;

/// <summary>
/// The CRITICAL test called out in MS-AUTHZ-SPEC.md §4: ListObjects returns permissions across every
/// tenant the subject has a role in, and ms-authz MUST filter to only the tenant being asked about
/// before responding to GET /me/permissions, or permissions leak between companies.
/// </summary>
public class EffectivePermissionsServiceTests
{
    [Fact]
    public async Task GetEffectivePermissionsAsync_filters_out_permissions_from_other_tenants()
    {
        var gateway = new FakeOpenFgaGateway();
        var user = OpenFgaIdentifiers.User("8f3c1a94-user");

        // Same subject holds a role in two different tenants — reproduces exactly the scenario from
        // MS-AUTHZ-SPEC.md §4's empirical ListObjects example.
        SeedRoleWithPermissions(gateway, user, "jurol", "vendedor", "Sales.Read", "Sales.Write");
        SeedRoleWithPermissions(gateway, user, "otraempresa", "comprador", "Purchasing.ApproveOrder");

        var sut = new EffectivePermissionsService(gateway, TestLogger.For<EffectivePermissionsService>());

        var result = await sut.GetEffectivePermissionsAsync("jurol", "8f3c1a94-user");

        result.Should().BeEquivalentTo(["Sales.Read", "Sales.Write"]);
        result.Should().NotContain("Purchasing.ApproveOrder");
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_returns_only_the_other_tenants_permissions_when_asked_for_it()
    {
        var gateway = new FakeOpenFgaGateway();
        var user = OpenFgaIdentifiers.User("8f3c1a94-user");

        SeedRoleWithPermissions(gateway, user, "jurol", "vendedor", "Sales.Read", "Sales.Write");
        SeedRoleWithPermissions(gateway, user, "otraempresa", "comprador", "Purchasing.ApproveOrder");

        var sut = new EffectivePermissionsService(gateway, TestLogger.For<EffectivePermissionsService>());

        var result = await sut.GetEffectivePermissionsAsync("otraempresa", "8f3c1a94-user");

        result.Should().BeEquivalentTo(["Purchasing.ApproveOrder"]);
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_returns_empty_for_a_tenant_the_subject_has_no_role_in()
    {
        var gateway = new FakeOpenFgaGateway();
        var user = OpenFgaIdentifiers.User("8f3c1a94-user");
        SeedRoleWithPermissions(gateway, user, "jurol", "vendedor", "Sales.Read");

        var sut = new EffectivePermissionsService(gateway, TestLogger.For<EffectivePermissionsService>());

        var result = await sut.GetEffectivePermissionsAsync("unrelatedtenant", "8f3c1a94-user");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_unions_permissions_from_two_roles_in_the_same_tenant()
    {
        var gateway = new FakeOpenFgaGateway();
        var user = OpenFgaIdentifiers.User("8f3c1a94-user");
        SeedRoleWithPermissions(gateway, user, "jurol", "vendedor", "Sales.Read", "Sales.Write");
        SeedRoleWithPermissions(gateway, user, "jurol", "cajero", "Cashier.Open", "Cashier.Close");

        var sut = new EffectivePermissionsService(gateway, TestLogger.For<EffectivePermissionsService>());

        var result = await sut.GetEffectivePermissionsAsync("jurol", "8f3c1a94-user");

        result.Should().BeEquivalentTo(["Sales.Read", "Sales.Write", "Cashier.Open", "Cashier.Close"]);
    }

    private static void SeedRoleWithPermissions(
        FakeOpenFgaGateway gateway, string user, string tenant, string roleCode, params string[] permissionCodes)
    {
        gateway.Seed(user, OpenFgaIdentifiers.AssigneeRelation, OpenFgaIdentifiers.Role(tenant, roleCode));

        foreach (var permissionCode in permissionCodes)
        {
            gateway.Seed(
                OpenFgaIdentifiers.RoleAssigneeUserset(tenant, roleCode),
                OpenFgaIdentifiers.GrantedRelation,
                OpenFgaIdentifiers.Permission(tenant, permissionCode));
        }
    }
}
