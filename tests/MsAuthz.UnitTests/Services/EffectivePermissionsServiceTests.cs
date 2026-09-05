using FluentAssertions;
using MsAuthz.Application.Common.Identifiers;
using MsAuthz.Application.Services;
using MsAuthz.UnitTests.TestDoubles;

namespace MsAuthz.UnitTests.Services;

public class EffectivePermissionsServiceTests
{
    private static readonly FakeCatalogRepository Catalog = new FakeCatalogRepository()
        .WithRole("vendedor", "Vendedor", "Sales.Read", "Sales.Write")
        .WithRole("cajero", "Cajero", "Cashier.Open", "Cashier.Close", "Sales.Read")
        .WithRole("comprador", "Comprador", "Purchasing.ApproveOrder");

    [Fact]
    public async Task GetEffectivePermissionsAsync_unions_the_catalog_permissions_of_every_assigned_role()
    {
        var gateway = new FakeOpenFgaGateway();
        Assign(gateway, "jurol", "u1", "vendedor");
        Assign(gateway, "jurol", "u1", "cajero");
        var sut = new EffectivePermissionsService(gateway, Catalog, TestLogger.For<EffectivePermissionsService>());

        var result = await sut.GetEffectivePermissionsAsync("jurol", "u1");

        result.Should().Equal("Cashier.Close", "Cashier.Open", "Sales.Read", "Sales.Write");
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_returns_only_the_tenant_asked_for()
    {
        var gateway = new FakeOpenFgaGateway();
        Assign(gateway, "jurol", "u1", "vendedor");
        Assign(gateway, "otraempresa", "u1", "comprador");
        var sut = new EffectivePermissionsService(gateway, Catalog, TestLogger.For<EffectivePermissionsService>());

        var inJurol = await sut.GetEffectivePermissionsAsync("jurol", "u1");
        var inOtra = await sut.GetEffectivePermissionsAsync("otraempresa", "u1");

        inJurol.Should().Equal("Sales.Read", "Sales.Write");
        inOtra.Should().Equal("Purchasing.ApproveOrder");
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_returns_empty_for_a_tenant_the_subject_has_no_role_in()
    {
        var gateway = new FakeOpenFgaGateway();
        Assign(gateway, "jurol", "u1", "vendedor");
        var sut = new EffectivePermissionsService(gateway, Catalog, TestLogger.For<EffectivePermissionsService>());

        var result = await sut.GetEffectivePermissionsAsync("unrelatedtenant", "u1");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_ignores_an_assigned_role_that_is_no_longer_in_the_catalog()
    {
        var gateway = new FakeOpenFgaGateway();
        Assign(gateway, "jurol", "u1", "vendedor");
        Assign(gateway, "jurol", "u1", "retired-role");
        var sut = new EffectivePermissionsService(gateway, Catalog, TestLogger.For<EffectivePermissionsService>());

        var result = await sut.GetEffectivePermissionsAsync("jurol", "u1");

        result.Should().Equal("Sales.Read", "Sales.Write");
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_drops_a_malformed_cross_tenant_assignment()
    {
        var gateway = new FakeOpenFgaGateway();
        Assign(gateway, "jurol", "u1", "vendedor");
        gateway.Seed(OpenFgaIdentifiers.User("jurol", "u1"), OpenFgaIdentifiers.AssigneeRelation, OpenFgaIdentifiers.Role("otraempresa", "comprador"));
        var sut = new EffectivePermissionsService(gateway, Catalog, TestLogger.For<EffectivePermissionsService>());

        var result = await sut.GetEffectivePermissionsAsync("jurol", "u1");

        result.Should().Equal("Sales.Read", "Sales.Write");
    }

    private static void Assign(FakeOpenFgaGateway gateway, string tenant, string subjectId, string roleCode)
        => gateway.Seed(OpenFgaIdentifiers.User(tenant, subjectId), OpenFgaIdentifiers.AssigneeRelation, OpenFgaIdentifiers.Role(tenant, roleCode));
}
