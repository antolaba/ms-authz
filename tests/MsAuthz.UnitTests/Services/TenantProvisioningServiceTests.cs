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
        var gateway = new FakeOpenFgaGateway();
        var sut = new TenantProvisioningService(catalog, gateway, TestLogger.For<TenantProvisioningService>());

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
    public async Task ProvisionTenantAsync_is_idempotent_when_called_twice_for_the_same_tenant()
    {
        var catalog = new FakeCatalogRepository()
            .WithRole("vendedor", "Vendedor", "Sales.Read", "Sales.Write");
        var gateway = new FakeOpenFgaGateway();
        var sut = new TenantProvisioningService(catalog, gateway, TestLogger.For<TenantProvisioningService>());

        await sut.ProvisionTenantAsync("jurol");
        var tupleCountAfterFirstRun = gateway.Tuples.Count;

        await sut.ProvisionTenantAsync("jurol");

        gateway.Tuples.Should().HaveCount(tupleCountAfterFirstRun, "re-running provisioning must not duplicate tuples");
    }

    [Fact]
    public async Task ProvisionTenantAsync_does_not_touch_tuples_already_materialized_for_another_tenant()
    {
        var catalog = new FakeCatalogRepository().WithRole("vendedor", "Vendedor", "Sales.Read");
        var gateway = new FakeOpenFgaGateway();
        var sut = new TenantProvisioningService(catalog, gateway, TestLogger.For<TenantProvisioningService>());

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

    [Fact]
    public async Task ProvisionTenantAsync_revokes_a_permission_the_catalog_no_longer_grants_a_role()
    {
        var gateway = new FakeOpenFgaGateway();

        var catalogBefore = new FakeCatalogRepository().WithRole("vendedor", "Vendedor", "Sales.Read", "Sales.Write");
        var sutBefore = new TenantProvisioningService(catalogBefore, gateway, TestLogger.For<TenantProvisioningService>());
        await sutBefore.ProvisionTenantAsync("jurol");

        var catalogAfter = new FakeCatalogRepository().WithRole("vendedor", "Vendedor", "Sales.Read");
        var sutAfter = new TenantProvisioningService(catalogAfter, gateway, TestLogger.For<TenantProvisioningService>());
        await sutAfter.ProvisionTenantAsync("jurol");

        gateway.Tuples.Should().Contain((
            OpenFgaIdentifiers.RoleAssigneeUserset("jurol", "vendedor"),
            OpenFgaIdentifiers.GrantedRelation,
            OpenFgaIdentifiers.Permission("jurol", "Sales.Read")));
        gateway.Tuples.Should().NotContain((
            OpenFgaIdentifiers.RoleAssigneeUserset("jurol", "vendedor"),
            OpenFgaIdentifiers.GrantedRelation,
            OpenFgaIdentifiers.Permission("jurol", "Sales.Write")));
    }

    [Fact]
    public async Task ProvisionTenantAsync_revoking_a_permission_in_one_tenant_does_not_touch_another_tenant()
    {
        var gateway = new FakeOpenFgaGateway();

        var catalogBefore = new FakeCatalogRepository().WithRole("vendedor", "Vendedor", "Sales.Read", "Sales.Write");
        var sutBefore = new TenantProvisioningService(catalogBefore, gateway, TestLogger.For<TenantProvisioningService>());
        await sutBefore.ProvisionTenantAsync("jurol");
        await sutBefore.ProvisionTenantAsync("otraempresa");

        var catalogAfter = new FakeCatalogRepository().WithRole("vendedor", "Vendedor", "Sales.Read");
        var sutAfter = new TenantProvisioningService(catalogAfter, gateway, TestLogger.For<TenantProvisioningService>());
        await sutAfter.ProvisionTenantAsync("jurol");

        gateway.Tuples.Should().NotContain((
            OpenFgaIdentifiers.RoleAssigneeUserset("jurol", "vendedor"),
            OpenFgaIdentifiers.GrantedRelation,
            OpenFgaIdentifiers.Permission("jurol", "Sales.Write")));
        gateway.Tuples.Should().Contain((
            OpenFgaIdentifiers.RoleAssigneeUserset("otraempresa", "vendedor"),
            OpenFgaIdentifiers.GrantedRelation,
            OpenFgaIdentifiers.Permission("otraempresa", "Sales.Write")),
            "re-provisioning jurol against a narrower catalog must not revoke otraempresa's grants");
    }

    [Theory]
    [InlineData("acme|prod")]
    [InlineData("acme#prod")]
    [InlineData("acme:prod")]
    [InlineData("acme corp")]
    [InlineData("")]
    public async Task ProvisionTenantAsync_rejects_an_invalid_tenant_code_without_writing_any_tuple(string invalidTenantCode)
    {
        var catalog = new FakeCatalogRepository().WithRole("vendedor", "Vendedor", "Sales.Read");
        var gateway = new FakeOpenFgaGateway();
        var sut = new TenantProvisioningService(catalog, gateway, TestLogger.For<TenantProvisioningService>());

        var act = () => sut.ProvisionTenantAsync(invalidTenantCode);

        await act.Should().ThrowAsync<ArgumentException>();
        gateway.Tuples.Should().BeEmpty();
    }
}
