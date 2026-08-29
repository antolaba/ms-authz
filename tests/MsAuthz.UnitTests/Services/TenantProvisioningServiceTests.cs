using FluentAssertions;
using MsAuthz.Application.Common.Identifiers;
using MsAuthz.Application.Services;
using MsAuthz.UnitTests.TestDoubles;

namespace MsAuthz.UnitTests.Services;

/// <summary>
/// Idempotent-materialization tests required by the task: re-running tenant provisioning must
/// produce the same tuple set, not duplicates or errors (MS-AUTHZ-SPEC.md §5).
/// </summary>
public class TenantProvisioningServiceTests
{
    [Fact]
    public async Task ProvisionTenantAsync_expands_every_role_permission_pair_into_a_tuple()
    {
        var catalog = new FakeCatalogRepository()
            .WithRole("vendedor", "Vendedor", "Sales.Read", "Sales.Write")
            .WithRole("cajero", "Cajero", "Cashier.Open");
        var registry = new FakeTenantRegistry();
        var gateway = new FakeOpenFgaGateway();
        var sut = new TenantProvisioningService(catalog, registry, gateway, TestLogger.For<TenantProvisioningService>());

        await sut.ProvisionTenantAsync("jurol");

        gateway.Tuples.Should().HaveCount(3);
        gateway.Tuples.Should().Contain((
            OpenFgaIdentifiers.RoleAssigneeUserset("jurol", "vendedor"),
            OpenFgaIdentifiers.GrantedRelation,
            OpenFgaIdentifiers.Permission("jurol", "Sales.Read")));
        gateway.Tuples.Should().Contain((
            OpenFgaIdentifiers.RoleAssigneeUserset("jurol", "vendedor"),
            OpenFgaIdentifiers.GrantedRelation,
            OpenFgaIdentifiers.Permission("jurol", "Sales.Write")));
        gateway.Tuples.Should().Contain((
            OpenFgaIdentifiers.RoleAssigneeUserset("jurol", "cajero"),
            OpenFgaIdentifiers.GrantedRelation,
            OpenFgaIdentifiers.Permission("jurol", "Cashier.Open")));
    }

    [Fact]
    public async Task ProvisionTenantAsync_registers_the_tenant_so_catalog_sync_can_find_it_later()
    {
        var catalog = new FakeCatalogRepository().WithRole("vendedor", "Vendedor", "Sales.Read");
        var registry = new FakeTenantRegistry();
        var gateway = new FakeOpenFgaGateway();
        var sut = new TenantProvisioningService(catalog, registry, gateway, TestLogger.For<TenantProvisioningService>());

        await sut.ProvisionTenantAsync("jurol");

        (await registry.GetAllTenantCodesAsync()).Should().ContainSingle().Which.Should().Be("jurol");
    }

    [Fact]
    public async Task ProvisionTenantAsync_is_idempotent_when_called_twice_for_the_same_tenant()
    {
        var catalog = new FakeCatalogRepository()
            .WithRole("vendedor", "Vendedor", "Sales.Read", "Sales.Write");
        var registry = new FakeTenantRegistry();
        var gateway = new FakeOpenFgaGateway();
        var sut = new TenantProvisioningService(catalog, registry, gateway, TestLogger.For<TenantProvisioningService>());

        await sut.ProvisionTenantAsync("jurol");
        var tupleCountAfterFirstRun = gateway.Tuples.Count;

        await sut.ProvisionTenantAsync("jurol");

        gateway.Tuples.Should().HaveCount(tupleCountAfterFirstRun, "re-running provisioning must not duplicate tuples");
        (await registry.GetAllTenantCodesAsync()).Should().ContainSingle("tenant registration must also be idempotent");
    }

    [Fact]
    public async Task ProvisionTenantAsync_does_not_touch_tuples_already_materialized_for_another_tenant()
    {
        var catalog = new FakeCatalogRepository().WithRole("vendedor", "Vendedor", "Sales.Read");
        var registry = new FakeTenantRegistry();
        var gateway = new FakeOpenFgaGateway();
        var sut = new TenantProvisioningService(catalog, registry, gateway, TestLogger.For<TenantProvisioningService>());

        await sut.ProvisionTenantAsync("jurol");
        await sut.ProvisionTenantAsync("otraempresa");

        gateway.Tuples.Should().Contain((
            OpenFgaIdentifiers.RoleAssigneeUserset("jurol", "vendedor"),
            OpenFgaIdentifiers.GrantedRelation,
            OpenFgaIdentifiers.Permission("jurol", "Sales.Read")));
        gateway.Tuples.Should().Contain((
            OpenFgaIdentifiers.RoleAssigneeUserset("otraempresa", "vendedor"),
            OpenFgaIdentifiers.GrantedRelation,
            OpenFgaIdentifiers.Permission("otraempresa", "Sales.Read")));
        gateway.Tuples.Should().HaveCount(2);
    }
}
