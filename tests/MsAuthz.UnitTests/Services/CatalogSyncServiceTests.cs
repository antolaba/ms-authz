using FluentAssertions;
using MsAuthz.Application.Common.Identifiers;
using MsAuthz.Application.Services;
using MsAuthz.UnitTests.TestDoubles;

namespace MsAuthz.UnitTests.Services;

public class CatalogSyncServiceTests
{
    [Fact]
    public async Task SyncAllTenantsAsync_reexpands_the_catalog_for_every_known_tenant()
    {
        var catalog = new FakeCatalogRepository().WithRole("vendedor", "Vendedor", "Sales.Read");
        var registry = new FakeTenantRegistry();
        var gateway = new FakeOpenFgaGateway();
        var provisioning = new TenantProvisioningService(catalog, registry, gateway, TestLogger.For<TenantProvisioningService>());

        await provisioning.ProvisionTenantAsync("jurol");
        await provisioning.ProvisionTenantAsync("otraempresa");

        var sut = new CatalogSyncService(registry, provisioning, TestLogger.For<CatalogSyncService>());
        var syncedTenants = await sut.SyncAllTenantsAsync();

        syncedTenants.Should().BeEquivalentTo(["jurol", "otraempresa"]);
    }

    [Fact]
    public async Task SyncAllTenantsAsync_picks_up_a_new_permission_added_to_the_catalog_after_provisioning()
    {
        var catalog = new FakeCatalogRepository().WithRole("vendedor", "Vendedor", "Sales.Read");
        var registry = new FakeTenantRegistry();
        var gateway = new FakeOpenFgaGateway();
        var provisioning = new TenantProvisioningService(catalog, registry, gateway, TestLogger.For<TenantProvisioningService>());
        await provisioning.ProvisionTenantAsync("jurol");

        // Simulate a catalog change: "vendedor" now also grants "Sales.Write".
        var updatedCatalog = new FakeCatalogRepository().WithRole("vendedor", "Vendedor", "Sales.Read", "Sales.Write");
        var provisioningWithUpdatedCatalog = new TenantProvisioningService(
            updatedCatalog, registry, gateway, TestLogger.For<TenantProvisioningService>());
        var sut = new CatalogSyncService(registry, provisioningWithUpdatedCatalog, TestLogger.For<CatalogSyncService>());

        await sut.SyncAllTenantsAsync();

        gateway.Tuples.Should().Contain((
            OpenFgaIdentifiers.RoleAssigneeUserset("jurol", "vendedor"),
            OpenFgaIdentifiers.GrantedRelation,
            OpenFgaIdentifiers.Permission("jurol", "Sales.Write")));
    }

    [Fact]
    public async Task SyncAllTenantsAsync_is_idempotent()
    {
        var catalog = new FakeCatalogRepository().WithRole("vendedor", "Vendedor", "Sales.Read");
        var registry = new FakeTenantRegistry();
        var gateway = new FakeOpenFgaGateway();
        var provisioning = new TenantProvisioningService(catalog, registry, gateway, TestLogger.For<TenantProvisioningService>());
        await provisioning.ProvisionTenantAsync("jurol");

        var sut = new CatalogSyncService(registry, provisioning, TestLogger.For<CatalogSyncService>());

        await sut.SyncAllTenantsAsync();
        var countAfterFirstSync = gateway.Tuples.Count;
        await sut.SyncAllTenantsAsync();

        gateway.Tuples.Should().HaveCount(countAfterFirstSync);
    }

    [Fact]
    public async Task SyncAllTenantsAsync_with_no_registered_tenants_does_nothing()
    {
        var catalog = new FakeCatalogRepository().WithRole("vendedor", "Vendedor", "Sales.Read");
        var registry = new FakeTenantRegistry();
        var gateway = new FakeOpenFgaGateway();
        var provisioning = new TenantProvisioningService(catalog, registry, gateway, TestLogger.For<TenantProvisioningService>());
        var sut = new CatalogSyncService(registry, provisioning, TestLogger.For<CatalogSyncService>());

        var syncedTenants = await sut.SyncAllTenantsAsync();

        syncedTenants.Should().BeEmpty();
        gateway.Tuples.Should().BeEmpty();
    }
}
