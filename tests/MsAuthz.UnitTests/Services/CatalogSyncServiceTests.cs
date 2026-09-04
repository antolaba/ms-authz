using FluentAssertions;
using MsAuthz.Application.Common.Exceptions;
using MsAuthz.Application.Common.Identifiers;
using MsAuthz.Application.Services;
using MsAuthz.UnitTests.TestDoubles;

namespace MsAuthz.UnitTests.Services;

public class CatalogSyncServiceTests
{
    [Fact]
    public async Task SyncTenantsAsync_reexpands_the_catalog_for_every_tenant_given()
    {
        var catalog = new FakeCatalogRepository().WithRole("vendedor", "Vendedor", "Sales.Read");
        var gateway = new FakeOpenFgaGateway();
        var provisioning = new TenantProvisioningService(catalog, gateway, TestLogger.For<TenantProvisioningService>());
        var sut = new CatalogSyncService(provisioning, TestLogger.For<CatalogSyncService>());

        var syncedTenants = await sut.SyncTenantsAsync(["jurol", "otraempresa"]);

        syncedTenants.Should().BeEquivalentTo(["jurol", "otraempresa"]);
        gateway.Tuples.Should().Contain((
            OpenFgaIdentifiers.RoleAssigneeUserset("jurol", "vendedor"),
            OpenFgaIdentifiers.GrantedRelation,
            OpenFgaIdentifiers.Permission("jurol", "Sales.Read")));
        gateway.Tuples.Should().Contain((
            OpenFgaIdentifiers.RoleAssigneeUserset("otraempresa", "vendedor"),
            OpenFgaIdentifiers.GrantedRelation,
            OpenFgaIdentifiers.Permission("otraempresa", "Sales.Read")));
    }

    [Fact]
    public async Task SyncTenantsAsync_picks_up_a_new_permission_added_to_the_catalog()
    {
        var updatedCatalog = new FakeCatalogRepository().WithRole("vendedor", "Vendedor", "Sales.Read", "Sales.Write");
        var gateway = new FakeOpenFgaGateway();
        var provisioning = new TenantProvisioningService(updatedCatalog, gateway, TestLogger.For<TenantProvisioningService>());
        var sut = new CatalogSyncService(provisioning, TestLogger.For<CatalogSyncService>());

        await sut.SyncTenantsAsync(["jurol"]);

        gateway.Tuples.Should().Contain((
            OpenFgaIdentifiers.RoleAssigneeUserset("jurol", "vendedor"),
            OpenFgaIdentifiers.GrantedRelation,
            OpenFgaIdentifiers.Permission("jurol", "Sales.Write")));
    }

    [Fact]
    public async Task SyncTenantsAsync_is_idempotent()
    {
        var catalog = new FakeCatalogRepository().WithRole("vendedor", "Vendedor", "Sales.Read");
        var gateway = new FakeOpenFgaGateway();
        var provisioning = new TenantProvisioningService(catalog, gateway, TestLogger.For<TenantProvisioningService>());
        var sut = new CatalogSyncService(provisioning, TestLogger.For<CatalogSyncService>());

        await sut.SyncTenantsAsync(["jurol"]);
        var countAfterFirstSync = gateway.Tuples.Count;
        await sut.SyncTenantsAsync(["jurol"]);

        gateway.Tuples.Should().HaveCount(countAfterFirstSync);
    }

    [Fact]
    public async Task SyncTenantsAsync_rejects_an_empty_tenant_list()
    {
        var catalog = new FakeCatalogRepository().WithRole("vendedor", "Vendedor", "Sales.Read");
        var gateway = new FakeOpenFgaGateway();
        var provisioning = new TenantProvisioningService(catalog, gateway, TestLogger.For<TenantProvisioningService>());
        var sut = new CatalogSyncService(provisioning, TestLogger.For<CatalogSyncService>());

        var act = () => sut.SyncTenantsAsync([]);

        await act.Should().ThrowAsync<InvalidCatalogRequestException>();
        gateway.Tuples.Should().BeEmpty();
    }

    [Fact]
    public async Task SyncTenantsAsync_rejects_an_invalid_tenant_code_before_touching_any_tenant()
    {
        var catalog = new FakeCatalogRepository().WithRole("vendedor", "Vendedor", "Sales.Read");
        var gateway = new FakeOpenFgaGateway();
        var provisioning = new TenantProvisioningService(catalog, gateway, TestLogger.For<TenantProvisioningService>());
        var sut = new CatalogSyncService(provisioning, TestLogger.For<CatalogSyncService>());

        var act = () => sut.SyncTenantsAsync(["jurol", "bad|code"]);

        await act.Should().ThrowAsync<ArgumentException>();
        gateway.Tuples.Should().BeEmpty("jurol must not be provisioned once a later code in the same request is invalid");
    }
}
